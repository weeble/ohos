using System;
using Mono.Addins;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.Device;
using OpenHome.Widget.Nodes;
using OpenHome.Widget.Nodes.Logging;

[assembly: AddinRoot("ohOs", "1.4")]

namespace OpenHome.Os.Platform
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
        public override string ToString()
        {
            return String.Format("{0}.{1}.{2}", Major, Minor, Build);
        }
        public static AppVersion Parse(string aString)
        {
            string[] segments = aString.Split('.');
            if (segments.Length != 3)
            {
                throw new ArgumentException();
            }
            return new AppVersion(uint.Parse(segments[0]), uint.Parse(segments[1]), uint.Parse(segments[2]));
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


    /// <summary>
    /// Provides access to the ohOs node's own device for suitably priveleged
    /// apps. Used to publish special services such as AppManager.
    /// </summary>
    public interface INodeDeviceAccessor
    {
        IDvDevice Device { get; }
    }

    public interface IAppServices
    {
        //string StorePath { get; } // Should eventually virtualize file system.
        INodeDeviceAccessor NodeDeviceAccessor { get; }
        INodeInformation NodeInformation { get; }
        IDvDeviceFactory DeviceFactory { get; }
        ICpUpnpDeviceListFactory CpDeviceListFactory { get; }
        INodeRebooter NodeRebooter { get; }
        IUpdateService UpdateService { get; }
        ICommandRegistry CommandRegistry { get; }
        ILogReader LogReader { get; }
        ILogController LogController { get; }

        T ResolveService<T>();
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
        /// <summary>
        /// The app's name.
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// Give the app a friendly name to appear in UIs. Not currently
    /// localized.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false, Inherited=true)]
    public class AppFriendlyNameAttribute : Attribute
    {
        public AppFriendlyNameAttribute(string aFriendlyName)
        {
            FriendlyName = aFriendlyName;
        }
        public string FriendlyName { get; private set; }
    }

    /// <summary>
    /// Apps should implement this interface. They can also apply attributes
    /// to add various optional metadata or behaviour:
    /// [AppFriendlyName]
    /// </summary>
    [TypeExtensionPoint("/ohOs/App")]
    public interface IApp : IDisposable
    {
        /// <summary>
        ///  If true, this app will never be started after the node
        ///  device is already published. Instead the node will be
        ///  restarted. This allows the app to publish services on the
        ///  node device.
        /// </summary>
        bool PublishesNodeServices { get; }
        /// <summary>
        /// Resource manager for the app. Note that this will be fetched
        /// before Start is invoked. Should fail gracefully if invoked
        /// before Start.
        /// </summary>
        IResourceManager ResourceManager { get; }
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
        void Start(IAppContext aAppServices);
        void Stop();
    }
}
