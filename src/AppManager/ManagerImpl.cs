using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using log4net;
using Mono.Addins;
using OpenHome.Net.Device;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.Generic;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

// !!!! need IOsContext definition
// !!!! which may include interface to proxy for InstallManager service

namespace OpenHome.Os.AppManager
{
    public interface IZipReader
    {
        IEnumerable<ZipEntry> Open(string aZipName);
        //void ExtractAll(string aDestination);
    }

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

    public class ManagerImpl : IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ManagerImpl));
        private class PublishedApp : IDisposable
        {
            public IApp App { get { return iApp; } }
            private readonly IApp iApp;
            private readonly IDvDevice iDevice;
            private readonly IDvProviderOpenhomeOrgApp1 iProvider;

            public PublishedApp(IApp aApp, IDvDevice aDevice, IDvProviderOpenhomeOrgApp1 aProvider)
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
                ((IDisposable)disposeSemaphore).Dispose();
                iApp.Stop(); // ???
                iApp.Dispose();
                iDevice.Dispose();
                iProvider.Dispose();
            }
        }

        private readonly Dictionary<string, PublishedApp> iApps;
        private readonly List<HistoryItem> iHistory;
        private bool iInitialising;
        readonly IAppServices iFullPrivilegeAppServices;
        private readonly IConfigFileCollection iConfiguration;
        bool iAppsStarted;
        readonly IAddinManager iAddinManager;
        readonly IAppsDirectory iAppsDirectory;
        readonly IStoreDirectory iStoreDirectory;
        readonly Func<DvDevice, IApp, IDvProviderOpenhomeOrgApp1> iAppProviderConstructor;
        readonly IZipReader iZipReader;
        private readonly Dictionary<string, string> iAppDirsToAppUdns = new Dictionary<string, string>();

        public List<HistoryItem> History
        {
            get { return new List<HistoryItem>(iHistory); }
        }

        public ManagerImpl(
            IAppServices aFullPrivilegeAppServices,
            IConfigFileCollection aConfiguration,
            IAddinManager aAddinManager,
            IAppsDirectory aAppsDirectory,
            IStoreDirectory aStoreDirectory,
            Func<DvDevice, IApp, IDvProviderOpenhomeOrgApp1> aAppProviderConstructor,
            IZipReader aZipReader,
            bool aAutoStart)
        {
            iFullPrivilegeAppServices = aFullPrivilegeAppServices;
            iZipReader = aZipReader;
            iConfiguration = aConfiguration;
            iAddinManager = aAddinManager;
            iAppsDirectory = aAppsDirectory;
            iStoreDirectory = aStoreDirectory;
            iAppProviderConstructor = aAppProviderConstructor;
            iApps = new Dictionary<string, PublishedApp>();
            iHistory = new List<HistoryItem>();
            // !!!! restore previous history from disk
            if (aAutoStart)
            {
                Start();
            }
        }
        public void Start()
        {
            if (iAppsStarted) return;
            iAppsStarted = true;
            iInitialising = false;
            UpdateAppList();
            iInitialising = true;
        }

        /// <summary>
        /// Install a plugin.
        /// </summary>
        /// <param name="aZipFile"></param>
        /// <remarks>
        /// The zip file should not be in a location writeable by anyone
        /// untrusted - no attempt is made to ensure that the file isn't
        /// modified while being read, meaning that malicious timing of
        /// modifications could be used to circumvent some verification.
        /// </remarks>
        public void Install(string aZipFile)
        {
            //string target = System.IO.Path.Combine(iInstallBase, "Temp"); // hardcoding of 'Temp' not threadsafe
            string appDirName = VerifyPluginZip(aZipFile);
            if (iAppDirsToAppUdns.ContainsKey(appDirName))
            {
                string appUdn = iAppDirsToAppUdns[appDirName];
                IApp app = iApps[appUdn].App;
                Logger.InfoFormat("Updating app {0} (UDN={1}, directory={2}).", app.Name, app.Udn, appDirName);
                // TODO: Stop all apps.
                iAppsDirectory.DeleteSubdirectory(appDirName, true);
            }
            else if (iAppsDirectory.DoesSubdirectoryExist(appDirName))
            {
                Logger.InfoFormat("Overwriting app in directory {0}.", appDirName);
                iAppsDirectory.DeleteSubdirectory(appDirName, true);
            }
            else
            {
                Logger.InfoFormat("Installing new app in directory {0}.", appDirName);
            }
            iAppsDirectory.InstallZipFile(aZipFile);
            //var unzipper = new FastZip();
            //unzipper.ExtractZip(aZipFile, target, "");
            UpdateAppList();
        }

        /// <summary>
        /// Verify that the plugin installs to a single subdirectory,
        /// and return the name of that subdirectory.
        /// </summary>
        /// <param name="aZipFile"></param>
        /// <returns></returns>
        public string VerifyPluginZip(string aZipFile)
        {
            var zf = iZipReader.Open(aZipFile);
            HashSet<string> topLevelDirectories = new HashSet<string>();
            try
            {
                foreach (ZipEntry entry in zf)
                {
                    string fname = entry.Name;
                    if (Path.IsPathRooted(fname))
                    {
                        throw new BadPluginException("Bad plugin: contains absolute paths.");
                    }
                    string path = fname;
                    string component;
                    while (true)
                    {
                        component = Path.GetFileName(path);
                        if (component==".." || component==".")
                        {
                            throw new BadPluginException("Bad plugin: contains special path components.");
                        }
                        string parent = Path.GetDirectoryName(path);
                        if (parent=="")
                        {
                            if (component=="")
                            {
                                // Zip files use entries like "foo\" to indicate an empty
                                // directory called foo. The top level directory should not
                                // be empty.
                                throw new BadPluginException("Bad plugin: empty directory entry.");
                            }
                            topLevelDirectories.Add(component);
                            break;
                        }
                        path = parent;
                    }
                }
            }
            catch (NotSupportedException)
            {
                throw new BadPluginException("Bad plugin: filenames contain illegal characters.");
            }
            if (topLevelDirectories.Count != 1)
            {
                throw new BadPluginException("Bad plugin: doesn't have exactly 1 subdirectory.");
            }
            return topLevelDirectories.First();
        }

        public void Uninstall(string aUdn)
        {
            Uninstall(aUdn, true);
            UpdateAppList();
        }

        public void UninstallAllApps()
        {
            throw new NotImplementedException();
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
            string appDir = iAppsDirectory.GetAssemblySubdirectory(app.App.GetType().Assembly);
            app.App.Dispose();
            iAppsDirectory.DeleteSubdirectory(appDir, true);
            iApps.Remove(aUdn);
            return true;
        }

        public void Stop()
        {
            if (!iAppsStarted) return;
            //iAddinManager.RemoveExtensionNodeHandler("/ohOs/App", AppListChanged);
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
            // !!!! write history to disk (here or earlier)
        }

        public void Dispose()
        {
            Stop();
        }

        private void AppAdded(IApp aApp)
        {
            if (!iAppsStarted) return;

            var app = aApp;

            string appDirName;
            try
            {
                appDirName = iAppsDirectory.GetAssemblySubdirectory(app.GetType().Assembly);
            }
            catch (PluginFoundInWrongDirectoryException)
            {
                Logger.WarnFormat("Ignoring app found in wrong directory: {0} in {1}", app.Name, app.GetType().Assembly.CodeBase);
                return;
            }

            // Take care here! We don't want an app peeking at other apps'
            // settings by injecting crazy XPath nonsense into its name.
            string sanitizedName = GetSanitizedAppName(app);
            IConfigFileCollection appConfig = iConfiguration.GetSubcollection(
                el=>el
                    .Elements("app-settings")
                    .Where(e=>(string)e.Attribute("name")==sanitizedName)
                    .FirstOrDefault()
                );
            if (sanitizedName != appDirName)
            {
                Logger.ErrorFormat("Bad app: name ({0}) does not match directory ({1}).", app.Name, appDirName);
                return;
            }

            AppContext appContext = new AppContext
            {
                Configuration = appConfig,
                Device = null,
                Services = iFullPrivilegeAppServices,
                StaticPath = iAppsDirectory.GetAbsolutePathForSubdirectory(appDirName),
                StorePath = iStoreDirectory.GetAbsolutePathForAppDirectory(appDirName)
            };

            // Initialize the app to allow it to read its config files before we
            // query its Udn.
            app.Init(appContext);

            string udn = app.Udn;
            iAppDirsToAppUdns[appDirName] = udn;

            IDvDevice device = CreateAppDevice(app, udn);
            appContext.Device = device.RawDevice;

            var provider = iAppProviderConstructor(device.RawDevice, app);
            var change = HistoryItem.ItemType.EInstall;
            if (!iInitialising && Uninstall(udn, false))
            {
                change = HistoryItem.ItemType.EUpdate;
            }
            iApps.Add(udn, new PublishedApp(app, device, provider));

            try
            {
                Logger.InfoFormat("Starting app {0} (UDN={1} directory={2}).", sanitizedName, udn, appDirName);
                app.Start(appContext);
                Logger.InfoFormat("App started: {0} (UDN={1}).", sanitizedName, udn);
            }
            catch (Exception e)
            {
                Logger.ErrorFormat("Exception during app startup: {0}\n{1}", sanitizedName, e);
                throw;
            }
            device.SetEnabled();
            iHistory.Add(new HistoryItem(app.Name, change, udn));
        }

        static string GetSanitizedAppName(IApp app)
        {
            return app.Name.Replace("'", "").Replace("\"", "").Replace("\\","-").Replace("/","-").Replace(":","").Replace(";","");
        }

        IDvDevice CreateAppDevice(IApp app, string udn)
        {
            IDvDevice device = (app.ResourceManager == null
                ? iFullPrivilegeAppServices.DeviceFactory.CreateDeviceStandard(udn)
                : iFullPrivilegeAppServices.DeviceFactory.CreateDeviceStandard(udn, app.ResourceManager));
            // Set initial values for the attributes mandated by UPnP
            // These may be over-ridden by the Start function below
            device.SetAttribute("Upnp.Domain", "openhome.org");
            device.SetAttribute("Upnp.Type", "App");
            device.SetAttribute("Upnp.Version", "1");
            device.SetAttribute("Upnp.FriendlyName", app.Name);
            device.SetAttribute("Upnp.Manufacturer", "N/A");
            device.SetAttribute("Upnp.ModelName", "ohOs Application");
            device.SetAttribute("Core.LongPollEnable", "");

            return device;
        }

        private void UpdateAppList()
        {
            if (!iAppsStarted) return;
            iAddinManager.UpdateRegistry(AppAdded, aApp => { });
        }
    }
}
