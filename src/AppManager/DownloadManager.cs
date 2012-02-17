using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using log4net;
using OpenHome.Net.Device;
using OpenHome.Widget.Nodes.Threading;

namespace OpenHome.Os.AppManager
{
    class DownloadInstruction
    {
        public string Url { get; set; }
        public bool Cancel { get; set; }
        public Action<string, DateTime> CompleteCallback { get; set; }
        public Action FailedCallback { get; set; }
    }

    class PollInstruction
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


    class DownloadThread : QuittableThread
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

            public void PollNow()
            {
                if (iCancelled) return;
                try
                {
                    Logger.DebugFormat("Polling URL for app update: {0}", iUrl);
                    var request = WebRequest.Create(iUrl);
                    request.Method = "HEAD";
                    using (var response = request.GetResponse())
                    {
                        var httpResponse = response as HttpWebResponse;
                        if (httpResponse == null)
                        {
                            iFailedAction();
                            return;
                        }
                        Logger.DebugFormat("Poll succeeded, available={0}, have={1}, same={2}", httpResponse.LastModified, iLastModified, httpResponse.LastModified == iLastModified);
                        if (httpResponse.LastModified != iLastModified)
                        {
                            iReadyAction();
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                    iFailedAction();
                }
            }
        }

        class PollManager
        {
            
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

            public PollManager()
            {
                MinPollingInterval = TimeSpan.FromSeconds(15);
                MaxAppPollingInterval = TimeSpan.FromMinutes(5); // TODO: Lengthen polling interval for normal use.
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
                        pollingUrl.PollNow();
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

        class Download
        {
            readonly string iUrl;
            readonly FileStream iOutStream;
            readonly byte[] iBuffer;
            readonly Channel<DownloadProgress> iProgressChannel;
            readonly string iFilename;
            long iOffset;
            HttpWebResponse iResponse;
            Stream iResponseStream;
            public DateTime LastModified { get; private set; }
            public Action<string, DateTime> CompletedCallback { get; private set; }
            public Action FailedCallback { get; private set; }

            public Download(string aUrl, int aBufferSize, FileStream aOutStream, Channel<DownloadProgress> aProgressChannel, Action<string, DateTime> aCompletedCallback, Action aFailedCallback)
            {
                CompletedCallback = aCompletedCallback;
                FailedCallback = aFailedCallback;
                iUrl = aUrl;
                iFilename = aOutStream.Name;
                iProgressChannel = aProgressChannel;
                iOutStream = aOutStream;
                iBuffer = new byte[aBufferSize];
            }

            public void Start()
            {
                try
                {
                    WebRequest request = WebRequest.Create(iUrl);
                    iResponse = request.GetResponse() as HttpWebResponse;
                    if (iResponse == null)
                    {
                        iProgressChannel.Send(DownloadProgress.CreateFailed(iUrl));
                        return;
                    }
                    LastModified = iResponse.LastModified;
                    iResponseStream = iResponse.GetResponseStream();
                    iOffset = 0;
                    BeginRead();
                }
                catch (Exception)
                {
                    iProgressChannel.Send(DownloadProgress.CreateFailed(iUrl));
                    if (iResponseStream != null)
                    {
                        iResponseStream.Dispose();
                    }
                }
            }

            public void Cancel()
            {
                // One of four situations exist:
                //    1. Start failed and sent a failure message already.
                //    2. The download has already completed and success was sent.
                //    3. Start completed, but a read failed, the stream was closed and a failure was sent.
                //    4. Start completed, reads are ongoing.
                // In 1-3, a double-dispose is safe and harmless.
                // In 4, the dispose will trigger OnReadComplete to finish, will cause an exception in
                // EndRead, and that will send a failure message.
                if (iResponseStream != null)
                {
                    iResponseStream.Dispose();
                }
            }

            void BeginRead()
            {
                iResponseStream.BeginRead(iBuffer, 0, iBuffer.Length, OnReadComplete, null);
            }

            void OnReadComplete(IAsyncResult aAr)
            {
                int count;
                try
                {
                    count = iResponseStream.EndRead(aAr);
                }
                catch (Exception)
                {
                    iResponseStream.Close();
                    iOutStream.Close();
                    iProgressChannel.Send(DownloadProgress.CreateFailed(iUrl));
                    return;
                }
                if (count == 0)
                {
                    iResponseStream.Dispose();
                    iOutStream.Close();
                    iProgressChannel.Send(DownloadProgress.CreateComplete(iUrl, (int)iOffset, iFilename));
                }
                else
                {
                    iOutStream.Write(iBuffer, 0, count);
                    iOffset += count;
                    BeginRead();
                    iProgressChannel.NonBlockingSend(DownloadProgress.CreateInProgress(iUrl, (int)iOffset, (int)iResponse.ContentLength));
                }
            }
        }

        class FailedDownload
        {
            public DateTime TimeOfFailure { get; set; }
            public DownloadProgress DownloadProgress { get; set; }
        }

        static readonly ILog Logger = LogManager.GetLogger(typeof(DownloadThread));

        readonly Channel<DownloadInstruction> iInstructionChannel;
        readonly Channel<PollInstruction> iPollInstructionChannel;
        readonly Dictionary<string, Download> iDownloads = new Dictionary<string, Download>();
        readonly IDownloadDirectory iDownloadDirectory;
        readonly Dictionary<string, DownloadProgress> iPublicDownloadInfo = new Dictionary<string, DownloadProgress>();
        readonly Queue<FailedDownload> iPublicFailedDownloads = new Queue<FailedDownload>();
        readonly object iPublicDownloadsLock = new object();
        public TimeSpan FailureTimeout { get; set; }
        public event EventHandler DownloadChanged;
        Channel<DownloadProgress> iInternalProgressChannel;
        readonly PollManager iPollManager;

        public DownloadThread(IDownloadDirectory aDownloadDirectory, Channel<DownloadInstruction> aInstructionChannel, Channel<PollInstruction> aPollInstructionChannel)
        {
            iInstructionChannel = aInstructionChannel;
            iPollInstructionChannel = aPollInstructionChannel;
            iDownloadDirectory = aDownloadDirectory;
            FailureTimeout = TimeSpan.FromSeconds(10);
            iPollManager = new PollManager();
        }

        public void InvokeDownloadChanged(EventArgs aE)
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

        DateTime? iLastPollTime;

        int GetMillisecondsUntilPollingRequired()
        {
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

        

        void ReceiveDownloadInstruction(DownloadInstruction aInstruction)
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
                    Download download = new Download(url, 8192, fileStream, iInternalProgressChannel, aInstruction.CompleteCallback, aInstruction.FailedCallback);
                    iDownloads[url] = download;
                    lock (iPublicDownloadInfo)
                    {
                        iPublicDownloadInfo[url] = DownloadProgress.CreateJustStarted(url);
                    }
                    InvokeDownloadChanged(EventArgs.Empty);
                    download.Start();
                }
            }
        }

        void ReceivePollInstruction(PollInstruction aInstruction)
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

        protected override void Run()
        {
            using (iInternalProgressChannel = new Channel<DownloadProgress>(1))
            {
                while (!Abandoned)
                {
                    CleanupFailedDownloads();
                    PollIfRequired();
                    SelectWithTimeout(
                        GetMillisecondsUntilActionRequired(),
                        iInstructionChannel.CaseReceive(ReceiveDownloadInstruction),
                        iPollInstructionChannel.CaseReceive(ReceivePollInstruction),
                        iInternalProgressChannel.CaseReceive(
                            aMessage =>
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
                            })
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
        readonly DownloadThread iDownloadThread;
        readonly IDownloadDirectory iDownloadDirectory;
        readonly Channel<DownloadInstruction> iInstructionChannel;
        readonly Channel<PollInstruction> iPollInstructionChannel;
        public int MaxSimultaneousDownloads { get; set; }
        public event EventHandler DownloadCountChanged
        {
            add { iDownloadThread.DownloadChanged += value; }
            remove { iDownloadThread.DownloadChanged -= value; }
        }

        public DownloadManager(IDownloadDirectory aDownloadDirectory)
        {
            iDownloadDirectory = aDownloadDirectory;
            iInstructionChannel = new Channel<DownloadInstruction>(2);
            iPollInstructionChannel = new Channel<PollInstruction>(2);
            iDownloadThread = new DownloadThread(iDownloadDirectory, iInstructionChannel, iPollInstructionChannel);
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
            return iDownloadThread.GetDownloadStatus();
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