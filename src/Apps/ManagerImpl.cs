using System;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using log4net;
//using Mono.Addins;
using OpenHome.Net.Device;
using ICSharpCode.SharpZipLib.Zip;
using System.Collections.Generic;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;
using OpenHome.Os.Platform.Collections;
using OpenHome.Widget.Nodes;

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
        public IAppServices Services { get; private set; }
        public string StaticPath { get; private set; }
        public string StorePath { get; private set; }
        public IConfigFileCollection Configuration { get; private set; }
        public DvDevice Device { get; set; }

        public AppContext(IAppServices aServices, string aStaticPath, string aStorePath, IConfigFileCollection aConfiguration, DvDevice aDevice)
        {
            Services = aServices;
            StaticPath = aStaticPath;
            StorePath = aStorePath;
            Configuration = aConfiguration;
            Device = aDevice;
        }
    }


    public class ZipVerifier : IZipVerifier
    {
        readonly IZipReader iZipReader;

        public ZipVerifier(IZipReader aZipReader)
        {
            iZipReader = aZipReader;
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
                    Debug.Assert(fname != null); // Zip library should assure this.
                    if (Path.IsPathRooted(fname))
                    {
                        throw new BadPluginException("Bad plugin: contains absolute paths.");
                    }

                    string topLevelDirectory = VerifyPluginZipEntry(fname);
                    topLevelDirectories.Add(topLevelDirectory);
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

        /// <summary>
        /// Verify that the given filename in a plugin zip-file:
        ///     1. Isn't absolute.
        ///     2. Contains no ".." segments.
        ///     3. Is a file (not a directory).
        ///     4. Isn't a file at the top-level.
        /// </summary>
        /// <param name="aFname"></param>
        /// <returns>The top-level directory that contains the file.</returns>
        static string VerifyPluginZipEntry(string aFname)
        {
            string path = aFname;
            bool isTerminalComponent = true;
            while (true)
            {
                string component = Path.GetFileName(path);
                Debug.Assert(component != null);
                // Path.GetFileName can only return null
                // if path is null. (Which it's not.)

                if (component==".." || component==".")
                {
                    throw new BadPluginException("Bad plugin: contains special path components.");
                }

                string parent = Path.GetDirectoryName(path);
                Debug.Assert(parent != null);
                // Path.GetDirectoryName can only return
                // null if path is null or a root
                // directory. (It's not.)

                if (parent=="")
                {
                    if (component=="")
                    {
                        // Zip files use entries like "foo\" to indicate an empty
                        // directory called foo. The top level directory should not
                        // be empty.
                        throw new BadPluginException("Bad plugin: empty directory entry.");
                    }
                    if (isTerminalComponent)
                    {
                        throw new BadPluginException("Bad plugin: contains file at top-level.");
                    }
                    return component;
                }
                path = parent;
                isTerminalComponent = false;
            }
        }
    }

    public interface IZipVerifier
    {
        /// <summary>
        /// Verify that the plugin installs to a single subdirectory,
        /// and return the name of that subdirectory.
        /// </summary>
        /// <param name="aZipFile"></param>
        /// <returns></returns>
        string VerifyPluginZip(string aZipFile);
    }

    public enum AppState
    {
        Running,
        NotRunning,
    }

    public class AppInfo
    {
        public string Name { get; private set; }
        public AppState State { get; private set; }
        public bool PendingUpdate { get; private set; }
        public bool PendingDelete { get; private set; }
        public string Udn { get; private set; }

        public AppInfo(string aName, AppState aState, bool aPendingUpdate, bool aPendingDelete, string aUdn)
        {
            Name = aName;
            State = aState;
            PendingUpdate = aPendingUpdate;
            PendingDelete = aPendingDelete;
            Udn = aUdn;
        }
    }

    public class ManagerImpl : IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ManagerImpl));
        private class PublishedApp : IDisposable
        {
            public IApp App { get { return iApp; } }
            public string Udn { get { return iUdn; } }
            private readonly IApp iApp;
            private readonly IDvDevice iDevice;
            private readonly IDvProviderOpenhomeOrgApp1 iProvider;
            readonly string iUdn;

            public PublishedApp(IApp aApp, IDvDevice aDevice, IDvProviderOpenhomeOrgApp1 aProvider)
            {
                iApp = aApp;
                iDevice = aDevice;
                iProvider = aProvider;
                iUdn = aDevice.Udn();
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

        private class KnownApp
        {
            readonly IAppMetadataStore iMetadataStore;
            readonly IAppsDirectory iAppsDirectory;
            readonly IZipVerifier iZipVerifier;

            public string AppName { get; private set; }

            public void WriteAppMetadata(AppMetadata value)
            {
                if (value.AppName != AppName)
                {
                    throw new ArgumentException("AppMetadata has incorrect AppName");
                }
                iMetadataStore.PutApp(value);
            }

            public AppMetadata ReadAppMetadata()
            {
                return iMetadataStore.GetApp(AppName);
            }

            public KnownApp(string aAppName, IAppMetadataStore aMetadataStore, IAppsDirectory aAppsDirectory, IZipVerifier aZipVerifier)
            {
                AppName = aAppName;
                iMetadataStore = aMetadataStore;
                iZipVerifier = aZipVerifier;
                iAppsDirectory = aAppsDirectory;
            }

            public PublishedApp PublishedApp { get; private set; }
            public bool IsPublished { get { return PublishedApp != null; } }
            public bool DirectoryExists { get { return iAppsDirectory.DoesSubdirectoryExist(AppName); } }
            public bool HasCodeLoaded { get; private set; }

            public void Publish(PublishedApp aPublishedApp)
            {
                // Regardless of whether we succeed to publish, record that we've
                // loaded code for this app, because it will stop us from deleting
                // it later, even if it is first unpublished.
                HasCodeLoaded = true;
                if (IsPublished)
                {
                    throw new InvalidOperationException("App is already published.");
                }
                if (aPublishedApp.App.Name != AppName)
                {
                    throw new ArgumentException(String.Format("IApp has incorrect Name. (Expected '{0}', got '{1}'.)", AppName, aPublishedApp.App.Name));
                }
                PublishedApp = aPublishedApp;
            }

            public void DeleteNow()
            {
                if (IsPublished)
                {
                    throw new InvalidOperationException("Cannot delete a published app.");
                }
                if (HasCodeLoaded)
                {
                    throw new InvalidOperationException("Cannot delete an app while it has code loaded.");
                }
                Logger.InfoFormat("Delete app {0}", AppName);
                if (iAppsDirectory.DoesSubdirectoryExist(AppName))
                {
                    iAppsDirectory.DeleteSubdirectory(AppName, true);
                }
                iMetadataStore.DeleteApp(AppName);
            }

            void ScheduleDelete()
            {
                Logger.InfoFormat("Schedule delete on restart for {0}", AppName);
                var metadata = ReadAppMetadata();
                metadata.DeletePending = true;
                WriteAppMetadata(metadata);
            }

            public void Delete()
            {
                if (HasCodeLoaded)
                {
                    ScheduleDelete();
                }
                else
                {
                    DeleteNow();
                }
            }

            public void Unpublish()
            {
                if (!IsPublished)
                {
                    throw new InvalidOperationException("App is not published.");
                }
                PublishedApp.Dispose();
                PublishedApp = null;
            }

            public void Upgrade(string aPersistentLocalPath)
            {
                ScheduleUpgrade(aPersistentLocalPath);
                if (!HasCodeLoaded)
                {
                    UpgradeNow();
                }
            }

            void ScheduleUpgrade(string aPersistentLocalPath)
            {
                Logger.InfoFormat("Schedule upgrade on restart for {0}", AppName);
                var metadata = ReadAppMetadata();
                metadata.LocalInstallLocation = aPersistentLocalPath;
                metadata.InstallPending = true;
                // If we previously scheduled a delete, the install supercedes it.
                metadata.DeletePending = false;
                WriteAppMetadata(metadata);
            }

            public void UpgradeNow()
            {
                Logger.InfoFormat("Install/upgrade app {0}", AppName);
                if (HasCodeLoaded)
                {
                    throw new InvalidOperationException("Cannot upgrade an app while it has code loaded.");
                }
                var metadata = ReadAppMetadata();
                string zipAppName = iZipVerifier.VerifyPluginZip(metadata.LocalInstallLocation);
                if (zipAppName != AppName)
                {
                    metadata.InstallPending = false;
                    WriteAppMetadata(metadata);
                    Logger.WarnFormat(
                        "App upgrade rejected because app name didn't match. Expected '{0}', but found '{1}' inside '{2}'.",
                        AppName, zipAppName, metadata.LocalInstallLocation);
                    return;
                }
                if (iAppsDirectory.DoesSubdirectoryExist(AppName))
                {
                    iAppsDirectory.DeleteSubdirectory(AppName, true);
                }
                iAppsDirectory.InstallZipFile(metadata.LocalInstallLocation);
                metadata.InstallPending = false;
                WriteAppMetadata(metadata);
            }

            public void ResolvePendingOperations()
            {
                if (HasCodeLoaded)
                {
                    throw new InvalidOperationException("Cannot resolve pending operations while app has code loaded.");
                }
                var metadata = ReadAppMetadata();
                if (metadata.DeletePending)
                {
                    DeleteNow();
                    return;
                }
                if (metadata.InstallPending)
                {
                    UpgradeNow();
                }
            }
            
        }

        private readonly List<HistoryItem> iHistory;
        readonly IAppServices iFullPrivilegeAppServices;
        private readonly IConfigFileCollection iConfiguration;
        bool iAppsStarted;
        readonly IAddinManager iAddinManager;
        readonly IAppsDirectory iAppsDirectory;
        readonly IStoreDirectory iStoreDirectory;
        readonly Func<DvDevice, IApp, IDvProviderOpenhomeOrgApp1> iAppProviderConstructor;
        //readonly IZipReader iZipReader;
        readonly IAppMetadataStore iMetadataStore;
        readonly IZipVerifier iZipVerifier;
        readonly INodeRebooter iNodeRebooter;
        readonly Bimap<string, string> iUdnsToAppNamesBimap = new Bimap<string, string>();

        IDictionary<string, string> UdnsToAppNames { get { return iUdnsToAppNamesBimap.Forward; } }
        IDictionary<string, string> AppNamesToUdns { get { return iUdnsToAppNamesBimap.Backward; } }
        readonly Dictionary<string, KnownApp> iKnownApps = new Dictionary<string, KnownApp>();

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
            IAppMetadataStore aMetadataStore,
            IZipVerifier aZipVerifier,
            bool aAutoStart)
        {
            iFullPrivilegeAppServices = aFullPrivilegeAppServices;
            iZipVerifier = aZipVerifier;
            //iZipReader = aZipReader;
            iMetadataStore = aMetadataStore;
            iConfiguration = aConfiguration;
            iAddinManager = aAddinManager;
            iAppsDirectory = aAppsDirectory;
            iStoreDirectory = aStoreDirectory;
            iAppProviderConstructor = aAppProviderConstructor;
            iNodeRebooter = iFullPrivilegeAppServices.NodeRebooter;
            //iApps = new Dictionary<string, PublishedApp>();
            iHistory = new List<HistoryItem>();
            // !!!! restore previous history from disk
            iKnownApps = new Dictionary<string, KnownApp>();
            foreach (var app in iMetadataStore.LoadAppsFromStore())
            {
                GetOrCreateKnownApp(app.AppName);
            }
            foreach (string dirname in iAppsDirectory.GetAppSubdirectories())
            {
                GetOrCreateKnownApp(dirname);
            }
            if (aAutoStart)
            {
                Start();
            }
        }

        public IEnumerable<AppInfo> GetApps()
        {
            List<AppInfo> apps = new List<AppInfo>();
            foreach (var app in iKnownApps.Values)
            {
                var metadata = app.ReadAppMetadata();
                string udn = null;
                if (app.IsPublished)
                {
                    udn = app.PublishedApp.Udn;
                }
                apps.Add(new AppInfo(app.AppName, app.IsPublished ? AppState.Running : AppState.NotRunning, metadata!=null && metadata.InstallPending, metadata!=null && metadata.DeletePending, udn));
            }
            return apps;
        }

        KnownApp GetOrCreateKnownApp(string aAppName)
        {
            if (aAppName == null) throw new ArgumentNullException("aAppName");
            KnownApp app;
            if (!iKnownApps.TryGetValue(aAppName, out app))
            {
                app = new KnownApp(aAppName, iMetadataStore, iAppsDirectory, iZipVerifier);
                iKnownApps[aAppName] = app;
            }
            if (app.ReadAppMetadata() == null)
            {
                app.WriteAppMetadata(
                    new AppMetadata
                    {
                        AppName = aAppName,
                        DeletePending = false,
                        GrantedPermissions = new List<string>(),
                        InstallPending = false,
                        LocalInstallLocation = null
                    });
            }
            return app;
        }

        public void Start()
        {
            if (iAppsStarted) return;
            iAppsStarted = true;
            List<string> deletedApps = new List<string>();
            foreach (var knownApp in iKnownApps.Values)
            {
                knownApp.ResolvePendingOperations();
                if (!knownApp.DirectoryExists && knownApp.ReadAppMetadata() == null)
                {
                    deletedApps.Add(knownApp.AppName);
                }
            }
            foreach (string appName in deletedApps)
            {
                iKnownApps.Remove(appName);
            }
            //iInitialising = false;
            UpdateAppList();
            //iInitialising = true;
        }

        /// <summary>
        /// Install a plugin.
        /// </summary>
        /// <param name="aPersistentZipFile"></param>
        /// <remarks>
        /// The zip file should not be in a location writeable by anyone
        /// untrusted - no attempt is made to ensure that the file isn't
        /// modified while being read, meaning that malicious timing of
        /// modifications could be used to circumvent some verification.
        /// </remarks>
        public void Install(string aPersistentZipFile)
        {
            //string target = System.IO.Path.Combine(iInstallBase, "Temp"); // hardcoding of 'Temp' not threadsafe
            string appDirName = iZipVerifier.VerifyPluginZip(aPersistentZipFile);
            var knownApp = GetOrCreateKnownApp(appDirName);
            knownApp.Upgrade(aPersistentZipFile);
            if (knownApp.ReadAppMetadata().InstallPending)
            {
                iNodeRebooter.SoftRestartNode();
            }
            else
            {
                UpdateAppList();
            }
        }

        

        public void UninstallByUdn(string aUdn)
        {
            UninstallByUdn(aUdn, true);
            UpdateAppList();
        }

        public void UninstallByAppName(string aAppName)
        {
            UninstallByAppName(aAppName, true);
        }

        public void UninstallAllApps()
        {
            throw new NotImplementedException();
        }

        private bool UninstallByUdn(string aUdn, bool aUpdateHistory)
        {
            string appName;
            if (!UdnsToAppNames.TryGetValue(aUdn, out appName))
            {
                return false;
            }
            return UninstallByAppName(appName, aUpdateHistory);
        }

        private bool UninstallByAppName(string aAppName, bool aUpdateHistory)
        {
            KnownApp app;
            if (!iKnownApps.TryGetValue(aAppName, out app))
            {
                return false;
            }
            if (app.IsPublished)
            {
                app.Unpublish();
            }

            AppNamesToUdns.Remove(aAppName); // Bimap cleans up inverse udn-to-appname.

            app.Delete();
            if (!app.DirectoryExists)
            {
                iKnownApps.Remove(aAppName);
            }
            return true;
        }

        public void Stop()
        {
            if (!iAppsStarted) return;
            List<Exception> exceptions = new List<Exception>();
            foreach (var app in iKnownApps.Values)
            {
                if (!app.IsPublished)
                {
                    continue;
                }
                try
                {
                    app.Unpublish();
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
            AppNamesToUdns.Clear();
            // !!!! write history to disk (here or earlier)
        }

        public void Dispose()
        {
            Stop();
        }

        private void AppAdded(DirectoryInfo aAppDirectoryInfo, IApp aApp)
        {
            if (!iAppsStarted) return;

            var app = aApp;

            string appDirName = aAppDirectoryInfo.Name;

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

            KnownApp knownApp = GetOrCreateKnownApp(appDirName);
            if (knownApp.IsPublished)
            {
                Logger.ErrorFormat("Bad app: multiple apps started from directory {0}.", appDirName);
                return;
            }
            AppMetadata appMetadata = knownApp.ReadAppMetadata();
            if (!appMetadata.GrantedPermissions.Contains("root"))
            {
                Logger.WarnFormat("App {0} is running with more permissions that it needs.", knownApp.AppName);
            }

            AppContext appContext = new AppContext(iFullPrivilegeAppServices,
                iAppsDirectory.GetAbsolutePathForSubdirectory(appDirName),
                iStoreDirectory.GetAbsolutePathForAppDirectory(appDirName), 
                appConfig,
                null);

            // Initialize the app to allow it to read its config files before we
            // query its Udn.
            app.Init(appContext);

            string udn = app.Udn;

            IDvDevice device = CreateAppDevice(app, udn);
            appContext.Device = device.RawDevice;

            var provider = iAppProviderConstructor(device.RawDevice, app);
            var change = HistoryItem.ItemType.EInstall;

            // TODO: Fix History. It no longer bears any relation to how apps actually work.
            //if (!iInitialising && Uninstall(udn, false))
            //{
            //    change = HistoryItem.ItemType.EUpdate;
            //}

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
            knownApp.Publish(new PublishedApp(app, device, provider));
            UdnsToAppNames[udn] = appDirName;
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
            iAddinManager.UpdateRegistry(AppAdded, (aAppDir, aApp) => { });
        }
    }
}
