using System;

namespace OpenHome.Os.Platform.Threading
{
    public class SafeCallbackTracker
    {
        private object iMonitor = new object();
        private bool iTerminating = false;
        private int iInflight = 0;

        /// <summary>
        /// Perform aAction, unless Close() has been called. Guarantee that
        /// aAction will not begin after Close() has returned, and will end
        /// before Close() returns.
        /// </summary>
        /// <param name="aAction"></param>
        public void PreventClose(Action aAction)
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
        }

        public EventHandler<T> Create<T>(EventHandler<T> aAction) where T : EventArgs
        {
            return (aSender, aArgs) => PreventClose(() => aAction(aSender, aArgs));
        }
        public Action Create(Action aAction)
        {
            return () => PreventClose(aAction);
        }
        public Action<T1,T2> Create<T1,T2>(Action<T1,T2> aAction)
        {
            return (aArg1, aArg2) => PreventClose(() => aAction(aArg1, aArg2));
        }
        public Action<T1> Create<T1>(Action<T1> aAction)
        {
            return aArg => PreventClose(() => aAction(aArg));
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