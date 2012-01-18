using System;
using Mono.Addins;

namespace OpenHome.Os.AppManager
{
    public class DefaultAddinManager : IAddinManager
    {
        const string AppAddinPath = "/ohOs/App";
        public DefaultAddinManager(string aConfigDir, string aAddinsDir, string aDatabaseDir)
        {
            AddinManager.Initialize(aConfigDir, aAddinsDir, aDatabaseDir);
        }
        //public void AddExtensionNodeHandler(string aPath, ExtensionNodeEventHandler aHandler)
        //{
        //}
        //public void RemoveExtensionNodeHandler(string aPath, ExtensionNodeEventHandler aHandler)
        //{
        //}
        public void UpdateRegistry(Action<IApp> aAppAddedAction, Action<IApp> aAppRemovedAction)
        {
            ExtensionNodeEventHandler handler = (object aSender, ExtensionNodeEventArgs aEventArgs) =>
                {
                    if (aEventArgs.Change == ExtensionChange.Add)
                    {
                        aAppAddedAction((IApp)aEventArgs.ExtensionObject);
                    }
                    else
                    {
                        aAppRemovedAction((IApp)aEventArgs.ExtensionObject);
                    }
                };
            AddinManager.AddExtensionNodeHandler(AppAddinPath, handler);
            AddinManager.Registry.Update();
            AddinManager.RemoveExtensionNodeHandler(AppAddinPath, handler);
        }
    }
}