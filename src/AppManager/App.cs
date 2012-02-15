using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenHome.Net.Device;
using OpenHome.Os.Apps;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    [Export(typeof(IApp))]
    [AppFriendlyName("App Manager")]
    public class AppManagerApp : IApp, IResourceManager
    {
        public void Dispose()
        {
        }

        public bool PublishesNodeServices
        {
            get { return true; }
        }

        public IResourceManager ResourceManager
        {
            get { return this; }
        }

        // public string Name
        // {
        //    get { return "ohOs.AppManager"; }
        // }

        public AppVersion Version
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return new AppVersion((uint)version.Major, (uint)version.Minor, (uint)version.Build);
            }
        }

        public string IconUri
        {
            get
            {
                // Awful programmer art. Insert talent here.
                return "data:image/png;base64,"+
                    "iVBORw0KGgoAAAANSUhEUgAAACAAAAAg"+
                    "CAIAAAD8GO2jAAAAAXNSR0IArs4c6QAA"+
                    "AARnQU1BAACxjwv8YQUAAAAJcEhZcwAA"+
                    "DsMAAA7DAcdvqGQAAAAadEVYdFNvZnR3"+
                    "YXJlAFBhaW50Lk5FVCB2My41LjEwMPRy"+
                    "oQAAAMtJREFUSEvtVkEOhCAM7Gv2LXvw"+
                    "JfseH7e/qe2y4lgiJWATD5CJIQQ70wHj"+
                    "EBWDmdsXZXN9lKUohCCpSGw4cRe9BnYz"+
                    "wgnQqRCLwgkqFmWX8wm5vuMGPdLQQXLM"+
                    "ofhV/4RBb30i+AZAyiLBwnQjVHFJQCvf"+
                    "AhF6TfBiGsTKDsGbuBsqroWg78sQWZOg"+
                    "bt20yL1a06LHWuQKG9tw/JNNxsPw0j3X"+
                    "FzE0pEIdzxxKymoOAYZJJDbrJvWcAqfh"+
                    "RC3j3fyjbqXBFrtMZDayNpdCZ7Z6ghye"+
                    "AAAAAElFTkSuQmCC";
            }
        }

        public string DescriptionUri
        {
            get { return "http://something.invalid/insert/description/url/here"; }
        }

        AppManager iAppManager;
        IResourceManager iResourceManager;
        DownloadManager iDownloadManager;

        public void Start(IAppContext aAppServices)
        {
            if (aAppServices.Device == null) throw new ArgumentNullException("aAppServices.Device");
            var appDevice = aAppServices.Device;
            var nodeDevice = aAppServices.Services.NodeDeviceAccessor.Device.RawDevice;
            string appResourceUrl = string.Format("/{0}/Upnp/resource/", appDevice.Udn());
            iDownloadManager = new DownloadManager(new DownloadDirectory(aAppServices.StorePath));
            iAppManager = new AppManager(
                appResourceUrl,
                new[]{appDevice, nodeDevice},
                (d,m,uri)=>new AppManagerProvider(d,m,uri), aAppServices.Services.ResolveService<IAppShell>(),
                iDownloadManager);
            iResourceManager = new NodeResourceManager(Path.Combine(aAppServices.StaticPath, "WebUi"), aAppServices.Device.Udn(), aAppServices.Services.NodeInformation.WebSocketPort ?? 0);
        }

        public void Stop()
        {
            iAppManager.Dispose();
            iDownloadManager.Dispose();
        }

        public void WriteResource(string aUriTail, uint aIpAddress, List<string> aLanguageList, IResourceWriter aWriter)
        {
            iResourceManager.WriteResource(aUriTail, aIpAddress, aLanguageList, aWriter);
        }
    }
}
