using System;

namespace OpenHome.Widget.Nodes.Threading
{
    /// <summary>
    /// Helps ensure thread-safe deterministic disposal.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class CountedReference<T> : IDisposable where T : IDisposable
    {
        // Locks:
        //    lock InternalCounter before reading or writing its
        //    RefCount member.
        //    No need to lock to access its Item member.
        //    the InternalCounter is not safe to hold while acquiring
        //    any other lock.

        // Note:
        //    This class is sealed.

        private class InternalCounter
        {
            public int RefCount;
            public T Item;
        }
        private InternalCounter iInternalCounter;

        /// <summary>
        /// Takes sole ownership of the given item. It will be disposed automatically
        /// when the every one of this reference *and* all copies derived from it are
        /// disposed.
        /// </summary>
        /// <param name="aItem"></param>
        public CountedReference(T aItem)
        {
            iInternalCounter = new InternalCounter { RefCount = 1, Item = aItem };
        }

        private CountedReference(InternalCounter aCounter)
        {
            iInternalCounter = aCounter;
        }

        /// <summary>
        /// Attempt to copy a reference *that might have been disposed*.
        /// This method is private because it's probably a bug if you find you
        /// are trying to use a CountedReference when another thread might have
        /// disposed it. It probably means you should have created a copy already,
        /// at a time when you could guarantee that the CountedReference you were
        /// copying wasn't disposed.
        /// </summary>
        /// <param name="aCopy"></param>
        /// <returns></returns>
        private bool TryCopy(out CountedReference<T> aCopy)
        {
            lock (iInternalCounter)
            {
                if (iInternalCounter.RefCount == 0)
                {
                    aCopy = null;
                    return false;
                }
                iInternalCounter.RefCount += 1;
            }
            aCopy = new CountedReference<T>(iInternalCounter);
            return true;
        }

        /// <summary>
        /// Make a copy of the reference which can be used to access
        /// the object. The object is not disposed until all references
        /// are disposed.
        /// </summary>
        /// <returns></returns>
        public CountedReference<T> Copy()
        {
            CountedReference<T> copy;
            bool success = TryCopy(out copy);
            if (!success)
            {
                throw new InvalidOperationException(
                    "Attempted to copy an expired reference counter.");
            }
            return copy;
        }

        /// <summary>
        /// Get the owned item.
        /// </summary>
        public T Value
        {
            get
            {
                return iInternalCounter.Item;
            }
        }

        /// <summary>
        /// Clean up. If this is the last CountedReference to the
        /// object, dispose the object. Otherwise one of the other
        /// references will have to handle it.
        /// </summary>
        public void Dispose()
        {
            // Safe to Dispose multiple times:
            if (iInternalCounter == null)
            {
                return;
            }
            bool shouldDisposeItem;
            lock (iInternalCounter)
            {
                iInternalCounter.RefCount -= 1;
                shouldDisposeItem = iInternalCounter.RefCount == 0;
            }
            if (shouldDisposeItem)
            {
                iInternalCounter.Item.Dispose();
            }
            iInternalCounter = null;
        }
    }
}
