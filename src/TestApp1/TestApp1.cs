using System;
using System.ComponentModel.Composition;
using OpenHome.Net.Device;
using OpenHome.Os.Platform;

namespace OpenHome.Os.TestApps
{
    [Export(typeof(IApp))]
    public class TestApp1 : IApp
    {
        //public string Udn { get { return "ohOs.TestApp1"; } }

        public bool PublishesNodeServices
        {
            get { return false; }
        }

        public IResourceManager ResourceManager { get { return null; } }
        //public string Name { get { return "ohOs.TestApp1"; } }
        public AppVersion Version { get { return iVersion; } }
        public string IconUri { get { return ""; } }
        public string DescriptionUri { get { return ""; } }
        public string AssemblyPath
        {
            get
            {
                string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return System.IO.Path.GetDirectoryName(path);
            }
        }
        private AppVersion iVersion;
        public TestApp1()
        {
            iVersion = new AppVersion(0, 1, 0);
        }

        public void Start(IAppContext aAppContext)
        {
            Console.WriteLine("Started app.");
            Console.WriteLine(aAppContext.Configuration.GetElementValue(e=>e.Element("test"))??"No value for test.");
        }
        public void Stop()
        {
            Console.WriteLine("Stopped app.");
        }
        public void Dispose()
        {
        }
    }
}
