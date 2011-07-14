using System;
using Mono.Addins;
using OpenHome.Net.Device;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.Generic;

[assembly: AddinRoot("ohOs", "1.1")]

// !!!! need IOsContext definition
// !!!! which may include interface to proxy for InstallManager service

namespace OpenHome.Os.AppManager
{
    public class HistoryItem
    {
        public enum ItemType
        {
            EInstall,
            EUninstall,
            EUpdate
        }
        public string Name { get { return iName; } }
        public ItemType Type { get { return iType; } }
        public DateTime Time { get { return iTime; } }
        public string Udn { get { return iUdn; } }

        private readonly string iName;
        private readonly ItemType iType;
        private readonly DateTime iTime;
        private readonly string iUdn;

        public HistoryItem(string aName, ItemType aType, string aUdn)
        {
            iName = aName;
            iType = aType;
            iTime = DateTime.Now;
            iUdn = aUdn;
        }
    }
    
    public class Manager : IDisposable
    {
        private class PublishedApp : IDisposable
        {
            public IApp App { get { return iApp; } }
            private readonly IApp iApp;
            private readonly DvDevice iDevice;
            private readonly ProviderApp iProvider;

            public PublishedApp(IApp aApp, DvDevice aDevice, ProviderApp aProvider)
            {
                iApp = aApp;
                iDevice = aDevice;
                iProvider = aProvider;
            }
            public void Dispose()
            {
                iApp.Stop(); // ???
                iApp.Dispose();
                iDevice.Dispose();
                iProvider.Dispose();
            }
        }

        private readonly string iInstallBase;
        private const string kAppsDirectory = "Apps";
        private readonly Dictionary<string, PublishedApp> iApps;
        private readonly List<HistoryItem> iHistory;
        private readonly bool iInitialising;

        public List<HistoryItem> History
        {
            get { lock (iHistory) { return iHistory; } }
        }

        public Manager(string aInstallBase)
        {
            iInstallBase = System.IO.Path.Combine(aInstallBase, kAppsDirectory);
            iApps = new Dictionary<string, PublishedApp>();
            iHistory = new List<HistoryItem>();
            // !!!! restore previous history from disk
            AddinManager.Initialize(iInstallBase, iInstallBase, iInstallBase);
            AddinManager.AddExtensionNodeHandler("/ohOs/App", AppListChanged);
            iInitialising = false;
            UpdateAppList();
            iInitialising = true;
        }
        public void Install(string aZipFile)
        {
            var unzipper = new FastZip();
            //string target = System.IO.Path.Combine(iInstallBase, "Temp"); // hardcoding of 'Temp' not threadsafe
            string target = iInstallBase;
            unzipper.ExtractZip(aZipFile, target, "");
            UpdateAppList();
        }
        public void Uninstall(string aUdn)
        {
            lock (iApps)
            {
                Uninstall(aUdn, true);
            }
            UpdateAppList();
        }
        private bool Uninstall(string aUdn, bool aUpdateHistory)
        {
            PublishedApp app;
            if (!iApps.TryGetValue(aUdn, out app))
            {
                return false;
            }
            app.App.Stop();
            if (aUpdateHistory)
            {
                iHistory.Add(new HistoryItem(app.App.Name, HistoryItem.ItemType.EUninstall, app.App.Udn));
            }
            string appPath = app.App.AssemblyPath;
            app.App.Dispose();
            System.IO.Directory.Delete(appPath, true);
            iApps.Remove(aUdn);
            return true;
        }
        public void Dispose()
        {
            // !!!! write history to disk (here or earlier)
        }
        private void AppListChanged(object aSender, ExtensionNodeEventArgs aArgs)
        {
            if (aArgs.Change == ExtensionChange.Remove)
            {
                return;
            }
            var app = (IApp)aArgs.ExtensionObject;
            DvDevice device = (app.ResourceManager == null
                                    ? new DvDeviceStandard(app.Udn)
                                    : new DvDeviceStandard(app.Udn, app.ResourceManager));
            // Set initial values for the attributes mandated by UPnP
            // These may be over-ridden by the Start function below
            device.SetAttribute("Upnp.Domain", "openhome.org");
            device.SetAttribute("Upnp.Type", "App");
            device.SetAttribute("Upnp.Version", "1");
            device.SetAttribute("Upnp.FriendlyName", app.Name);
            device.SetAttribute("Upnp.Manufacturer", "N/A");
            device.SetAttribute("Upnp.ModelName", "ohOs Application");
            var provider = new ProviderApp(device, app);
            var change = HistoryItem.ItemType.EInstall;
            lock (iApps)
            {
                if (!iInitialising && Uninstall(app.Udn, false))
                {
                    change = HistoryItem.ItemType.EUpdate;
                }
                iApps.Add(app.Udn, new PublishedApp(app, device, provider));
            }
            app.Start(device);
            device.SetEnabled();
            iHistory.Add(new HistoryItem(app.Name, change, app.Udn));
        }
        private void UpdateAppList()
        {
            lock (this)
            {
                AddinManager.Registry.Update();
            }
        }
    }
}
