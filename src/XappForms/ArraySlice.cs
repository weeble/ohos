using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace OpenHome.XappForms
{
    public struct ArraySlice<T> : IList<T>
    {
        readonly ArraySegment<T> iSegment;

        void Validate()
        {
            if (iSegment.Array == null)
                throw new InvalidOperationException();
        }

        public ArraySlice(T[] aArray)
            : this(new ArraySegment<T>(aArray))
        {
        }

        public ArraySlice(ArraySegment<T> aSegment)
            : this()
        {
            if (aSegment.Array == null)
            {
                throw new ArgumentException();
            }
            // If Array is not null, the ArraySegment was constructed properly,
            // and the count and offset are valid.
            iSegment = aSegment;
        }

        public IEnumerator<T> GetEnumerator()
        {
            Validate();
            for (int i=iSegment.Offset; i<iSegment.Count; ++i)
            {
                yield return iSegment.Array[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T aItem)
        {
            throw new InvalidOperationException();
        }

        public void Clear()
        {
            throw new InvalidOperationException();
        }

        public bool Contains(T aItem)
        {
            return ((IEnumerable<T>)this).Contains(aItem);
        }

        public void CopyTo(T[] aArray, int aArrayIndex)
        {
            Validate();
            Array.Copy(iSegment.Array, iSegment.Offset, aArray, aArrayIndex, iSegment.Count);
        }

        public bool Remove(T item)
        {
            throw new InvalidOperationException();
        }

        public int Count
        {
            get
            {
                Validate();
                return iSegment.Count;
            }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public int IndexOf(T aItem)
        {
            Validate();
            for (int i=iSegment.Offset; i<iSegment.Count; ++i)
            {
                if (iSegment.Array[i].Equals(aItem))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Insert(int index, T item)
        {
            throw new InvalidOperationException();
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        public T this[int aIndex]
        {
            get
            {
                Validate();
                if (aIndex > iSegment.Count)
                    throw new IndexOutOfRangeException();
                return iSegment.Array[iSegment.Offset + aIndex];
            }
            set { throw new InvalidOperationException(); }
        }

        public ArraySlice<T> Slice(int aStartIndex, int aEndIndex)
        {
            Validate();
            int availableCount = iSegment.Count;
            if (aStartIndex < 0)
            {
                aStartIndex = aStartIndex + availableCount;
                if (aStartIndex < 0)
                {
                    aStartIndex = 0;
                }
            }
            if (aStartIndex > availableCount)
            {
                aStartIndex = availableCount;
            }
            if (aEndIndex < 0)
            {
                aEndIndex = aEndIndex + availableCount;
                if (aEndIndex < 0)
                {
                    aEndIndex = 0;
                }
            }
            if (aEndIndex > availableCount)
            {
                aEndIndex = availableCount;
            }
            if (aEndIndex < aStartIndex)
            {
                aEndIndex = 0;
            }
            int count = aEndIndex - aStartIndex;
            if (count < 0)
            {
                count = 0;
            }
            return new ArraySlice<T>(
                new ArraySegment<T>(
                    iSegment.Array,
                    iSegment.Offset + aStartIndex,
                    count));
        }
    }
}