using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.Diagnostics;
using System.Threading;
using log4net;

namespace OpenHome.Os.Update
{
    public class ProgressEventArgs : EventArgs
    {
        public readonly uint progress;

        public ProgressEventArgs(uint value)
        {
            progress = value;
        }
    }

    public class Updater
    {
        //private readonly ILogger iLogger;
        static readonly ILog Logger = LogManager.GetLogger(typeof(Updater));
        
        private const string kUnknownVersion    = "no-version";
        
        private const string kVersionFile       = "version";
        private const string kUpdateFile        = "update";
        private const string kPathLocal         = "/";
        private const string kPathUpdate        = "/opt/update";
        private const string kProgressFile      = "/tmp/update_progress";

        private const int kPollInterval = 1000;

        private readonly string iLocalVersion;
        private string iRemoteVersion;

        private string iServer = "10.201.0.13";
        private string iChannel = "openhome/nightly/main";
        private bool iUpdateAvailable;
        private readonly WebClient iWebClient;

        private delegate void ProgressUpdater();

        public event EventHandler<ProgressEventArgs> ProgressEventHandler;

        public Updater()
        {
            iWebClient = new WebClient();

            iUpdateAvailable = false;

            try
            {
                iLocalVersion = File.ReadAllText(Path.Combine(kPathLocal, kVersionFile)).TrimEnd('\n', '\r');
            }
            catch ( IOException )
            {
                iLocalVersion = kUnknownVersion;
            }
        }
        
        public string Server
        {
            get
            {
                lock(this)
                    return iServer;
                
            }
            set
            {
                lock(this)
                    iServer = value;
                Logger.Info("Update server set to: " + iServer);
            }
        }
        
        public string Channel
        {
            get
            {
                lock(this)
                    return iChannel;
            }
            set
            {
                lock(this)
                    iChannel = value;
                Logger.Info("Update channel set to: " + iChannel);
            }
        }
        
        public string LocalVersion
        {
            get
            {
                lock(this)
                    return iLocalVersion;
            }
        }
        
        public string RemoteVersion
        {
            get
            {
                lock(this)
                    return iRemoteVersion;
            }
        }
        
        public bool UpdateAvailable
        {
            get
            {
                lock(this)
                    return iUpdateAvailable;
            }
        }
        
        public void Refresh()
        {
            lock(this)
            {
                try
                {
                    iRemoteVersion = iWebClient.DownloadString(MakeUri(kVersionFile)).TrimEnd('\n', '\r');
                    iUpdateAvailable = (iLocalVersion != iRemoteVersion);
                    Logger.InfoFormat("Updater: remote version is '{0}', local version is '{1}'.", iRemoteVersion, iLocalVersion);
                }
                catch (WebException e)
                {
                    Logger.WarnFormat("Could not connect to update server {0}, message was {1}", iServer, e.Message);
                }
            }
        } 
        
        public bool FetchUpdate(CancellationToken ct)
        {
            int status = -1;
            string filePath = Path.Combine(kPathUpdate, kUpdateFile);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            /* Download the update script and make it executable */
            try
            {
                iWebClient.DownloadFile(MakeUri(kUpdateFile), filePath);
                status = WaitForProc(Process.Start("chmod", string.Format("+x {0}", filePath)), ct);
            }
            catch (Exception e)
            {
                Logger.Error("Exception trying to fetch update: " + e.Message);
            }

            if (status != 0) 
            {
                return false;
            }

            /* Download the flash images */
            try
            {
                status = WaitForProc(Process.Start(filePath, "-c download"), ct, ReportDownLoadProgress);
            }
            catch (Exception e)
            {
                Logger.Error("Exception trying to fetch update: " + e.Message);
            }
            return (status != 0) ? false : true;
        }

        public bool ApplyUpdate(BootMode target, CancellationToken ct)
        {
            int status = -1;
            string updateArgs = "-c reflash -r " + ((target == BootMode.eRfs0) ? "0" : "1");
            string filePath = Path.Combine(kPathUpdate, kUpdateFile);
            try
            {
                status = WaitForProc(Process.Start(filePath, updateArgs), ct, ReportReflashProgress);
            }
            catch (Exception e)
            {
                Logger.Error("Exception trying to run update: " + e.Message);
            }
            return (status != 0) ? false : true;
        }

        private static int WaitForProc(Process proc, CancellationToken ct, ProgressUpdater progressUpdater = null)
        {
            int id = proc.Id;
            while (!proc.HasExited)
            {
                if (ct.IsCancellationRequested)
                {
                    KillChildProcs(id);
                    if (!proc.HasExited)
                    {
                        proc.Kill();
                    }
                }
                proc.WaitForExit(kPollInterval);
                if (!ct.IsCancellationRequested && (progressUpdater != null))
                {
                    progressUpdater();
                }
            }
            return proc.ExitCode;
        }

        private static void KillChildProcs(int procId)
        {
            Process proc = Process.Start("pkill", "-P " + procId);
            proc.WaitForExit();
        }
        
        private Uri MakeUri(string aPath)
        {
            return new Uri("http://" + iServer + '/' + iChannel + '/' + aPath);
        }

        private void ReportProgressValue(string type)
        {
            if ((ProgressEventHandler != null) && File.Exists(kProgressFile))
            {                
                try
                {
                    uint progressVal;
                    var xmlTree = XElement.Load(kProgressFile);
                    var elem = xmlTree.Element(type);
                    if ((elem != null) && uint.TryParse(elem.Value, out progressVal))
                    {
                        ProgressEventHandler(this, new ProgressEventArgs(progressVal));
                    }
                }
                catch (XmlException e)
                {
                    Logger.Error("Failed to parse progress update file: " + e.Message);
                }                
            }
        }

        private void ReportDownLoadProgress()
        {
            ReportProgressValue("download");
        }

        private void ReportReflashProgress()
        {
            ReportProgressValue("update");
        }
    }
}
