using System;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Update
{
    public class UpdateService : IUpdateService
    {
        public const int PollTimeMilliseconds = 1000 * 60 * 60 * 8;
        private readonly Updater iUpdater;
        private readonly IBootControl iBootControl;
        private readonly INodeRebooter iRebooter;
        static readonly ILog Logger = LogManager.GetLogger(typeof(UpdateService));
        readonly Timer iTimer;
        int iRefreshing;
        private CancellationTokenSource iCtSrc;
        private Task iTask;
        private AutoResetEvent iStopSync;
        int isRunning;

        public event EventHandler<UpdateEventArgs> UpdateEventHandler;

        public UpdateService(Updater aUpdater, IBootControl aBootControl, INodeRebooter aRebooter)
        {
            iUpdater = aUpdater;
            iUpdater.ProgressEventHandler += HandleProgressEvent;
            iRebooter = aRebooter;
            iBootControl = aBootControl;
            iStopSync = new AutoResetEvent(true);
            iTimer = new Timer(aObj => InternalRefresh(), null, Timeout.Infinite, PollTimeMilliseconds);
        }

        public void Start()
        {
            if (Interlocked.CompareExchange(ref isRunning, 1, 0) == 0)
            {
                iStopSync.Set();
                iTimer.Change(PollTimeMilliseconds, PollTimeMilliseconds);
                Logger.Info("Update: service started.");
            }
        }

        public void Stop()
        {
            if (Interlocked.CompareExchange(ref isRunning, 0, 1) == 1)
            {
                iTimer.Change(Timeout.Infinite, PollTimeMilliseconds);
                iStopSync.WaitOne();
                Logger.Info("Update: service stopped.");
            }
        }

        public bool UpdateAvailable
        {
            get { return iUpdater.UpdateAvailable; }
        }

        public bool DownloadUpdate()
        {
            if ((iTask != null) && !iTask.IsCompleted)
            {
                Logger.Warn("Cannot start update download, while another task is running");
                return false;
            }
            AllocCancellationToken();
            iTask = Task.Factory.StartNew(DownloadTaskAction, iCtSrc.Token);
            return true;
        }

        public bool DoUpdate()
        {
            if ((iTask != null) && !iTask.IsCompleted)
            {
                Logger.Warn("Cannot apply update, while another task is running");
                return false;
            }
            AllocCancellationToken();
            iTask = Task.Factory.StartNew(UpdateTaskAction, iCtSrc.Token);
            return true;
        }

        public void CancelUpdate()
        {
            if ((iTask != null) && !iTask.IsCompleted)
            {
                Logger.Info("Cancelling update ...");
                iCtSrc.Cancel();
                try
                {
                    iTask.Wait();
                }
                catch (AggregateException) { }
                iTask.Dispose();
                iTask = null;
                iCtSrc.Dispose();
                iCtSrc = null;
            }
        }

        public void Reboot()
        {
            Logger.Info("Unit rebooting ...");
            iRebooter.RebootNode();
        }

        public string Server
        {
            get { return iUpdater.Server; }
            set { iUpdater.Server = value; }
        }
        
        public string Channel
        {
            get { return iUpdater.Channel; }
            set { iUpdater.Channel = value; }
        }
        
        public bool CheckForUpdate()
        {
            if ((iTask != null) && !iTask.IsCompleted)
            {
                Logger.Warn("Cannot check for updates, while another task is running");
                return false;
            }
            AllocCancellationToken();
            iTask = Task.Factory.StartNew(InternalRefresh, iCtSrc.Token);
            return true;
        }

        private void InternalRefresh()
        {
            iStopSync.Reset();
            if (Interlocked.CompareExchange(ref iRefreshing, 1, 0) == 0)
            {
                iUpdater.Refresh();
                OnUpdateEvent(new UpdateEventArgs(UpdateEventType.UpdateAvailable));
                iRefreshing = 0;
            }
            iStopSync.Set();
        }

        private void UpdateTaskAction()
        {
            Logger.Info("Applying update ...");
            try
            {
                BootMode targetRfs = (iBootControl.Current == BootMode.eRfs0) ? BootMode.eRfs1 : BootMode.eRfs0;
                Logger.Debug("Starting update on " + targetRfs.ToString() + "...");
                if (iUpdater.ApplyUpdate(targetRfs, iCtSrc.Token))
                {
                    iBootControl.Pending = targetRfs;
                    Logger.Info("Update completed successfully.");
                    OnUpdateEvent(new UpdateEventArgs(UpdateEventType.UpdateComplete));
                }
                else if (iCtSrc.Token.IsCancellationRequested)
                {
                    Logger.Info("Update cancelled.");
                }
                else
                {
                    Logger.Error("Update failed.");
                    OnUpdateEvent(new UpdateEventArgs(UpdateEventType.UpdateFail));
                }
            }
            catch (Exception e)
            {
                Logger.Error("Exception trying to run update: " + e.Message);
                OnUpdateEvent(new UpdateEventArgs(UpdateEventType.UpdateFail));
            }
        }

        private void DownloadTaskAction()
        {
            Logger.Info("Downloading update ...");
            if (iUpdater.FetchUpdate(iCtSrc.Token))
            {
                Logger.Info("Update download completed successfully.");
                OnUpdateEvent(new UpdateEventArgs(UpdateEventType.DownloadComplete));
            }
            else if (iCtSrc.Token.IsCancellationRequested)
            {
                Logger.Info("Update download cancelled.");
            }
            else
            {
                Logger.Warn("Update download failed.");
                OnUpdateEvent(new UpdateEventArgs(UpdateEventType.DownloadFail));
            }
        }

        private void AllocCancellationToken()
        {
            if (iCtSrc != null)
            {
                iCtSrc.Dispose();
            }
            iCtSrc = new CancellationTokenSource();
        }        

        protected void OnUpdateEvent(UpdateEventArgs aE)
        {
            if (UpdateEventHandler != null) UpdateEventHandler(this, aE);
        }

        private void HandleProgressEvent(object source, ProgressEventArgs aE)
        {
            OnUpdateEvent(new UpdateEventArgs(UpdateEventType.Progress, aE.progress));
        }

        public void Dispose()
        {
            Logger.Debug("Update: cleaning up ...");
            iUpdater.ProgressEventHandler -= HandleProgressEvent;
            var disposeSync = new AutoResetEvent(false);
            CancelUpdate();
            iTimer.Dispose(disposeSync);
            disposeSync.WaitOne();
            if (iCtSrc != null)
            {
                iCtSrc.Dispose();
            }
            Logger.Debug("Update: finished cleanup");
        }
    }
}
