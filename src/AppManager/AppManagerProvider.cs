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
        void SetDownloadCount(uint aCount);
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
        readonly string iPresentationUri;

        public AppManagerProvider(DvDevice aDevice, IAppManagerActionHandler aProvider, string aPresentationUri) : base(aDevice)
        {
            EnablePropertyDownloadCount();
            EnablePropertyAppHandleArray();
            EnablePropertyAppSequenceNumberArray();
            EnableActionGetAppStatus();
            EnableActionGetMultipleAppsStatus();
            EnableActionGetAllDownloadsStatus();
            EnableActionInstallAppFromUrl();
            EnableActionRemoveApp();
            EnableActionCancelDownload();
            EnableActionUpdateApp();
            EnableActionGetPresentationUri();
            SetPropertyDownloadCount(0);
            SetPropertyAppHandleArray(Converter.ConvertUintListToNetworkOrderByteArray(new List<uint>()));
            SetPropertyAppSequenceNumberArray(Converter.ConvertUintListToNetworkOrderByteArray(new List<uint>()));
            //BumpDummySequenceNumber();
            iProvider = aProvider;
            iPresentationUri = aPresentationUri;
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

        public void SetDownloadCount(uint aCount)
        {
            SetPropertyDownloadCount(aCount);
        }

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
        protected override void GetPresentationUri(IDvInvocation aInvocation, out string aAppManagerPresentationUri)
        {
            aAppManagerPresentationUri = iPresentationUri;
        }
    }
}
