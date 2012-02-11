using System;
using System.Threading;
using OpenHome.Widget.Utils;

namespace OpenHome.Os.Remote.Test
{
    public class Program
    {
        static void Main(string[] aArgs)
        {
            OptionParser parser = new OptionParser(aArgs);
            // adapter (string), port (uint), udn (string)
            OptionParser.OptionString optionAdapter = new OptionParser.OptionString("", "--adapter", "127.0.0.1", "Network adapter proxied device is on", "");
            parser.AddOption(optionAdapter);
            OptionParser.OptionUint optionPort = new OptionParser.OptionUint("", "--port", 55178, "Port proxied device is on", "");
            parser.AddOption(optionPort);
            OptionParser.OptionString optionUdn = new OptionParser.OptionString("", "--udn", "", "Udn for proxied device", "");
            parser.AddOption(optionUdn);
            parser.Parse();
            if (parser.HelpSpecified())
                return;

            Runner runner = new Runner(optionAdapter.Value, optionPort.Value, optionUdn.Value);
            runner.Start();
            Thread.Sleep(60 * 60 * 1000); // wait for 1 hour
            runner.Dispose();
        }
    }
    internal class Runner : IDisposable, ILoginValidator
    {
        private readonly ProxyServer iProxy;
        internal Runner(string aNetworkAdapter, uint aPort, string aUdn)
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
