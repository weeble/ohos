using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{

    public class DefaultZipReader : IZipReader
    {
        class ZipContent : IZipContent
        {
            readonly ZipFile iZipFile;

            public ZipContent(ZipFile aZipFile)
            {
                iZipFile = aZipFile;
            }

            public IEnumerator<ZipEntry> GetEnumerator()
            {
                return iZipFile.Cast<ZipEntry>().GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Dispose()
            {
                iZipFile.Close();
            }
        }


        public IZipContent Open(string aZipName)
        {
            ZipFile zipFile = new ZipFile(aZipName);
            return new ZipContent(zipFile);
        }
    }

    /// <summary>
    /// Assembles and configures the components to make an app shell.
    /// Whereas unit tests want to test each component separately,
    /// applications just want all the components sensibly configured,
    /// connected and ready to use. That's what the module is
    /// responsible for.
    /// </summary>
    public class AppShellModule : IDisposable
    {
        private const string DefaultAppsDirectory = "InstalledApps";
        public IAppShell AppShell { get; private set; }
        public AppShellModule(
            IAppServices aFullPrivilegeAppServices,
            IConfigFileCollection aConfiguration,
            string aNodeGuid)
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
            DefaultZipReader zipReader = new DefaultZipReader();
            //DefaultAddinManager addinManager = new DefaultAddinManager(installBase, installBase, installBase);
            var addinManager = new MefAddinManager(appsDirectory);
            AppMetadataStore appMetadataStore = new AppMetadataStore(new DirectoryInfo(Path.Combine(storePath, "_installed")));
            ZipVerifier zipVerifier = new ZipVerifier(zipReader);
            AppShell = new AppShell(
                aFullPrivilegeAppServices,
                aConfiguration,
                addinManager,
                appsDirectory,
                storeDirectory,
                (aDevice, aApp, aAppName)=>new ProviderApp(aDevice, aApp, aNodeGuid, String.Format("/{0}/Upnp/Resources/", aNodeGuid), aAppName),
                zipReader,
                appMetadataStore,
                zipVerifier,
                false);
        }

        public void Dispose()
        {
            AppShell.Dispose();
            AppShell = null;
        }
    }
}
