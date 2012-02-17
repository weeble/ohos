using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using log4net;
using OpenHome.Net.Device;
using OpenHome.Os.Apps;
using OpenHome.Os.Platform.Collections;
using OpenHome.Widget.Nodes.IO;
using OpenHome.Widget.Nodes.Threading;

namespace OpenHome.Os.AppManager
{

    class AppManager : IAppManagerActionHandler, IDisposable
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

        void OnDownloadCountChanged(object aSender, EventArgs aE)
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

        void OnAppAvailableForDownload(string aAppName)
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
                    if (managedApp.Info.DownloadLastModified != null)
                    {
                        string appName = managedApp.Info.Name;
                        iDownloadManager.StartPollingForAppUpdate(managedApp.Info.Name, managedApp.Info.UpdateUrl,
                            () => OnAppAvailableForDownload(appName),
                            () => OnAppPollFailed(appName),
                            managedApp.Info.DownloadLastModified.Value);
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

        void OnAppStatusChanged(object aSender, AppStatusChangeEventArgs aE)
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

        public string GetAppStatus(uint aAppHandle)
        {
            return GetMultipleAppsStatus(new List<uint> { aAppHandle });
        }

        public void CancelDownload(string aAppUrl)
        {
            iDownloadManager.CancelDownload(aAppUrl);
        }

        XElement DownloadProgressToXElement(DownloadProgress aDownload)
        {
            XElement element = new XElement("download",
                new XElement("status", aDownload.HasFailed ? "failed" : "downloading"),
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

        public string GetAllDownloadsStatus()
        {
            IEnumerable<DownloadProgress> downloads = iDownloadManager.GetDownloadsStatus();
            lock (iLock)
            {
                XElement root = new XElement("downloadList",downloads.Select(DownloadProgressToXElement));
                return root.ToString();
            }
        }

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
                        var element =
                            new XElement("app",
                                new XElement("handle", app.Handle),
                                new XElement("id", app.Info.Name),
                                new XElement("friendlyName", app.Info.FriendlyName),
                                new XElement("version", app.Info.Version == null ? "" : app.Info.Version.ToString()),
                                new XElement("updateUrl", app.Info.UpdateUrl),
                                new XElement("autoUpdate", app.Info.AutoUpdate),
                                new XElement("status", app.Info.State == AppState.Running ? "running" : "broken"),
                                new XElement("updateStatus", app.DownloadAvailable ? "available" : "noUpdate"));
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

                string url = managedApp.Info.UpdateUrl;
                string name = managedApp.Info.Name;
                iDownloadManager.StartDownload(
                    url,
                    (aLocalFile, aLastModified) =>
                    {
                        try
                        {
                            iAppShell.Upgrade(name, aLocalFile, url, aLastModified);
                        }
                        catch (BadPluginException)
                        {
                            // TODO: Update download status to record failure.
                            Logger.Warn("Update failed: bad plugin.");
                        }
                    });
                managedApp.Downloading = true;
            }
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
                });
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
