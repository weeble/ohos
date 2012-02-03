using System;
using System.IO;
using OpenHome.Os.Platform;

//using Mono.Addins;

namespace OpenHome.Os.Apps
{
    /// <summary>
    /// Detects added and removed IApp instances.
    /// </summary>
    public interface IAddinManager
    {
        void UpdateRegistry(Action<DirectoryInfo, IApp> aAppAddedAction, Action<DirectoryInfo, IApp> aAppRemovedAction);
    }
}