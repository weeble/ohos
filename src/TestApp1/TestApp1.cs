using System;
using Mono.Addins;
using OpenHome.Os.AppManager;
using OpenHome.Net.Device;

namespace OpenHome.Os.TestApps
{
    [Extension("/ohOs/App")]
    public class TestApp1 : IApp
    {
        public string Udn { get { return "TestApp1"; } }
        public IResourceManager ResourceManager { get { return null; } }
        public string Name { get { return "TestApp1"; } }
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
        public void Start(DvDevice aDevice)
        {
        }
        public void Stop()
        {
        }
        public void Dispose()
        {
        }
    }
}
