using System;
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
        <id>dummyApp</id>
        <name>OpenHome Dummy App</name>
        <version>1.2.3</version>
        <description>This is a dummy app to stub out the AppManager API.</description>
        <status><running /></status>
        <updateStatus><downloading progress=""2%"" /></updateStatus>
    </app>
</appList>";

        private static readonly ILog iLog = LogManager.GetLogger(typeof(AppManagerProvider));
        public AppManagerProvider(DvDevice aDevice) : base(aDevice)
        {
            EnablePropertyAppListXml();
            EnableActionGetAppPermissions();
            EnableActionInstallAppFromUrl();
            EnableActionRemoveApp();
            EnableActionSetAppGrantedPermissions();
            SetPropertyAppListXml(DummyAppListXml);
        }
        protected override void GetAppPermissions(IDvInvocation aInvocation, string aAppId, out string aAppPermissionsXml)
        {
            iLog.ErrorFormat("GetAppPermissions(\"{0}\") - not implemented.", aAppId);
            aAppPermissionsXml = String.Format(
                "<appPermissions id=\"{0}\"><required></required><granted></granted></appPermissions>",
                aAppId);
        }
        protected override void InstallAppFromUrl(IDvInvocation aInvocation, string aAppUrl)
        {
            iLog.ErrorFormat("InstallAppFromUrl(\"{0}\") - not implemented.", aAppUrl);
        }
        protected override void RemoveApp(IDvInvocation aInvocation, string aAppId)
        {
            iLog.ErrorFormat("RemoveApp(\"{0}\") - not implemented.", aAppId);
        }
        protected override void SetAppGrantedPermissions(IDvInvocation aInvocation, string aAppId, string aAppPermissionsXml)
        {
            iLog.ErrorFormat("SetAppGrantedPermissions(\"{0}\", \"{1}\") - not implemented.", aAppId, aAppPermissionsXml);
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
