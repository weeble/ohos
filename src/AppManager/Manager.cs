using System;
using System.Collections.Generic;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    public interface IManager : IDisposable
    {
        void Start();
        void Install(string aZipFile);
        void Uninstall(string aUdn);
        void UninstallAllApps();
        void Stop();
    }

    public class Manager : IManager
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

        public Manager(
            IAppServices aFullPrivilegeAppServices,
            IConfigFileCollection aConfiguration,
            IAddinManager aAddinManager,
            IAppsDirectory aAppsDirectory,
            IStoreDirectory aStoreDirectory,
            Func<DvDevice, IApp, IDvProviderOpenhomeOrgApp1> aAppProviderConstructor,
            bool aAutoStart)
        {
            lock (iLock)
            {
                iImpl = new ManagerImpl(aFullPrivilegeAppServices, aConfiguration, aAddinManager, aAppsDirectory, aStoreDirectory, aAppProviderConstructor, aAutoStart);
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

        public void Uninstall(string aUdn)
        {
            lock (iLock)
            {
                iImpl.Uninstall(aUdn);
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
    }
}
