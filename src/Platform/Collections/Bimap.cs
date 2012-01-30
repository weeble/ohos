using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenHome.Os.Platform.Collections
{
    public class Bimap<T1, T2> : IEnumerable<KeyValuePair<T1,T2>>
    {
        public IDictionary<T1, T2> Forward { get; private set; }
        public IDictionary<T2, T1> Backward { get; private set; }

        public Bimap()
        {
            Dictionary<T1, T2> forwardDict = new Dictionary<T1, T2>();
            Dictionary<T2, T1> backwardDict = new Dictionary<T2, T1>();
            Forward = new HalfBimap<T1, T2>(forwardDict, backwardDict);
            Backward = new HalfBimap<T2, T1>(backwardDict, forwardDict);
        }

        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        {
            return Forward.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public int Count { get { return Forward.Count; } }
    }

    internal class HalfBimap<T1, T2> : IDictionary<T1, T2>
    {
        readonly Dictionary<T1, T2> iForward;
        readonly Dictionary<T2, T1> iBackward;

        public HalfBimap(Dictionary<T1, T2> aForward, Dictionary<T2, T1> aBackward)
        {
            iForward = aForward;
            iBackward = aBackward;
        }

        public IEnumerator<KeyValuePair<T1, T2>> GetEnumerator()
        {
            return iForward.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<T1, T2> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            iForward.Clear();
            iBackward.Clear();
        }

        public bool Contains(KeyValuePair<T1, T2> item)
        {
            return ((ICollection<KeyValuePair<T1, T2>>)iForward).Contains(item);
        }

        public void CopyTo(KeyValuePair<T1, T2>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<T1, T2>>)iForward).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<T1, T2> item)
        {
            if (!((ICollection<KeyValuePair<T1, T2>>)iForward).Contains(item))
            {
                return false;
            }
            iForward.Remove(item.Key);
            iBackward.Remove(item.Value);
            return true;
        }

        public int Count
        {
            get { return iForward.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool ContainsKey(T1 key)
        {
            return iForward.ContainsKey(key);
        }

        public void Add(T1 key, T2 value)
        {
            if (iForward.ContainsKey(key))
            {
                throw new ArgumentException("Key collision.");
            }
            if (iBackward.ContainsKey(value))
            {
                throw new ArgumentException("Reverse key collision.");
            }
            iForward.Add(key, value);
            iBackward.Add(value, key);
        }

        public bool Remove(T1 key)
        {
            T2 value;
            if (!iForward.TryGetValue(key, out value))
            {
                return false;
            }
            iForward.Remove(key);
            iBackward.Remove(value);
            return true;
        }

        public bool TryGetValue(T1 key, out T2 value)
        {
            return iForward.TryGetValue(key, out value);
        }

        public T2 this[T1 key]
        {
            get { return iForward[key]; }
            set
            {
                T2 oldValue;
                if (iForward.TryGetValue(key, out oldValue))
                {
                    iBackward.Remove(oldValue);
                }
                iForward[key] = value;
                iBackward[value] = key;
            }
        }

        public ICollection<T1> Keys
        {
            get { return iForward.Keys; }
        }

        public ICollection<T2> Values
        {
            get { return iBackward.Keys; }
        }
    }
}
