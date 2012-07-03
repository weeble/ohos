using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OpenHome.XappForms
{
    public interface ITimerCallback : IDisposable
    {
        /// <summary>
        /// Cancel any outstanding invocation and schedule a new invocation
        /// for the specified time. As the reschedule is asynchronous, it
        /// might not take effect immediately. Use the returned Task to
        /// know when it has taken effect. (If the invocation is scheduled
        /// now or in the past, it will take effect as soon as possible.)
        /// For example, do not Dispose resources the callback might use
        /// immediately after a Reschedule(DateTime.MaxValue); instead
        /// either first wait for the returned Task to complete, or use
        /// a separate mechanism to protect the callback against its
        /// resources being Disposed.
        /// </summary>
        /// <param name="aDateTime">
        /// Time to invoke callback. Set to DateTime.MaxValue to never
        /// invoke.
        /// </param>
        Task Reschedule(DateTime aDateTime);
    }

    class TimerCallback : ITimerCallback
    {
        readonly Action iCallback;
        readonly TimerThread iTimerThread;
        readonly Strand iStrand;

        BinaryHeapNode<DateTime> iNode;

        public TimerCallback(Action aCallback, TimerThread aTimerThread, Strand aStrand)
        {
            iCallback = aCallback;
            iTimerThread = aTimerThread;
            iStrand = aStrand;
        }

        /// <summary>
        /// Cancel any outstanding invocation and schedule a new invocation
        /// for the specified time. As the reschedule is asynchronous, it
        /// might not take effect immediately. Use the returned Task to
        /// know when it has taken effect. (If the invocation is scheduled
        /// now or in the past, it will take effect as soon as possible.)
        /// </summary>
        /// <param name="aDateTime"></param>
        public Task Reschedule(DateTime aDateTime)
        {
            var obj = new object();
            Console.WriteLine("Schedule {0}", obj.GetHashCode());
            return iStrand.ScheduleExclusive(
                ()=>
                {
                    Console.WriteLine("Perform {0}", obj.GetHashCode());
                    if (iNode == null)
                    {
                        iNode = iTimerThread.CreateNode(aDateTime, this);
                    }
                    else
                    {
                        iNode.Value = aDateTime;
                    }
                    iTimerThread.RefreshTimer();
                });
        }

        public void Dispose()
        {
            iStrand.ScheduleExclusive(
                ()=>
                {
                    iTimerThread.Cancel(iNode);
                    iNode = null;
                });
        }

        internal void Invoke()
        {
            iCallback();
        }
    }

    public interface ITimerThread
    {
        /// <summary>
        /// Register a callback. Use the returned TimerCallback to schedule it for
        /// invocation.
        /// </summary>
        /// <param name="aCallback"></param>
        /// <returns></returns>
        ITimerCallback RegisterCallback(Action aCallback);
    }

    // TODO: Perhaps TimerThread behaviour could usefully be folded into Strand?

    /// <summary>
    /// Schedules callbacks while making sure only one runs at once.
    /// </summary>
    class TimerThread : ITimerThread
    {
        readonly Strand iTimerThread;
        readonly Timer iTimer;
        readonly BinaryHeap<DateTime> iHeap;
        readonly Func<DateTime> iClock;
        readonly Dictionary<BinaryHeapNode<DateTime>, TimerCallback> iCallbacks = new Dictionary<BinaryHeapNode<DateTime>, TimerCallback>();
        DateTime iNextTime = DateTime.MaxValue;

        public TimerThread(Func<DateTime> aClock)
        {
            iTimerThread = new Strand();
            iTimer = new Timer(Callback);
            iHeap = new BinaryHeap<DateTime>(Comparer<DateTime>.Default);
            iClock = aClock;
        }

        /// <summary>
        /// Register a callback. Use the returned TimerCallback to schedule it for
        /// invocation.
        /// </summary>
        /// <param name="aCallback"></param>
        /// <returns></returns>
        public ITimerCallback RegisterCallback(Action aCallback)
        {
            TimerCallback timerCallback = new TimerCallback(aCallback, this, iTimerThread);
            return timerCallback;
        }

        void Callback(object aState)
        {
            iTimerThread.ScheduleExclusive(ProcessNextTimerEvent);
        }

        internal void Cancel(BinaryHeapNode<DateTime> aNode)
        {
            iCallbacks.Remove(aNode);
            aNode.Remove();
        }

        internal BinaryHeapNode<DateTime> CreateNode(DateTime aDateTime, TimerCallback aCallback)
        {
            BinaryHeapNode<DateTime> node = iHeap.Insert(aDateTime);
            iCallbacks[node] = aCallback;
            return node;
        }

        void ProcessNextTimerEvent()
        {
            iNextTime = DateTime.MaxValue;
            int processedEvents = 0;
            while (iHeap.Count > 0)
            {
                var top = iHeap.Peek();
                if (top.Value == DateTime.MaxValue) break;
                if (top.Value > iClock()) break;
                top.Value = DateTime.MaxValue;
                iCallbacks[top].Invoke();
                processedEvents += 1;
            }
            if (processedEvents == 0)
            {
                Console.WriteLine("Woke up too early!");
            }
            RefreshTimer();
        }

        internal void RefreshTimer()
        {
            if (iHeap.Count == 0)
            {
                //Console.WriteLine("RefreshTimer: Heap is empty.");
                iTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            var top = iHeap.Peek();
            if (iNextTime == top.Value)
            {
                //Console.WriteLine("RefreshTimer: Already scheduled correctly.");
                return;
            }
            iNextTime = top.Value;
            if (iNextTime == DateTime.MaxValue)
            {
                //Console.WriteLine("RefreshTimer: Nothing active.");
                iTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                var sleepTime = iNextTime - iClock() + TimeSpan.FromMilliseconds(1); // Tiny delay to reduce the risk of being woken too early.
                if (sleepTime <= TimeSpan.Zero)
                {
                    sleepTime = TimeSpan.Zero;
                }
                //Console.WriteLine("RefreshTimer: Was scheduled for {0}, now scheduling for {1}, wait for {2}.",
                //    oldScheduledTime, iNextTime, sleepTime);
                iTimer.Change(sleepTime, TimeSpan.FromMilliseconds(-1));
            }
        }
    }
}
