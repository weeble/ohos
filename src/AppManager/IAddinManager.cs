using System;
using System.IO;

//using Mono.Addins;

namespace OpenHome.Os.AppManager
{
    /// <summary>
    /// Detects added and removed IApp instances.
    /// </summary>
    public interface IAddinManager
    {
        void UpdateRegistry(Action<DirectoryInfo, IApp> aAppAddedAction, Action<DirectoryInfo, IApp> aAppRemovedAction);
    }
}