using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
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
        //class ManagedDownload
        //{
        //    public string Url { get; set; }
        //}

        readonly object iLock = new object();
        readonly IAppShell iAppShell;
        List<IAppManagerProvider> iProviders;
        readonly SafeCallbackTracker iCallbackTracker = new SafeCallbackTracker();
        IdDictionary<string, ManagedApp> iApps = new IdDictionary<string, ManagedApp>();
        //Dictionary<string, ManagedDownload> iDownloads = new Dictionary<string, ManagedDownload>();

        public AppManager(
            string aResourceUri,
            IEnumerable<DvDevice> aDevices,
            Func<DvDevice, IAppManagerActionHandler, string, IAppManagerProvider> aProviderConstructor,
            IAppShell aAppShell)
        {
            iAppShell = aAppShell;
            iProviders = aDevices.Select(aDevice=>aProviderConstructor(aDevice, this, aResourceUri)).ToList();
            iAppShell.AppStatusChanged += OnAppStatusChanged;
            RefreshApps();
        }

        public void Dispose()
        {
            iAppShell.AppStatusChanged -= OnAppStatusChanged;
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
            /*lock (iLock)
            {
                
                string appName = iApps.GetKeyForId(aAppHandle);
                ManagedApp app;
                if (iApps.TryGetValueById(aAppHandle, out app))
                {
                    return new XElement("appList",
                        new XElement("app"),
                            new XElement("handle", aAppHandle),
                            new XElement("id", app.Info.Name),
                            new XElement("version", "DUMMY"),
                            new XElement("url", "http://notimplemented.invalid"),
                            new XElement("description", "Hi there"),
                            new XElement("status", app.Info.State == AppState.Running ? "running" : "broken"),
                            new XElement("updateStatus", "noUpdate")).ToString();
                }
                throw new ActionError("Handle not found");
            }*/
        }

        public void CancelDownload(string aAppUrl)
        {
            throw new NotImplementedException();
        }

        public string GetAllDownloadsStatus()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public void RemoveApp(uint aAppHandle)
        {
            throw new NotImplementedException();
        }

    }
}
