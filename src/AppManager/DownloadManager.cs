using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using OpenHome.Net.Device;
using OpenHome.Os.Platform.Threading;
using log4net;

namespace OpenHome.Os.AppManager
{
    public class DownloadInstruction
    {
        public string Url { get; set; }
        public bool Cancel { get; set; }
        public Action<string, DateTime> CompleteCallback { get; set; }
        public Action FailedCallback { get; set; }
    }

    public class PollInstruction
    {
        public string Url { get; set; }
        public bool Cancel { get; set; }
        public DateTime LastModified { get; set; }
        public Action AvailableCallback { get; set; }
        public Action ErrorCallback { get; set; }
        public string AppName { get; set; }
    }

    class DownloadResult
    {
        public string Url { get; set; }
        public string FileName { get; set; }
        public bool Success { get; set; }
    }

    public class DownloadProgress
    {
        public string Uri { get; private set; }
        public string LocalPath { get; private set; }
        public int DownloadedBytes { get; private set; }
        public int TotalBytes { get; private set; }
        public bool HasTotalBytes { get; private set; }
        public bool HasFailed { get; private set; }
        public bool HasCompleted { get; private set; }

        public DownloadProgress(string aUri, string aLocalPath, int aDownloadedBytes, int aTotalBytes, bool aHasTotalBytes, bool aHasFailed, bool aHasCompleted)
        {
            Uri = aUri;
            LocalPath = aLocalPath;
            DownloadedBytes = aDownloadedBytes;
            TotalBytes = aTotalBytes;
            HasTotalBytes = aHasTotalBytes;
            HasFailed = aHasFailed;
            HasCompleted = aHasCompleted;
        }
        public static DownloadProgress CreateFailed(string aUri)
        {
            return new DownloadProgress(aUri, "", 0, -1, false, true, false);
        }
        public static DownloadProgress CreateComplete(string aUri, int aBytes, string aLocalPath)
        {
            return new DownloadProgress(aUri, aLocalPath, aBytes, aBytes, true, false, true);
        }
        public static DownloadProgress CreateInProgress(string aUri, int aBytes, int aBytesTotal)
        {
            return new DownloadProgress(aUri, "", aBytes, aBytesTotal, aBytesTotal != -1, false, false);
        }
        public static DownloadProgress CreateJustStarted(string aUri)
        {
            return new DownloadProgress(aUri, "", 0, -1, false, false, false);
        }
    }


    public class Downloader
    {
        class FailedDownload
        {
            public DateTime TimeOfFailure { get; set; }
            public DownloadProgress DownloadProgress { get; set; }
        }

        readonly Channel<DownloadInstruction> iInstructionChannel;
        readonly Channel<PollInstruction> iPollInstructionChannel;
        readonly Dictionary<string, DownloadListener> iDownloads = new Dictionary<string, DownloadListener>();
        readonly IDownloadDirectory iDownloadDirectory;
        readonly Dictionary<string, DownloadProgress> iPublicDownloadInfo = new Dictionary<string, DownloadProgress>();
        readonly Queue<FailedDownload> iPublicFailedDownloads = new Queue<FailedDownload>();
        readonly object iPublicDownloadsLock = new object();
        readonly IPollManager iPollManager;
        public TimeSpan FailureTimeout { get; set; }
        public event EventHandler DownloadChanged;
        Channel<DownloadProgress> iInternalProgressChannel;
        IThreadCommunicator iThread;
        DateTime? iLastPollTime;
        IUrlFetcher iUrlFetcher;


        public Downloader(
            IDownloadDirectory aDownloadDirectory,
            Channel<DownloadInstruction> aInstructionChannel,
            Channel<PollInstruction> aPollInstructionChannel,
            IPollManager aPollManager,
            IUrlFetcher aUrlFetcher)
        {
            iInstructionChannel = aInstructionChannel;
            iPollInstructionChannel = aPollInstructionChannel;
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
            Downloader iParent;
            string iUri;
            string iLocalPath;
            public Action<string, DateTime> CompletedCallback { get; private set; }
            public Action FailedCallback { get; private set; }
            public IDisposable Download { get; set; }
            public DateTime LastModified { get; private set; }

            public DownloadListener(Downloader aParent, string aUri, string aLocalPath)
            {
                iParent = aParent;
                iUri = aUri;
                iLocalPath = aLocalPath;
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
                iParent.iInternalProgressChannel.Send(DownloadProgress.CreateComplete(iUri, 0, iLocalPath));
            }

            public void Failed()
            {
                iParent.iInternalProgressChannel.Send(DownloadProgress.CreateFailed(iUri));
            }

            public void Progress(int aBytes, int aBytesTotal)
            {
                iParent.iInternalProgressChannel.NonBlockingSend(
                    DownloadProgress.CreateInProgress(iUri, aBytes, aBytesTotal));
            }
        }

        public void ReceiveDownloadInstruction(DownloadInstruction aInstruction)
        {
            string url = aInstruction.Url;
            bool cancel = aInstruction.Cancel;
            if (cancel)
            {
                if (iDownloads.ContainsKey(url))
                {
                    iDownloads[url].Cancel();
                }
            }
            else
            {
                if (!iDownloads.ContainsKey(url))
                {
                    FileStream fileStream = iDownloadDirectory.CreateFile();
                    var downloadListener = new DownloadListener(this, url, fileStream.Name);
                    downloadListener.Download = iUrlFetcher.Fetch(url, fileStream, downloadListener);
                    iDownloads[url] = downloadListener;
                    lock (iPublicDownloadInfo)
                    {
                        iPublicDownloadInfo[url] = DownloadProgress.CreateJustStarted(url);
                    }
                    InvokeDownloadChanged(EventArgs.Empty);
                }
            }
        }

        public void ReceivePollInstruction(PollInstruction aInstruction)
        {
            if (aInstruction.Cancel)
            {
                iPollManager.CancelPollingApp(aInstruction.AppName);
            }
            else
            {
                iPollManager.StartPollingApp(aInstruction.AppName, aInstruction.Url, aInstruction.LastModified, aInstruction.AvailableCallback, aInstruction.ErrorCallback);
            }
        }

        public void ReceiveInternalProgressMessage(DownloadProgress aMessage)
        {
            string url = aMessage.Uri;
            if (!iDownloads.ContainsKey(url))
            {
                return;
            }

            if (aMessage.HasFailed)
            {
                iDownloads[url].FailedCallback();
                iDownloads.Remove(url);
                lock (iPublicDownloadsLock)
                {
                    iPublicDownloadInfo.Remove(url);
                }
                iPublicFailedDownloads.Enqueue(new FailedDownload { TimeOfFailure = DateTime.UtcNow, DownloadProgress = aMessage });
                InvokeDownloadChanged(EventArgs.Empty);
            }
            else if (aMessage.HasCompleted)
            {
                iDownloads[url].CompletedCallback(aMessage.LocalPath, iDownloads[url].LastModified);
                iDownloads.Remove(url);
                lock (iPublicDownloadsLock)
                {
                    iPublicDownloadInfo.Remove(url);
                }
                InvokeDownloadChanged(EventArgs.Empty);
            }
            else
            {
                lock (iPublicDownloadsLock)
                {
                    iPublicDownloadInfo[aMessage.Uri] = aMessage;
                }
            }
        }

        public void Run(IThreadCommunicator aThread)
        {
            iThread = aThread;
            using (iInternalProgressChannel = new Channel<DownloadProgress>(1))
            {
                while (!iThread.Abandoned)
                {
                    CleanupFailedDownloads();
                    PollIfRequired();
                    iThread.SelectWithTimeout(
                        GetMillisecondsUntilActionRequired(),
                        iInstructionChannel.CaseReceive(ReceiveDownloadInstruction),
                        iPollInstructionChannel.CaseReceive(ReceivePollInstruction),
                        iInternalProgressChannel.CaseReceive(ReceiveInternalProgressMessage)
                        );
                    
                }
                foreach (var download in iDownloads.Values)
                {
                    download.Cancel();
                }
                while (iDownloads.Count>0)
                {
                    var result = iInternalProgressChannel.Receive();
                    if (result.HasFailed || result.HasCompleted)
                    {
                        iDownloads.Remove(result.Uri);
                    }
                }
            }
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
        readonly Channel<DownloadInstruction> iInstructionChannel;
        readonly Channel<PollInstruction> iPollInstructionChannel;
        public int MaxSimultaneousDownloads { get; set; }
        public event EventHandler DownloadCountChanged
        {
            add { iDownloader.DownloadChanged += value; }
            remove { iDownloader.DownloadChanged -= value; }
        }

        public DownloadManager(IDownloadDirectory aDownloadDirectory, IUrlFetcher aUrlFetcher)
        {
            iDownloadDirectory = aDownloadDirectory;
            iInstructionChannel = new Channel<DownloadInstruction>(2);
            iPollInstructionChannel = new Channel<PollInstruction>(2);
            var urlPoller = new DefaultUrlPoller();
            var pollManager = new PollManager(urlPoller);
            iDownloader = new Downloader(iDownloadDirectory, iInstructionChannel, iPollInstructionChannel, pollManager, aUrlFetcher);
            iDownloadThread = new CommunicatorThread(iDownloader.Run, "DownloadManager");
            iDownloadThread.Start();
        }

        public void StartPollingForAppUpdate(string aAppName, string aUrl, Action aAvailableAction, Action aFailedAction, DateTime aLastModified)
        {
            iPollInstructionChannel.Send(new PollInstruction { AppName = aAppName, Url = aUrl, AvailableCallback = aAvailableAction, Cancel = false, ErrorCallback = aFailedAction, LastModified = aLastModified });
        }

        public void StopPollingForAppUpdate(string aAppName)
        {
            iPollInstructionChannel.Send(new PollInstruction { AppName = aAppName, Cancel = true });
        }

        public void StartDownload(string aUrl, Action<string, DateTime> aCallback)
        {
            if (!iInstructionChannel.NonBlockingSend(new DownloadInstruction
                                                     {
                                                         Cancel = false,
                                                         Url = aUrl,
                                                         CompleteCallback = aCallback,
                                                         FailedCallback = () => { }
                                                     }))
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
            if (!iInstructionChannel.NonBlockingSend(new DownloadInstruction{Cancel=false, Url=aAppUrl}))
            {
                throw new ActionError("Too busy.");
            }
        }

        public void Dispose()
        {
            iDownloadThread.Dispose();
        }
    }
}