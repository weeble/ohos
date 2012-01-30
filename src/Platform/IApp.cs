using System;
using System.Collections.Generic;
using Mono.Addins;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.Device;
using OpenHome.Os.Platform;
using OpenHome.Widget.Nodes;
using OpenHome.Widget.Nodes.Logging;

[assembly: AddinRoot("ohOs", "1.4")]

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

        /// <summary>
        /// Indicates that the node should seek out and federate with
        /// other nodes on the network.
        /// </summary>
        bool MultiNodeEnabled { get; }

        /// <summary>
        /// Port the device's web server is running on.
        /// </summary>
        uint DvServerPort { get; }
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
        /// <summary>
        /// Udn for the app device. Not valid to query until after Init.
        /// </summary>
        string Udn { get; }
        /// <summary>
        /// Resource manager for the app. Not valid to fetch until after Init,
        /// and the ResourceManager itself is not valid to query until after
        /// Start.
        /// </summary>
        IResourceManager ResourceManager { get; }
        /// <summary>
        /// Name for the app. Always valid for query.
        /// </summary>
        string Name { get; }
        /// <summary>
        /// Version. Always valid for query.
        /// </summary>
        AppVersion Version { get; }
        /// <summary>
        /// Uri to fetch an icon. Not valid to query until after Init.
        /// </summary>
        string IconUri { get; }
        /// <summary>
        /// ??? Not valid to query until after Init.
        /// </summary>
        string DescriptionUri { get; }
        string AssemblyPath { get; } // !!!! should remove this in favour of Manager determining which path each of its app assemblies in running from
        void Init(IAppContext aAppServices);
        void Start(IAppContext aAppServices);
        void Stop();
    }
}
