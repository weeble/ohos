using System.Collections.Generic;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
{
    interface IAppManagerActionHandler
    {
        string GetAppStatus(uint aAppHandle);
        void CancelDownload(string aAppUrl);
        string GetAllDownloadsStatus();
        string GetMultipleAppsStatus(List<uint> aHandles);
        void UpdateApp(uint aAppHandle);
        void InstallAppFromUrl(string aAppUrl);
        void RemoveApp(uint aAppHandle);
    }
    interface IAppManagerProvider : IDvProviderOpenhomeOrgAppManager1
    {
        void SetAppHandles(List<uint> aAppHandles, List<uint> aSequenceNumbers);
    }

    class AppManagerProvider : Net.Device.Providers.DvProviderOpenhomeOrgAppManager1, IAppManagerProvider
    {
        //const int NoSuchAppError = 801;
        /*const string DummyAppListXml =
@"<appList>
    <app>
        <handle>1</handle>
        <id>dummyApp</id>
        <name>OpenHome Dummy App</name>
        <version>1.2.3</version>
        <url>http://www.openhome.org/dummyapp.zip</url>
        <description>This is a dummy app to stub out the AppManager API.</description>
        <status>running</status>
        <updateStatus>downloading</updateStatus>
    </app>
</appList>";
        const string DummyAppDownloadXml =
@"<downloadList>
    <download>
         <status>downloading</status>
         <url>http://www.openhome.org/dummyapp.zip</url>
         <appId>dummyApp</appId>
         <appHandle>1</appHandle>
         <progressPercent>77</progressPercent>
         <progressBytes>7700</progressBytes>
         <totalBytes>10000</totalBytes>
    </download>
</downloadList>";*/

        //private static readonly ILog iLog = LogManager.GetLogger(typeof(AppManagerProvider));

        readonly IAppManagerActionHandler iProvider;

        public AppManagerProvider(DvDevice aDevice, IAppManagerActionHandler aProvider) : base(aDevice)
        {
            EnablePropertyAppHandleArray();
            EnablePropertyAppSequenceNumberArray();
            EnableActionGetAppStatus();
            EnableActionGetMultipleAppsStatus();
            EnableActionGetAllDownloadsStatus();
            EnableActionInstallAppFromUrl();
            EnableActionRemoveApp();
            EnableActionCancelDownload();
            EnableActionUpdateApp();
            SetPropertyAppHandleArray(Converter.ConvertUintListToNetworkOrderByteArray(new List<uint>()));
            SetPropertyAppSequenceNumberArray(Converter.ConvertUintListToNetworkOrderByteArray(new List<uint>()));
            //BumpDummySequenceNumber();
            iProvider = aProvider;
        }

        public void SetAppHandles(List<uint> aAppHandles, List<uint> aSequenceNumbers)
        {
            byte[] handlesBytes = Converter.ConvertUintListToNetworkOrderByteArray(aAppHandles);
            byte[] seqNoBytes = Converter.ConvertUintListToNetworkOrderByteArray(aSequenceNumbers);
            PropertiesLock();
            SetPropertyAppHandleArray(handlesBytes);
            SetPropertyAppSequenceNumberArray(seqNoBytes);
            PropertiesUnlock();
        }

        /*public void BumpDummySequenceNumber()
        {
            lock (iDummySeqNoLock)
            {
                iLog.InfoFormat("Bumping the AppManager dummy sequence number up to {0}.", iDummySequenceNumber + 1);
                iDummySequenceNumber += 1;
                uint value = iDummySequenceNumber;
                SetPropertyAppSequenceNumberArray(Converter.ConvertUintListToNetworkOrderByteArray(new List<uint> { value }));
            }
        }*/

        protected override void GetAppStatus(IDvInvocation aInvocation, uint aAppHandle, out string aAppListXml)
        {
            aAppListXml = iProvider.GetAppStatus(aAppHandle);
        }
        protected override void CancelDownload(IDvInvocation aInvocation, string aAppUrl)
        {
            iProvider.CancelDownload(aAppUrl);
        }
        protected override void GetAllDownloadsStatus(IDvInvocation aInvocation, out string aDownloadStatusXml)
        {
            aDownloadStatusXml = iProvider.GetAllDownloadsStatus();
        }
        protected override void GetMultipleAppsStatus(IDvInvocation aInvocation, byte[] aAppHandles, out string aAppListXml)
        {
            List<uint> handles = Converter.BinaryToUintArray(aAppHandles);
            aAppListXml = iProvider.GetMultipleAppsStatus(handles);
        }
        protected override void UpdateApp(IDvInvocation aInvocation, uint aAppHandle)
        {
            iProvider.UpdateApp(aAppHandle);
        }
        protected override void InstallAppFromUrl(IDvInvocation aInvocation, string aAppUrl)
        {
            iProvider.InstallAppFromUrl(aAppUrl);
        }
        protected override void RemoveApp(IDvInvocation aInvocation, uint aAppHandle)
        {
            iProvider.RemoveApp(aAppHandle);
        }
    }

    /*public class AppController : IDisposable
    {
        private readonly AppManagerProvider iProvider;
        private readonly DvDevice iDevice;
        bool iDisposed;

        public AppController(string aUdn)
        {
            iDevice = new DvDeviceStandard(aUdn);
            // Set initial values for the attributes mandated by UPnP
            iDevice.SetAttribute("Upnp.Domain", "openhome.org");
            iDevice.SetAttribute("Upnp.Type", "AppManager");
            iDevice.SetAttribute("Upnp.Version", "1");
            iDevice.SetAttribute("Upnp.FriendlyName", "OpenHome App AppShell");
            iDevice.SetAttribute("Upnp.Manufacturer", "N/A");
            iDevice.SetAttribute("Upnp.ModelName", "OpenHome App AppShell");
            iProvider = new AppManagerProvider(iDevice);
            iDevice.SetEnabled();
        }
        public void BumpDummySequenceNumber()
        {
            iProvider.BumpDummySequenceNumber();
        }
        public void Dispose()
        {
            if (iDisposed) return;
            iDisposed = true;
            Semaphore disabledSemaphore = new Semaphore(0, 1);
            iDevice.SetDisabled(() => disabledSemaphore.Release());
            disabledSemaphore.WaitOne();
            ((IDisposable)disabledSemaphore).Dispose();
            iProvider.Dispose();
            iDevice.Dispose();
        }
    }*/
}
