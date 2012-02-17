using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace OpenHome.Os.Platform.Threading
{
    public static class Channel
    {
        public static void Select(params ChannelAction[] aActions)
        {
            WaitHandle[] handles = aActions.Select(action => action.WaitHandle).ToArray();
            int index = WaitHandle.WaitAny(handles);
            aActions[index].WaitCompleted();
        }
        /// <summary>
        /// Block until one of the actions can be performed, then perform it.
        /// If no action becomes possible before the timeout, give up.
        /// </summary>
        /// <param name="aTimeoutMilliseconds"></param>
        /// <param name="aActions"></param>
        /// <returns>True if an action was performed, false if we timed out.</returns>
        public static bool SelectWithTimeout(
            int aTimeoutMilliseconds,
            params ChannelAction[] aActions)
        {
            WaitHandle[] handles = aActions.Select(action => action.WaitHandle).ToArray();
            int index = WaitHandle.WaitAny(handles, aTimeoutMilliseconds);
            if (index != WaitHandle.WaitTimeout)
            {
                aActions[index].WaitCompleted();
                return true;
            }
            return false;
        }
    }
    public abstract class ChannelAction
    {
        internal abstract WaitHandle WaitHandle { get; }
        internal abstract void WaitCompleted();
        public void Now()
        {
            WaitHandle.WaitOne();
            WaitCompleted();
        }
    }
    /// <summary>
    /// A "Go"-style channel. A thread-safe queue with the additional behaviour that you
    /// can select on a number of channels at once, acting on the first to become
    /// available.
    /// </summary>
    /// <typeparam name="T">
    /// The type of item transferred on the channel.
    /// </typeparam>
    public class Channel<T> : IDisposable
    {
        /// <summary>
        /// Semaphore indicating the number of items in the queue. Initially
        /// zero, increments as items are enqueued and decrements as items are
        /// dequeued.
        /// </summary>
        private readonly Semaphore iReadSemaphore;
        /// <summary>
        /// Semaphore indicating the number of free slots in the queue. Initially
        /// equal to the capacity of the queue, decrements as items are enqueued
        /// and increments when items are dequeued.
        /// </summary>
        private readonly Semaphore iWriteSemaphore;
        private readonly object iQueueLock = new object();
        private readonly Queue<T> iQueue;
        private bool iDisposed;

        public Channel(int aSize)
        {
            iReadSemaphore = new Semaphore(0, aSize);
            iWriteSemaphore = new Semaphore(aSize, aSize);
            iQueue = new Queue<T>(aSize);
        }

        public void Dispose()
        {
            if (iDisposed)
            {
                return;
            }
            iReadSemaphore.Close();
            iWriteSemaphore.Close();
            iDisposed = true;
        }

        private class ReceiveAction : ChannelAction
        {
            private readonly Channel<T> iChannel;
            private readonly Action<T> iAction;
            public ReceiveAction(Channel<T> aChannel, Action<T> aAction)
            {
                iChannel = aChannel;
                iAction = aAction;
            }

            internal override WaitHandle WaitHandle
            {
                get { return iChannel.iReadSemaphore; }
            }

            internal override void WaitCompleted()
            {
                T item = iChannel.InternalReceive();
                iAction(item);
            }
        }

        /// <summary>
        /// See Channel.Select. Use this to try to receive on this channel
        /// during a Channel.Select call.
        /// </summary>
        /// <param name="aReceiveAction">
        /// Action to perform when an item is received on the channel. The
        /// item is passed as the only argument to the action.
        /// </param>
        /// <returns>
        /// ChannelAction to pass to Channel.Select.
        /// </returns>
        public ChannelAction CaseReceive(Action<T> aReceiveAction)
        {
            return new ReceiveAction(this, aReceiveAction);
        }

        private T InternalReceive()
        {
            T item;
            lock (iQueueLock)
            {
                item = iQueue.Dequeue();
            }
            iWriteSemaphore.Release();
            return item;
        }

        /// <summary>
        /// Block until an item is received on the channel, returning
        /// the item.
        /// </summary>
        /// <returns></returns>
        public T Receive()
        {
            iReadSemaphore.WaitOne();
            return InternalReceive();
        }

        /// <summary>
        /// Non-blocking receive - returns false if nothing to receive.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool TryReceive(out T item)
        {
            if (!iReadSemaphore.WaitOne(0))
            {
                item = default(T);
                return false;
            }
            item = InternalReceive();
            return true;
        }

        private class SendAction : ChannelAction
        {
            private readonly Channel<T> iChannel;
            private readonly Action iAction;
            private readonly T iItem;
            public SendAction(Channel<T> aChannel, T aItem, Action aAction)
            {
                iChannel = aChannel;
                iAction = aAction;
                iItem = aItem;
            }

            internal override WaitHandle WaitHandle
            {
                get { return iChannel.iWriteSemaphore; }
            }

            internal override void WaitCompleted()
            {
                iChannel.InternalSend(iItem);
                if (iAction != null)
                {
                    iAction();
                }
            }
        }

        /// <summary>
        /// See Channel.Select. Use this to try to send on this channel
        /// during a Channel.Select call.
        /// </summary>
        /// <param name="aItem">
        /// Item to send on the channel.
        /// </param>
        /// <param name="aSendAction">
        /// Action to perform when the item is successfully placed on the
        /// channel.
        /// </param>
        /// <returns>
        /// ChannelAction to pass to Channel.Select.</returns>
        public ChannelAction CaseSend(T aItem, Action aSendAction)
        {
            return new SendAction(this, aItem, aSendAction);
        }

        /// <summary>
        /// See Channel.Select. Use this to try to send on this channel
        /// during a Channel.Select call.
        /// </summary>
        /// <param name="aItem">
        /// Item to send on the channel.
        /// </param>
        /// <returns></returns>
        public ChannelAction CaseSend(T aItem)
        {
            return new SendAction(this, aItem, null);
        }

        private void InternalSend(T aItem)
        {
            lock (iQueueLock)
            {
                iQueue.Enqueue(aItem);
            }
            iReadSemaphore.Release();
        }

        /// <summary>
        /// Block until an item can be sent on the channel, then send
        /// the item.
        /// </summary>
        /// <param name="aItem"></param>
        public void Send(T aItem)
        {
            iWriteSemaphore.WaitOne();
            InternalSend(aItem);
        }

        /// <summary>
        /// If there is space in the channel, enqueue the item and return
        /// true immediately. Otherwise, return false immediately.
        /// </summary>
        /// <param name="aItem"></param>
        /// <returns></returns>
        public bool NonBlockingSend(T aItem)
        {
            if (iWriteSemaphore.WaitOne(0))
            {
                InternalSend(aItem);
                return true;
            }
            return false;
        }
    }
}
