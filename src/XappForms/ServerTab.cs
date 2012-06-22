using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using OpenHome.XappForms.Json;
using Owin;

namespace OpenHome.XappForms
{
    class ServerTabTimeoutPolicy
    {
        public TimeSpan MaxAgeWithoutListener { get; private set; }
        public TimeSpan MaxPollDuration { get; private set; }

        public ServerTabTimeoutPolicy(TimeSpan aMaxAgeWithoutListener, TimeSpan aMaxPollDuration)
        {
            MaxAgeWithoutListener = aMaxAgeWithoutListener;
            MaxPollDuration = aMaxPollDuration;
        }
    }
    class ServerTab
    {
        readonly ITabStatusListener iListener;
        readonly Func<DateTime> iClock;
        readonly ServerTabTimeoutPolicy iTimeoutPolicy;
        readonly ITimerThread iTimerThread;
        readonly ISoftThread iAppsStateThread;
        readonly ISession iSession;
        public string SessionId { get; private set; }
        public string TabKey { get; private set; }
        public AppRecord AppRecord { get; set; }
        readonly IJsonEventQueue iEventQueue;
        readonly ITimerCallback iTimerCallback;

        public IAppTab AppTab { get; set; }

        DateTime iLastRead;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="aSessionId">
        /// The unique identifier of the session that the tab is in.
        /// </param>
        /// <param name="aTabKey">
        /// An identifier for this tab, unique within its session.
        /// </param>
        /// <param name="aListener">
        /// A listener that will be notified about changes to the state of the
        /// event queue. Used to implement the "server health" app.
        /// Important: The event-handler will be invoked while some app-state
        /// locks are held. To avoid deadlock, it's a good idea to use
        /// TabStatusQueue to transfer the events to another thread.
        /// </param>
        /// <param name="aClock">
        /// Provides access to the current UTC time. Production code should
        /// likely use ()=>DateTime.UtcNow, testing code will want more direct
        /// control over the perceived time.
        /// </param>
        /// <param name="aTimeoutPolicy">
        /// Policy on how long to keep long polls alive and how long to
        /// keep a tab alive with no outstanding long poll.
        /// </param>
        /// <param name="aTimerThread">
        /// Timer for scheduling maintenance work, such as checking for
        /// expired tabs and long polls.
        /// </param>
        /// <param name="aAppsStateThread">
        /// The soft thread for scheduling all asynchronous work. When we
        /// get invoked from the timer thread, we dispatch back to this
        /// thread before touching any of our mutable state.
        /// </param>
        /// <param name="aSession">
        /// Something that wants to know when a tab should expire due to
        /// inactivity.
        /// </param>
        /// <param name="aAppRecord">
        /// AppRecord associated with the tab.
        /// </param>
        public ServerTab(
            string aSessionId,
            string aTabKey,
            ITabStatusListener aListener,
            Func<DateTime> aClock,
            ServerTabTimeoutPolicy aTimeoutPolicy,
            ITimerThread aTimerThread,
            ISoftThread aAppsStateThread,
            ISession aSession,
            AppRecord aAppRecord)
        {
            SessionId = aSessionId;
            iListener = aListener;
            iAppsStateThread = aAppsStateThread;
            iSession = aSession;
            iTimerThread = aTimerThread;
            iClock = aClock;
            iTimeoutPolicy = aTimeoutPolicy;
            TabKey = aTabKey;
            AppRecord = aAppRecord;
            iEventQueue = new JsonEventQueue(12000);// aEventQueue;
            iLastRead = iClock();
            iTimerCallback = iTimerThread.RegisterCallback(
                ()=>iAppsStateThread.ScheduleExclusive(DoMaintenance));
            RescheduleMaintenance();
        }

        void RescheduleMaintenance()
        {
            var time = GetMaintenanceTime();
            iTimerCallback.Reschedule(time);
        }

        public DateTime GetMaintenanceTime()
        {
            DateTime maintenanceTime;
            if (iEventQueue.HasListener)
            {
                maintenanceTime = iLastRead + iTimeoutPolicy.MaxPollDuration;
            }
            else
            {
                maintenanceTime = iLastRead + iTimeoutPolicy.MaxAgeWithoutListener;
            }
            return maintenanceTime;
        }

        public void DoMaintenance()
        {
            var now = iClock();
            if (iEventQueue.HasListener)
            {
                if (iLastRead + iTimeoutPolicy.MaxPollDuration <= now)
                {
                    iEventQueue.CompleteNow();
                    iLastRead = now;
                    iListener.UpdateTabStatus(SessionId, TabKey, iSession.UserId, iEventQueue.QueueSize, iLastRead, false);
                }
            }
            else
            {
                if (iLastRead + iTimeoutPolicy.MaxAgeWithoutListener <= now)
                {
                    iSession.NotifyTabExpired(TabKey);
                    iLastRead = now;
                }
            }
            RescheduleMaintenance();
        }

        public void Send(string aType, JsonValue aJsonValue)
        {
            iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    iEventQueue.Append(aType, aJsonValue);
                    if (iEventQueue.QueueSize == 0)
                    {
                        iLastRead = iClock();
                    }
                    RescheduleMaintenance();
                    iListener.UpdateTabStatus(SessionId, TabKey, iSession.UserId, iEventQueue.QueueSize, iLastRead, false);
                });
        }

        public BodyDelegate Serve()
        {
            return AddRequest;
        }

        void AddRequest(Func<ArraySegment<byte>, bool> aWrite, Func<Action, bool> aFlush, Action<Exception> aEnd, CancellationToken aCancelToken)
        {
            iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    iEventQueue.AddRequest(new PollRequest(this, aWrite, aFlush, aEnd, aCancelToken));
                    iLastRead = iClock();
                    RescheduleMaintenance();
                    iListener.UpdateTabStatus(SessionId, TabKey, iSession.UserId, iEventQueue.QueueSize, iLastRead, iEventQueue.HasListener);
                });
        }

        public void CancelRequest(PollRequest aPollRequest)
        {
            iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    iEventQueue.CancelRequest(aPollRequest);
                    iListener.UpdateTabStatus(SessionId, TabKey, iSession.UserId, iEventQueue.QueueSize, iLastRead, iEventQueue.HasListener);
                    RescheduleMaintenance();
                });
        }

        public void TabClosed()
        {
            if (AppTab != null)
            {
                AppTab.TabClosed();
            }
            iTimerCallback.Dispose();
            iListener.TabClosed(SessionId, TabKey);
        }

        public void SwitchUser(string aUserId)
        {
            iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    iSession.SwitchUser(aUserId);
                });
        }

        public void SetCookie(string aName, string aValue, CookieAttributes aAttributes)
        {
            iAppsStateThread.ScheduleExclusive(
                () =>
                {
                    iEventQueue.UpdateCookie(aName, aValue, aAttributes);
                });
        }
    }

    interface IJsonEventQueue
    {
        bool HasListener { get; }
        int QueueSize { get; }
        void CancelRequest(PollRequest aRequest);
        void AddRequest(PollRequest aRequest);
        void Append(string aType, JsonValue aPayload);
        void CompleteNow();
        void UpdateCookie(string aName, string aValue, CookieAttributes aAttributes);
    }

    class CookieData
    {
        string Name { get;  set; }
        string Value { get; set; }
        CookieAttributes Attributes { get; set; }
    }
    class JsonEventQueue : IJsonEventQueue
    {
        readonly Queue<string> iItems;
        readonly int iSizeLimit;
        int iCurrentSize;
        bool iOverflow;
        PollRequest iRequest;
        Dictionary<string, CookieData> iCookies = new Dictionary<string, CookieData>();

        static readonly JsonValue OverflowEvent = new JsonObject { { "type", "error" }, { "value", "overflow" } };
        static readonly JsonValue ClashEvent = new JsonObject { { "type", "error" }, { "value", "clash" } };
        static JsonValue CreateSendEvent(string aType, JsonValue aPayload)
        {
            return new JsonObject { { "type", aType }, { "value", aPayload } };
        }
        public bool HasListener { get { return iRequest != null; } }

        public void CancelRequest(PollRequest aRequest)
        {
            Console.WriteLine("TAB CANCEL");
            if (iRequest == aRequest)
            {
                iRequest = null;
            }
        }

        public int QueueSize { get { return iItems.Count; } }

        public void AddRequest(PollRequest aRequest)
        {
            if (iRequest != null)
            {
                Debugger.Break();
                aRequest.Complete(new JsonArray { ClashEvent }.ToString());
                return;
            }
            if (iItems.Count > 0)
            {
                try
                {
                    aRequest.Complete(ToJsonString());
                    Clear();
                }
                catch (SocketException)
                {
                    Console.WriteLine("SOCKET EXCEPTION");
                    // FIXME: See note in CompleteNow()
                }
            }
            else
            {
                iRequest = aRequest;
            }
        }

        /// <summary>
        /// Create a new event queue.
        /// </summary>
        /// <param name="aSizeLimit">
        /// Approximate character limit. If more characters are enqueued than this
        /// then they will start to be discarded and an overflow event will replace
        /// them. Note that .NET characters are 16-bit.
        /// </param>
        public JsonEventQueue(int aSizeLimit)
        {
            iItems = new Queue<string>();
            iSizeLimit = aSizeLimit;
        }

        public void Append(JsonValue aPayload)
        {
            Append("event", aPayload);
        }

        public void Append(string aType, JsonValue aPayload)
        {
            if (iOverflow)
            {
                iItems.Enqueue(OverflowEvent.ToString());
            }
            else
            {
                string jsonPayload = CreateSendEvent(aType, aPayload).ToString();
                if (iCurrentSize + jsonPayload.Length > iSizeLimit)
                {
                    iOverflow = true;
                }
                iItems.Enqueue(jsonPayload);
            }
            CompleteNow();
        }

        public void CompleteNow()
        {
            if (iRequest != null)
            {
                try
                {
                    iRequest.Complete(ToJsonString());
                    Clear();
                    iRequest = null;
                }
                catch (SocketException)
                {
                    Console.WriteLine("SOCKET EXCEPTION");
                    // FIXME: We assume that a SocketException means that
                    // the message was not sent, but this is not necessarily
                    // the case. We could introduce a more robust protocol
                    // to recover from transient errors.
                    iRequest = null;
                }
                catch
                {
                    iRequest = null;
                    throw;
                }
            }
        }

        public void UpdateCookie(string aName, string aValue, CookieAttributes aAttributes)
        {
            throw new NotImplementedException();
        }

        void Clear()
        {
            iItems.Clear();
            iCurrentSize = 0;
        }

        string ToJsonString()
        {
            iOverflow = false;
            string eventSequence = String.Join(",", iItems);
            //iItems.Clear();
            //iCurrentSize = 0;
            return "[" + eventSequence + "]";
        }

    }
}