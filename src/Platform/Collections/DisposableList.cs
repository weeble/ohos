using System;
using System.Collections;
using System.Collections.Generic;
using OpenHome.Widget.Nodes.Proxies;
using System.Linq;

namespace OpenHome.Widget.Nodes.Collections
{
    /// <summary>
    /// A list that disposes of its contents when it is itself disposed.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public abstract class DisposableSequence<TItem> : IDisposableContainer<TItem> where TItem : IDisposable
    {
        public void Dispose()
        {
            DisposableSequence.DisposeSequence(this);
        }

        public abstract IEnumerator<TItem> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public static class DisposableSequence
    {
        public static void DisposeSequence<T>(IEnumerable<T> aItems) where T : IDisposable
        {
            List<Exception> exceptions = new List<Exception>();
            foreach (var item in aItems)
            {
                try
                {
                    item.Dispose();
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }
            if (exceptions.Count > 0)
            {
                throw new Exception("One or more exceptions occurred while disposing sequence.", exceptions[0]);
                // .NET Framework 4.0: throw new AggregateException(exceptions);
            }
        }
    }

    /// <summary>
    /// A list that disposes of its contents when it is itself disposed.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public sealed class DisposableList<TItem> : DisposableSequence<TItem> where TItem : IDisposable
    {
        private readonly List<TItem> iItems;
        public DisposableList(List<TItem> aItems)
        {
            iItems = aItems;
        }

        public override IEnumerator<TItem> GetEnumerator()
        {
            return iItems.GetEnumerator();
        }
    }

    /// <summary>
    /// A list that disposes of its contents in reverse order when it is itself disposed.
    /// </summary>
    /// <typeparam name="TItem"></typeparam>
    public class DisposableStack<TItem> : DisposableSequence<TItem> where TItem : IDisposable
    {
        private readonly List<TItem> iItems;
        public DisposableStack(List<TItem> aItems)
        {
            iItems = aItems;
        }

        public override IEnumerator<TItem> GetEnumerator()
        {
            List<TItem> itemsCopy = new List<TItem>(iItems);
            itemsCopy.Reverse();
            return itemsCopy.GetEnumerator();
        }
    }
}