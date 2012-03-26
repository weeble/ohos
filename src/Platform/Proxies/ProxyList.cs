using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using OpenHome.Os.Platform.Collections;
using OpenHome.Os.Platform.Threading;
using OpenHome.Net.ControlPoint;
using OpenHome.Net.Device;

namespace OpenHome.Os.Platform.Proxies
{
    public interface IDevicePresenceListener
    {
        void DeviceAdded(CpDevice aDevice);
        void DeviceRemoved(CpDevice aDevice);
    }

    public interface IDevicePresencePublisher : IDisposable
    {
        void Start();
        void Stop();
    }

    public delegate IDisposable ListenForDevicesFunc(Action<CpDevice> aAdded, Action<CpDevice> aRemoved);


    public static class ProxyList
    {
        public static Func<IDevicePresenceListener, IDevicePresencePublisher> Filtered(
            this Func<IDevicePresenceListener, IDevicePresencePublisher> aOldFunc,
            Predicate<CpDevice> aFilter)
        {
            return aListener => aOldFunc(new FilteringDevicePresenceListener(aListener, aFilter));
        }
        public static Func<IDevicePresenceListener, IDevicePresencePublisher> UpnpDevicesByService(
            ICpUpnpDeviceListFactory aFactory,
            string aDomain,
            string aType,
            uint aVersion)
        {
            return aListener => new UpnpDevicePresencePublisher(
                aListener,
                (aAdded, aRemoved) => aFactory.CreateListServiceType(aDomain, aType, aVersion, aAdded, aRemoved));
        }
    }

    public class ProxyList<T> : IDisposable where T : IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(String.Format("OpenHome.Os.Platform.Proxies.ProxyList<{0}>", typeof(T).Name));
        private class ProxyRecord : IDeviceDisappearanceWatcher
        {
            readonly CountedReference<T> iRef;
            readonly object iDisappearanceLock = new object();
            bool iDisappeared;
            EventHandler iDisappearedHandler;

            public ProxyRecord(CpDevice aDevice, CountedReference<T> aRef)
            {
                Device = aDevice;
                iRef = aRef;
            }

            public CountedReference<T> Ref
            {
                get { return iRef; }
            }

            public CpDevice Device { get; private set; }

            public void InvokeDisappeared()
            {
                lock (iDisappearanceLock)
                {
                    if (iDisappeared)
                    {
                        return;
                    }
                    iDisappeared = true;
                    if (iDisappearedHandler != null)
                    {
                        iDisappearedHandler(this, EventArgs.Empty);
                        iDisappearedHandler = null;
                    }
                }
            }

            public event EventHandler DeviceDisappeared
            {
                add
                {
                    bool disappeared = false;
                    lock (iDisappearanceLock)
                    {
                        if (iDisappeared)
                        {
                            disappeared = true;
                        }
                        else
                        {
                            iDisappearedHandler += value;
                        }
                    }
                    if (disappeared && value != null)
                    {
                        value(this, EventArgs.Empty);
                    }
                }
                remove
                {
                    lock (iDisappearanceLock)
                    {
                        if (iDisappeared)
                        {
                            return;
                        }
                        iDisappearedHandler -= value;
                    }
                }
            }
        }
        private readonly Dictionary<string, ProxyRecord> iProxiesByUdn;
        readonly IDevicePresencePublisher iPublisher;
        readonly Listener iListener;

        private bool iStarted;

        public ProxyList(
            Func<CpDevice, T> aProxyConstructor,
            Func<IDevicePresenceListener, IDevicePresencePublisher> aPublisherFunc)
        {
            iProxiesByUdn = new Dictionary<string, ProxyRecord>();
            iListener = new Listener(aProxyConstructor, iProxiesByUdn);
            iPublisher = aPublisherFunc(iListener);
        }

        public ProxyList(
            ICpUpnpDeviceListFactory aCpDeviceListFactory,
            Func<CpDevice, T> aProxyConstructor,
            DvDevice aLocalDevice,
            string aDomain,
            string aType,
            uint aVersion,
            bool aMultiNodeEnable)
            :this(
                aProxyConstructor,
                CreatePublisherFunc(
                    aCpDeviceListFactory,
                    aLocalDevice,
                    aDomain,
                    aType,
                    aVersion,
                    aMultiNodeEnable))
        {
        }

        static Func<IDevicePresenceListener, IDevicePresencePublisher> CreatePublisherFunc(
            ICpUpnpDeviceListFactory aCpDeviceListFactory,
            DvDevice aLocalDevice,
            string aDomain,
            string aType,
            uint aVersion,
            bool aMultiNodeEnable)
        {
            return aListener => CreatePublisher(aListener, aCpDeviceListFactory, aLocalDevice, aDomain, aType, aVersion, aMultiNodeEnable);
        }

        static IDevicePresencePublisher CreatePublisher(
            IDevicePresenceListener aListener,
            ICpUpnpDeviceListFactory aCpDeviceListFactory,
            DvDevice aLocalDevice,
            string aDomain,
            string aType,
            uint aVersion,
            bool aMultiNodeEnable)
        {
            Func<CpDeviceList.ChangeHandler, CpDeviceList.ChangeHandler,IDisposable> deviceListFunc =
                    (aAdded, aRemoved) => aCpDeviceListFactory.CreateListServiceType(
                        aDomain,
                        aType,
                        aVersion,
                        aAdded,
                        aRemoved);
            if (aMultiNodeEnable)
            {
                if (aLocalDevice == null)
                {
                    return new UpnpDevicePresencePublisher(aListener, deviceListFunc);
                }
                var upnpPublisher = new UpnpDevicePresencePublisher(
                    new FilteringDevicePresenceListener(
                        aListener,
                        aDevice => aDevice.Udn() != aLocalDevice.Udn()),
                    deviceListFunc);
                var cpDvPublisher = new CpDvDevicePresencePublisher(aLocalDevice, aListener);
                return new CompoundDevicePresencePublisher(new List<IDevicePresencePublisher> { upnpPublisher, cpDvPublisher });
            }
            // Multinode *not* enabled, so don't use Upnp.
            if (aLocalDevice != null)
            {
                return new CpDvDevicePresencePublisher(aLocalDevice, aListener);
            }
            throw new ArgumentException("Non-multinode list must have a non-null local device");
        }

        public void Start()
        {
            if (iStarted)
            {
                throw new InvalidOperationException();
            }
            iPublisher.Start();
            iStarted = true;
        }

        public class ProxyEventArgs : EventArgs
        {
            /// <summary>
            /// The device the proxy is associated with. If need-be, you can create a new
            /// proxy, such as if you want to register callbacks and subscribe to events,
            /// or if you happen to know that the device supports other services.
            /// </summary>
            public CpDevice Device { get; private set; }
            /// <summary>
            /// A counted reference to an unsubscribed proxy for the newly detected proxy.
            /// Not valid after the event handler has returned! Call .Copy() and keep the
            /// returned value in the event handler if you need to retain it.
            /// </summary>
            public CountedReference<T> ProxyRef { get; private set; }
            public IDeviceDisappearanceWatcher DisappearanceWatcher { get; private set; }
            internal ProxyEventArgs(CpDevice aDevice, CountedReference<T> aProxyRef, IDeviceDisappearanceWatcher aDisappearanceWatcher)
            {
                Device = aDevice;
                ProxyRef = aProxyRef;
                DisappearanceWatcher = aDisappearanceWatcher;
            }
        }

        // Only safe to fetch while holding the iProxiesByUdn lock.

        /// <summary>
        /// An event handler that is invoked once for every detected device, both those
        /// that are already in the list, and all those detected later.
        /// </summary>
        public event EventHandler<ProxyEventArgs> DeviceDetected
        {
            add
            {
                List<CountedReference<T>> proxyRefs = new List<CountedReference<T>>();
                List<ProxyEventArgs> eventArgs = new List<ProxyEventArgs>();
                EventHandler<ProxyEventArgs> handler;
                lock (iProxiesByUdn)
                {
                    foreach (ProxyRecord proxyRecord in iProxiesByUdn.Values)
                    {
                        var proxyRef = proxyRecord.Ref.Copy();
                        proxyRefs.Add(proxyRef);
                        eventArgs.Add(new ProxyEventArgs(proxyRecord.Device, proxyRef, proxyRecord));
                    }
                    iListener.DeviceDetectedHandler += value;
                    handler = iListener.DeviceDetectedHandler;
                }
                using (new DisposableList<CountedReference<T>>(proxyRefs))
                {
                    if (handler != null)
                    {
                        foreach (var args in eventArgs)
                        {
                            handler(this, args);
                        }
                    }
                }
            }
            remove
            {
                lock (iProxiesByUdn)
                {
                    iListener.DeviceDetectedHandler -= value;
                }
            }
        }

        private void Stop()
        {
            if (iStarted)
            {
                Logger.Debug("Stopping ProxyList. Close callback-tracker...");
                iPublisher.Stop();
                //iCallbackTracker.Close();
                Logger.Debug("Clean up remaining proxies...");
                foreach (var kvp in iProxiesByUdn)
                {
                    Logger.DebugFormat("Cleaning up proxy with key={0}", kvp.Key);
                    kvp.Value.InvokeDisappeared();
                    CountedReference<T> proxyRef = kvp.Value.Ref;
                    proxyRef.Dispose();
                    kvp.Value.Device.RemoveRef();
                }
                iStarted = false;
                Logger.Debug("Finished cleanup");
            }
        }

        public void Dispose()
        {
            if (iStarted)
            {
                Stop();
                if (iPublisher != null)
                {
                    iPublisher.Dispose();
                }
            }
        }

        class Listener : IDevicePresenceListener
        {
            private readonly Func<CpDevice, T> iProxyConstructor;
            private readonly Dictionary<string, ProxyRecord> iProxiesByUdn;
            public EventHandler<ProxyEventArgs> DeviceDetectedHandler;

            public Listener(Func<CpDevice, T> aProxyConstructor, Dictionary<string, ProxyRecord> aProxiesByUdn)
            {
                iProxyConstructor = aProxyConstructor;
                iProxiesByUdn = aProxiesByUdn;
            }

            public void DeviceRemoved(CpDevice aDevice)
            {
                string udn = aDevice.Udn();
                Logger.DebugFormat("DeviceRemoved: UDN={0}", udn);
                ProxyRecord oldProxyRecord = null;
                lock (iProxiesByUdn)
                {
                    if (iProxiesByUdn.ContainsKey(udn))
                    {
                        oldProxyRecord = iProxiesByUdn[udn];
                        iProxiesByUdn.Remove(udn);
                    }
                }
                if (oldProxyRecord != null)
                {
                    oldProxyRecord.InvokeDisappeared();
                    oldProxyRecord.Ref.Dispose();
                    oldProxyRecord.Device.RemoveRef();
                }
            }

            public void DeviceAdded(CpDevice aDevice)
            {
                string udn = aDevice.Udn();
                Logger.DebugFormat("DeviceAdded: UDN={0}", udn);
                CountedReference<T> newProxyRef = new CountedReference<T>(iProxyConstructor(aDevice));
                aDevice.AddRef();
                ProxyRecord newProxyRecord = new ProxyRecord(aDevice, newProxyRef);
                ProxyRecord oldProxyRecord = null;
                EventHandler<ProxyEventArgs> handler;
                lock (iProxiesByUdn)
                {
                    if (iProxiesByUdn.ContainsKey(udn))
                    {
                        oldProxyRecord = iProxiesByUdn[udn];
                    }
                    iProxiesByUdn[aDevice.Udn()] = newProxyRecord;
                    handler = DeviceDetectedHandler;
                    Monitor.PulseAll(iProxiesByUdn);
                }
                if (oldProxyRecord != null)
                {
                    oldProxyRecord.InvokeDisappeared();
                    oldProxyRecord.Ref.Dispose();
                    oldProxyRecord.Device.RemoveRef();
                }
                if (handler != null)
                {
                    handler(this, new ProxyEventArgs(aDevice, newProxyRef, newProxyRecord));
                }
            }
        }

        public CountedReference<T> GetProxyRef(string aProxyUdn)
        {
            lock (iProxiesByUdn)
            {
                ProxyRecord proxyRecord;
                if (!iProxiesByUdn.TryGetValue(aProxyUdn, out proxyRecord))
                {
                    throw new ProxyError();
                }
                return proxyRecord.Ref.Copy();
            }
        }

        public string GetDeviceAttribute(string aProxyUdn, string aAttributeName)
        {
            lock (iProxiesByUdn)
            {
                ProxyRecord proxyRecord;
                if (!iProxiesByUdn.TryGetValue(aProxyUdn, out proxyRecord))
                {
                    throw new ProxyError();
                }
                string value;
                if (!proxyRecord.Device.GetAttribute(aAttributeName, out value))
                {
                    throw new ProxyError();
                }
                return value;
            }
        }

        /// <summary>
        /// Wait until the specified device appears in the proxy list. Note that
        /// the device might have gone away by the time this method returns. The
        /// main use for this method is for tests.
        /// </summary>
        /// <param name="aDeviceUdn"></param>
        /// <param name="aTimeout"></param>
        public bool WaitForDevice(string aDeviceUdn, DateTime aTimeout)
        {
            if (!iStarted)
            {
                throw new InvalidOperationException();
            }
            lock (iProxiesByUdn)
            {
                while (!iProxiesByUdn.ContainsKey(aDeviceUdn))
                {
                    TimeSpan wait = aTimeout - DateTime.Now;
                    if (wait.CompareTo(TimeSpan.Zero)<=0)
                    {
                        Logger.WarnFormat("Failed to find: {0}", aDeviceUdn);
                        Logger.WarnFormat("Present after timeout: {0}", String.Join(", ", iProxiesByUdn.Keys.ToArray()));
                        return false;
                    }
                    Monitor.Wait(iProxiesByUdn, wait);
                }
                return true;
            }
        }

        /// <summary>
        /// Get a container of counted references to all the proxies. Thread-safe.
        /// Some of the proxies might be stale by the time the method returns, but this
        /// is always a risk at *any* time. Callers should dispose the container when
        /// they are done - the container will dispose the individual references.
        /// </summary>
        /// <returns></returns>
        public IDisposableContainer<CountedReference<T>> GetAllProxyRefs()
        {
            DisposableList<CountedReference<T>> proxies;
            lock (iProxiesByUdn)
            {
                proxies = new DisposableList<CountedReference<T>>(iProxiesByUdn.Values.Select(aProxyRecord=>aProxyRecord.Ref.Copy()).ToList());
            }
            return proxies;
        }

        public IEnumerable<string> GetDeviceUdns()
        {
            lock (iProxiesByUdn)
            {
                return iProxiesByUdn.Keys.ToList();
            }
        }

        /// <summary>
        /// Apply an action to all proxies. Because devices can disappear at any
        /// time, discard all ProxyError exceptions.
        /// </summary>
        /// <param name="aAction"></param>
        public void ApplyToAllProxies(Action<T> aAction)
        {
            if (!iStarted)
            {
                // We don't bother to lock for this because if there's any possibility
                // of this happening then the calling code is definitely broken. Callers
                // must not Dispose the ProxyList until they are able to guarantee that
                // no other thread will try to use it. Callers *must not* call methods
                // on a potentially disposed instance and catch InvalidOperationException,
                // because we make no guarantee that we are safe to races between disposal
                // and other access.
                throw new InvalidOperationException("Object used after being disposed.");
            }
            using (var proxyRefList = GetAllProxyRefs())
            {
                foreach (var proxyRef in proxyRefList)
                {
                    try
                    {
                        aAction(proxyRef.Value);
                    }
                    catch (ProxyError)
                    {
                        // Devices can disappear at any time. There's no point in
                        // telling the caller that they just happened to call this
                        // right at the moment a node was disappearing.
                    }
                }
            }
        }
    }

    class FilteringDevicePresenceListener : IDevicePresenceListener
    {
        readonly IDevicePresenceListener iTargetListener;
        readonly Predicate<CpDevice> iFilter;

        public FilteringDevicePresenceListener(IDevicePresenceListener aTargetListener, Predicate<CpDevice> aFilter)
        {
            iTargetListener = aTargetListener;
            iFilter = aFilter;
        }

        public void DeviceAdded(CpDevice aDevice)
        {
            if (iFilter(aDevice)) iTargetListener.DeviceAdded(aDevice);
        }

        public void DeviceRemoved(CpDevice aDevice)
        {
            iTargetListener.DeviceRemoved(aDevice);
        }
    }

    class CompoundDevicePresencePublisher : IDevicePresencePublisher
    {
        readonly List<IDevicePresencePublisher> iSubPublishers;

        public CompoundDevicePresencePublisher(List<IDevicePresencePublisher> aSubPublishers)
        {
            iSubPublishers = aSubPublishers;
        }

        public void Dispose()
        {
            foreach (var p in iSubPublishers)
            {
                p.Dispose();
            }
        }

        public void Start()
        {
            foreach (var p in iSubPublishers)
            {
                p.Start();
            }
        }

        public void Stop()
        {
            foreach (var p in iSubPublishers)
            {
                p.Stop();
            }
        }
    }

    class CpDvDevicePresencePublisher : IDevicePresencePublisher
    {
        readonly DvDevice iDvDevice;
        CpDeviceDv iCpDeviceDv;
        readonly IDevicePresenceListener iListener;
        bool iStarted;

        public CpDvDevicePresencePublisher(DvDevice aDvDevice, IDevicePresenceListener aListener)
        {
            iDvDevice = aDvDevice;
            iListener = aListener;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            if (iStarted) return;
            iStarted = true;
            iCpDeviceDv = new CpDeviceDv(iDvDevice);
            iListener.DeviceAdded(iCpDeviceDv);
        }

        public void Stop()
        {
            if (!iStarted) return;
            {
                iStarted = false;
                iListener.DeviceRemoved(iCpDeviceDv);
                iCpDeviceDv.RemoveRef();
                iCpDeviceDv = null;
            }
        }
    }

    class UpnpDevicePresencePublisher : IDevicePresencePublisher
    {
        readonly IDevicePresenceListener iListener;
        IDisposable iDeviceList;

        readonly SafeCallbackTracker iCallbackTracker = new SafeCallbackTracker();
        readonly Func<CpDeviceList.ChangeHandler, CpDeviceList.ChangeHandler, IDisposable> iDeviceListFunc;
        bool iStarted;

        public UpnpDevicePresencePublisher(IDevicePresenceListener aListener, Func<CpDeviceList.ChangeHandler, CpDeviceList.ChangeHandler, IDisposable> aDeviceListFunc)
        {
            iListener = aListener;
            iDeviceListFunc = aDeviceListFunc;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            if (iStarted) return;
            iStarted = true;
            iDeviceList = iDeviceListFunc(DeviceAdded, DeviceRemoved);
        }

        void DeviceAdded(CpDeviceList aAlist, CpDevice aAdevice)
        {
            iCallbackTracker.PreventClose(() => iListener.DeviceAdded(aAdevice));
        }

        void DeviceRemoved(CpDeviceList aAlist, CpDevice aAdevice)
        {
            iCallbackTracker.PreventClose(() => iListener.DeviceRemoved(aAdevice));
        }


        public void Stop()
        {
            if (iDeviceList != null)
            {
                iCallbackTracker.Close();
                iDeviceList.Dispose();
                iDeviceList = null;
                iStarted = false;
            }
        }
    }

    public interface IDeviceDisappearanceWatcher
    {
        /// <summary>
        /// Registered event handlers will each be invoked once when the device disappears.
        /// If registered *after* the device has disappeared, the handler will be invoked
        /// immediately. Once invoked, handlers are removed, so you need not remove
        /// in response to invocation. You may remove a handler before invocation - after
        /// the "remove" returns the handler can be guaranteed not to be called and you
        /// can safely deallocate resources it depends on.
        /// </summary>
        event EventHandler DeviceDisappeared;

        CpDevice Device { get; }
    }
}