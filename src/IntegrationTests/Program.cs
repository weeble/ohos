using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using log4net.Config;
using Node;
using OpenHome.Net.Device;
using OpenHome.Os.Apps;
using OpenHome.Net.Core;
using OpenHome.Os.Host;
using OpenHome.Widget.Nodes;

namespace OpenHome.Os.IntegrationTests
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
        static void PrintAppState(AppInfo aAppInfo)
        {
            Console.Error.WriteLine("Name={0}, State={1}, PendingUpdate={2}, PendingDelete={3}, Udn={4}", aAppInfo.Name, aAppInfo.State, aAppInfo.PendingUpdate, aAppInfo.PendingDelete, aAppInfo.Udn);
        }

        static int Main(string[] aArgs)
        {
            BasicConfigurator.Configure();
            InitParams initParams = new InitParams();
            initParams.UseLoopbackNetworkAdapter = true;
            string nodeGuid = Guid.NewGuid().ToString();
            using (Library library = Library.Create(initParams))
            {
                SubnetList subnetList = new SubnetList();
                NetworkAdapter nif = subnetList.SubnetAt(0);
                uint subnet = nif.Subnet();
                subnetList.Destroy();
                var combinedStack = library.StartCombined(subnet);
                AppServices services = new AppServices();
                services.NodeRebooter = new NullNodeRebooter();
                services.DeviceFactory = new DvDeviceFactory(combinedStack.DeviceStack);
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
                using (var installModule = new AppShellModule(services, config, nodeGuid))
                {
                    installModule.AppShell.Start();

                    installModule.AppShell.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.zip"));

                    List<AppInfo> apps = installModule.AppShell.GetApps().ToList();
                    if (apps.Count!=1)
                    {
                        Console.Error.WriteLine("There should be one app installed. (Found {0}.)", apps.Count);
                        return 1;
                    }
                    if (apps[0].Name!="ohOs.TestApp1")
                    {
                        Console.Error.WriteLine("Wrong app name. (Got '{0}'.)", apps[0].Name);
                        PrintAppState(apps[0]);
                        return 1;
                    }
                    if (apps[0].State!=AppState.Running)
                    {
                        Console.Error.WriteLine("App should be running. (It isn't.)");
                        PrintAppState(apps[0]);
                        return 1;
                    }

                }
            }
            return 0;
        }
    }
}
