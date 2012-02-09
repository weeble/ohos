using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using OpenHome.Net.Device;
using OpenHome.Os.AppManager;
using OpenHome.Widget.Nodes.Threading;

namespace OpenHome.Os.Apps
{
    public class NodeDevice : IDisposable
    {
        private readonly AppListProvider iProvider;
        private readonly DvDevice iDevice;
        bool iDisposed;
        readonly IAppShell iAppShell;
        readonly SafeCallbackTracker iCallbackTracker;
        readonly EventHandler<AppStatusChangeEventArgs> iHandler;

        public NodeDevice(string aUdn, IAppShell aAppShell)
        {
            iDevice = new DvDeviceStandard(aUdn);
            // Set initial values for the attributes mandated by UPnP
            iDevice.SetAttribute("Upnp.Domain", "openhome.org");
            iDevice.SetAttribute("Upnp.Type", "OpenHomeOS");
            iDevice.SetAttribute("Upnp.Version", "1");
            iDevice.SetAttribute("Upnp.FriendlyName", "OpenHomeOS Node");
            iDevice.SetAttribute("Upnp.Manufacturer", "N/A");
            iDevice.SetAttribute("Upnp.ModelName", "OpenHomeOS Node");
            iProvider = new AppListProvider(iDevice);
            iDevice.SetEnabled();
            iCallbackTracker = new SafeCallbackTracker();
            iHandler = iCallbackTracker.Create<AppStatusChangeEventArgs>(OnAppStatusChanged);
            iAppShell = aAppShell;
            iAppShell.AppStatusChanged += iHandler;
            OnAppStatusChanged(this, new AppStatusChangeEventArgs());
        }

        void OnAppStatusChanged(object aSender, AppStatusChangeEventArgs aE)
        {
            PublishAppList(iAppShell.GetApps());
        }

        public void PublishAppList(IEnumerable<AppInfo> aApps)
        {
            XElement root = new XElement("runningAppList",
                from app in aApps
                where app.State == AppState.Running
                select
                    new XElement("runningApp",
                        new XElement("udn", app.Udn),
                        new XElement("resourceUrl", String.Format("/{0}/Upnp/Resources/", app.Udn))));
            iProvider.SetPropertyRunningAppList(root.ToString());
        }
        public void Dispose()
        {
            if (iDisposed) return;
            iDisposed = true;
            iAppShell.AppStatusChanged -= OnAppStatusChanged;
            iCallbackTracker.Close();
            Semaphore disabledSemaphore = new Semaphore(0, 1);
            iDevice.SetDisabled(() => disabledSemaphore.Release());
            disabledSemaphore.WaitOne();
            ((IDisposable)disabledSemaphore).Dispose();
            iProvider.Dispose();
            iDevice.Dispose();
        }
    }
}
