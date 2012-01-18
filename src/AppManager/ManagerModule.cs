using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    /// <summary>
    /// Assembles and configures the components to make an app manager.
    /// Whereas unit tests want to test each component separately,
    /// applications just want all the components sensibly configured,
    /// connected and ready to use. That's what the module is
    /// responsible for.
    /// </summary>
    public class ManagerModule : IDisposable
    {
        private const string DefaultAppsDirectory = "InstalledApps";
        public IManager Manager { get; private set; }
        public ManagerModule(
            IAppServices aFullPrivilegeAppServices,
            IConfigFileCollection aConfiguration)
        {
            string storePath = aConfiguration.GetElementValueAsFilepath(e=>e.Element("system-settings").Element("store"));
            if (storePath == null)
            {
                storePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "store");
            }
            DefaultStoreDirectory storeDirectory = new DefaultStoreDirectory(storePath);
            string installBase = aConfiguration.GetElementValueAsFilepath(e=>e.Element("system-settings").Element("installed-apps"));
            if (installBase == null)
            {
                installBase = Path.Combine(storePath, DefaultAppsDirectory);
            }
            DefaultAppsDirectory appsDirectory = new DefaultAppsDirectory(installBase);
            DefaultAddinManager addinManager = new DefaultAddinManager(installBase, installBase, installBase);
            Manager = new Manager(
                aFullPrivilegeAppServices,
                aConfiguration,
                addinManager,
                appsDirectory,
                storeDirectory,
                (aDevice, aApp)=>new ProviderApp(aDevice, aApp),
                false);
        }

        public void Dispose()
        {
            Manager.Dispose();
            Manager = null;
        }
    }
}
