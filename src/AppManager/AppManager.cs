using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using log4net;
using OpenHome.Net.Device;
using OpenHome.Os.Apps;
using OpenHome.Os.Platform.Collections;
using OpenHome.Os.Platform.Threading;

namespace OpenHome.Os.AppManager
{

    /// <summary>
    /// High level management of apps. Checks for updates, coordinates downloads, provides UPnP
    /// control over apps.
    /// </summary>
    public class AppManager : IAppManagerActionHandler, IDisposable
    {
        class ManagedApp
        {
            public AppInfo Info { get; set; }
            public uint SequenceNumber { get; set; }
            public uint Handle { get; set; }

            public bool DownloadAvailable { get; set; }
            public bool Downloading { get; set; }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(AppManager));

        readonly object iLock = new object();
        readonly IAppShell iAppShell;
        readonly List<IAppManagerProvider> iProviders;
        readonly SafeCallbackTracker iCallbackTracker = new SafeCallbackTracker();
        readonly IdDictionary<string, ManagedApp> iApps = new IdDictionary<string, ManagedApp>();
        readonly IDownloadManager iDownloadManager;
        readonly Bimap<string, string> iUpgradeAppNamesToUrls = new Bimap<string, string>();

        public AppManager(
            string aResourceUri,
            IEnumerable<DvDevice> aDevices,
            Func<DvDevice, IAppManagerActionHandler, string, IAppManagerProvider> aProviderConstructor,
            IAppShell aAppShell,
            IDownloadManager aDownloadManager)
        {
            iDownloadManager = aDownloadManager;
            iDownloadManager.DownloadCountChanged += OnDownloadCountChanged;
            iAppShell = aAppShell;
            iProviders = aDevices.Select(aDevice=>aProviderConstructor(aDevice, this, aResourceUri)).ToList();
            iAppShell.AppStatusChanged += OnAppStatusChanged;
            RefreshApps();
        }

        public void OnDownloadCountChanged(object aSender, EventArgs aE)
        {
            iCallbackTracker.PreventClose(() =>
            {
                int downloadCount = iDownloadManager.GetDownloadsStatus().Count();
                foreach (var provider in iProviders)
                {
                    provider.SetDownloadCount((uint)downloadCount);
                }
            });
        }

        public void Dispose()
        {
            iDownloadManager.DownloadCountChanged -= OnDownloadCountChanged;
            iAppShell.AppStatusChanged -= OnAppStatusChanged;
            iCallbackTracker.Close();
            foreach (var provider in iProviders)
            {
                provider.Dispose();
            }
        }

        public void OnAppAvailableForDownload(string aAppName)
        {
            Logger.InfoFormat("App update available: {0}", aAppName);
            iCallbackTracker.PreventClose(() =>
            {
                lock (iLock)
                {
                    ManagedApp managedApp;
                    if (iApps.TryGetValueByKey(aAppName, out managedApp))
                    {
                        if (!managedApp.DownloadAvailable)
                        {
                            managedApp.DownloadAvailable = true;
                            managedApp.SequenceNumber += 1;
                            UpdateHandles();
                        }
                        if (managedApp.Info.IsSystemApp && managedApp.Info.State != AppState.Running)
                        {
                            // If a system app isn't installed or isn't running, update
                            // it without prompting.
                            DoUpdate(managedApp);
                        }
                    }
                }
            });
        }

        void RefreshApps()
        {
            HashSet<string> unseenApps = new HashSet<string>(iApps.ItemsByKey.Select(aKvp => aKvp.Key));
            foreach (var app in iAppShell.GetApps())
            {
                // Ignore apps pending delete.
                if (app.PendingDelete)
                {
                    continue;
                }
                unseenApps.Remove(app.Name);
                ManagedApp managedApp;
                if (!iApps.TryGetValueByKey(app.Name, out managedApp))
                {
                    uint handle;
                    managedApp = new ManagedApp();
                    iApps.TryAdd(app.Name, managedApp, out handle);
                    managedApp.Handle = handle;
                }
                if (managedApp.Info != app)
                {
                    managedApp.Info = app;
                    if (managedApp.Info.AutoUpdate)
                    {
                        DateTime? lastModified = managedApp.Info.DownloadLastModified;

                        string appName = managedApp.Info.Name;
                        iDownloadManager.StartPollingForAppUpdate(managedApp.Info.Name, managedApp.Info.UpdateUrl,
                            () => OnAppAvailableForDownload(appName),
                            () => OnAppPollFailed(appName),
                            lastModified ?? new DateTime(1900, 1, 1));
                    }
                    managedApp.SequenceNumber += 1;
                }
            }
            foreach (string missingApp in unseenApps)
            {
                iDownloadManager.StopPollingForAppUpdate(missingApp);
                iApps.TryRemoveByKey(missingApp);
            }
            UpdateHandles();
        }

        void OnAppPollFailed(string aAppName)
        {
            Logger.InfoFormat("Poll for app update received error: {0}", aAppName);
        }

        public void OnAppStatusChanged(object aSender, AppStatusChangeEventArgs aE)
        {
            iCallbackTracker.PreventClose(() =>
            {
                lock (iLock)
                {
                    RefreshApps();
                }
            });
        }

        void UpdateHandles()
        {
            List<uint> handles = new List<uint>(iApps.Count);
            List<uint> seqNos = new List<uint>(iApps.Count);
            foreach (var kvp in iApps.ItemsById)
            {
                handles.Add(kvp.Key);
                seqNos.Add(kvp.Value.SequenceNumber);
            }
            foreach (var provider in iProviders)
            {
                provider.SetAppHandles(handles, seqNos);
            }
        }

        /// <summary>
        /// Get XML describing the status of one app.
        /// </summary>
        /// <param name="aAppHandle"></param>
        /// <returns></returns>
        public string GetAppStatus(uint aAppHandle)
        {
            return GetMultipleAppsStatus(new List<uint> { aAppHandle });
        }

        /// <summary>
        /// Cancel a download by its URL.
        /// </summary>
        /// <param name="aAppUrl"></param>
        public void CancelDownload(string aAppUrl)
        {
            iDownloadManager.CancelDownload(aAppUrl);
        }

        XElement DownloadProgressToXElement(DownloadProgress aDownload)
        {
            XElement appHandleElement = null;
            XElement appIdElement = null;
            string appName;
            if (iUpgradeAppNamesToUrls.Backward.TryGetValue(aDownload.Uri, out appName))
            {
                uint appHandle = iApps.GetIdForKey(appName);
                appHandleElement = new XElement("appHandle", appHandle);
                appIdElement = new XElement("appId", appName);
            }
            XElement element = new XElement("download",
                new XElement("status", aDownload.HasFailed ? "failed" : "downloading"),
                appHandleElement,
                appIdElement,
                new XElement("url", aDownload.Uri),
                new XElement("progressBytes", aDownload.DownloadedBytes));
            if (aDownload.HasTotalBytes)
            {
                element.Add(
                    new XElement("totalBytes", aDownload.TotalBytes),
                    new XElement("progressPercent", (int)Math.Round(100.0 * aDownload.DownloadedBytes / aDownload.TotalBytes)));
            }
            return element;
        }

        /// <summary>
        /// Get XML describing the status of all downloads.
        /// </summary>
        /// <returns></returns>
        public string GetAllDownloadsStatus()
        {
            IEnumerable<DownloadProgress> downloads = iDownloadManager.GetDownloadsStatus();
            lock (iLock)
            {
                XElement root = new XElement("downloadList",downloads.Select(DownloadProgressToXElement));
                return root.ToString();
            }
        }

        /// <summary>
        /// Get the XML status of multiple apps. If zero handles are supplied, gets the
        /// status of all apps.
        /// </summary>
        /// <param name="aHandles"></param>
        /// <returns></returns>
        public string GetMultipleAppsStatus(List<uint> aHandles)
        {
            lock (iLock)
            {
                if (aHandles.Count == 0)
                {
                    aHandles = iApps.ItemsById.Select(aKvp => aKvp.Key).ToList();
                }
                XElement appListElement = new XElement("appList");
                foreach (uint handle in aHandles)
                {
                    ManagedApp app;
                    if (iApps.TryGetValueById(handle, out app))
                    {
                        bool isUpdating = iUpgradeAppNamesToUrls.Forward.ContainsKey(app.Info.Name);
                        var element =
                            new XElement("app",
                                new XElement("handle", app.Handle),
                                new XElement("id", app.Info.Name),
                                new XElement("friendlyName", app.Info.FriendlyName),
                                new XElement("version", app.Info.Version == null ? "" : app.Info.Version.ToString()),
                                new XElement("updateUrl", app.Info.UpdateUrl),
                                new XElement("autoUpdate", app.Info.AutoUpdate),
                                new XElement("status", app.Info.State == AppState.Running ? "running" : "broken"),
                                new XElement("updateStatus",
                                    isUpdating ? "downloading" :
                                    app.DownloadAvailable ? "available" :
                                    "noUpdate"));
                        if (!string.IsNullOrEmpty(app.Info.Udn))
                        {
                            element.Add(new XElement("url", String.Format("/{0}/Upnp/resource/", app.Info.Udn)));
                        }
                        appListElement.Add(element);
                    }
                }
                return appListElement.ToString();
            }
        }

        /// <summary>
        /// Update an app.
        /// </summary>
        /// <param name="aAppHandle"></param>
        public void UpdateApp(uint aAppHandle)
        {
            lock (iLock)
            {
                ManagedApp managedApp;
                if (!iApps.TryGetValueById(aAppHandle, out managedApp))
                {
                    throw new ActionError("No such app.");
                }

                if (!managedApp.DownloadAvailable)
                {
                    throw new ActionError("No update available.");
                }

                DoUpdate(managedApp);
                return;
            }
        }

        void DoUpdate(ManagedApp aManagedApp)
        {
            string url = aManagedApp.Info.UpdateUrl;
            string name = aManagedApp.Info.Name;
            if (iUpgradeAppNamesToUrls.Forward.ContainsKey(name))
            {
                return;
            }
            iUpgradeAppNamesToUrls.Forward[name] = url;
            iDownloadManager.StartDownload(
                url,
                (aLocalFile, aLastModified) =>
                {
                    lock (iLock)
                    {
                        iUpgradeAppNamesToUrls.Forward.Remove(name);
                    }
                    try
                    {
                        iAppShell.Upgrade(name, aLocalFile, url, aLastModified);
                    }
                    catch (BadPluginException bpe)
                    {
                        // TODO: Update download status to record failure.
                        Logger.Warn("Update failed: bad plugin.", bpe);
                    }
                },
                () =>
                {
                    lock (iLock)
                    {
                        iUpgradeAppNamesToUrls.Forward.Remove(name);
                    }
                }
                );
            aManagedApp.Downloading = true;
        }

        public void InstallAppFromUrl(string aAppUrl)
        {
            Logger.InfoFormat("InstallAppFromUrl({0})", aAppUrl);
            iDownloadManager.StartDownload(
                aAppUrl,
                (aLocalFile, aLastModified) =>
                {
                    try
                    {
                        iAppShell.InstallNew(aLocalFile, aAppUrl, aLastModified);
                    }
                    catch (BadPluginException)
                    {
                        // TODO: Update download status to record failure.
                        Logger.Warn("Install failed: bad plugin.");
                    }
                },
                () => { });
        }

        public void RemoveApp(uint aAppHandle)
        {
            string appName;
            lock (iLock)
            {
                appName = iApps.GetKeyForId(aAppHandle);
            }
            iAppShell.UninstallByAppName(appName);
        }
    }
}
