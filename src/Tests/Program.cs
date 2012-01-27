using System;
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
                string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                using (var installModule = new ManagerModule(services, new NullConfigFileCollection()))
                {
                    installModule.Manager.Start();

                    installModule.Manager.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.zip"));
                }
            }
        }
    }
}
