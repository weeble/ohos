using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using log4net;
using Mono.Addins;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition;

namespace OpenHome.Os.AppManager
{
    public class MefAddinManager : IAddinManager
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(MefAddinManager));
        class MefAddin
        {
            public string Name { get; set; }
            public ComposablePartCatalog Catalog { get; set; }
            public CompositionContainer Container { get; set; }
            public List<Type> Types { get; set; }
            [Import]
            public IApp App { get; set; }
        }
        IAppsDirectory iAppsDirectory;
        Dictionary<string, MefAddin> iAddins = new Dictionary<string, MefAddin>();

        public MefAddinManager(IAppsDirectory aAppsDirectory)
        {
            iAppsDirectory = aAppsDirectory;
        }



        public void UpdateRegistry(Action<IApp> aAppAddedAction, Action<IApp> aAppRemovedAction)
        {
            HashSet<string> missingAddins = new HashSet<string>(iAddins.Keys);
            foreach (string dirname in iAppsDirectory.GetAppSubdirectories())
            {
                MefAddin addin;
                missingAddins.Remove(dirname);
                if (!iAddins.TryGetValue(dirname, out addin))
                {
                    Logger.DebugFormat("Loading addin {0} using MEF...", dirname);
                    addin = new MefAddin();
                    addin.Name = dirname;
                    addin.Catalog = new DirectoryCatalog(iAppsDirectory.GetAbsolutePathForSubdirectory(dirname),"*.App.dll");
                    addin.Container = new CompositionContainer(addin.Catalog);
                    try
                    {
                        addin.Container.ComposeParts(addin);
                    }
                    catch (System.Reflection.ReflectionTypeLoadException rtle)
                    {
                        Logger.ErrorFormat("ReflectionTypeLoadException while loading app {0}:\n{1}", dirname, rtle.LoaderExceptions[0]);
                        continue;
                    }
                    aAppAddedAction(addin.App);
                }
            }
            foreach (string missingAddin in missingAddins)
            {
                Logger.WarnFormat("Adding disappeared at runtime: {0}. (We don't expect this.)", missingAddin);
            }
        }
    }
    public class DefaultAddinManager : IAddinManager
    {
        ILog Logger = LogManager.GetLogger(typeof(DefaultAddinManager));
        const string AppAddinPath = "/ohOs/App";
        bool iLazyInit;
        Action iInitAction;
        public DefaultAddinManager(string aConfigDir, string aAddinsDir, string aDatabaseDir)
        {
            iInitAction = ()=>AddinManager.Initialize(aConfigDir, aAddinsDir, aDatabaseDir);
            Logger.Info("Deferred addin manager initialization.");
        }
        //public void AddExtensionNodeHandler(string aPath, ExtensionNodeEventHandler aHandler)
        //{
        //}
        //public void RemoveExtensionNodeHandler(string aPath, ExtensionNodeEventHandler aHandler)
        //{
        //}
        public void UpdateRegistry(Action<IApp> aAppAddedAction, Action<IApp> aAppRemovedAction)
        {
            if (!iLazyInit)
            {
                iLazyInit = true;
                Logger.Info("Initialized addin manager.");
                iInitAction();
            }
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
            Logger.Info("Update addin registry.");
            AddinManager.AddExtensionNodeHandler(AppAddinPath, handler);
            AddinManager.Registry.Update();
            AddinManager.RemoveExtensionNodeHandler(AppAddinPath, handler);
        }
    }
}