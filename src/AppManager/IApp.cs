using System;
using Mono.Addins;
using OpenHome.Net.Device;

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
        void Start(DvDevice aDevice);
        void Stop();
    }
}
