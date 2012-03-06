using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using log4net;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
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
            [ImportMany]
            public IEnumerable<IApp> Apps { get; set; }
        }
        IAppsDirectory iAppsDirectory;
        Dictionary<string, MefAddin> iAddins = new Dictionary<string, MefAddin>();

        public MefAddinManager(IAppsDirectory aAppsDirectory)
        {
            iAppsDirectory = aAppsDirectory;
        }



        public void UpdateRegistry(Action<DirectoryInfo, IApp> aAppAddedAction, Action<DirectoryInfo, IApp> aAppRemovedAction)
        {
            HashSet<string> missingAddins = new HashSet<string>(iAddins.Keys);
            foreach (string dirname in iAppsDirectory.GetAppSubdirectories())
            {
                MefAddin addin;
                missingAddins.Remove(dirname);
                if (!iAddins.TryGetValue(dirname, out addin))
                {
                    DirectoryInfo appDirectoryInfo = new DirectoryInfo(iAppsDirectory.GetAbsolutePathForSubdirectory(dirname));
                    Logger.DebugFormat("Loading addin {0} from {1} using MEF...", dirname, appDirectoryInfo.FullName);
                    addin = new MefAddin();
                    addin.Name = dirname;
                    addin.Catalog = new DirectoryCatalog(appDirectoryInfo.FullName, "*.App.dll");
                    addin.Container = new CompositionContainer(addin.Catalog);
                    List<IApp> apps;
                    try
                    {
                        addin.Container.ComposeParts(addin);
                        apps = new List<IApp>(addin.Apps);
                        if (apps.Count != 1)
                        {
                            Logger.ErrorFormat("App {0} is broken. Expected 1 export of IApp, found {1}.", dirname, apps.Count);
                            continue;
                        }
                    }
                    catch (ChangeRejectedException)
                    {
                        Logger.ErrorFormat("App is broken: {0}", dirname);
                        continue;

                    }
                    catch (System.Reflection.ReflectionTypeLoadException rtle)
                    {
                        Logger.ErrorFormat("ReflectionTypeLoadException while loading app {0}:\n{1}", dirname, rtle.LoaderExceptions[0]);
                        continue;
                    }
                    catch (Exception e)
                    {
                        Logger.ErrorFormat("Error loading app. Perhaps it's for a different version of ohOs? Exception:\n{0}", e);
                        continue;
                    }
                    aAppAddedAction(appDirectoryInfo, apps.First());
                }
            }
            foreach (string missingAddin in missingAddins)
            {
                Logger.WarnFormat("Adding disappeared at runtime: {0}. (We don't expect this.)", missingAddin);
            }
        }
    }
    /*
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
    }*/
}
