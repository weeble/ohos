using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHome.Os.Platform.Collections
{

    public interface IIdDispenser<TKey, TId, TValue>
    {
        TId AllocId(TKey aKey, TValue aValue);
        void ReleaseId(TId aId);
    }

    public class UintIdDispenser<TKey, TValue> : IIdDispenser<TKey, uint, TValue>
    {
        private readonly Queue<uint> iFreeIds = new Queue<uint>();
        protected uint iNextId = 0;
        public virtual uint AllocId(TKey aKey, TValue aValue)
        {
            if (iFreeIds.Count==0)
            {
                uint id = iNextId;
                iNextId += 1;
                return id;
            }
            return iFreeIds.Dequeue();
        }
        public virtual void ReleaseId(uint aId)
        {
            iFreeIds.Enqueue(aId);
        }
    }

    /// <summary>
    /// A dictionary where every element has a key and a unique Id
    /// </summary>
    public class IdDictionary<TKey, TValue>
    {
        private struct Entry
        {
            public readonly uint Id;
            public readonly TValue Value;
            public Entry(uint aId, TValue aValue)
            {
                Id = aId;
                Value = aValue;
            }
        }
        private readonly IIdDispenser<TKey, uint, TValue> iIdDispenser;
        private readonly Dictionary<TKey, Entry> iDictionary = new Dictionary<TKey, Entry>();
        private readonly Dictionary<uint, TKey> iIdTable = new Dictionary<uint, TKey>();
        public IdDictionary()
            :this(new UintIdDispenser<TKey, TValue>())
        {
        }
        public IdDictionary(IIdDispenser<TKey, uint, TValue> aIdDispenser)
        {
            iIdDispenser = aIdDispenser;
        }
        public bool TryAdd(TKey aKey, TValue aValue, out uint aId)
        {
            if (iDictionary.ContainsKey(aKey))
            {
                aId = uint.MaxValue;
                return false;
            }
            uint id = iIdDispenser.AllocId(aKey, aValue);
            iIdTable[id] = aKey;
            iDictionary[aKey] = new Entry(id, aValue);
            aId = id;
            return true;
        }

        public bool TryUpdateByKey(TKey aKey, TValue aValue)
        {
            Entry oldEntry;
            if (!iDictionary.TryGetValue(aKey, out oldEntry))
            {
                return false;
            }
            Entry newEntry = new Entry(oldEntry.Id, aValue);
            iDictionary[aKey] = newEntry;
            return true;
        }
        public bool TryUpdateById(uint aId, TValue aValue)
        {
            TKey key;
            if (!iIdTable.TryGetValue(aId, out key))
            {
                return false;
            }
            Entry newEntry = new Entry(aId, aValue);
            iDictionary[key] = newEntry;
            return true;
        }
        public bool TryGetValueByKey(TKey aKey, out TValue aValue)
        {
            Entry entry;
            if (iDictionary.TryGetValue(aKey, out entry))
            {
                aValue = entry.Value;
                return true;
            }
            aValue = default(TValue);
            return false;
        }
        public bool TryGetValueById(uint aId, out TValue aValue)
        {
            TKey key;
            if (iIdTable.TryGetValue(aId, out key))
            {
                aValue = iDictionary[key].Value;
                return true;
            }
            aValue = default(TValue);
            return false;
        }
        public bool ContainsKey(TKey aKey)
        {
            return iDictionary.ContainsKey(aKey);
        }
        public bool ContainsId(uint aId)
        {
            return iIdTable.ContainsKey(aId);
        }
        public bool TryRemoveByKey(TKey aKey)
        {
            Entry entry;
            if (iDictionary.TryGetValue(aKey, out entry))
            {
                iIdTable.Remove(entry.Id);
                iIdDispenser.ReleaseId(entry.Id);
                iDictionary.Remove(aKey);
                return true;
            }
            return false;
        }
        public bool TryRemoveById(uint aId)
        {
            TKey key;
            if (iIdTable.TryGetValue(aId, out key))
            {
                iIdTable.Remove(aId);
                iIdDispenser.ReleaseId(aId);
                iDictionary.Remove(key);
                return true;
            }
            return false;
        }
        public int Count
        {
            get { return iDictionary.Count; }
        }
        public IEnumerable<KeyValuePair<uint, TValue>> ItemsById
        {
            get
            {
                return iIdTable.Select(kvp => new KeyValuePair<uint, TValue>(kvp.Key, iDictionary[kvp.Value].Value));
            }
        }
        public IEnumerable<KeyValuePair<TKey, TValue>> ItemsByKey
        {
            get
            {
                return iDictionary.Select(kvp => new KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value.Value));
            }
        }
        public TKey GetKeyForId(uint aId)
        {
            TKey key;
            if (iIdTable.TryGetValue(aId, out key))
            {
                return key;
            }
            throw new KeyNotFoundException(String.Format("No entry with id=={0} in dictionary.", aId));
        }
        public uint GetIdForKey(TKey aKey)
        {
            Entry entry;
            if (iDictionary.TryGetValue(aKey, out entry))
            {
                return entry.Id;
            }
            throw new KeyNotFoundException(String.Format("No entry with key=={0} in dictionary.", aKey));
        }

        public void Clear()
        {
            List<uint> oldIds = new List<uint>(iIdTable.Keys);
            iIdTable.Clear();
            iDictionary.Clear();
            foreach (uint oldId in oldIds)
            {
                iIdDispenser.ReleaseId(oldId);
            }
        }
    }
}
