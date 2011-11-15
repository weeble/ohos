using System.Threading;

namespace OpenHome.Widget.Nodes.Threading
{
    public interface IThreadPool
    {
        void QueueUserWorkItem(WaitCallback aCallback);
    }
}
