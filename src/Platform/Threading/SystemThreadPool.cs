﻿namespace OpenHome.Widget.Nodes.Threading
{
    public class SystemThreadPool : IThreadPool
    {
        public void QueueUserWorkItem(System.Threading.WaitCallback aCallback)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(aCallback);
        }
    }
}
