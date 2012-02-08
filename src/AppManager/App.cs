using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenHome.Net.Device;
using OpenHome.Os.Apps;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    [Export(typeof(IApp))]
    public class AppManagerApp : IApp
    {
        public void Dispose()
        {
        }

        public string Udn
        {
            get { return null; }
        }

        public IResourceManager ResourceManager
        {
            get { return null; }
        }

        public string Name
        {
            get { return "ohOs.AppManager"; }
        }

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

        public string AssemblyPath
        {
            get { return "IGNORED"; }
        }

        AppManager iAppManager;

        public void Init(IAppContext aAppServices)
        {
        }

        public void Start(IAppContext aAppServices)
        {
            if (aAppServices.Device == null) throw new ArgumentNullException("aAppServices.Device");
            iAppManager = new AppManager(aAppServices.Device, (d,m)=>new AppManagerProvider(d,m), aAppServices.Services.ResolveService<IAppShell>());
        }

        public void Stop()
        {
            iAppManager.Dispose();
        }
    }
}
