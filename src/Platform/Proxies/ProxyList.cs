using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using log4net;
using OpenHome.Widget.Nodes.Collections;
using OpenHome.Widget.Nodes.Threading;
using OpenHome.Net.ControlPoint;

namespace OpenHome.Widget.Nodes.Proxies
{
    public class ProxyList<T> : IDisposable where T : IDisposable
    {
        static readonly ILog Logger = LogManager.GetLogger(String.Format("OpenHome.Widget.Nodes.Proxies.ProxyList<{0}>", typeof(T).Name));
        private const int MaxCallbacks = 8;
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
        private ICpDeviceList iDeviceList;
        private readonly ICpUpnpDeviceListFactory iCpDeviceListFactory;
        private readonly Func<CpDevice, T> iProxyConstructor;
        private readonly Dictionary<string, ProxyRecord> iProxiesByUdn;
        private readonly string iDomain;
        private readonly string iType;
        private readonly uint iVersion;
        private readonly SafeCallbackTracker iCallbackTracker = new SafeCallbackTracker();

        private bool iStarted;

        public ProxyList(
            ICpUpnpDeviceListFactory aCpDeviceListFactory,
            Func<CpDevice, T> aProxyConstructor,
            string aDomain,
            string aType,
            uint aVersion)
        {
            iProxyConstructor = aProxyConstructor;
            iDomain = aDomain;
            iType = aType;
            iVersion = aVersion;
            iCpDeviceListFactory = aCpDeviceListFactory;
            iProxiesByUdn = new Dictionary<string, ProxyRecord>();
        }

        public void Start()
        {
            if (iStarted)
            {
                throw new InvalidOperationException();
            }
            Action<CpDeviceList, CpDevice> safeDeviceAdded = iCallbackTracker.Create<CpDeviceList,CpDevice>(DeviceAdded);
            Action<CpDeviceList, CpDevice> safeDeviceRemoved = iCallbackTracker.Create<CpDeviceList,CpDevice>(DeviceRemoved);
            iDeviceList = iCpDeviceListFactory.CreateListServiceType(
                iDomain,
                iType,
                iVersion,
                (aDeviceList, aDevice) => safeDeviceAdded(aDeviceList,aDevice),
                (aDeviceList, aDevice) => safeDeviceRemoved(aDeviceList,aDevice));
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
        private EventHandler<ProxyEventArgs> iDeviceDetectedHandler;

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
                    iDeviceDetectedHandler += value;
                    handler = iDeviceDetectedHandler;
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
                    iDeviceDetectedHandler -= value;
                }
            }
        }

        private void Stop()
        {
            if (iStarted)
            {
                Logger.Debug("Stopping ProxyList. Close callback-tracker...");
                iCallbackTracker.Close();
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
                iDeviceList.Dispose();
            }
        }

        private void DeviceAdded(CpDeviceList aDeviceList, CpDevice aDevice)
        {
            string udn = aDevice.Udn();
            Logger.DebugFormat("DeviceAdded: UDN={0}", udn);
            CountedReference<T> newProxyRef =
                    new CountedReference<T>(
                        iProxyConstructor(aDevice));
            aDevice.AddRef();
            ProxyRecord newProxyRecord = new ProxyRecord(aDevice, newProxyRef);
            // We need two references - one that goes in the iIdDictionary, and
            // one that we keep to use when invoking DeviceDetected. The
            // latter needs to be disposed even in the event of an exception.
            using (var callbackProxyRef = newProxyRef.Copy())
            {
                ProxyRecord oldProxyRecord = null;
                EventHandler<ProxyEventArgs> handler;
                lock (iProxiesByUdn)
                {
                    if (iProxiesByUdn.ContainsKey(udn))
                    {
                        // Discard the old one.
                        // TODO: Attempt to sensibly handle cases where add and remove
                        // messages overtake each other, or verify that the Zapp library
                        // prevents this.
                        // trac#84
                        oldProxyRecord = iProxiesByUdn[udn];
                    }
                    iProxiesByUdn[aDevice.Udn()] = newProxyRecord;
                    handler = iDeviceDetectedHandler;
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
                    handler(this, new ProxyEventArgs(aDevice, callbackProxyRef, newProxyRecord));
                }
            }
        }


        private void DeviceRemoved(CpDeviceList aDeviceList, CpDevice aDevice)
        {
            string udn = aDevice.Udn();
            Logger.DebugFormat("DeviceRemoved: UDN={0}", udn);
            ProxyRecord oldProxyRecord = null;
            lock (iProxiesByUdn)
            {
                if (iProxiesByUdn.ContainsKey(udn))
                {
                    // Discard the old one.
                    // TODO: Attempt to sensibly handle cases where add and remove
                    // messages overtake each other, or verify that the Zapp library
                    // prevents this.
                    // trac#84
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
                        Console.WriteLine("Failed to find: {0}", aDeviceUdn);
                        Console.WriteLine("Present after timeout: {0}", String.Join(", ", iProxiesByUdn.Keys.ToArray()));
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
