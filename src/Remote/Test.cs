using System.Threading;

namespace OpenHome.Os.Remote
{
    public class Program
    {
        static void Main()
        {
            ProxyServer proxy = new ProxyServer();
            const uint adapter = (1 << 24) | 127;
            //const uint adapter = (78 << 24) | (9 << 16) | (2 << 8) | 10;
            proxy.Enable(adapter, 57022, "remote");
            Thread.Sleep(60 * 60 * 1000); // wait for 1 hour
            proxy.Dispose();
        }
    }
}
