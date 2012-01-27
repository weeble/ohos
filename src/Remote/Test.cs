using System;
using System.Threading;

namespace OpenHome.Os.Remote
{
    public class Program
    {
        static void Main()
        {
            const uint adapter = (1 << 24) | 127;
            //const uint adapter = (78 << 24) | (9 << 16) | (2 << 8) | 10;
            Runner runner = new Runner(adapter, 57022, "remote");
            runner.Start();
            Thread.Sleep(60 * 60 * 1000); // wait for 1 hour
            runner.Dispose();
        }
    }
    internal class Runner : IDisposable, ILoginValidator
    {
        private readonly ProxyServer iProxy;
        internal Runner(uint aNetworkAdapter, uint aPort, string aUdn)
        {
            iProxy = new ProxyServer(aNetworkAdapter);
            iProxy.AddApp(aPort, aUdn);
        }
        public void Start()
        {
            iProxy.Start(this);
        }
        public bool ValidateCredentials(string aUserName, string aPassword)
        {
            return true; // validate every login attempt
        }
        public void Dispose()
        {
            iProxy.Dispose();
        }
    }
}
