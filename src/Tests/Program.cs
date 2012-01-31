using System;
using System.IO;
using System.Xml.Linq;
using Node;
using OpenHome.Os.AppManager;
using OpenHome.Net.Core;
using OpenHome.Os.Platform;
using OpenHome.Widget.Nodes;

namespace OpenHome.Os
{
    class NullNodeRebooter : INodeRebooter
    {
        public void RebootNode()
        {
        }
        public void SoftRestartNode()
        {
        }
    }
    public class Program
    {
        static void Main(string[] aArgs)
        {
            InitParams initParams = new InitParams();
            initParams.UseLoopbackNetworkAdapter = true;
            using (Library library = Library.Create(initParams))
            {
                SubnetList subnetList = new SubnetList();
                NetworkAdapter nif = subnetList.SubnetAt(0);
                uint subnet = nif.Subnet();
                subnetList.Destroy();
                /*var combinedStack = */ library.StartCombined(subnet);
                AppServices services = new AppServices();
                services.NodeRebooter = new NullNodeRebooter();
                string exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string storePath = Path.Combine(exePath, "test-store");
                ConfigFileCollection config = new ConfigFileCollection(new string[] { });
                config.AddFile("config.xml",
                    new XElement("ohos",
                        new XElement("system-settings",
                            new XElement("store", storePath)
                        )
                    )
                );
                if (Directory.Exists(storePath))
                {
                    Directory.Delete(storePath, true);
                }
                using (var installModule = new ManagerModule(services, config))
                {
                    installModule.Manager.Start();

                    installModule.Manager.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.zip"));
                }
            }
        }
    }
}
