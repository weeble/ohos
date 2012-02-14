using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml.Linq;
using log4net;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Apps;
using OpenHome.Os.Platform.Collections;
using OpenHome.Widget.Nodes.IO;
using OpenHome.Widget.Nodes.Threading;

namespace OpenHome.Os.AppManager
{
    class DownloadFinishedEventArgs : EventArgs
    {
        public bool Success { get; private set; }
    }

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

    class DownloadDirectory
    {
        string iPath;
        //XmlDiskStore iStore;

        public DownloadDirectory(string aPath)
        {
            iPath = aPath;
            if (!Directory.Exists(iPath))
                Directory.CreateDirectory(iPath);
            //iStore = aStore;
            //iStore.LoadXmlFiles(
        }

        public void Clear()
        {
            foreach (var fname in Directory.GetFiles(iPath))
            {
                File.Delete(fname);
            }
        }

        public FileStream CreateFile()
        {
            string filepath = Path.Combine(iPath, Guid.NewGuid() + ".download");
            return File.Create(filepath);
        }
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
        readonly DownloadDirectory iDownloadDirectory;
        readonly Dictionary<string, DownloadProgress> iPublicDownloadInfo = new Dictionary<string, DownloadProgress>();
        readonly Queue<FailedDownload> iPublicFailedDownloads = new Queue<FailedDownload>();
        readonly object iPublicDownloadsLock = new object();
        public TimeSpan FailureTimeout { get; set; }
        public event EventHandler<DownloadFinishedEventArgs> DownloadCompleted;

        public DownloadThread(DownloadDirectory aDownloadDirectory, Channel<DownloadInstruction> aInstructionChannel)
        {
            iInstructionChannel = aInstructionChannel;
            iDownloadDirectory = aDownloadDirectory;
            FailureTimeout = TimeSpan.FromMinutes(10);
        }

        public void InvokeDownloadCompleted(DownloadFinishedEventArgs aE)
        {
            EventHandler<DownloadFinishedEventArgs> handler = DownloadCompleted;
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
            lock (iPublicDownloadsLock)
            {
                while (
                    (iPublicFailedDownloads.Count>0) &&
                    (DateTime.UtcNow - iPublicFailedDownloads.Peek().TimeOfFailure > FailureTimeout))
                {
                    iPublicFailedDownloads.Dequeue();
                }
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
                                }
                                else if (aMessage.HasCompleted)
                                {
                                    iDownloads[url].CompletedCallback(aMessage.LocalPath);
                                    iDownloads.Remove(url);
                                    lock (iPublicDownloadsLock)
                                    {
                                        iPublicDownloadInfo.Remove(url);
                                    }
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

    class DownloadManager : IDisposable
    {
        readonly DownloadThread iDownloadThread;
        readonly DownloadDirectory iDownloadDirectory;
        readonly Channel<DownloadInstruction> iInstructionChannel;
        public int MaxSimultaneousDownloads { get; set; }
        //public event EventHandler DownloadComplete;
        public event EventHandler DownloadCountChanged;

        public void InvokeDownloadCountChanged(EventArgs aE)
        {
            EventHandler handler = DownloadCountChanged;
            if (handler != null) handler(this, aE);
        }

        public DownloadManager(DownloadDirectory aDownloadDirectory)
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
                CompleteCallback = aLocalFile =>
                {
                    InvokeDownloadCountChanged(EventArgs.Empty);
                    aCallback(aLocalFile);
                },
                FailedCallback = () => InvokeDownloadCountChanged(EventArgs.Empty)
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

    class AppManager : IAppManagerActionHandler, IDisposable
    {
        class ManagedApp
        {
            public AppInfo Info { get; set; }
            public uint SequenceNumber { get; set; }
            public uint Handle { get; set; }
        }
        //class ManagedDownload
        //{
        //    public string Url { get; set; }
        //}

        static readonly ILog Logger = LogManager.GetLogger(typeof(AppManager));

        readonly object iLock = new object();
        readonly IAppShell iAppShell;
        readonly List<IAppManagerProvider> iProviders;
        readonly SafeCallbackTracker iCallbackTracker = new SafeCallbackTracker();
        readonly IdDictionary<string, ManagedApp> iApps = new IdDictionary<string, ManagedApp>();
        readonly DownloadManager iDownloadManager;
        //Dictionary<string, ManagedDownload> iDownloads = new Dictionary<string, ManagedDownload>();

        public AppManager(
            string aResourceUri,
            IEnumerable<DvDevice> aDevices,
            Func<DvDevice, IAppManagerActionHandler, string, IAppManagerProvider> aProviderConstructor,
            IAppShell aAppShell,
            DownloadDirectory aDownloadDirectory)
        {
            iDownloadManager = new DownloadManager(aDownloadDirectory);
            iAppShell = aAppShell;
            iProviders = aDevices.Select(aDevice=>aProviderConstructor(aDevice, this, aResourceUri)).ToList();
            iAppShell.AppStatusChanged += OnAppStatusChanged;
            RefreshApps();
        }

        public void Dispose()
        {
            iAppShell.AppStatusChanged -= OnAppStatusChanged;
            iDownloadManager.Dispose();
            iCallbackTracker.Close();
            foreach (var provider in iProviders)
            {
                provider.Dispose();
            }
        }

        void RefreshApps()
        {
            HashSet<string> unseenApps = new HashSet<string>(iApps.ItemsByKey.Select(aKvp => aKvp.Key));
            foreach (var app in iAppShell.GetApps())
            {
                unseenApps.Remove(app.Name);
                ManagedApp managedApp;
                if (!iApps.TryGetValueByKey(app.Name, out managedApp))
                {
                    uint handle;
                    managedApp = new ManagedApp();
                    iApps.TryAdd(app.Name, managedApp, out handle);
                    managedApp.Handle = handle;
                }
                managedApp.Info = app;
            }
            foreach (string missingApp in unseenApps)
            {
                iApps.TryRemoveByKey(missingApp);
            }
            UpdateHandles();
        }

        void OnAppStatusChanged(object aSender, AppStatusChangeEventArgs aE)
        {
            iCallbackTracker.PreventClose(() =>
            {
                lock (iLock)
                {
                    RefreshApps();
                }
            });
        }

        void UpdateHandles()
        {
            List<uint> handles = new List<uint>(iApps.Count);
            List<uint> seqNos = new List<uint>(iApps.Count);
            foreach (var kvp in iApps.ItemsById)
            {
                handles.Add(kvp.Key);
                seqNos.Add(kvp.Value.SequenceNumber);
            }
            foreach (var provider in iProviders)
            {
                provider.SetAppHandles(handles, seqNos);
            }
        }

        public string GetAppStatus(uint aAppHandle)
        {
            return GetMultipleAppsStatus(new List<uint> { aAppHandle });
        }

        public void CancelDownload(string aAppUrl)
        {
            iDownloadManager.CancelDownload(aAppUrl);
        }

        XElement DownloadProgressToXElement(DownloadProgress aDownload)
        {
            XElement element = new XElement("download",
                new XElement("status", aDownload.HasFailed ? "failed" : "downloading"),
                new XElement("url", aDownload.Uri),
                new XElement("progressBytes", aDownload.DownloadedBytes));
            if (aDownload.HasTotalBytes)
            {
                element.Add(
                    new XElement("totalBytes", aDownload.TotalBytes),
                    new XElement("progressPercent", (int)Math.Round((float)aDownload.DownloadedBytes / aDownload.TotalBytes)));
            }
            return element;
        }

        public string GetAllDownloadsStatus()
        {
            IEnumerable<DownloadProgress> downloads = iDownloadManager.GetDownloadsStatus();
            lock (iLock)
            {
                XElement root = new XElement("downloadList",downloads.Select(DownloadProgressToXElement));
                return root.ToString();
            }
        }

        public string GetMultipleAppsStatus(List<uint> aHandles)
        {
            lock (iLock)
            {
                if (aHandles.Count == 0)
                {
                    aHandles = iApps.ItemsById.Select(aKvp => aKvp.Key).ToList();
                }
                XElement appListElement = new XElement("appList");
                foreach (uint handle in aHandles)
                {
                    ManagedApp app;
                    if (iApps.TryGetValueById(handle, out app))
                    {
                        appListElement.Add(
                            new XElement("app",
                                new XElement("handle", app.Handle),
                                new XElement("id", app.Info.Name),
                                new XElement("version", "DUMMY"),
                                new XElement("url", String.Format("/{0}/Upnp/resource/", app.Info.Udn)),
                                new XElement("description", "Hi there"),
                                new XElement("status", app.Info.State == AppState.Running ? "running" : "broken"),
                                new XElement("updateStatus", "noUpdate")));
                    }
                }
                return appListElement.ToString();
            }
        }

        public void UpdateApp(uint aAppHandle)
        {
            /*lock (iLock)
            {
                ManagedApp app;
                if (iApps.TryGetValueById(aAppHandle, out app))
                {
                    app.Info.
                    appListElement.Add(
                        new XElement("app",
                            new XElement("handle", app.Handle),
                            new XElement("id", app.Info.Name),
                            new XElement("version", "DUMMY"),
                            new XElement("url", String.Format("/{0}/Upnp/resource/", app.Info.Udn)),
                            new XElement("description", "Hi there"),
                            new XElement("status", app.Info.State == AppState.Running ? "running" : "broken"),
                            new XElement("updateStatus", "noUpdate")));
                }
            }*/
            throw new NotImplementedException();
        }

        public void InstallAppFromUrl(string aAppUrl)
        {
            Logger.InfoFormat("InstallAppFromUrl({0})", aAppUrl);
            iDownloadManager.StartDownload(
                aAppUrl,
                aLocalFile =>
                {
                    try
                    {
                        iAppShell.Install(aLocalFile);
                    }
                    catch (BadPluginException)
                    {
                        // TODO: Update download status to record failure.
                        Console.WriteLine("Bad plugin");
                    }
                });
        }

        public void RemoveApp(uint aAppHandle)
        {
            string appName;
            lock (iLock)
            {
                appName = iApps.GetKeyForId(aAppHandle);
            }
            iAppShell.UninstallByAppName(appName);
        }
    }
}
