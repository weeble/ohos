using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using log4net;
using Mono.Addins;
using OpenHome.Net.Device;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.Generic;
using OpenHome.Os.Platform;

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

    public class AppContext : IAppContext
    {
        public IAppServices Services { get; set; }
        public string StaticPath { get; set; }
        public string StorePath { get; set; }
        public IConfigFileCollection Configuration { get; set; }
        public DvDevice Device { get; set; }
    }
    
    public class Manager : IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(Manager));
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
                Semaphore disposeSemaphore = new Semaphore(0,1);
                iDevice.SetDisabled(() => disposeSemaphore.Release());
                disposeSemaphore.WaitOne();
                disposeSemaphore.Dispose();
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
        readonly IAppServices iFullPrivilegeAppServices;
        private readonly IConfigFileCollection iConfiguration;
        string iStorePath;

        public List<HistoryItem> History
        {
            get { lock (iHistory) { return iHistory; } }
        }

        public Manager(string aInstallBase, IAppServices aFullPrivilegeAppServices, IConfigFileCollection aConfiguration)
        {
            iFullPrivilegeAppServices = aFullPrivilegeAppServices;
            iConfiguration = aConfiguration;
            iStorePath = iConfiguration.GetElementValueAsFilepath("system-settings/store");
            if (iStorePath == null)
            {
                iStorePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "store");
            }
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
            AddinManager.RemoveExtensionNodeHandler("/ohOs/App", AppListChanged);
            lock (iApps)
            {
                List<Exception> exceptions = new List<Exception>();
                foreach (var app in iApps.Values)
                {
                    try
                    {
                        app.Dispose();
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
                if (exceptions.Count > 0)
                {
                    throw new Exception(String.Format("{0} exceptions during Dispose().", exceptions.Count), exceptions[0]);
                }
                iApps.Clear();
            }
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

            // Take care here! We don't want an app peeking at other apps'
            // settings by injecting crazy XPath nonsense into its name.
            string sanitizedName = app.Name.Replace("'", "").Replace("\"", "");
            IConfigFileCollection appConfig = iConfiguration.GetSubcollection(String.Format("app-settings[@name='{0}']", sanitizedName));

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
            AppContext appContext = new AppContext
            {
                Configuration = appConfig,
                Device = device,
                Services = iFullPrivilegeAppServices,
                StaticPath = iInstallBase,
                StorePath = Path.Combine(iStorePath, "apps", sanitizedName)
            };
            try
            {
                Logger.InfoFormat("Starting app: {0}", sanitizedName);
                app.Start(appContext);
                Logger.InfoFormat("App started: {0}", sanitizedName);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Exception during app startup: {0}\n{1}", sanitizedName, e);
                throw;
            }
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
