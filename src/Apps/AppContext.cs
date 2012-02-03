using OpenHome.Net.Device;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{
    public class AppContext : IAppContext
    {
        public IAppServices Services { get; private set; }
        public string StaticPath { get; private set; }
        public string StorePath { get; private set; }
        public IConfigFileCollection Configuration { get; private set; }
        public DvDevice Device { get; set; }

        public AppContext(IAppServices aServices, string aStaticPath, string aStorePath, IConfigFileCollection aConfiguration, DvDevice aDevice)
        {
            Services = aServices;
            StaticPath = aStaticPath;
            StorePath = aStorePath;
            Configuration = aConfiguration;
            Device = aDevice;
        }
    }
}