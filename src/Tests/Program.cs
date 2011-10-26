using System;
using OpenHome.Os.AppManager;
using OpenHome.Net.Core;

namespace OpenHome.Os
{
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
                var combinedStack = library.StartCombined(subnet);

                string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                using (var installer = new Manager(exePath))
                {
                    installer.Install(System.IO.Path.Combine(exePath, "ohOs.TestApp1.zip"));
                }
            }
        }
    }
}
