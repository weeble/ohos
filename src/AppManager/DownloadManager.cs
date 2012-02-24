using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHome.Net.Device;
using OpenHome.Os.Platform.Threading;

namespace OpenHome.Os.AppManager
{

    public interface IDownloadThread
    {
        void StartDownload(string aUrl, Action<string, DateTime> aCompleteCallback, Action aFailedCallback);
        void CancelDownload(string aUrl);
        void StartPollingUrl(string aId, string aUrl, DateTime aLastModified, Action aAvailableCallback, Action aErrorCallback);
        void StopPollingUrl(string aId);
        void NotifyDownloadFailed(string aUrl);
        void NotifyDownloadComplete(string aUrl, int aBytes, string aLocalPath);
        void NotifyDownloadProgress(string aUrl, int aBytes, int aTotalBytes);
    }

    public class DownloadProgress
    {
        public string Uri { get; private set; }
        public int DownloadedBytes { get; private set; }
        public int TotalBytes { get; private set; }
        public bool HasTotalBytes { get { return TotalBytes != -1; } }
        public bool HasFailed { get; private set; }

        public DownloadProgress(string aUri, int aDownloadedBytes, int aTotalBytes, bool aHasFailed)
        {
            Uri = aUri;
            DownloadedBytes = aDownloadedBytes;
            TotalBytes = aTotalBytes;
            HasFailed = aHasFailed;
        }
        
        public static DownloadProgress CreateFailed(string aUri)
        {
            return new DownloadProgress(aUri, 0, 0, true);
        }
        public static DownloadProgress CreateJustStarted(string aUri)
        {
            return new DownloadProgress(aUri, 0, 0, false);
        }
    }

    /// <summary>
    /// Note: only public for test purposes.
    /// Runs on a thread and carries out downloads and polling that we don't
    /// want to block provider threads. (Even asynchronous web requests can
    /// block for a long time on DNS resolution and establishing a connection
    /// before they start transferring data asynchronously.)
    /// </summary>
    public class Downloader : IDownloadThread
    {
        class FailedDownload
        {
            public DateTime TimeOfFailure { get; set; }
            public DownloadProgress DownloadProgress { get; set; }
        }

        readonly IDownloadDirectory iDownloadDirectory;
        readonly IPollManager iPollManager;
        readonly IUrlFetcher iUrlFetcher;
        readonly Channel<Action<IDownloadThread>> iMessageQueue;

        readonly Dictionary<string, DownloadListener> iDownloads = new Dictionary<string, DownloadListener>();
        readonly Dictionary<string, DownloadProgress> iPublicDownloadInfo = new Dictionary<string, DownloadProgress>();
        readonly Queue<FailedDownload> iPublicFailedDownloads = new Queue<FailedDownload>();
        readonly object iPublicDownloadsLock = new object();

        public TimeSpan FailureTimeout { get; set; }
        public event EventHandler DownloadChanged;
        IThreadCommunicator iThread;
        DateTime? iLastPollTime;
        bool iIgnoreRequests;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="aDownloadDirectory"></param>
        /// <param name="aMessageQueue">
        /// Message queue holding actions for the thread to perform in sequence.
        /// </param>
        /// <param name="aPollManager">
        /// Performs URL polling to see if an update is available.
        /// </param>
        /// <param name="aUrlFetcher">
        /// Performs URL fetching.
        /// </param>
        public Downloader(
            IDownloadDirectory aDownloadDirectory,
            Channel<Action<IDownloadThread>> aMessageQueue,
            IPollManager aPollManager,
            IUrlFetcher aUrlFetcher)
        {
            iMessageQueue = aMessageQueue;
            iDownloadDirectory = aDownloadDirectory;
            iPollManager = aPollManager;
            iUrlFetcher = aUrlFetcher;
            FailureTimeout = TimeSpan.FromSeconds(10);
        }

        void InvokeDownloadChanged(EventArgs aE)
        {
            EventHandler handler = DownloadChanged;
            if (handler != null) handler(this, aE);
        }

        public IEnumerable<DownloadProgress> GetDownloadStatus()
        {
            lock (iPublicDownloadInfo)
            {
                List<DownloadProgress> downloads = iPublicDownloadInfo.Values.ToList();
                downloads.AddRange(iPublicFailedDownloads.Select(aFailure => aFailure.DownloadProgress));
                return downloads;
            }
        }

        int GetMillisecondsUntilPollingRequired()
        {
            if (iPollManager.Empty)
            {
                return -1;
            }
            if (iLastPollTime == null)
            {
                return 0;
            }
            var now = DateTime.UtcNow;
            var timeForNextPoll = iLastPollTime.Value + iPollManager.PollingInterval;
            var timeUntilNextPoll = timeForNextPoll - now;
            if (timeUntilNextPoll <= TimeSpan.Zero)
            {
                return 0;
            }
            return (int)Math.Ceiling(timeUntilNextPoll.TotalMilliseconds);
        }

        int GetMillisecondsUntilCleanupRequired()
        {
            lock (iPublicDownloadsLock)
            {
                if (iPublicFailedDownloads.Count==0)
                {
                    return -1;
                }
                var now = DateTime.UtcNow;
                var failureTime = iPublicFailedDownloads.Peek().TimeOfFailure;
                var cleanupTime = failureTime + FailureTimeout;
                var wait = cleanupTime - now;
                if (wait < TimeSpan.Zero)
                {
                    return 0;
                }
                return (int)Math.Ceiling(wait.TotalMilliseconds);
            }
        }

        int GetMillisecondsUntilActionRequired()
        {
            int cleanupTime = GetMillisecondsUntilCleanupRequired();
            int pollTime = GetMillisecondsUntilPollingRequired();
            if (cleanupTime == -1) return pollTime;
            if (pollTime == -1) return -1;
            return Math.Min(cleanupTime, pollTime);
        }

        void CleanupFailedDownloads()
        {
            bool cleanedUp = false;
            lock (iPublicDownloadsLock)
            {
                while (
                    (iPublicFailedDownloads.Count>0) &&
                        (DateTime.UtcNow - iPublicFailedDownloads.Peek().TimeOfFailure > FailureTimeout))
                {
                    iPublicFailedDownloads.Dequeue();
                    cleanedUp = true;
                }
            }
            if (cleanedUp)
            {
                InvokeDownloadChanged(EventArgs.Empty);
            }
        }


        /// <summary>
        /// Receives notifications from asynchronous downloads and queues them back
        /// to the downloader thread.
        /// </summary>
        class DownloadListener : IDownloadListener
        {
            readonly Downloader iParent;
            readonly string iUri;
            readonly string iLocalPath;
            public Action<string, DateTime> CompletedCallback { get; private set; }
            public Action FailedCallback { get; private set; }
            public IDisposable Download { private get; set; }
            public DateTime LastModified { get; private set; }

            public DownloadListener(Downloader aParent, string aUri, string aLocalPath, Action<string, DateTime> aCompletedCallback, Action aFailedCallback)
            {
                iParent = aParent;
                iUri = aUri;
                iLocalPath = aLocalPath;
                CompletedCallback = aCompletedCallback;
                FailedCallback = aFailedCallback;
            }

            public void Cancel()
            {
                if (Download != null)
                {
                    Download.Dispose();
                }
            }

            public void Complete(DateTime aLastModified)
            {
                LastModified = aLastModified;
                iParent.iMessageQueue.Send(aThread => aThread.NotifyDownloadComplete(iUri, 0, iLocalPath));
            }

            public void Failed()
            {
                iParent.iMessageQueue.Send(aThread => aThread.NotifyDownloadFailed(iUri));
            }

            public void Progress(int aBytes, int aBytesTotal)
            {
                iParent.iMessageQueue.NonBlockingSend(aThread => aThread.NotifyDownloadProgress(iUri, aBytes, aBytesTotal));
            }
        }

        public void StartDownload(string aUrl, Action<string, DateTime> aCompleteCallback, Action aFailedCallback)
        {
            if (!iDownloads.ContainsKey(aUrl))
            {
                FileStream fileStream;
                string fileName;
                iDownloadDirectory.CreateFile(out fileStream, out fileName);
                var downloadListener = new DownloadListener(this, aUrl, fileName, aCompleteCallback, aFailedCallback);
                iDownloads[aUrl] = downloadListener;
                downloadListener.Download = iUrlFetcher.Fetch(aUrl, fileStream, downloadListener);
                lock (iPublicDownloadInfo)
                {
                    iPublicDownloadInfo[aUrl] = DownloadProgress.CreateJustStarted(aUrl);
                }
                InvokeDownloadChanged(EventArgs.Empty);
            }
        }

        public void CancelDownload(string aUrl)
        {
            if (iDownloads.ContainsKey(aUrl))
            {
                iDownloads[aUrl].Cancel();
            }
        }

        public void StartPollingUrl(string aId, string aUrl, DateTime aLastModified, Action aAvailableCallback, Action aErrorCallback)
        {
            if (iIgnoreRequests) return;
            iPollManager.StartPollingApp(aId, aUrl, aLastModified, aAvailableCallback, aErrorCallback);
        }

        public void StopPollingUrl(string aId)
        {
            if (iIgnoreRequests) return;
            iPollManager.CancelPollingApp(aId);
        }

        public void NotifyDownloadFailed(string aUrl)
        {
            if (!iDownloads.ContainsKey(aUrl))
            {
                return;
            }
            iDownloads[aUrl].FailedCallback();
            iDownloads.Remove(aUrl);
            lock (iPublicDownloadsLock)
            {
                iPublicDownloadInfo.Remove(aUrl);
            }
            iPublicFailedDownloads.Enqueue(new FailedDownload { TimeOfFailure = DateTime.UtcNow, DownloadProgress = DownloadProgress.CreateFailed(aUrl) });
            InvokeDownloadChanged(EventArgs.Empty);
        }

        public void NotifyDownloadComplete(string aUrl, int aBytes, string aLocalPath)
        {
            if (!iDownloads.ContainsKey(aUrl))
            {
                return;
            }
            iDownloads[aUrl].CompletedCallback(aLocalPath, iDownloads[aUrl].LastModified);
            iDownloads.Remove(aUrl);
            lock (iPublicDownloadsLock)
            {
                iPublicDownloadInfo.Remove(aUrl);
            }
            InvokeDownloadChanged(EventArgs.Empty);
        }

        public void NotifyDownloadProgress(string aUrl, int aBytes, int aTotalBytes)
        {
            if (!iDownloads.ContainsKey(aUrl))
            {
                return;
            }
            lock (iPublicDownloadsLock)
            {
                iPublicDownloadInfo[aUrl] = new DownloadProgress(aUrl, aBytes, aTotalBytes, false);
            }
        }

        public void Step()
        {
            CleanupFailedDownloads();
            PollIfRequired();
            iThread.SelectWithTimeout(
                GetMillisecondsUntilActionRequired(),
                iMessageQueue.CaseReceive(aAction=>aAction(this))
                );
        }

        public void CancelAllDownloads()
        {
            foreach (var download in iDownloads.Values)
            {
                download.Cancel();
            }
        }

        public void WaitForAllDownloads()
        {
            iIgnoreRequests = true;
            while (iDownloads.Count>0)
            {
                iMessageQueue.Receive()(this);
            }
        }

        public void Run(IThreadCommunicator aThread)
        {
            iThread = aThread;
            while (!iThread.Abandoned)
            {
                Step();
            }
            CancelAllDownloads();
            WaitForAllDownloads();
        }

        void PollIfRequired()
        {
            if (!iPollManager.Empty && GetMillisecondsUntilPollingRequired() == 0)
            {
                iPollManager.PollNext();
                iLastPollTime = DateTime.UtcNow;
            }
        }
    }

    public interface IDownloadManager
    {
        int MaxSimultaneousDownloads { get; set; }
        event EventHandler DownloadCountChanged;
        void StartDownload(string aUrl, Action<string, DateTime> aCallback);
        IEnumerable<DownloadProgress> GetDownloadsStatus();
        void CancelDownload(string aAppUrl);
        void StartPollingForAppUpdate(string aAppName, string aUrl, Action aAvailableAction, Action aFailedAction, DateTime aLastModified);
        void StopPollingForAppUpdate(string aAppName);
    }

    public class DownloadManager : IDownloadManager
    {
        readonly CommunicatorThread iDownloadThread;
        readonly Downloader iDownloader;
        readonly IDownloadDirectory iDownloadDirectory;
        readonly Channel<Action<IDownloadThread>> iMessageQueue;
        public int MaxSimultaneousDownloads { get; set; }
        public event EventHandler DownloadCountChanged
        {
            add { iDownloader.DownloadChanged += value; }
            remove { iDownloader.DownloadChanged -= value; }
        }

        public DownloadManager(IDownloadDirectory aDownloadDirectory, IUrlFetcher aUrlFetcher)
        {
            iDownloadDirectory = aDownloadDirectory;
            iMessageQueue = new Channel<Action<IDownloadThread>>(5);
            var urlPoller = new DefaultUrlPoller();
            var pollManager = new PollManager(urlPoller);
            iDownloader = new Downloader(iDownloadDirectory, iMessageQueue, pollManager, aUrlFetcher);
            iDownloadThread = new CommunicatorThread(iDownloader.Run, "DownloadManager");
            iDownloadThread.Start();
        }

        public void StartPollingForAppUpdate(string aAppName, string aUrl, Action aAvailableAction, Action aFailedAction, DateTime aLastModified)
        {
            iMessageQueue.Send(aThread => aThread.StartPollingUrl(aAppName, aUrl, aLastModified, aAvailableAction, aFailedAction));
        }

        public void StopPollingForAppUpdate(string aAppName)
        {
            iMessageQueue.Send(aThread => aThread.StopPollingUrl(aAppName));
        }

        public void StartDownload(string aUrl, Action<string, DateTime> aCallback)
        {
            if (!iMessageQueue.NonBlockingSend(aThread => aThread.StartDownload(aUrl, aCallback, () => { })))
            {
                throw new ActionError("Too busy.");
            }
        }

        public IEnumerable<DownloadProgress> GetDownloadsStatus()
        {
            return iDownloader.GetDownloadStatus();
        }

        public void CancelDownload(string aAppUrl)
        {
            if (!iMessageQueue.NonBlockingSend(aThread => aThread.CancelDownload(aAppUrl)))
            {
                throw new ActionError("Too busy.");
            }
        }

        public void Dispose()
        {
            iDownloadThread.Dispose();
            iMessageQueue.Dispose();
        }
    }
}