using System;

namespace OpenHome.Widget.Nodes.Threading
{
    public class SafeCallbackTracker
    {
        private object iMonitor = new object();
        private bool iTerminating = false;
        private int iInflight = 0;
        public Action Create(Action aAction)
        {
            return () =>
                       {
                           lock (iMonitor)
                           {
                               if (iTerminating) { return; }
                               iInflight += 1;
                           }
                           try
                           {
                               aAction();
                           }
                           finally
                           {
                               lock (iMonitor)
                               {
                                   iInflight -= 1;
                                   System.Threading.Monitor.Pulse(iMonitor);
                               }
                           }
                       };
        }
        public Action<T1,T2> Create<T1,T2>(Action<T1,T2> aAction)
        {
            return (aArg1, aArg2) =>
                       {
                           lock (iMonitor)
                           {
                               if (iTerminating) { return; }
                               iInflight += 1;
                           }
                           try
                           {
                               aAction(aArg1, aArg2);
                           }
                           finally
                           {
                               lock (iMonitor)
                               {
                                   iInflight -= 1;
                                   System.Threading.Monitor.Pulse(iMonitor);
                               }
                           }
                       };
        }
        public Action<T1> Create<T1>(Action<T1> aAction)
        {
            return (aArg) =>
                       {
                           lock (iMonitor)
                           {
                               if (iTerminating) { return; }
                               iInflight += 1;
                           }
                           try
                           {
                               aAction(aArg);
                           }
                           finally
                           {
                               lock (iMonitor)
                               {
                                   iInflight -= 1;
                                   System.Threading.Monitor.Pulse(iMonitor);
                               }
                           }
                       };
        }
        /// <summary>
        /// Prevent all further callbacks. Block until no callbacks
        /// are running and no further callbacks can occur.
        /// Not allowed during a callback created by this tracker -
        /// deadlock will (obviously!) result.
        /// </summary>
        public void Close()
        {
            lock (iMonitor)
            {
                iTerminating = true;
                while (iInflight > 0)
                {
                    System.Threading.Monitor.Wait(iMonitor);
                }
            }
        }
    }
}