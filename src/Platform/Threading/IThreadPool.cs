using System.Threading;

namespace OpenHome.Os.Platform.Threading
{
    public interface IThreadPool
    {
        void QueueUserWorkItem(WaitCallback aCallback);
    }
}
