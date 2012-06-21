using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenHome.XappForms.Json;
using Timer = System.Timers.Timer;

// Threading
// 
// The server does not use explicit threads (System.Threading.Thread), but it does use
// what we call "soft" threads. These allow actions to be requested asynchronously and
// then run one at a time using System.Threading.Tasks. So there's no link between the
// soft thread and a System.Threading.Thread, but only one action can run concurrently
// for any given soft thread. We use two soft threads:
// 
// The AppsState thread. This manages the AppsState object and its children. It can be seen
// as a central coordinator. Other threads may cause work to be scheduled on this thread.
// It must not block. Instead of blocking it should schedule work onto another thread.
//
// The Timer thread. This manages timeouts. When it fires a timeout it schedules an action
// back on the AppsState thread.
//
// In addition, the web server and apps can each cause items to be scheduled on the
// AppsState thread.

namespace OpenHome.XappForms
{
    interface IAppsStateFactory
    {
        AppsState CreateAppsState();
        SessionRecord CreateSessionRecord(string aSessionId);
        ServerTab CreateServerTab(string aSessionId, string aTabId, SessionRecord aSessionRecord, AppRecord aAppRecord);
    }

    class AppsStateFactory : IAppsStateFactory
    {
        //ITabStatusListener iTabListener;
        readonly TabStatusQueue iTabStatusQueue;
        readonly Func<DateTime> iClock;
        readonly ServerTabTimeoutPolicy iTimeoutPolicy;
        readonly UserList iUserList;
        readonly SoftThread iAppsStateThread;
        readonly TimerThread iTimerThread;

        public AppsStateFactory(ITabStatusListener aTabListener, Func<DateTime> aClock, ServerTabTimeoutPolicy aTimeoutPolicy, UserList aUserList)
        {
            iAppsStateThread = new SoftThread();
            iTabStatusQueue = new TabStatusQueue(aTabListener);
            iTimeoutPolicy = aTimeoutPolicy;
            iUserList = aUserList;
            iClock = aClock;
            iTimerThread = new TimerThread(iClock);
        }

        public AppsState CreateAppsState()
        {
            return new AppsState(this, iAppsStateThread);
        }
        public SessionRecord CreateSessionRecord(string aSessionId)
        {
            return new SessionRecord(aSessionId, iTabStatusQueue, this, iAppsStateThread, iUserList);
        }
        public ServerTab CreateServerTab(string aSessionId, string aTabId, SessionRecord aSessionRecord, AppRecord aAppRecord)
        {
            return new ServerTab(aSessionId, aTabId, iTabStatusQueue, iClock, iTimeoutPolicy, iTimerThread, iAppsStateThread, aSessionRecord, aAppRecord);
        }
    }

    class AppsState
    {
        readonly SoftThread iAppsStateThread;
        readonly IAppsStateFactory iAppsStateFactory;
        public Dictionary<string, AppRecord> Apps { get; private set; }
        public Dictionary<string, SessionRecord> Sessions { get; private set; }
        //object iLock = new object();
        static System.Security.Cryptography.RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        public AppsState(IAppsStateFactory aAppsStateFactory, SoftThread aAppsStateThread)
        {
            iAppsStateFactory = aAppsStateFactory;
            iAppsStateThread = aAppsStateThread;
            Apps = new Dictionary<string, AppRecord>();
            Sessions = new Dictionary<string, SessionRecord>();
        }

        public Task<AppRecord> AddApp(string aName, IAppLayer0 aApp)
        {
            return iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    return Apps[aName] = new AppRecord(aApp, aName, new SoftThread());
                });
        }

        public Task<SessionRecord> FindOrCreateSession(string aSessionCookie)
        {
            return iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    SessionRecord session;
                    if (aSessionCookie != null && Sessions.TryGetValue(aSessionCookie, out session))
                    {
                        return session;
                    }
                    byte[] bytes = new byte[12];
                    _rng.GetBytes(bytes);
                    string id = Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');
                    if (Sessions.ContainsKey(id))
                    {
                        throw new Exception("Session ID collision. (Wow, you have a bad random number generator!)");
                    }
                    session = iAppsStateFactory.CreateSessionRecord(id);
                    Sessions[id] = session;
                    return session;
                });
        }

        public Task<SessionRecord> GetSession(string aSessionCookie)
        {
            return iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    SessionRecord session;
                    if (aSessionCookie != null && Sessions.TryGetValue(aSessionCookie, out session))
                    {
                        return session;
                    }
                    return null;
                });
        }

        public Task<AppRecord> GetApp(string aAppName)
        {
            return iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    AppRecord record;
                    Apps.TryGetValue(aAppName, out record);
                    return record;
                });
        }

        public Task<ServerTab> GetTab(string aSessionId, string aTabId)
        {
            return iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    SessionRecord session;
                    Sessions.TryGetValue(aSessionId, out session);
                    if (session != null)
                    {
                        return session.GetTab(aTabId);
                    }
                    return null;
                });
        }
    }

    public interface ISession
    {
        string UserId { get; }
        void NotifyTabExpired(string aTabId);
        void SwitchUser(string aUserId);
    }

    class SessionRecord : ISession
    {
        readonly TabStatusQueue iListener;
        readonly IAppsStateFactory iAppsStateFactory;
        readonly SoftThread iAppsStateThread;
        public string Key { get; private set; }
        public Dictionary<string, ServerTab> Tabs { get; private set; }
        int iCounter = 0;
        string iUserId = "";
        readonly UserList iUserList;

        public SessionRecord(string aKey, TabStatusQueue aListener, IAppsStateFactory aAppsStateFactory, SoftThread aAppsStateThread, UserList aUserList)
        {
            iListener = aListener;
            iAppsStateFactory = aAppsStateFactory;
            iAppsStateThread = aAppsStateThread;
            iUserList = aUserList;
            Key = aKey;
            Tabs = new Dictionary<string, ServerTab>();
        }

        public string UserId { get { return iUserId; } }

        public Task ChangeUser(string aUserId)
        {
            return iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    if (aUserId == "")
                    {
                        iUserId = aUserId;
                    }
                    else
                    {
                        Task.Factory.StartNew(() => { }, TaskCreationOptions.AttachedToParent);
                    }
                });
        }

        //public 

        public Task<ServerTab> CreateTab(AppRecord aApp, string aUserId)
        {
            return iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    SwitchUser(aUserId);
                    ServerTab newServerTab;

                    iCounter += 1;
                    string tabKey = iCounter.ToString();
                    iListener.NewTab(Key, tabKey, iUserId, aApp.Id);
                    BrowserTabProxy browserTabProxy = new BrowserTabProxy();
                    browserTabProxy.ServerTab = newServerTab = Tabs[tabKey] = iAppsStateFactory.CreateServerTab(Key, tabKey, this, aApp);
                    User user;
                    if (iUserId==null || !iUserList.TryGetUserById(iUserId, out user))
                    {
                        user = null;
                    }
                    var serverTab = aApp.App.CreateTab(browserTabProxy, user);
                    newServerTab.AppTab = new AppThreadScheduler(serverTab, aApp.SoftThread);
                    return newServerTab;
                });
        }

        public ServerTab GetTab(string aTabId)
        {
            ServerTab serverTab;
            return Tabs.TryGetValue(aTabId, out serverTab) ? serverTab : null;
        }

        public void NotifyTabExpired(string aTabId)
        {
            ServerTab tab;
            if (Tabs.TryGetValue(aTabId, out tab))
            {
                Tabs.Remove(aTabId);
                tab.TabClosed();
            }
        }

        public void SwitchUser(string aUserId)
        {
            User user = null;
            if (aUserId == null || iUserList.TryGetUserById(aUserId, out user))
            {
                iUserId = aUserId;
                foreach (var tab in Tabs.Values)
                {
                    tab.AppTab.ChangeUser(user);
                }
            }
        }
    }

    class PollRequest
    {
        Func<ArraySegment<byte>, bool> iWrite;
        //Func<Action, bool> iFlush;
        Action<Exception> iEnd;
        CancellationToken iCancelToken;
        ServerTab iServerTab;

        public PollRequest(ServerTab aServerTab, Func<ArraySegment<byte>, bool> aWrite, Func<Action, bool> aFlush, Action<Exception> aEnd, CancellationToken aCancelToken)
        {
            iServerTab = aServerTab;
            iWrite = aWrite;
            iEnd = aEnd;
            iCancelToken = aCancelToken;
            iCancelToken.Register(() => iServerTab.CancelRequest(this));
        }

        public void Complete(string aValue)
        {
            try
            {
                iWrite(new ArraySegment<byte>(Encoding.UTF8.GetBytes(aValue)));
                iEnd(null);
            }
            catch (Exception e)
            {
                Console.WriteLine("\n\n");
                Console.WriteLine(e);
                Console.WriteLine("\n\n");
                iEnd(e);
            }
        }

    }

    /// <summary>
    /// Dispatches events to the server health app asynchronously, while
    /// ensuring that they still run in order.
    /// </summary>
    class TabStatusQueue : ITabStatusListener
    {
        readonly object iAppenderLock = new object();
        readonly ITabStatusListener iListener;
        Task iTask;

        public TabStatusQueue(ITabStatusListener aListener)
        {
            iListener = aListener;
            iTask = Task.Factory.StartNew(() => { });
        }

        public void NewTab(string aSessionId, string aTabId, string aUserId, string aId)
        {
            lock (iAppenderLock)
            {
                iTask = iTask.ContinueWith(aTask => iListener.NewTab(aSessionId, aTabId, aUserId, aId));
            }
        }

        public void TabClosed(string aSessionId, string aTabId)
        {
            lock (iAppenderLock)
            {
                iTask = iTask.ContinueWith(aTask => iListener.TabClosed(aSessionId, aTabId));
            }
        }

        public void UpdateTabStatus(string aSessionId, string aTabId, string aUserId, int aQueueLength, DateTime aLastRead, bool aHasListener)
        {
            lock (iAppenderLock)
            {
                iTask = iTask.ContinueWith(aTask => iListener.UpdateTabStatus(aSessionId, aTabId, aUserId, aQueueLength, aLastRead, aHasListener));
            }
        }
    }

    class BrowserTabProxy : IBrowserTabProxy
    {
        public ServerTab ServerTab { get; set; }
        public void Send(JsonValue aJsonValue)
        {
            ServerTab.Send("event", aJsonValue);
        }

        public string SessionId { get { return ServerTab.SessionId; } }
        public string TabId { get { return ServerTab.TabKey; } }

        static string FormatCookie(string aName, string aValue, CookieAttributes aAttributes)
        {
            string expires, path, secure, httponly;
            if (aAttributes != null)
            {
                expires = aAttributes.Expires == null ? "" : aAttributes.Expires.Value.ToString(" ddd, d MMM yyyy HH:mm:ss UTC;");
                path = aAttributes.Path == null ? "" : (" path=" + aAttributes.Path + ";");
                secure = aAttributes.Secure ? " Secure;" : "";
                httponly = aAttributes.HttpOnly ? " HttpOnly;" : "";
            }
            else
            {
                expires = path = secure = httponly = "";
            }
            string formatted = String.Format("{0}={1};{2}{3}{4}{5}", aName, aValue, expires, path, secure, httponly);
            return formatted;
        }

        public void SetCookie(string aName, string aValue, CookieAttributes aAttributes)
        {
            ServerTab.Send(
                "set-cookie",
                new JsonString(FormatCookie(aName, aValue, aAttributes)));
            
        }

        public void ReloadPage()
        {
            ServerTab.Send(
                "refresh-browser",
                JsonNull.Instance);
        }


        public void SwitchUser(string aUserId)
        {
            ServerTab.SwitchUser(aUserId);
        }
    }

    class AppRecord
    {
        public IAppLayer0 App { get; private set; }
        public string Id { get; private set; }
        public SoftThread SoftThread { get; private set; }

        public AppRecord(IAppLayer0 aApp, string aId, SoftThread aSoftThread)
        {
            App = aApp;
            Id = aId;
            SoftThread = aSoftThread;
        }
    }

    class AppSessionRecord
    {
        public AppRecord AppRecord { get; private set; }

        public AppSessionRecord(AppRecord aAppRecord)
        {
            AppRecord = aAppRecord;
        }
    }

    public interface IAppLayer0
    {
        void ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder);
        IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser);
        Dictionary<string, string> GetBrowserDiscriminationMappings();
    }

    class AppThreadScheduler : IAppTab
    {
        readonly IAppTab iAppTab;
        readonly SoftThread iAppThread;

        public AppThreadScheduler(IAppTab aAppTab, SoftThread aAppThread)
        {
            iAppTab = aAppTab;
            iAppThread = aAppThread;
        }

        public void ChangeUser(User aUser)
        {
            iAppThread.ScheduleExclusive(() => iAppTab.ChangeUser(aUser));
        }

        public void Receive(JsonValue aJsonValue)
        {
            iAppThread.ScheduleExclusive(() => iAppTab.Receive(aJsonValue));
        }

        public void TabClosed()
        {
            iAppThread.ScheduleExclusive(() => iAppTab.TabClosed());
        }
    }

    public interface IAppTab
    {
        void ChangeUser(User aUser);
        void Receive(JsonValue aJsonValue);
        void TabClosed();
    }

    public class CookieAttributes
    {
        public string Domain { get; set; }
        public string Path { get; set; }
        public DateTime? Expires { get; set; }
        public bool Secure { get; set; }
        public bool HttpOnly { get; set; }
//Domain=.foo.com; Path=/; Expires=Wed, 13-Jan-2021 22:23:01 GMT; Secure; HttpOnly
    }

    public interface IBrowserTabProxy
    {
        void Send(JsonValue aJsonValue);
        void SwitchUser(string aUserId);
        string SessionId { get; }
        string TabId { get; }
        void SetCookie(string aName, string aValue, CookieAttributes aAttributes);
        void ReloadPage();
    }

}
