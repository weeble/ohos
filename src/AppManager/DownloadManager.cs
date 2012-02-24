using System;
using System.Collections.Generic;
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