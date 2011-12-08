using System;
using System.Collections.Generic;
using System.Threading;
using log4net;
using OpenHome.Net.Device;

namespace OpenHome.Os.AppManager
{
    class AppManagerProvider : Net.Device.Providers.DvProviderOpenhomeOrgAppManager1
    {
        const string DummyAppListXml =
@"<appList>
    <app>
        <handle>1</handle>
        <id>dummyApp</id>
        <name>OpenHome Dummy App</name>
        <version>1.2.3</version>
        <description>This is a dummy app to stub out the AppManager API.</description>
        <status><running /></status>
        <updateStatus><downloading progress=""2%"" /></updateStatus>
    </app>
</appList>";

        const string EmptyAppListXml = "<appList/>";
        const uint DummyAppId = 1;

        private static readonly ILog iLog = LogManager.GetLogger(typeof(AppManagerProvider));

        private uint iDummySequenceNumber;
        private object iDummySeqNoLock = new object();

        public AppManagerProvider(DvDevice aDevice) : base(aDevice)
        {
            EnablePropertyAppHandleArray();
            EnablePropertyAppSequenceNumberArray();
            // TODO: Initialize handle array.
            // TODO: Initialize seqno array.

            EnableActionGetAppStatus();
            EnableActionGetMultipleAppsStatus();
            EnableActionGetAppPermissions();
            EnableActionInstallAppFromUrl();
            EnableActionRemoveApp();
            EnableActionSetAppGrantedPermissions();
            SetPropertyAppHandleArray(Platform.Converter.ConvertUintListToNetworkOrderByteArray(new List<uint> { DummyAppId }));
            BumpDummySequenceNumber();
        }

        public void BumpDummySequenceNumber()
        {
            uint value;
            lock (iDummySeqNoLock)
            {
                iLog.InfoFormat("Bumping the AppManager dummy sequence number up to {0}.", iDummySequenceNumber + 1);
                iDummySequenceNumber += 1;
                value = iDummySequenceNumber;
            }
            SetPropertyAppSequenceNumberArray(Platform.Converter.ConvertUintListToNetworkOrderByteArray(new List<uint> { value }));
        }

        protected override void GetAppStatus(IDvInvocation aInvocation, uint aAppHandle, out string aAppListXml)
        {
            iLog.ErrorFormat("GetAppStatus(\"{0}\") - not implemented.", aAppHandle);
            aAppListXml = (aAppHandle == DummyAppId) ? DummyAppListXml : EmptyAppListXml;
        }
        protected override void GetMultipleAppsStatus(IDvInvocation aInvocation, byte[] aAppHandles, out string aAppListXml)
        {
            List<uint> handles = Platform.Converter.BinaryToUintArray(aAppHandles);
            iLog.ErrorFormat("GetMultipleAppsStatus(\"{0}\") - not implemented.", String.Join(", ", handles.ToArray()));
            aAppListXml = (handles.Contains(DummyAppId)) ? DummyAppListXml : EmptyAppListXml;
        }
        protected override void GetAppPermissions(IDvInvocation aInvocation, uint aAppHandle, out string aAppPermissionsXml)
        {
            iLog.ErrorFormat("GetAppPermissions(\"{0}\") - not implemented.", aAppHandle);
            aAppPermissionsXml = String.Format(
                "<appPermissions id=\"{0}\"><required></required><granted></granted></appPermissions>",
                aAppHandle);
        }
        protected override void InstallAppFromUrl(IDvInvocation aInvocation, string aAppUrl)
        {
            iLog.ErrorFormat("InstallAppFromUrl(\"{0}\") - not implemented.", aAppUrl);
        }
        protected override void RemoveApp(IDvInvocation aInvocation, uint aAppHandle)
        {
            iLog.ErrorFormat("RemoveApp(\"{0}\") - not implemented.", aAppHandle);
        }
        protected override void SetAppGrantedPermissions(IDvInvocation aInvocation, uint aAppHandle, string aAppPermissionsXml)
        {
            iLog.ErrorFormat("SetAppGrantedPermissions(\"{0}\", \"{1}\") - not implemented.", aAppHandle, aAppPermissionsXml);
        }
    }

    public class AppController : IDisposable
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
            iDevice.SetAttribute("Upnp.FriendlyName", "OpenHome App Manager");
            iDevice.SetAttribute("Upnp.Manufacturer", "N/A");
            iDevice.SetAttribute("Upnp.ModelName", "OpenHome App Manager");
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
    }
}
