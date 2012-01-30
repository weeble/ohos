using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    public class AppManagerConsoleCommands
    {
        readonly IManager iAppManager;

        public AppManagerConsoleCommands(IManager aAppManager)
        {
            iAppManager = aAppManager;
        }

        void Install(string aArgs)
        {
            try
            {
                iAppManager.Install(aArgs);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        void Uninstall(string aArgs)
        {
            try
            {
                iAppManager.UninstallByAppName(aArgs);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        void ListApps(string aArgs)
        {
            var apps = iAppManager.GetApps();
            Console.WriteLine("Installed apps:");
            bool any = false;
            foreach (var app in apps)
            {
                string runningString = app.State==AppState.Running ? "Running" : "Stopped";
                string installState = app.PendingDelete ? "Delete on next restart" : app.PendingUpdate ? "Update on next restart" : "";
                string udn = app.Udn ?? "";
                Console.WriteLine("    {0,-20} {1,-10} {2,-20} {3}", app.Name, runningString, installState, udn);
                any = true;
            }
            if (!any)
            {
                Console.WriteLine("    <none>");
            }
        }


        public void Register(ICommandRegistry aCommandRegistry)
        {
            aCommandRegistry.AddCommand("install", Install, "Install an app from a file.");
            aCommandRegistry.AddCommand("uninstall", Uninstall, "Uninstall an app by name.");
            aCommandRegistry.AddCommand("listapps", ListApps, "List installed apps.");
        }
    }
}
