using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
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

    [Serializable]
    public class BadPluginException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public BadPluginException()
        {
        }

        public BadPluginException(string message) : base(message)
        {
        }

        public BadPluginException(string message, Exception inner) : base(message, inner)
        {
        }

        protected BadPluginException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

    public class ManagerImpl : IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(ManagerImpl));
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
                ((IDisposable)disposeSemaphore).Dispose();
                iApp.Stop(); // ???
                iApp.Dispose();
                iDevice.Dispose();
                iProvider.Dispose();
            }
        }

        private readonly string iInstallBase;
        private const string kAppsDirectory = "InstalledApps";
        private readonly Dictionary<string, PublishedApp> iApps;
        private readonly List<HistoryItem> iHistory;
        private bool iInitialising;
        readonly IAppServices iFullPrivilegeAppServices;
        private readonly IConfigFileCollection iConfiguration;
        string iStorePath;
        bool iAppsStarted;
        private readonly Dictionary<string, string> iAppDirsToAppUdns = new Dictionary<string, string>();

        public List<HistoryItem> History
        {
            get { return new List<HistoryItem>(iHistory); }
        }

        public ManagerImpl(IAppServices aFullPrivilegeAppServices, IConfigFileCollection aConfiguration, bool aAutoStart)
        {
            iFullPrivilegeAppServices = aFullPrivilegeAppServices;
            iConfiguration = aConfiguration;
            iStorePath = iConfiguration.GetElementValueAsFilepath(e=>e.Element("system-settings").Element("store"));
            if (iStorePath == null)
            {
                iStorePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "store");
            }
            iInstallBase = iConfiguration.GetElementValueAsFilepath(e=>e.Element("system-settings").Element("installed-apps"));
            if (iInstallBase == null)
            {
                    iInstallBase = System.IO.Path.Combine(iStorePath, kAppsDirectory);
            }
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
            AddinManager.Initialize(iInstallBase, iInstallBase, iInstallBase);
            AddinManager.AddExtensionNodeHandler("/ohOs/App", AppListChanged);
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
            var unzipper = new FastZip();
            //string target = System.IO.Path.Combine(iInstallBase, "Temp"); // hardcoding of 'Temp' not threadsafe
            string target = iInstallBase;
            string appDirName = VerifyPluginZip(aZipFile);
            if (iAppDirsToAppUdns.ContainsKey(appDirName))
            {
                string appUdn = iAppDirsToAppUdns[appDirName];
                IApp app = iApps[appUdn].App;
                Logger.InfoFormat("Updating app {0} (UDN={1}, directory={2}).", app.Name, app.Udn, appDirName);
                // TODO: Stop all apps.
                Directory.Delete(Path.Combine(target, appDirName), true);
            }
            else if (Directory.Exists(Path.Combine(target, appDirName)))
            {
                Logger.InfoFormat("Overwriting app in directory {0}.", appDirName);
                Directory.Delete(Path.Combine(target, appDirName), true);
            }
            else
            {
                Logger.InfoFormat("Installing new app in directory {0}.", appDirName);
            }
            unzipper.ExtractZip(aZipFile, target, "");
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
            ZipFile zf = new ZipFile(aZipFile);
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
            string appPath = app.App.AssemblyPath;
            app.App.Dispose();
            System.IO.Directory.Delete(appPath, true);
            iApps.Remove(aUdn);
            return true;
        }
        public void Stop()
        {
            if (!iAppsStarted) return;
            AddinManager.RemoveExtensionNodeHandler("/ohOs/App", AppListChanged);
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
        private static string GetAssemblyCodeBasePath(Assembly aAssembly)
        {
            // Note: different from Location when shadow-copied. This will return
            // the location the file was copied *from*, while Location would return
            // the shadow copy's path.
            return new Uri(aAssembly.CodeBase).LocalPath;
        }
        private void AppListChanged(object aSender, ExtensionNodeEventArgs aArgs)
        {
            if (!iAppsStarted) return;
            if (aArgs.Change == ExtensionChange.Remove)
            {
                return;
            }
            var app = (IApp)aArgs.ExtensionObject;

            string appDir = Path.GetDirectoryName(GetAssemblyCodeBasePath(app.GetType().Assembly));
            string appDirParent = Path.GetDirectoryName(appDir);
            string appDirName = Path.GetFileName(appDir);

            if (Path.GetFullPath(appDirParent) != Path.GetFullPath(iInstallBase))
            {
                Logger.WarnFormat("Ignoring app found in wrong directory: {0} in {1}", app.Name, appDir);
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
                Logger.ErrorFormat("Bad app: name ({0}) does not match directory ({1}).", app.Name, appDir);
                return;
            }

            AppContext appContext = new AppContext
            {
                Configuration = appConfig,
                Device = null,
                Services = iFullPrivilegeAppServices,
                StaticPath = appDir,
                StorePath = Path.Combine(iStorePath, Path.Combine("apps", sanitizedName))
            };

            // Initialize the app to allow it to read its config files before we
            // query its Udn.
            app.Init(appContext);

            string udn = app.Udn;
            Console.WriteLine("UDN:{0}", udn);
            iAppDirsToAppUdns[appDirName] = udn;

            DvDevice device = CreateAppDevice(app, udn);
            appContext.Device = device;

            var provider = new ProviderApp(device, app);
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

        static DvDevice CreateAppDevice(IApp app, string udn)
        {
            DvDevice device = (app.ResourceManager == null
                ? new DvDeviceStandard(udn)
                : new DvDeviceStandard(udn, app.ResourceManager));
            // Set initial values for the attributes mandated by UPnP
            // These may be over-ridden by the Start function below
            device.SetAttribute("Upnp.Domain", "openhome.org");
            device.SetAttribute("Upnp.Type", "App");
            device.SetAttribute("Upnp.Version", "1");
            device.SetAttribute("Upnp.FriendlyName", app.Name);
            device.SetAttribute("Upnp.Manufacturer", "N/A");
            device.SetAttribute("Upnp.ModelName", "ohOs Application");

            return device;
        }

        private void UpdateAppList()
        {
            if (!iAppsStarted) return;
            AddinManager.Registry.Update();
        }
    }
}
