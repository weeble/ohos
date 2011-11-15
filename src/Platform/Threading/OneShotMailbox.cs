using System;

namespace OpenHome.Widget.Nodes.Threading
{
    /// <summary>
    /// Simple synchronisation device to communicate one item between threads
    /// with timeouts and ensuring that the item cannot be leaked.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OneShotMailbox<T>
    {
        private object iMonitor = new object();
        private bool iSealed = false;
        private bool iPosted = false;
        private T iItem;
        /// <summary>
        /// Wait for the mailbox to receive its item, or for a timeout to pass.
        /// </summary>
        public void Wait(int aTimeout)
        {
            lock (iMonitor)
            {
                if (iSealed || !iPosted)
                {
                    System.Threading.Monitor.Wait(iMonitor, aTimeout);
                }
            }
        }
        /// <summary>
        /// Seal the box. Guarantees that if a subsequent call to inspect finds
        /// the box empty, no post will ever succeed to the box. This allows us
        /// to have certainty that a sender will not put an item into the box
        /// after we have given up waiting for it.
        /// </summary>
        public void Seal()
        {
            lock (iMonitor)
            {
                iSealed = true;
            }
        }
        /// <summary>
        /// Convenience method, equivalent to a call to Wait then a call to Seal.
        /// </summary>
        /// <param name="aTimeout"></param>
        public void WaitAndSeal(int aTimeout)
        {
            Wait(aTimeout);
            Seal();
        }
        /// <summary>
        /// Put an item in the box.
        /// </summary>
        /// <returns>
        /// True if the item went in the box, false if it was already sealed.
        /// </returns>
        public bool Post(T aItem)
        {
            lock (iMonitor)
            {
                if (iSealed)
                {
                    return false;
                }
                if (iPosted)
                {
                    throw new Exception("Double-post to Mailbox.");
                }
                iItem = aItem;
                iPosted = true;
                System.Threading.Monitor.Pulse(iMonitor);
            }
            return true;
        }

        /// <summary>
        /// Find out what's in the box. While you can call this before
        /// sealing the box, you risk leaking the item if you don't
        /// subsequently seal and re-inspect the box.
        /// </summary>
        /// <param name="aItem"></param>
        /// <returns></returns>
        public bool Inspect(out T aItem)
        {
            lock (iMonitor)
            {
                if (iPosted)
                {
                    aItem = iItem;
                    return true;
                }
                else
                {
                    aItem = default(T);
                    return false;
                }
            }
        }

        public class TimeoutException : Exception
        {
        }

        /// <summary>
        /// Convenience method. Waits for a an item to be posted to the box
        /// and returns it, or throws TimeoutException if the timeout
        /// expires.
        /// </summary>
        /// <param name="aTimeout"></param>
        /// <returns></returns>
        public T Receive(int aTimeout)
        {
            WaitAndSeal(aTimeout);
            T item;
            if (Inspect(out item))
            {
                return item;
            }
            throw new TimeoutException();
        }

        /// <summary>
        /// Find out if there is an item in the box. Once this returns
        /// true, it will always return true. Once the box has been
        /// sealed, HasItem will never change value.
        /// </summary>
        public bool HasItem
        {
            get
            {
                lock (iMonitor)
                {
                    return iPosted;
                }
            }
        }
    }
}
