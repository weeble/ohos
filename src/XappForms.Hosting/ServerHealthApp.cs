using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms
{
    class ServerHealthApp : IXapp, ITabStatusListener
    {
        readonly string iHttpDirectory;

        class TabStats
        {
            public TabStats(string aAppName, string aUserId)
            {
                AppName = aAppName;
                UserId = aUserId;
            }

            public string AppName { get; private set; }
            public int Queue { get; set; }
            public bool Reader { get; set; }
            public DateTime LastRead { get; set; }
            public string UserId { get; set; }
        }

        readonly Dictionary<Tuple<string, string>, TabStats> iTabStats = new Dictionary<Tuple<string, string>, TabStats>();
        readonly object iLock = new object();

        readonly Dictionary<Tuple<string, string>, ServerHealthAppTab> iServerHealthTabs = new Dictionary<Tuple<string, string>, ServerHealthAppTab>();

        readonly AppUrlDispatcher iUrlDispatcher;

        public ServerHealthApp(string aHttpDirectory)
        {
            iHttpDirectory = aHttpDirectory;
            iUrlDispatcher = new AppUrlDispatcher();
            iUrlDispatcher.MapPath(new string[] { }, ServeAppHtml);
            iUrlDispatcher.MapPrefixToDirectory(new string[] { }, aHttpDirectory);
        }

        string GetPath(string aFilename)
        {
            return Path.Combine(iHttpDirectory, aFilename);
        }

        void ServeAppHtml(RequestData aRequest, IWebRequestResponder aResponder)
        {
            string browser = aRequest.Cookies["xappbrowser"].First();
            string filename = GetBrowserDiscriminationMappings()[browser];
            aResponder.SendFile(GetPath(filename));
        }

        public void NewTab(string aSessionId, string aTabId, string aUserId, string aAppId)
        {
            var tabKey = Tuple.Create(aSessionId, aTabId);
            lock (iLock)
            {
                if (iIterating) Debugger.Break();
                iTabStats[tabKey] = new TabStats(aAppId, aUserId);
                Broadcast(
                    new JsonObject {
                        {"type", "newtab"},
                        {"session", aSessionId},
                        {"tab", aTabId},
                        {"app", aAppId},
                        {"queue", 0},
                        {"reader", false},
                        {"user", aUserId},
                        {"lastread", DateTime.UtcNow.ToString()}
                    }, null, null);
            }
        }

        public void TabClosed(string aSessionId, string aTabId)
        {
            var tabKey = Tuple.Create(aSessionId, aTabId);
            lock (iLock)
            {
                var tabStats = iTabStats[tabKey];
                if (!iTabStats.Remove(tabKey))
                {
                    //TODO: Use logging instead of console.
                    //Console.WriteLine("serverhealth: Ignored close event for unknown tab {0}/{1}", aSessionId, aTabId);
                    return;
                }
                Broadcast(
                    new JsonObject {
                        {"type", "closedtab"},
                        {"session", aSessionId},
                        {"tab", aTabId},
                        {"app", tabStats.AppName},
                    }, null, null);
            }
        }

        public void UpdateTabStatus(string aSessionId, string aTabId, string aUserId, int aQueueLength, DateTime aLastRead, bool aHasListener)
        {
            var tabKey = Tuple.Create(aSessionId, aTabId);
            lock (iLock)
            {
                if (iIterating) Debugger.Break();
                TabStats tabStats;
                if (!iTabStats.TryGetValue(tabKey, out tabStats))
                {
                    //TODO: Use logging instead of console.
                    //Console.WriteLine("serverhealth: Ignored event for unknown tab {0}/{1}", aSessionId, aTabId);
                    return;
                }
                bool userChanged = tabStats.UserId != aUserId;
                tabStats.Queue = aQueueLength;
                tabStats.Reader = aHasListener;
                tabStats.LastRead = aLastRead;
                tabStats.UserId = aUserId;
                if (tabStats.AppName != "serverhealth" || userChanged)
                {
                    // Note: We suppress most updates on serverhealth to avoid creating
                    // an infinite loop of updates between server health apps about
                    // their own queues.
                    var json=
                        new JsonObject {
                            {"type", "updatetab"},
                            {"session", aSessionId},
                            {"tab", aTabId},
                            {"app", tabStats.AppName},
                            {"queue", aQueueLength},
                            {"reader", aHasListener},
                            {"user", aUserId},
                            {"lastread", aLastRead.ToString()}};
                    Broadcast(
                        json,
                        userChanged ? null : aSessionId,
                        userChanged ? null : aTabId);
                }
            }
        }

        bool iIterating;

        public void ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder)
        {
            iUrlDispatcher.ServeRequest(aRequest, aResponder);
        }

        public IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser)
        {
            var tabKey = Tuple.Create(aTabProxy.SessionId, aTabProxy.TabId);
            var tab = new ServerHealthAppTab(this, aTabProxy);
            lock (iLock)
            {
                iServerHealthTabs[tabKey] = tab;
                iIterating = true;
                foreach (var kvp in iTabStats)
                {
                    string sessionId = kvp.Key.Item1;
                    string tabId = kvp.Key.Item2;
                    tab.NewMessage(
                        new JsonObject {
                            {"type", "newtab"},
                            {"session", sessionId},
                            {"tab", tabId},
                            {"app", kvp.Value.AppName},
                            {"queue", kvp.Value.Queue},
                            {"reader", kvp.Value.Reader},
                            {"user", kvp.Value.UserId},
                            {"lastread", kvp.Value.LastRead.ToString()}
                        });
                }
                iIterating = false;
            }
            return tab;
        }

        public Dictionary<string, string> GetBrowserDiscriminationMappings()
        {
            return new Dictionary<string, string>{
                {"desktop", "desktop.html"},
                {"mobile", "mobile.html"},
                {"tablet", "tablet.html"},
                {"default", "desktop.html"}};
        }

        void Broadcast(JsonValue aMessage, string aExcludeSessionId, string aExcludeTabId)
        {
            List<ServerHealthAppTab> tabs;
            lock (iLock)
            {
                tabs = iServerHealthTabs.Values.ToList();
            }
            foreach (var tab in tabs)
            {
                if (tab.BrowserTabProxy.SessionId == aExcludeSessionId && tab.BrowserTabProxy.TabId == aExcludeTabId)
                {
                    continue;
                }
                tab.NewMessage(aMessage);
            }
        }

        public void RemoveTab(string aSessionId, string aTabId)
        {
            var tabKey = Tuple.Create(aSessionId, aTabId);
            lock (iLock)
            {
                iServerHealthTabs.Remove(tabKey);
            }
        }
    }

    class ServerHealthAppTab : IAppTab
    {
        readonly ServerHealthApp iServerHealthApp;
        readonly IBrowserTabProxy iBrowserTabProxy;

        public ServerHealthAppTab(ServerHealthApp aServerHealthApp, IBrowserTabProxy aBrowserTabProxy)
        {
            iServerHealthApp = aServerHealthApp;
            iBrowserTabProxy = aBrowserTabProxy;
        }

        public ServerHealthApp ServerHealthApp
        {
            get { return iServerHealthApp; }
        }

        public IBrowserTabProxy BrowserTabProxy
        {
            get { return iBrowserTabProxy; }
        }

        public void ChangeUser(User aUser)
        {
        }

        public void Receive(JsonValue aJsonValue)
        {
        }

        public void NewMessage(JsonValue aMessage)
        {
            BrowserTabProxy.Send(aMessage);
        }

        public void TabClosed()
        {
            ServerHealthApp.RemoveTab(BrowserTabProxy.SessionId, BrowserTabProxy.TabId);
        }
    }
}
