using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenHome.XappForms
{
    public class UserChange
    {
        public string UserId { get; private set; }
        public User OldValue { get; private set; }
        public User NewValue { get; private set; }
        public UserChange(string aUserId, User aOldValue, User aNewValue)
        {
            UserId = aUserId;
            OldValue = aOldValue;
            NewValue = aNewValue;
        }
    }
    public class UserEventArgs : EventArgs
    {
        public IEnumerable<UserChange> Changes { get; private set; }
        public bool SubscriptionEnded { get; private set; }
        public UserEventArgs(
                IEnumerable<UserChange> aChanges,
                bool aSubscriptionEnded)
        {
            Changes = aChanges.AsEnumerable();
            SubscriptionEnded = aSubscriptionEnded;
        }
    }
    public class UserList
    {
        Dictionary<string, User> iUsers;
        object iLock = new object();
        Task iEventTask = Task.Factory.StartNew(()=>{});
        public UserList()
        {
            iUsers = new Dictionary<string, User>();
        }
        EventHandler<UserEventArgs> iHandler;
        public event EventHandler<UserEventArgs> Updated
        {
            add
            {
                lock (iLock)
                {
                    iHandler += value;
                    if (value != null)
                    {
                        var eventArgs = new UserEventArgs(
                            iUsers.Values.Select(
                                aUser =>
                                    new UserChange(
                                        aUser.Id,
                                        null,
                                        aUser)
                                ).ToList(),
                            false);
                        iEventTask.ContinueWith((aTask)=>value(this, eventArgs));
                    }
                }
            }
            remove
            {
                lock (iLock)
                {
                    var oldHandler = iHandler;
                    iHandler = iHandler - value;
                    if (oldHandler != iHandler)
                    {
                        var eventArgs = new UserEventArgs(
                            Enumerable.Empty<UserChange>(),
                            true);
                        iEventTask.ContinueWith((aTask)=>value(this, eventArgs));
                    }
                }
            }
        }

        public bool TryGetUserById(string aUserId, out User aUser)
        {
            lock (iLock)
            {
                return iUsers.TryGetValue(aUserId, out aUser);
            }
        }

        public void SetUser(User aUser)
        {
            if (aUser == null)
            {
                throw new ArgumentNullException("aUser");
            }
            lock (iLock)
            {
                User oldValue;
                if (iHandler != null)
                {
                    iUsers.TryGetValue(aUser.Id, out oldValue);
                    var eventArgs = new UserEventArgs(
                        new[]{ new UserChange(aUser.Id, oldValue, aUser) },
                        false);
                    iEventTask.ContinueWith((aTask)=>iHandler(this, eventArgs));
                }
                iUsers[aUser.Id] = aUser;
            }
        }
        public void RemoveUser(string aUserId)
        {
            if (aUserId == null)
            {
                throw new ArgumentNullException("aUserId");
            }
            lock (iLock)
            {
                User oldValue;
                if (iHandler != null && iUsers.TryGetValue(aUserId, out oldValue))
                {
                    var eventArgs = new UserEventArgs(
                        new[]{ new UserChange(aUserId, oldValue, null) },
                        false);
                    iEventTask.ContinueWith((aTask)=>iHandler(this, eventArgs));
                }
                iUsers.Remove(aUserId);
            }
        }

    }
}
