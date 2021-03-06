﻿using System;
using System.Threading;

namespace OpenHome.Os.Platform.Threading
{
    /// <summary>
    /// Interface for provided by CommunicatorThread to the code
    /// it runs to allow communication over channels.
    /// </summary>
    public interface IThreadCommunicator
    {
        bool Abandoned { get; }
        bool CheckAbandoned();
        bool SelectWithTimeout(
            int aTimeoutMilliseconds,
            params ChannelAction[] aActions);
        void Select(params ChannelAction[] aActions);
    }

    public class CommunicatorThread : QuittableThread, IThreadCommunicator
    {
        Action<IThreadCommunicator> iRunMethod;

        public CommunicatorThread(
            Action<IThreadCommunicator> aRunMethod,
            string aName)
            :base(aName)
        {
            iRunMethod = aRunMethod;
        }

        protected override void Run ()
        {
            iRunMethod(this);
        }

        bool IThreadCommunicator.Abandoned { get { return Abandoned; } }
        bool IThreadCommunicator.CheckAbandoned() { return CheckAbandoned(); }
        bool IThreadCommunicator.SelectWithTimeout(int aTimeoutMilliseconds, params ChannelAction[] aActions)
        {
            return SelectWithTimeout(aTimeoutMilliseconds, aActions);
        }
        void IThreadCommunicator.Select(params ChannelAction[] aActions)
        {
            Select(aActions);
        }
    }

    /// <summary>
    /// A thread that can perform blocking channel operations with Channel.Select
    /// which can also be cancelled "nicely".
    /// </summary>
    public abstract class QuittableThread : IDisposable
    {
        private readonly Channel<int> iQuitChannel;
        private readonly Thread iThread;
        private bool iQuitSent;
        private bool iDisposed;
        private bool iAbandoned;
        private bool iStarted;
        public string Name
        {
            get { return iThread.Name; }
            set { iThread.Name = value; }
        }

        protected bool Abandoned
        {
            get { return iAbandoned; }
        }

        protected bool CheckAbandoned()
        {
            if (Abandoned) { return true; }
            int ignored;
            if (iQuitChannel.TryReceive(out ignored))
            {
                iAbandoned = true;
                return true;
            }
            return false;
        }
        protected QuittableThread()
        {
            iQuitChannel = new Channel<int>(1);
            iThread = new Thread(ThreadMethod);
        }
        protected QuittableThread(string aName)
            :this()
        {
            iThread.Name = aName;
        }
        private void ThreadMethod()
        {
            Run();
        }
        protected abstract void Run();

        protected bool SelectWithTimeout(
            int aTimeoutMilliseconds,

            params ChannelAction[] aActions)
        {
            ChannelAction[] allActions = new ChannelAction[aActions.Length + 1];
            allActions[0] = iQuitChannel.CaseReceive(v => iAbandoned = true);
            aActions.CopyTo(allActions,1);
            return Channel.SelectWithTimeout(aTimeoutMilliseconds, allActions);
        }

        protected void Select(params ChannelAction[] aActions)
        {
            SelectWithTimeout(-1, aActions);
        }

        public void Start()
        {
            if (iStarted) { throw new InvalidOperationException("QuittableThread started twice."); }
            if (iDisposed) { throw new ObjectDisposedException("QuittableThread"); }
            if (iQuitSent) { throw new InvalidOperationException("Cannot re-start a stopped actor."); }
            iThread.Start();
            iStarted = true;
        }
        public void Stop()
        {
            if (iDisposed) { throw new ObjectDisposedException("QuittableThread"); }
            InternalStop();
        }
        private void InternalStop()
        {
            if (!iStarted) { return; }
            if (iQuitSent) { return; }
            iQuitSent = true;
            iQuitChannel.Send(0);
        }
        /// <summary>
        /// A subclass can override this to take action during Dispose().
        /// The overriding implementation must not call Dispose() itself.
        /// </summary>
        protected virtual void ActorThreadDispose()
        {
        }
        public void Dispose()
        {
            if (iDisposed) { return; }
            ActorThreadDispose();
            iDisposed = true;
            if (iStarted)
            {
                InternalStop();
                iThread.Join();
            }
            iQuitChannel.Dispose();
        }
    }
}