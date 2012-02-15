using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using log4net;
using OpenHome.Net.Device;
using OpenHome.Os.Apps;
using OpenHome.Os.Platform.Collections;
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
            iDownloadManager = aDownloadManager; // new DownloadManager(aDownloadDirectory);
            iDownloadManager.DownloadCountChanged += OnDownloadCountChanged;
            iAppShell = aAppShell;
            iProviders = aDevices.Select(aDevice=>aProviderConstructor(aDevice, this, aResourceUri)).ToList();
            iAppShell.AppStatusChanged += OnAppStatusChanged;
            RefreshApps();
        }

        void OnDownloadCountChanged(object aSender, EventArgs aE)
        {
            int downloadCount = iDownloadManager.GetDownloadsStatus().Count();
            foreach (var provider in iProviders)
            {
                provider.SetDownloadCount((uint)downloadCount);
            }
        }

        public void Dispose()
        {
            iDownloadManager.DownloadCountChanged -= OnDownloadCountChanged;
            iAppShell.AppStatusChanged -= OnAppStatusChanged;
            //iDownloadManager.Dispose();
            iCallbackTracker.Close();
            foreach (var provider in iProviders)
            {
                provider.Dispose();
            }
        }

        void RefreshApps()
        {
            HashSet<string> unseenApps = new HashSet<string>(iApps.ItemsByKey.Select(aKvp => aKvp.Key));
            foreach (var app in iAppShell.GetApps())
            {
                unseenApps.Remove(app.Name);
                ManagedApp managedApp;
                if (!iApps.TryGetValueByKey(app.Name, out managedApp))
                {
                    uint handle;
                    managedApp = new ManagedApp();
                    iApps.TryAdd(app.Name, managedApp, out handle);
                    managedApp.Handle = handle;
                }
                managedApp.Info = app;
            }
            foreach (string missingApp in unseenApps)
            {
                iApps.TryRemoveByKey(missingApp);
            }
            UpdateHandles();
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
                        appListElement.Add(
                            new XElement("app",
                                new XElement("handle", app.Handle),
                                new XElement("id", app.Info.Name),
                                new XElement("version", "DUMMY"),
                                new XElement("url", String.Format("/{0}/Upnp/resource/", app.Info.Udn)),
                                new XElement("description", "Hi there"),
                                new XElement("status", app.Info.State == AppState.Running ? "running" : "broken"),
                                new XElement("updateStatus", "noUpdate")));
                    }
                }
                return appListElement.ToString();
            }
        }

        public void UpdateApp(uint aAppHandle)
        {
            throw new NotImplementedException();
        }

        public void InstallAppFromUrl(string aAppUrl)
        {
            Logger.InfoFormat("InstallAppFromUrl({0})", aAppUrl);
            iDownloadManager.StartDownload(
                aAppUrl,
                aLocalFile =>
                {
                    try
                    {
                        iAppShell.Install(aLocalFile);
                    }
                    catch (BadPluginException)
                    {
                        // TODO: Update download status to record failure.
                        Console.WriteLine("Bad plugin");
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
