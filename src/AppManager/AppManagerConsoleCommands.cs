using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    public class AppManagerConsoleCommands
    {
        readonly IAppShell iAppAppShell;

        public AppManagerConsoleCommands(IAppShell aAppAppShell)
        {
            iAppAppShell = aAppAppShell;
        }

        void Install(string aArgs)
        {
            try
            {
                iAppAppShell.Install(aArgs);
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
                iAppAppShell.UninstallByAppName(aArgs);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        void ListApps(string aArgs)
        {
            var apps = iAppAppShell.GetApps();
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
