using System;
using System.Collections.Generic;
using Mono.Addins;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.Device;
using OpenHome.Os.Platform;
using OpenHome.Widget.Nodes;
using OpenHome.Widget.Nodes.Logging;

[assembly: AddinRoot("ohOs", "1.2")]

namespace OpenHome.Os.AppManager
{
    public class AppVersion
    {
        public uint Major { get; private set; }
        public uint Minor { get; private set; }
        public uint Build { get; private set; }

        public AppVersion(uint aMajor, uint aMinor, uint aBuild)
        {
            Major = aMajor;
            Minor = aMinor;
            Build = aBuild;
        }
    }

    public interface INodeInformation
    {
        /// <summary>
        /// Port that is listening for ohNet websocket connections.
        /// Null if websockets are disabled.
        /// </summary>
        uint? WebSocketPort { get; }
    }

    public interface IAppServices
    {
        //string StorePath { get; } // Should eventually virtualize file system.
        INodeInformation NodeInformation { get; }
        IDvDeviceFactory DeviceFactory { get; }
        ICpUpnpDeviceListFactory CpDeviceListFactory { get; }
        INodeRebooter NodeRebooter { get; }
        IUpdateService UpdateService { get; }
        ICommandRegistry CommandRegistry { get; }
        ILogReader LogReader { get; }
        ILogController LogController { get; }

        object ResolveService<T>();
    }

    public interface IAppContext
    {
        /// <summary>
        /// Node services exposed to the app.
        /// </summary>
        IAppServices Services { get; }
        /// <summary>
        /// Path where the app's static data files are stored.
        /// </summary>
        string StaticPath { get; }
        /// <summary>
        /// Path where the app's mutable data files can be stored.
        /// </summary>
        string StorePath { get; }
        /// <summary>
        /// Access the app's section of the configuration file.
        /// </summary>
        IConfigFileCollection Configuration { get; }
        /// <summary>
        /// The device that represents the app for communication purposes.
        /// </summary>
        DvDevice Device { get; }
    }

    [TypeExtensionPoint("/ohOs/App")]
    public interface IApp : IDisposable
    {
        string Udn { get; }
        IResourceManager ResourceManager { get; }
        string Name { get; }
        AppVersion Version { get; }
        string IconUri { get; }
        string DescriptionUri { get; }
        string AssemblyPath { get; } // !!!! should remove this in favour of Manager determining which path each of its app assemblies in running from
        void Start(IAppContext aAppServices);
        void Stop();
    }
}
