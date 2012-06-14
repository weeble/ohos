using System;
using System.Collections.Generic;

namespace OpenHome.XappForms
{
    public class BinaryHeapNode<T>
    {
        internal BinaryHeap<T> Heap { get; set; }
        T iValue;

        /// <summary>
        /// Get or set the value of this node. Changing the
        /// value of the node will cause it to be reordered within
        /// the heap.
        /// </summary>
        public T Value
        {
            get
            {
                return iValue;
            }
            set
            {
                iValue = value;
                Heap.Adjust(this);
            }
        }

        /// <summary>
        /// Current index within the heap.
        /// </summary>
        internal int Index { get; set; }

        internal BinaryHeapNode(BinaryHeap<T> aHeap, T aValue, int aIndex)
        {
            Heap = aHeap;
            iValue = aValue;
            Index = aIndex;
        }

        /// <summary>
        /// Remove this node from the heap.
        /// </summary>
        public void Remove()
        {
            Heap.Remove(this);
        }
    }
    
    /// <summary>
    /// Data structure with O(log N) insert, delete and find-minimum.
    /// </summary>
    /// <typeparam name="T">
    /// The data type stored in the heap.
    /// Must either:
    /// 1. be immutable, or
    /// 2. never change in a way that two items can compare differently from before
    /// </typeparam>
    public class BinaryHeap<T>
    {
        List<BinaryHeapNode<T>> iItems;
        IComparer<T> iComparer;

        /// <summary>
        /// Construct a new, empty heap, using the provided comparer when
        /// finding the minimum entry.
        /// </summary>
        /// <param name="aComparer"></param>
        public BinaryHeap(IComparer<T> aComparer)
        {
            iItems = new List<BinaryHeapNode<T>>();
            iComparer = aComparer;
        }

        /// <summary>
        /// Insert an item into the heap.
        /// </summary>
        /// <param name="aItem"></param>
        /// <returns>
        /// A heap node reference that can be used to remove the item, or
        /// to adjust its value up or down.
        /// </returns>
        public BinaryHeapNode<T> Insert(T aItem)
        {
            int index = iItems.Count;
            BinaryHeapNode<T> node = new BinaryHeapNode<T>(this, aItem, index);
            iItems.Add(node);
            BubbleUp(index);
            return node;
        }

        void Swap(int aIndex1, int aIndex2)
        {
            if (aIndex1 == aIndex2) return;
            var temp = iItems[aIndex1];
            iItems[aIndex1] = iItems[aIndex2];
            iItems[aIndex2] = temp;
            iItems[aIndex1].Index = aIndex1;
            iItems[aIndex2].Index = aIndex2;
        }

        /// <summary>
        /// Find the minimum value node in the heap, remove it and
        /// return its value.
        /// Throws Invalid OperationException if empty.
        /// </summary>
        /// <returns></returns>
        public T Pop()
        {
            var result = Peek().Value;
            RemoveMin();
            return result;
        }

        /// <summary>
        /// The number of nodes in the heap.
        /// </summary>
        public int Count
        {
            get { return iItems.Count; }
        }

        /// <summary>
        /// Remove the minimum value node from the heap.
        /// Throws Invalid OperationException if empty.
        /// </summary>
        public void RemoveMin()
        {
            if (iItems.Count==0)
            {
                throw new InvalidOperationException();
            }
            var resultNode = iItems[0];
            Swap(0, iItems.Count-1);
            iItems.RemoveAt(iItems.Count-1);
            BubbleDown(0);
            resultNode.Heap = null;
        }

        /// <summary>
        /// Get the minimum value node in the heap.
        /// Throws Invalid OperationException if empty.
        /// </summary>
        /// <returns></returns>
        public BinaryHeapNode<T> Peek()
        {
            if (iItems.Count==0)
            {
                throw new InvalidOperationException();
            }
            return iItems[0];
        }

        internal void Remove(BinaryHeapNode<T> aItem)
        {
            int index = aItem.Index;
            if (iItems[index] != aItem)
            {
                return;
            }
            BubbleToTop(index);
            Swap(0, iItems.Count-1);
            iItems.RemoveAt(iItems.Count-1);
            aItem.Heap = null;
            BubbleDown(0);
        }

        internal void Adjust(BinaryHeapNode<T> aItem)
        {
            int index = aItem.Index;
            int newIndex = BubbleUp(index);
            if (newIndex == index)
            {
                BubbleDown(index);
            }
        }

        void BubbleToTop(int aIndex)
        {
            // Assumes the heap obeys the heap property *except* for
            // the item at the given index, about which no assumptions
            // are made. Move it to the top of the heap in log N time.
            int index = aIndex;
            while (index>0)
            {
                int next = (index-1)/2;
                Swap(next, index);
                index = next;
            }
        }

        int BubbleUp(int aIndex)
        {
            // Assumes the heap obeys the heap property *except* for
            // the item at the given index, which must be smaller than
            // its children, but might also be smaller than its parent.
            // Bubble it up until the heap property is restored.
            int index = aIndex;
            while (index>0)
            {
                int next = (index-1)/2;
                if (iComparer.Compare(iItems[next].Value, iItems[index].Value)<0)
                {
                    return index;
                }
                Swap(next, index);
                index = next;
            }
            return index;
        }

        void BubbleDown(int aIndex)
        {
            // Assumes the heap obeys the heap property *except* for
            // the item at the given index, which must be larger or
            // equal to its parent, but may also be larger than its
            // children. Bubble it down until the heap property is
            // restored.
            int index = aIndex;
            while (true)
            {
                int leftIndex = index * 2 + 1;
                int rightIndex = index * 2 + 2;
                if (leftIndex >= iItems.Count) return;
                if (rightIndex >= iItems.Count)
                {
                    if (iComparer.Compare(iItems[index].Value, iItems[leftIndex].Value)<=0)
                    {
                        return;
                    }
                    Swap(index, leftIndex);
                    return;
                }
                bool biggerThanLeft = iComparer.Compare(iItems[index].Value, iItems[leftIndex].Value)>0;
                bool biggerThanRight = iComparer.Compare(iItems[index].Value, iItems[rightIndex].Value)>0;
                if (biggerThanLeft && !biggerThanRight)
                {
                    Swap(index, leftIndex);
                    index = leftIndex;
                    continue;
                }
                if (biggerThanRight && !biggerThanLeft)
                {
                    Swap(index, rightIndex);
                    index = rightIndex;
                    continue;
                }
                if (!biggerThanLeft && !biggerThanRight)
                {
                    return;
                }
                bool leftBiggerThanRight = iComparer.Compare(iItems[leftIndex].Value, iItems[rightIndex].Value)>0;
                if (leftBiggerThanRight)
                {
                    Swap(index, rightIndex);
                    index = rightIndex;
                    continue;
                }
                else
                {
                    Swap(index, leftIndex);
                    index = leftIndex;
                    continue;
                }
            }
        }
    }
}
