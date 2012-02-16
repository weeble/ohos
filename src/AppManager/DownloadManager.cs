using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using OpenHome.Net.Device;
using OpenHome.Widget.Nodes.Threading;

namespace OpenHome.Os.AppManager
{
    class DownloadInstruction
    {
        public string Url { get; set; }
        public bool Cancel { get; set; }
        public Action<string> CompleteCallback { get; set; }
        public Action FailedCallback { get; set; }
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
        class Download
        {
            readonly string iUrl;
            readonly FileStream iOutStream;
            readonly byte[] iBuffer;
            readonly Channel<DownloadProgress> iProgressChannel;
            readonly string iFilename;
            long iOffset;
            WebResponse iResponse;
            Stream iResponseStream;
            public Action<string> CompletedCallback { get; private set; }
            public Action FailedCallback { get; set; }

            public Download(string aUrl, int aBufferSize, FileStream aOutStream, Channel<DownloadProgress> aProgressChannel, Action<string> aCompletedCallback, Action aFailedCallback)
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
                    iResponse = request.GetResponse();
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

        readonly Channel<DownloadInstruction> iInstructionChannel;
        readonly Dictionary<string, Download> iDownloads = new Dictionary<string, Download>();
        readonly IDownloadDirectory iDownloadDirectory;
        readonly Dictionary<string, DownloadProgress> iPublicDownloadInfo = new Dictionary<string, DownloadProgress>();
        readonly Queue<FailedDownload> iPublicFailedDownloads = new Queue<FailedDownload>();
        readonly object iPublicDownloadsLock = new object();
        public TimeSpan FailureTimeout { get; set; }
        public event EventHandler DownloadChanged;

        public DownloadThread(IDownloadDirectory aDownloadDirectory, Channel<DownloadInstruction> aInstructionChannel)
        {
            iInstructionChannel = aInstructionChannel;
            iDownloadDirectory = aDownloadDirectory;
            FailureTimeout = TimeSpan.FromSeconds(10);
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

        protected override void Run()
        {
            using (Channel<DownloadProgress> internalProgressChannel = new Channel<DownloadProgress>(1))
            {
                while (!Abandoned)
                {
                    CleanupFailedDownloads();
                    SelectWithTimeout(
                        GetMillisecondsUntilCleanupRequired(),
                        iInstructionChannel.CaseReceive(
                            aInstruction =>
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
                                        Download download = new Download(url, 8192, fileStream, internalProgressChannel, aInstruction.CompleteCallback, aInstruction.FailedCallback);
                                        iDownloads[url] = download;
                                        lock (iPublicDownloadInfo)
                                        {
                                            iPublicDownloadInfo[url] = DownloadProgress.CreateJustStarted(url);
                                        }
                                        InvokeDownloadChanged(EventArgs.Empty);
                                        download.Start();
                                    }
                                }
                            }),
                        internalProgressChannel.CaseReceive(
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
                                    iDownloads[url].CompletedCallback(aMessage.LocalPath);
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
                    var result = internalProgressChannel.Receive();
                    if (result.HasFailed || result.HasCompleted)
                    {
                        iDownloads.Remove(result.Uri);
                    }
                }
            }
        }
    }

    public interface IDownloadManager
    {
        int MaxSimultaneousDownloads { get; set; }
        event EventHandler DownloadCountChanged;
        void StartDownload(string aUrl, Action<string> aCallback);
        IEnumerable<DownloadProgress> GetDownloadsStatus();
        void CancelDownload(string aAppUrl);
    }

    public class DownloadManager : IDownloadManager
    {
        readonly DownloadThread iDownloadThread;
        readonly IDownloadDirectory iDownloadDirectory;
        readonly Channel<DownloadInstruction> iInstructionChannel;
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
            iDownloadThread = new DownloadThread(iDownloadDirectory, iInstructionChannel);
            iDownloadThread.Start();
        }

        public void StartDownload(string aUrl, Action<string> aCallback)
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