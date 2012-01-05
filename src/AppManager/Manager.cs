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
                ((IDisposable)disposeSemaphore).Dispose();
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
        private bool iInitialising;
        readonly IAppServices iFullPrivilegeAppServices;
        private readonly IConfigFileCollection iConfiguration;
        string iStorePath;
        bool iAppsStarted;

        public List<HistoryItem> History
        {
            get { lock (iHistory) { return iHistory; } }
        }

        public Manager(string aInstallBase, IAppServices aFullPrivilegeAppServices, IConfigFileCollection aConfiguration, bool aAutoStart)
        {
            iFullPrivilegeAppServices = aFullPrivilegeAppServices;
            iConfiguration = aConfiguration;
            iStorePath = iConfiguration.GetElementValueAsFilepath(e=>e.Element("system-settings").Element("store"));
            if (iStorePath == null)
            {
                iStorePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "store");
            }
            iInstallBase = System.IO.Path.Combine(aInstallBase, kAppsDirectory);
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

        public void Install(string aZipFile)
        {
            var unzipper = new FastZip();
            //string target = System.IO.Path.Combine(iInstallBase, "Temp"); // hardcoding of 'Temp' not threadsafe
            string target = iInstallBase;
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
            lock (iApps)
            {
                Uninstall(aUdn, true);
            }
            UpdateAppList();
        }
        public void UninstallAllApps()
        {

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
        public void Dispose()
        {
            Stop();
        }
        private void AppListChanged(object aSender, ExtensionNodeEventArgs aArgs)
        {
            if (!iAppsStarted) return;
            if (aArgs.Change == ExtensionChange.Remove)
            {
                return;
            }
            var app = (IApp)aArgs.ExtensionObject;

            // Take care here! We don't want an app peeking at other apps'
            // settings by injecting crazy XPath nonsense into its name.
            string sanitizedName = app.Name.Replace("'", "").Replace("\"", "").Replace("\\","-").Replace("/","-");
            IConfigFileCollection appConfig = iConfiguration.GetSubcollection(
                el=>el
                    .Elements("app-settings")
                    .Where(e=>(string)e.Attribute("name")==sanitizedName)
                    .FirstOrDefault()
                );

            AppContext appContext = new AppContext
            {
                Configuration = appConfig,
                Device = null,
                Services = iFullPrivilegeAppServices,
                StaticPath = iInstallBase,
                StorePath = Path.Combine(iStorePath, Path.Combine("apps", sanitizedName))
            };

            // Initialize the app to allow it to read its config files before we
            // query its Udn.
            app.Init(appContext);

            Console.WriteLine("UDN:{0}", app.Udn);

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

            appContext.Device = device;

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
            // TODO: This locking is a mess. Fix it.
            // Here we lock on 'this', elsewhere 'iApps' and 'iHistory'.
            lock (this)
            {
                if (!iAppsStarted) return;
                AddinManager.Registry.Update();
            }
        }
    }
}
