using System;
using System.Collections.Specialized;
using System.Xml;
using System.Xml.Linq;
using System.IO;
using System.Threading;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;
using log4net;

namespace OpenHome.Widget.Nodes.Global
{
    public class ProviderSystemUpdate : DvProviderOpenhomeOrgSystemUpdate1
    {
        public const int kStateIdle = 0;
        public const int kStateUpdateAvailable = 1;
        public const int kStateDownloading = 2;
        public const int kStateUpdateDownloaded = 3;
        public const int kStateUpdating = 4;
        public const int kStateRebootNeeded = 5;

        public const int kActionSetSourceInfo = 0;
        public const int kActionCheckForUpdate = 1;
        public const int kActionDownloadUpdate = 2;
        public const int kActionDoUpdate = 3;
        public const int kActionCancelUpdate = 4;
        public const int kActionReboot = 5;
        public const int kActionSetAutoUpdate = 6;

        public const int kUpdateRetryInterval = 4 * 60 * 60 * 1000;
        public const int kAutoUpdateDelay = 2 * 1000;

        private const string kStorageFileName = "SystemUpdate.xml";

        readonly string[] StateNames =
        {
            "Idle", "UpdateAvailable", "Downloading", "UpdateDownloaded", "Updating", "RebootNeeded"
        };

        readonly bool[,] IsActionAllowed =
        {
           /* Idle  UpdateAvailable  Downloading  UpdateDownloaded  Updating  RebootNeeded  */
           {  true,      true,          false,        false,         false,      false  },   /* SetSourceInfo */
           {  true,      true,          false,        false,         false,      false  },   /* CheckForUpdate */
           {  false,     true,          false,        false,         false,      false  },   /* DownloadUpdate */
           {  false,     false,         false,        true,          false,      false  },   /* DoUpdate */
           {  false,     false,         true,         true,          true,       false  },   /* CancelUpdate */
           {  false,     false,         false,        false,         false,      true   },   /* Reboot */
           {  true,      true,          false,        false,         false,      false  }    /* SetAutoUpdate */
        };

        private readonly IUpdateService iUpdateService;
        private readonly ILog iLogger;

        private StringCollection stateTable;
        private string userConfigFile;

        private Timer autoUpdateTimer;

        XElement userConfigXml;

        public ProviderSystemUpdate(DvDevice aDevice, IUpdateService aUpdateService, string aDfltConfigFile, string aUserConfigFile)
            : base(aDevice)
        {
            iLogger = LogManager.GetLogger(typeof(ProviderSystemUpdate));

            stateTable = new StringCollection();
            stateTable.AddRange(StateNames);            

            EnablePropertyState();           
            EnablePropertyProgress();
            EnablePropertyServer();
            EnablePropertyChannel();
            EnablePropertyLastError();
            EnablePropertyAutoUpdate();

            EnableActionCheckForUpdate();
            EnableActionDownloadUpdate();
            EnableActionDoUpdate();
            EnableActionCancelUpdate();
            EnableActionSetSourceInfo();
            EnableActionReboot();
            EnableActionSetAutoUpdate();

            SetPropertyProgress(0);
            SetLastError("Service created OK");

            iUpdateService = aUpdateService;

            /* Set default values */
            CurrentState = kStateIdle;
            SetPropertyServer(aUpdateService.Server);
            SetPropertyChannel(aUpdateService.Channel);
            SetPropertyAutoUpdate(true);

            /* Read the default config file first, if it exists */
            if (aDfltConfigFile != null)
            {
                string file = aDfltConfigFile; // Path.Combine(aDfltConfigDir, kStorageFileName);
                try
                {
                    LoadConfig(file);
                }
                catch (FileNotFoundException)
                {
                    iLogger.Info("Default config file '" + file + "' not found. Using hard-coded values ...");
                }
                catch (DirectoryNotFoundException)
                {
                    iLogger.Info("Default config file '" + file + "' not found. Using hard-coded values ...");
                }
            }

            /* Then try to read the user config file */
            if (aUserConfigFile != null)
            {
                userConfigFile = aUserConfigFile; // Path.Combine(aUserConfigDir, kStorageFileName);
                try
                {
                    LoadConfig(userConfigFile, out userConfigXml);
                }
                catch (FileNotFoundException)
                {
                    iLogger.Info("User config file '" + userConfigFile + "' not found. Using defaults ...");
                }
                catch (DirectoryNotFoundException)
                {
                    iLogger.Info("User config file '" + userConfigFile + "' not found. Using defaults ...");
                }
            }

            if (userConfigXml == null)
            {
                userConfigXml = new XElement("SystemUpdate");
            }

            /* If the state read from the config file was UpdateAvailable then we set it
             * to Idle. The service will notify us if an update is still available.
             * If the state was RebootNeeded then this is our first reboot after a software update
             */             
            if ( (CurrentState == kStateUpdateAvailable) || (CurrentState == kStateRebootNeeded))
            {
                CurrentState = kStateIdle;
            }

            UpdateUserConfig("State", PropertyState());

            autoUpdateTimer = new Timer(OnAutoUpdateTimer);

            iUpdateService.Server = PropertyServer();
            iUpdateService.Channel = PropertyChannel();
            
            iUpdateService.UpdateEventHandler += HandleUpdateEvent;

            /* If we are in Idle state then start the service */
            if (CurrentState == kStateIdle)
            {
                iUpdateService.Start();
            }

            /* If autoupdate is on and an update has been downloaded then
             * kick-off the timer to apply it */
            if (PropertyAutoUpdate() && (CurrentState == kStateUpdateDownloaded))
            {
                autoUpdateTimer.Change(kAutoUpdateDelay, Timeout.Infinite);
            }
        }

        protected override void CheckForUpdate(IDvInvocation aInvocation, out bool aStatus)
        {
            aStatus = StartCheckForUpdate();
        }

        protected override void DownloadUpdate(IDvInvocation aInvocation, out bool aStatus)
        {
            aStatus = StartUpdateDownload();
        }

        protected override void DoUpdate(IDvInvocation aInvocation, out bool aStatus)
        {
            aStatus = StartApplyUpdate();
        }

        protected override void CancelUpdate(IDvInvocation aInvocation, out bool aStatus)
        {
            lock (this)
            {
                if (IsActionAllowed[kActionCancelUpdate, CurrentState])
                {
                    autoUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    iUpdateService.CancelUpdate();
                    CurrentState = kStateIdle;
                    UpdateUserConfig("State", PropertyState());
                    iUpdateService.Start();
                    aStatus = true;
                }
                else
                {
                    SetLastError("Action not allowed in the current state");
                    aStatus = false;
                }
            }
        }

        protected override void Reboot(IDvInvocation aInvocation, out bool aStatus)
        {
            if (IsActionAllowed[kActionReboot, CurrentState])
            {
                iUpdateService.Reboot();
                aStatus = true;
            }
            else
            {
                SetLastError("Action not allowed in the current state");
                aStatus = false;
            }
        }

        protected override void SetSourceInfo(IDvInvocation aInvocation, string aServer, string aChannel, out bool aStatus)
        {
            lock (this)
            {
                aStatus = true;
                if ((aServer != PropertyServer()) || (aChannel != PropertyChannel()))
                {
                    if (IsActionAllowed[kActionSetSourceInfo, CurrentState])
                    {                    
                        iUpdateService.Server = aServer;
                        iUpdateService.Channel = aChannel;
                        PropertiesLock();
                        CurrentState = kStateIdle;
                        SetPropertyServer(aServer);
                        SetPropertyChannel(aChannel);
                        PropertiesUnlock();
                        UpdateUserConfig("State", PropertyState(), "Server", aServer, "Channel", aChannel);
                    }
                    else
                    {
                        SetLastError("Action not allowed in the current state");
                        aStatus = false;
                    }
                }
            }
        }

        protected override void SetAutoUpdate(IDvInvocation aInvocation, bool aEnable, out bool aStatus)
        {
            lock (this)
            {
                aStatus = true;
                if (PropertyAutoUpdate() != aEnable)
                {
                    if (IsActionAllowed[kActionSetAutoUpdate, CurrentState])
                    {
                        SetPropertyAutoUpdate(aEnable);
                        autoUpdateTimer.Change(
                                       (aEnable && (CurrentState == kStateUpdateAvailable)) ? kAutoUpdateDelay : Timeout.Infinite,
                                       Timeout.Infinite
                                       );
                        UpdateUserConfig("AutoUpdate", aEnable.ToString());
                    }
                    else
                    {
                        SetLastError("Action not allowed in the current state");
                        aStatus = false;
                    }
                }
            }
        }

        void HandleUpdateEvent(object source, UpdateEventArgs aE)
        {
            lock (this)
            {
                switch (aE.eventType)
                {
                    case UpdateEventType.UpdateAvailable:
                        int lastState = CurrentState;
                        CurrentState = iUpdateService.UpdateAvailable ? kStateUpdateAvailable : kStateIdle;                        
                        if (lastState != CurrentState) 
                        {
                            UpdateUserConfig("State", PropertyState());
                            if ((CurrentState == kStateUpdateAvailable) && PropertyAutoUpdate())
                            {
                                autoUpdateTimer.Change(kAutoUpdateDelay, Timeout.Infinite);
                            }
                        }
                        break;

                    case UpdateEventType.DownloadFail:
                        SetLastError("Download failed");
                        PropertiesLock();
                        SetPropertyProgress(0);
                        CurrentState = kStateUpdateAvailable;
                        PropertiesUnlock();
                        iUpdateService.Start();
                        if (PropertyAutoUpdate())
                        {
                            autoUpdateTimer.Change(kUpdateRetryInterval, Timeout.Infinite);
                        }
                        break;

                    case UpdateEventType.DownloadComplete:
                        CurrentState = kStateUpdateDownloaded;
                        UpdateUserConfig("State", PropertyState());
                        if (PropertyAutoUpdate())
                        {
                            autoUpdateTimer.Change(kAutoUpdateDelay, Timeout.Infinite);
                        }
                        break;

                    case UpdateEventType.UpdateFail:
                        SetLastError("Update failed");
                        PropertiesLock();
                        SetPropertyProgress(0);
                        CurrentState = kStateUpdateAvailable;
                        PropertiesUnlock();
                        UpdateUserConfig("State", PropertyState());
                        iUpdateService.Start();
                        if (PropertyAutoUpdate())
                        {
                            autoUpdateTimer.Change(kUpdateRetryInterval, Timeout.Infinite);
                        }
                        break;

                    case UpdateEventType.UpdateComplete:
                        CurrentState = kStateRebootNeeded;
                        UpdateUserConfig("State", PropertyState());
                        break;

                    case UpdateEventType.Progress:
                        SetPropertyProgress((uint) aE.eventData);
                        break;
                }
            }
        }

        public override void Dispose()
        {
            iLogger.Info("Cleaning up ...");
            iUpdateService.Stop();
            iUpdateService.UpdateEventHandler -= HandleUpdateEvent;
            iUpdateService.CancelUpdate();

            var sync = new AutoResetEvent(false);
            autoUpdateTimer.Dispose(sync);
            sync.WaitOne();

            base.Dispose();
            iLogger.Info("Finished cleanup ...");
        }

        private int CurrentState
        {
            get { return stateTable.IndexOf(PropertyState()); }
            set
            {
                SetPropertyState(stateTable[value]);
                iLogger.Info("Update state: " + PropertyState());
            }
        }

        private void LoadConfig(string configFile)
        {
            XElement xmltree;
            LoadConfig(configFile, out xmltree);
        }

        private void LoadConfig(string configFile, out XElement configXml)
        {
            iLogger.Info("Loading config from: " + configFile);

            configXml = null;
            try
            {                
                XElement elem;
                configXml = XElement.Load(configFile);

                PropertiesLock();
                elem = configXml.Element("Server");
                if (elem != null)
                {
                    SetPropertyServer(elem.Value);
                }
                elem = configXml.Element("Channel");
                if (elem != null)
                {
                    SetPropertyChannel(elem.Value);
                }
                bool autoUpdate;
                elem = configXml.Element("AutoUpdate");
                if ((elem != null) && bool.TryParse(elem.Value, out autoUpdate))
                {
                    SetPropertyAutoUpdate(autoUpdate);
                }
                elem = configXml.Element("State");
                if (elem != null)
                {
                    int state = stateTable.IndexOf(elem.Value);
                    if (state >= 0)
                    {
                        CurrentState = state;
                    }
                }
                PropertiesUnlock();
            }
            catch (XmlException e)
            {
                iLogger.Error("Failed to parse config file: " + e.Message);                
            }            
        }
        
        private void UpdateUserConfig(params string[] data)
        {
            for (int i = 0; i < data.Length; i+=2)
            {
                var elem = userConfigXml.Element(data[i]);
                if (elem != null)
                {
                    elem.SetValue(data[i + 1]);
                }
                else
                {
                    userConfigXml.Add(new XElement(data[i], data[i + 1]));
                }                    
            }
            if (userConfigFile != null)
            {
                string directory = Path.GetDirectoryName(userConfigFile);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                userConfigXml.Save(userConfigFile);
            }
        }

        private void SetLastError(string errorDescription)
        {
            XElement xmlErr = new XElement("UpdateError",
                                    new XElement("Time", DateTime.Now.ToString("s")),
                                    new XElement("Description", errorDescription)
                                    );
            SetPropertyLastError(xmlErr.ToString());
        }

        private bool StartCheckForUpdate()
        {
            bool aStatus;

            lock (this)
            {
                if (IsActionAllowed[kActionCheckForUpdate, CurrentState])
                {
                    aStatus = iUpdateService.CheckForUpdate();
                }
                else
                {
                    SetLastError("Action not allowed in the current state");
                    aStatus = false;
                }
            }
            return aStatus;
        }

        private bool StartUpdateDownload()
        {
            bool aStatus;
            lock (this)
            {
                if (IsActionAllowed[kActionDownloadUpdate, stateTable.IndexOf(PropertyState())])
                {
                    /* 
                     * We stop the update service as our state now will change to Downloading
                     * The service will be restarted if the update is cancelled or fails
                     */
                    iUpdateService.Stop();
                    aStatus = iUpdateService.DownloadUpdate();
                    if (aStatus)
                    {
                        PropertiesLock();
                        SetPropertyProgress(0);
                        CurrentState = kStateDownloading;
                        PropertiesUnlock();
                    }
                    else
                    {
                        iUpdateService.Start();
                    }
                }
                else
                {
                    SetLastError("Action not allowed in the current state");
                    aStatus = false;
                }
            }
            return aStatus;
        }

        private bool StartApplyUpdate()
        {
            bool aStatus;
            lock (this)
            {
                if (IsActionAllowed[kActionDoUpdate, stateTable.IndexOf(PropertyState())])
                {
                    aStatus = iUpdateService.DoUpdate();
                    if (aStatus)
                    {
                        PropertiesLock();
                        SetPropertyProgress(0);
                        CurrentState = kStateUpdating;
                        PropertiesUnlock();
                    }
                }
                else
                {
                    SetLastError("Action not allowed in the current state");
                    aStatus = false;
                }
            }
            return aStatus;
        }

        private void OnAutoUpdateTimer(object state)
        {
            lock (this)
            {
                if (!PropertyAutoUpdate())
                {
                    return;
                }

                switch (CurrentState)
                {
                    case kStateUpdateAvailable:
                        StartUpdateDownload();
                        break;

                    case kStateUpdateDownloaded:
                        StartApplyUpdate();
                        break;
                }
            }
        }
    } 
}
