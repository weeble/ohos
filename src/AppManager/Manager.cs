using System;
using System.Collections.Generic;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    public class Manager : IDisposable
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
        public Manager(IAppServices aFullPrivilegeAppServices, IConfigFileCollection aConfiguration, bool aAutoStart)
        {
            lock (iLock)
            {
                iImpl = new ManagerImpl(aFullPrivilegeAppServices, aConfiguration, aAutoStart);
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
