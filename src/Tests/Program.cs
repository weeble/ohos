using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using Node;
using OpenHome.Os.AppManager;
using OpenHome.Net.Core;
using OpenHome.Os.Platform;
using OpenHome.Widget.Nodes;
using log4net.Config;


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
        static void PrintAppState(AppInfo aAppInfo)
        {
            Console.Error.WriteLine("Name={0}, State={1}, PendingUpdate={2}, PendingDelete={3}, Udn={4}", aAppInfo.Name, aAppInfo.State, aAppInfo.PendingUpdate, aAppInfo.PendingDelete, aAppInfo.Udn);
        }

        static int Main(string[] aArgs)
        {
            BasicConfigurator.Configure();
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

                    installModule.Manager.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.App.zip"));

                    List<AppInfo> apps = installModule.Manager.GetApps().ToList();
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
