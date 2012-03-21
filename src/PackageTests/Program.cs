using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.ControlPoint.Proxies;
using OpenHome.Net.Core;
using OpenHome.Os.Platform.Proxies;
using Action = System.Action;

namespace OpenHome.Os.PackageTests
{

    /*
    class StackedOutput
    {
        class Context
        {
            public string StartText;
            public string EndText;
            public string IndentText;
            public string ParentIndentText;
            public bool Split;
            public void WriteStart() { Console.Write(StartText); }
            public void WriteEnd(string aIndent)
            {
                if (Console.CursorLeft + EndText.Length > 72)
                {
                    Console.WriteLine();
                    Console.Write(aIndent);
                }
                Console.CursorLeft = 72 - EndText.Length;
                Console.Write(EndText);
            }

        }

        List<Context> contexts = new List<Context>();
        string currentIndent = "";
        bool leftClear;

        public void OpenContext(string aStartText, string aEndText, string aIndentText)
        {
            Context c = new Context { StartText = aStartText, EndText = aEndText, IndentText = aIndentText, ParentIndentText = currentIndent };
            if (contexts.Count >= 1)
            {
                Context lastContext = contexts[contexts.Count-1];
                int rollbackLines = contexts.Count;
                Console.CursorLeft=0;
                Console.CursorTop-=rollbackLines;
                if (!lastContext.Split)
                {
                    Console.WriteLine((c.ParentIndentText + c.StartText).PadRight(72));
                    lastContext.Split = true;
                }
            }
            contexts.Add(c);
            for (int i=contexts.Count-1; i>=0; --i)
            {
                Console.WriteLine(c.ParentIndentText + c.EndText.PadLeft(72-c.ParentIndentText.Length));
            }

                int deleteChars = contexts[contexts.Count - 1].EndText.Length;
                Console.CursorLeft -= deleteChars;
                Console.Write("".PadRight(deleteChars));
                if (leftClear)
                {
                    Console.CursorLeft = 1;
                }
                else
                {
                    Console.WriteLine();
                }
                Console.Write(
            }
        }
    }*/

    /// <summary>
    /// Tests against a running node that don't make any permanent changes.
    /// </summary>
    class NonInvasiveTests
    {
        static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(30);
        ICpUpnpDeviceListFactory iDeviceListFactory;
        //ManualResetEvent iFinishedTestingAppList = new ManualResetEvent(false);

        public NonInvasiveTests(ICpUpnpDeviceListFactory aDeviceListFactory)
        {
            iDeviceListFactory = aDeviceListFactory;
        }

        public void RunAll(string aUdn)
        {
            string aAppManagerUdn = null;

            TimedTest("Test app list service", () => aAppManagerUdn = TestAppList(aUdn));
            TimedTest("Test app service", () => TestApp(aAppManagerUdn));
            TimedTest("Test app manager service", () => TestAppManager(aAppManagerUdn));
            /*
            WaitForAppListService();
            SubscribeToAppList();
            CheckAppManagerIsInAppList();
            string appManagerUdn = GetAppManagerUdnFromAppList();
            WaitForAppService(appManagerUdn);
            SubscribeToAppService();
            WaitForAppManagerService(appManagerUdn);
            SubscribeToAppManagerService();
            */
        }

        void TimedTest(string aDescription, Action aTestAction)
        {
            Stopwatch timer = new Stopwatch();
            Console.Write("{0,-60}[       ]", aDescription);
            Console.CursorLeft -= 8;
            try
            {
                timer.Start();
                aTestAction();
                timer.Stop();
                Console.WriteLine("{0,5}ms", timer.ElapsedMilliseconds);
                return;
            }
            catch (Exception)
            {
                Console.WriteLine("FAIL");
                //Console.WriteLine(e);
                throw;
            }
        }

        ProxyList<CpProxyOpenhomeOrgApp1> CreateAppProxyList(string aUdn)
        {
            var proxyList = new ProxyList<CpProxyOpenhomeOrgApp1>(
                aDevice => new CpProxyOpenhomeOrgApp1(aDevice),
                ProxyList.UpnpDevicesByService(
                    iDeviceListFactory, "openhome.org", "App", 1)
                /*.Filtered(aDv=>aDv.Udn()==aUdn)*/);
            proxyList.Start();
            return proxyList;
        }
        ProxyList<CpProxyOpenhomeOrgAppManager1> CreateAppManagerProxyList(string aUdn)
        {
            var proxyList = new ProxyList<CpProxyOpenhomeOrgAppManager1>(
                aDevice => new CpProxyOpenhomeOrgAppManager1(aDevice),
                ProxyList.UpnpDevicesByService(
                    iDeviceListFactory, "openhome.org", "AppManager", 1)
                .Filtered(aDv=>aDv.Udn()==aUdn));
            proxyList.Start();
            return proxyList;
        }
        ProxyList<CpProxyOpenhomeOrgAppList1> CreateAppListProxyList(string aUdn)
        {
            var proxyList = new ProxyList<CpProxyOpenhomeOrgAppList1>(
                aDevice => new CpProxyOpenhomeOrgAppList1(aDevice),
                ProxyList.UpnpDevicesByService(
                    iDeviceListFactory, "openhome.org", "AppList", 1)
                /*.Filtered(aDv=>aDv.Udn()==aUdn)*/);
            proxyList.Start();
            return proxyList;
        }

        string TestAppList(string aAppListUdn)
        {
            using (var proxyList = CreateAppListProxyList(aAppListUdn))
            {
                WaitForDevice(proxyList, aAppListUdn, "app list");
                using (var proxyRef = proxyList.GetProxyRef(aAppListUdn))
                {
                    Subscribe(proxyRef.Value, aAppListUdn, "app list");
                    XElement root = XElement.Parse(proxyRef.Value.PropertyRunningAppList());
                    var apps = root.Elements().Select(
                        el => new {
                            Id = (string)el.Element("id"),
                            Udn = (string)el.Element("udn"),
                            ResourceUrl = (string)el.Element("resourceUrl")
                        }).ToList();
                    if (!apps.Any(aApp => aApp.Id == "ohOs.AppManager"))
                    {
                        Fail("AppList should contain an entry for ohOs.AppManager");
                    }
                    return apps.First(aApp => aApp.Id == "ohOs.AppManager").Udn;
                }
            }
        }

        void TestApp(string aAppManagerUdn)
        {
            using (var proxyList = CreateAppProxyList(aAppManagerUdn))
            {
                WaitForDevice(proxyList, aAppManagerUdn, "app");
                using (var proxyRef = proxyList.GetProxyRef(aAppManagerUdn))
                {
                    Subscribe(proxyRef.Value, aAppManagerUdn, "app");
                    if (proxyRef.Value.PropertyName() != "ohOs.AppManager")
                    {
                        Fail("AppManager's app service reports wrong name.");
                    }
                    
                    //Uri baseUri = new Uri(proxyList.GetDeviceAttribute(aAppManagerUdn, "Upnp.Location"));
                    //Console.WriteLine(baseUri);
                }
            }
        }

        void TestAppManager(string aAppManagerUdn)
        {
            using (var proxyList = CreateAppManagerProxyList(aAppManagerUdn))
            {
                WaitForDevice(proxyList, aAppManagerUdn, "app");
                using (var proxyRef = proxyList.GetProxyRef(aAppManagerUdn))
                {
                    Subscribe(proxyRef.Value, aAppManagerUdn, "app manager");
                    
                    Uri baseUri = new Uri(proxyList.GetDeviceAttribute(aAppManagerUdn, "Upnp.Location"));

                    string appListXml;
                    proxyRef.Value.SyncGetMultipleAppsStatus(new byte[] { }, out appListXml);
                    XElement root = XElement.Parse(appListXml);
                    var apps = root.Elements().Select(aEl => new { Id = (string)aEl.Element("id"), Url = (string)aEl.Element("url") }).ToList();
                    string appManagerPartialUrl = apps.First(aApp => aApp.Id == "ohOs.AppManager").Url;

                    Uri appManagerUrl = new Uri(baseUri, appManagerPartialUrl);

                    WebRequest webRequest = WebRequest.Create(appManagerUrl);
                    var response = (HttpWebResponse)webRequest.GetResponse();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        Fail("App manager main page wasn't 200 OK. URI: {0}", appManagerUrl);
                    }
                }
            }
        }

        static void Subscribe(CpProxy aProxy, string aUdn, string aName)
        {
            ManualResetEvent initialEvent = new ManualResetEvent(false);
            aProxy.SetPropertyInitialEvent(() => initialEvent.Set());
            aProxy.Subscribe();
            if (!initialEvent.WaitOne(SearchTimeout))
            {
                Fail("Subscription timed out on {0} with UDN: {1}", aName, aUdn);
            }
        }

        static void WaitForDevice<T>(ProxyList<T> aProxyList, string aUdn, string aName) where T : IDisposable
        {
            if (!aProxyList.WaitForDevice(aUdn, DateTime.Now + SearchTimeout))
            {
                Fail("Could not find {0} with UDN: {1}\nInstead found {2}", aName, aUdn, string.Join(", ", aProxyList.GetDeviceUdns().ToArray()));
            }
        }

        static void Fail(string aMessage, params object[] aArgs)
        {
            throw new TestFailureException(String.Format(aMessage, aArgs));
        }
    }

    class TestFailureException : Exception
    {
        public TestFailureException(string aMessage) : base(aMessage) { }
    }

    class Program
    {
        static int Main(string[] aArgs)
        {
            InitParams initParams = new InitParams {UseLoopbackNetworkAdapter = false, MsearchTimeSecs = 2};
            using (Library library = Library.Create(initParams))
            {
                SubnetList subnetList = new SubnetList();
                Console.WriteLine(subnetList.Size());
                Console.WriteLine(string.Join(", ", Enumerable.Range(0, (int)subnetList.Size()).Select(aI => subnetList.SubnetAt((uint)aI).Address())));
                NetworkAdapter nif = subnetList.SubnetAt(0);
                uint subnet = nif.Subnet();
                subnetList.Dispose();
                var cpStack = library.StartCp(subnet);
                var devListFactory = new CpUpnpDeviceListFactory(cpStack);
                var tests = new NonInvasiveTests(devListFactory);
                tests.RunAll(aArgs[0]);
            }
            return 0;
        }
    }
}
