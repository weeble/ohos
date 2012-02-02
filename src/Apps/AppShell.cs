using System;
using System.Collections.Generic;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
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
    }

    public class AppShell : IAppShell
    {
        private readonly object iLock = new object();
        private readonly ManagerImpl iImpl;

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
                iImpl = new ManagerImpl(
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
            }
        }

        public void Dispose()
        {
            lock (iLock)
            {
                iImpl.Dispose();
            }
        }

        public void Start()
        {
            lock (iLock)
            {
                iImpl.Start();
            }
        }

        public void Install(string aZipFile)
        {
            lock (iLock)
            {
                iImpl.Install(aZipFile);
            }
        }

        public void UninstallByUdn(string aUdn)
        {
            lock (iLock)
            {
                iImpl.UninstallByUdn(aUdn);
            }
        }

        public void UninstallByAppName(string aAppName)
        {
            lock (iLock)
            {
                iImpl.UninstallByAppName(aAppName);
            }
        }

        public void UninstallAllApps()
        {
            lock (iLock)
            {
                iImpl.UninstallAllApps();
            }
        }

        public void Stop()
        {
            lock (iLock)
            {
                iImpl.Stop();
            }
        }

        public IEnumerable<AppInfo> GetApps()
        {
            lock (iLock)
            {
                return iImpl.GetApps();
            }
        }
    }
}
