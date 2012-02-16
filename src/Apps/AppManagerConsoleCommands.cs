using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
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
            iAppAppShell.Install(aArgs);
        }

        void Uninstall(string aArgs)
        {
            iAppAppShell.UninstallByAppName(aArgs);
        }

        void Upgrade(string aArgs)
        {
            string[] segments = aArgs.Split();
            iAppAppShell.Upgrade(segments[0], segments[1]);
        }

        void InstallNew(string aArgs)
        {
            iAppAppShell.InstallNew(aArgs);
        }

        Action<string> Guard(Action<string> aAction)
        {
            return aString =>
                {
                    try
                    {
                        aAction(aString);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                    }
                };
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
            aCommandRegistry.AddCommand("install", Guard(Install), "Install (or upgrade) an app from a file.");
            aCommandRegistry.AddCommand("uninstall", Guard(Uninstall), "Uninstall an app by name.");
            aCommandRegistry.AddCommand("listapps", Guard(ListApps), "List installed apps.");
            aCommandRegistry.AddCommand("installnew", Guard(InstallNew), "Install an app from a file.");
            aCommandRegistry.AddCommand("upgrade", Guard(Upgrade), "Upgrade an app from a file. Usage: 'upgrade <appname> <zipfile>'");
        }
    }
}
