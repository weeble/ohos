using System;
using System.Threading.Tasks;

namespace OpenHome.XappForms
{
    public interface IStrand
    {
        /// <summary>
        /// Schedule an action to occur in series with all other
        /// scheduled actions.
        /// </summary>
        /// <param name="a"></param>
        Task ScheduleExclusive(Action a);

        /// <summary>
        /// Schedule an action to occur in series with all other
        /// scheduled actions.
        /// </summary>
        /// <param name="a"></param>
        Task<T> ScheduleExclusive<T>(Func<T> a);
    }

    /// <summary>
    /// Scheduling mechanism that invokes actions asynchronously in
    /// serial. I.e. actions can be enqueued from any thread, but only
    /// one will ever be invoked at a time. Doesn't create a
    /// System.Threading.Thread, instead relies on System.Threading.Tasks.
    /// (Named after boost::asio::strand.)
    /// </summary>
    public class Strand : IStrand
    {
        readonly object iLock = new object();
        Task iLastAction;

        void CheckForLastTask(Task aTask)
        {
            lock (iLock)
            {
                if (iLastAction.Id == Task.CurrentId)
                {
                    iLastAction = null;
                }
            }
        }

        /// <summary>
        /// Schedule an action to occur in series with all other
        /// scheduled actions.
        /// </summary>
        /// <param name="a"></param>
        public Task ScheduleExclusive(Action a)
        {
            lock (iLock)
            {
                Task task;
                if (iLastAction == null)
                {
                    task = Task.Factory.StartNew(a);
                }
                else
                {
                    task = iLastAction.ContinueWith(t=>a());
                }
                iLastAction = task.ContinueWith(CheckForLastTask);
                return task;
            }
        }

        /// <summary>
        /// Schedule an action to occur in series with all other
        /// scheduled actions.
        /// </summary>
        /// <param name="a"></param>
        public Task<T> ScheduleExclusive<T>(Func<T> a)
        {
            lock (iLock)
            {
                Task<T> task;
                if (iLastAction == null)
                {
                    task = Task.Factory.StartNew(a);
                }
                else
                {
                    task = iLastAction.ContinueWith(t=>a());
                }
                iLastAction = task.ContinueWith(CheckForLastTask);
                return task;
            }
        }
    }
}