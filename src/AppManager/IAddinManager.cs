using System;
//using Mono.Addins;

namespace OpenHome.Os.AppManager
{
    /// <summary>
    /// Detects added and removed IApp instances.
    /// </summary>
    public interface IAddinManager
    {
        void UpdateRegistry(Action<IApp> aAppAddedAction, Action<IApp> aAppRemovedAction);
    }
}