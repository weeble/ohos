using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace OpenHome.Os.AppManager
{
    public interface IPollManager
    {
        TimeSpan MaxAppPollingInterval { get; set; }
        TimeSpan MinPollingInterval { get; set; }
        TimeSpan PollingInterval { get; }
        bool Empty { get; }
        void StartPollingApp(string aAppName, string aUrl, DateTime aLastModified, Action aReadyAction, Action aFailedAction);
        void PollNext();
        void CancelPollingApp(string aAppName);
    }

    public class PollManager : IPollManager
    {
        class PollingUrl
        {
            readonly string iUrl;
            readonly DateTime iLastModified;
            readonly Action iReadyAction;
            readonly Action iFailedAction;
            bool iCancelled;

            public PollingUrl(string aUrl, DateTime aLastModified, Action aReadyAction, Action aFailedAction)
            {
                iUrl = aUrl;
                iLastModified = aLastModified;
                iReadyAction = aReadyAction;
                iFailedAction = aFailedAction;
                iCancelled = false;
            }

            public bool Cancelled { get { return iCancelled; } set { iCancelled = value; } }

            public void PollNow(IUrlPoller aPoller)
            {
                if (iCancelled) return;
                switch (aPoller.Poll(iUrl, iLastModified))
                {
                    case DownloadAvailableState.Available:
                        iReadyAction();
                        return;
                    case DownloadAvailableState.Error:
                        iFailedAction();
                        return;
                    case DownloadAvailableState.NotAvailable:
                        return;
                }
            }
        }

        /// <summary>
        /// Interval to wait before polling the same app again. Apps will be polled
        /// round-robin, with this interval determining the period over which every
        /// app will be polled once, unless that would breach MinPollingInterval.
        /// </summary>
        public TimeSpan MaxAppPollingInterval { get; set; }
        /// <summary>
        /// Minimum interval between any two polling attempts.
        /// </summary>
        public TimeSpan MinPollingInterval { get; set; }
        public TimeSpan PollingInterval
        {
            get
            {
                if (iPollingUrls.Count == 0)
                {
                    return MaxAppPollingInterval;
                }
                long ticks = MaxAppPollingInterval.Ticks / iPollingUrls.Count;
                ticks = Math.Max(MinPollingInterval.Ticks, ticks);
                return TimeSpan.FromTicks(ticks);
            }
        }
        public bool Empty { get { return iPollingUrls.Count==0; } }

        readonly Dictionary<string, PollingUrl> iPollingUrls = new Dictionary<string, PollingUrl>();
        Queue<PollingUrl> iPollingOrder = new Queue<PollingUrl>();
        readonly IUrlPoller iUrlPoller;

        public PollManager(IUrlPoller aUrlPoller)
        {
            MinPollingInterval = TimeSpan.FromSeconds(15);
            MaxAppPollingInterval = TimeSpan.FromMinutes(5); // TODO: Lengthen polling interval for normal use.
            iUrlPoller = aUrlPoller;
        }

        public void StartPollingApp(string aAppName, string aUrl, DateTime aLastModified, Action aReadyAction, Action aFailedAction)
        {
            PollingUrl pollingUrl;
            if (iPollingUrls.TryGetValue(aAppName, out pollingUrl))
            {
                pollingUrl.Cancelled = true;
            }
            pollingUrl = new PollingUrl(aUrl, aLastModified, aReadyAction, aFailedAction);
            iPollingUrls[aAppName] = pollingUrl;
            iPollingOrder.Enqueue(pollingUrl);
        }

        public void PollNext()
        {
            if (iPollingUrls.Count > 0)
            {
                for (; ; )
                {
                    var pollingUrl = iPollingOrder.Dequeue();
                    if (pollingUrl.Cancelled)
                    {
                        continue;
                    }
                    iPollingOrder.Enqueue(pollingUrl);
                    pollingUrl.PollNow(iUrlPoller);
                    break;
                }
            }
        }

        public void CancelPollingApp(string aAppName)
        {
            PollingUrl pollingUrl;
            if (iPollingUrls.TryGetValue(aAppName, out pollingUrl))
            {
                pollingUrl.Cancelled = true;
                iPollingUrls.Remove(aAppName);
                // Normally we mark URLs as cancelled, but don't bother to remove them
                // from the queue until they come up during polling. However, if we
                // add and remove items far more often than polling occurs, we will
                // end up with a silly number of cancelled entries in the queue. In
                // that case, purge them whenever there are too many.
                if (iPollingUrls.Count == 0)
                {
                    iPollingOrder.Clear();
                }
                if (iPollingUrls.Count * 2 < iPollingOrder.Count)
                {
                    CleanPollingOrder();
                }
            }
        }

        void CleanPollingOrder()
        {
            iPollingOrder = new Queue<PollingUrl>(iPollingOrder.Where(aPollingUrl => !aPollingUrl.Cancelled));
        }
    }
}