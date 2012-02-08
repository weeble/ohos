using System;
using System.Collections.Generic;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{
    public interface IAppShell : IDisposable
    {
        void Start();
        void Install(string aZipFile);
        void UninstallByUdn(string aUdn);
        void UninstallByAppName(string aAppName);
        void UninstallAllApps();
        void Stop();
        IEnumerable<AppInfo> GetApps();
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
            Func<DvDevice, IApp, IDvProviderOpenhomeOrgApp1> aAppProviderConstructor,
            IZipReader aZipReader,
            IAppMetadataStore aAppMetadataStore, IZipVerifier aZipVerifier,
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
            Action invokeQueuedEvents;
            lock (iLock)
            {
                iImpl.Start();
                invokeQueuedEvents = DeferQueuedEvents();
            }
            invokeQueuedEvents();
        }

        /// <summary>
        /// Install an app from a local file. If an old version of the app is
        /// currently running, defer installation until restart and then schedule
        /// a restart.
        /// </summary>
        /// <param name="aZipFile"></param>
        public void Install(string aZipFile)
        {
            Action invokeQueuedEvents;
            lock (iLock)
            {
                iImpl.Install(aZipFile);
                invokeQueuedEvents = DeferQueuedEvents();
            }
            invokeQueuedEvents();
        }

        /// <summary>
        /// Stop a running app and schedule it for deletion on restart.
        /// </summary>
        /// <param name="aUdn"></param>
        public void UninstallByUdn(string aUdn)
        {
            Action invokeQueuedEvents;
            lock (iLock)
            {
                iImpl.UninstallByUdn(aUdn);
                invokeQueuedEvents = DeferQueuedEvents();
            }
            invokeQueuedEvents();
        }

        /// <summary>
        /// Stop a running app and schedule it for deletion on restart.
        /// </summary>
        public void UninstallByAppName(string aAppName)
        {
            Action invokeQueuedEvents;
            lock (iLock)
            {
                iImpl.UninstallByAppName(aAppName);
                invokeQueuedEvents = DeferQueuedEvents();
            }
            invokeQueuedEvents();
        }

        public void UninstallAllApps()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Stop all apps.
        /// </summary>
        public void Stop()
        {
            Action invokeQueuedEvents;
            lock (iLock)
            {
                iImpl.Stop();
                invokeQueuedEvents = DeferQueuedEvents();
            }
            invokeQueuedEvents();
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
