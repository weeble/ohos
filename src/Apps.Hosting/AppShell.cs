using System;
using System.Collections.Generic;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;
using OpenHome.XappForms;

namespace OpenHome.Os.Apps
{
    public interface IAppShell : IDisposable
    {
        /// <summary>
        /// Start running apps.
        /// </summary>
        void Start();
        /// <summary>
        /// Install or upgrade from the given app package.
        /// </summary>
        /// <param name="aZipFile"></param>
        void Install(string aZipFile);
        /// <summary>
        /// Install the given app package, or throw BadPluginException if it
        /// collides with an existing app.
        /// </summary>
        /// <param name="aZipFile"></param>
        void InstallNew(string aZipFile);
        /// <summary>
        /// Install the given app package. Flag it for automatic update
        /// from the specified HTTP/HTTPS URL, as soon as the Last-Modified
        /// header changes.
        /// </summary>
        /// <param name="aZipFile"></param>
        /// <param name="aUrl"></param>
        /// <param name="aLastModified"></param>
        void InstallNew(string aZipFile, string aUrl, DateTime aLastModified);
        /// <summary>
        /// Upgrade the named app with the given package. Throws
        /// BadPluginException if the package does not match.
        /// </summary>
        /// <param name="aAppName"></param>
        /// <param name="aZipFile"></param>
        void Upgrade(string aAppName, string aZipFile);
        /// <summary>
        /// Upgrade the named app with the given package. Throws
        /// BadPluginException if the package does not match. Further
        /// automatic updates will occur from the specified HTTP/HTTPS URL,
        /// as soon as the Last-Modified header changes.
        /// </summary>
        /// <param name="aAppName"></param>
        /// <param name="aZipFile"></param>
        /// <param name="aUrl"></param>
        /// <param name="aLastModified"></param>
        void Upgrade(string aAppName, string aZipFile, string aUrl, DateTime aLastModified);
        /// <summary>
        /// Uninstall the running app with given UDN.
        /// </summary>
        /// <param name="aUdn"></param>
        void UninstallByUdn(string aUdn);
        /// <summary>
        /// Uninstall the app with the given name.
        /// </summary>
        /// <param name="aAppName"></param>
        void UninstallByAppName(string aAppName);
        /// <summary>
        /// Uninstall all apps.
        /// </summary>
        void UninstallAllApps();
        /// <summary>
        /// Stop running apps.
        /// </summary>
        void Stop();
        /// <summary>
        /// Get information on all the apps.
        /// </summary>
        /// <returns></returns>
        IEnumerable<AppInfo> GetApps();
        /// <summary>
        /// Subscribe for notifications when apps are installed/uninstalled/started/stopped.
        /// </summary>
        event EventHandler<AppStatusChangeEventArgs> AppStatusChanged;
    }

    /// <summary>
    /// Hosts apps in a process.
    /// </summary>
    public class AppShell : IAppShell
    {
        private readonly object iLock = new object();
        private readonly AppShellImpl iImpl;

        public List<HistoryItem> History
        {
            get
            {
                lock (iLock)
                {
                    return iImpl.History;
                }
            }
        }

        /// <summary>
        /// Create an app shell. Hosted apps are not automatically started.
        /// </summary>
        /// <param name="aFullPrivilegeAppServices">
        /// Services that will be provided to apps granted permissions.
        /// (If we implement restricted permissions, such apps would
        /// receive only a subset of these services.)
        /// </param>
        /// <param name="aConfiguration">
        /// Parsed config files. Some pre-installed apps need to read
        /// configuration  information (such as location of serial devices)
        /// from these files.
        /// </param>
        /// <param name="aAddinManager">
        /// Interface to the addin manager that handles actual loading of
        /// plugins. (Currently we use MEF.)
        /// </param>
        /// <param name="aAppsDirectory">
        /// Interface to inspect and manipulate the apps directory, where
        /// we put app binaries and their static data.
        /// </param>
        /// <param name="aStoreDirectory">
        /// Interface to inspect and manipulate the store directory, where
        /// apps store their dynamic, persistent data.
        /// </param>
        /// <param name="aAppProviderConstructor">
        /// Constructor to create an AppProvider. The AppShell is responsible
        /// for creating a device for each app and publishing the app service
        /// on that device on behalf of the app, and it uses this to construct
        /// such a provider. (Unit tests need to be able to pass in a
        /// substitute here.
        /// </param>
        /// <param name="aZipReader">
        /// Reads entries from a zip file.
        /// </param>
        /// <param name="aAppMetadataStore">
        /// Stores persistent data about apps, such as deferred deletions or
        /// upgrades.
        /// </param>
        /// <param name="aZipVerifier">
        /// Verifies that a zip file contains a valid OpenHome app.
        /// </param>
        /// <param name="aAutoStart">
        /// If true, start the AppShell immediately. Otherwise, caller needs
        /// to call Start() when they want to start apps.
        /// </param>
        public AppShell(
            IAppServices aFullPrivilegeAppServices,
            IConfigFileCollection aConfiguration,
            IAddinManager aAddinManager,
            IAppsDirectory aAppsDirectory,
            IStoreDirectory aStoreDirectory,
            Func<DvDevice, string, string, string, IDvProviderOpenhomeOrgApp1> aAppProviderConstructor,
            IZipReader aZipReader,
            IAppMetadataStore aAppMetadataStore, IZipVerifier aZipVerifier,
            ISystemAppsConfiguration aSystemAppsConfiguration,
            IXappServer aXappServer,
            bool aAutoStart)
        {
            lock (iLock)
            {
                iImpl = new AppShellImpl(
                    aFullPrivilegeAppServices,
                    aConfiguration,
                    aAddinManager,
                    aAppsDirectory,
                    aStoreDirectory,
                    aAppProviderConstructor,
                    aZipReader,
                    aAppMetadataStore,
                    aZipVerifier,
                    aSystemAppsConfiguration,
                    aXappServer,
                    aAutoStart);
                iImpl.AppStatusChanged += OnAppStatusChanged;
            }
        }

        List<AppStatusChangeEventArgs> iEventQueue = new List<AppStatusChangeEventArgs>();

        void OnAppStatusChanged(object aSender, AppStatusChangeEventArgs aE)
        {
            iEventQueue.Add(aE);
        }

        Action DeferQueuedEvents()
        {
            var queuedEvents = iEventQueue;
            var handler = iAppStatusChanged;
            iEventQueue = new List<AppStatusChangeEventArgs>();
            return () =>
                {
                    if (handler != null)
                    {
                        foreach (var ev in queuedEvents)
                        {
                            handler(this, ev);
                        }
                    }
                };

        }

        EventHandler<AppStatusChangeEventArgs> iAppStatusChanged;
        public event EventHandler<AppStatusChangeEventArgs> AppStatusChanged
        {
            add { iAppStatusChanged += value; }
            remove { iAppStatusChanged -= value; }
        }

        private void InvokeAndFireEvents(Action aAction)
        {
            Action invokeQueuedEvents;
            lock (iLock)
            {
                aAction();
                invokeQueuedEvents = DeferQueuedEvents();
            }
            invokeQueuedEvents();
        }

        public void Dispose()
        {
            lock (iLock)
            {
                iImpl.Dispose();
            }
        }

        /// <summary>
        /// Performs any deferred operations, then starts all apps.
        /// </summary>
        public void Start()
        {
            InvokeAndFireEvents(() => iImpl.Start());
        }

        /// <summary>
        /// Install an app from a local file. If an old version of the app is
        /// currently running, defer installation until restart and then schedule
        /// a restart.
        /// </summary>
        /// <param name="aZipFile"></param>
        public void Install(string aZipFile)
        {
            InvokeAndFireEvents(()=>iImpl.Install(aZipFile));
        }

        public void InstallNew(string aZipFile)
        {
            InvokeAndFireEvents(() => iImpl.InstallNew(aZipFile));
        }


        public void InstallNew(string aZipFile, string aUrl, DateTime aLastModified)
        {
            InvokeAndFireEvents(() => iImpl.InstallNew(aZipFile, aUrl, aLastModified));
        }


        public void Upgrade(string aAppName, string aZipFile)
        {
            InvokeAndFireEvents(() => iImpl.Upgrade(aAppName, aZipFile));
        }

        public void Upgrade(string aAppName, string aZipFile, string aUrl, DateTime aLastModified)
        {
            InvokeAndFireEvents(() => iImpl.Upgrade(aAppName, aZipFile, aUrl, aLastModified));
        }

        /// <summary>
        /// Stop a running app and schedule it for deletion on restart.
        /// </summary>
        /// <param name="aUdn"></param>
        public void UninstallByUdn(string aUdn)
        {
            InvokeAndFireEvents(() => iImpl.UninstallByUdn(aUdn));
        }

        /// <summary>
        /// Stop a running app and schedule it for deletion on restart.
        /// </summary>
        public void UninstallByAppName(string aAppName)
        {
            InvokeAndFireEvents(() => iImpl.UninstallByAppName(aAppName));
        }

        public void UninstallAllApps()
        {
            InvokeAndFireEvents(() => iImpl.UninstallAllApps());
        }

        /// <summary>
        /// Stop all apps.
        /// </summary>
        public void Stop()
        {
            InvokeAndFireEvents(() => iImpl.Stop());
        }

        /// <summary>
        /// Get information about all apps, including those pending deletion
        /// or currently unable to run.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AppInfo> GetApps()
        {
            lock (iLock)
            {
                return iImpl.GetApps();
            }
        }
    }
}
