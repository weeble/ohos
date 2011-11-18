using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using OpenHome.Os.Platform.Collections;

namespace OpenHome.Widget.Nodes
{


    public class SequencedValue<T>
    {
        private readonly T iValue;
        private readonly uint iSeqNo;

        private SequencedValue(T aValue, uint aSeqNo)
        {
            iValue = aValue;
            iSeqNo = aSeqNo;
        }

        public T Value
        {
            get { return iValue; }
        }

        public uint SeqNo
        {
            get { return iSeqNo; }
        }

        public static SequencedValue<T> Initial(T aValue)
        {
            return new SequencedValue<T>(aValue, 1);
        }

        public SequencedValue<T> Updated(T aValue)
        {
            return new SequencedValue<T>(aValue, SeqNo + 1);
        }
    }

    public interface IGlobalPresetRepository
    {
        event EventHandler SequenceNumberChange;
        uint CreatePreset(Snapshot aSnapshot);
        void UpdatePreset(uint aPresetHandle, Snapshot aSnapshot);
        void RemovePreset(uint aPresetHandle);
        void GetPresetDetails(uint aPresetHandle, out string aPresetId, out Snapshot aPresetSnapshot);
        string GetPresetId(uint aPresetHandle);
        void GetSequenceNumbers(out List<uint> aHandles, out List<uint> aSequenceNumbers);
    }

    public interface ILocalPresetRepository
    {
        event EventHandler SequenceNumberChange;
        Snapshot GetPresetById(string aId);
        void GetSequenceNumbers(out List<uint> aHandles, out List<uint> aSequenceNumbers);
    }

    public class OneNodePresetRepository : IGlobalPresetRepository, ILocalPresetRepository
    {
        private readonly OneNodePresetRepositoryImpl iImpl;

        public OneNodePresetRepository(IPresetDiskStore aDiskStore)
        {
            iImpl = new OneNodePresetRepositoryImpl(aDiskStore);
        }

        public event EventHandler SequenceNumberChange;

        private void InvokeSequenceNumberChange()
        {
            var handler = SequenceNumberChange;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }
        }
        public uint CreatePreset(Snapshot aSnapshot)
        {
            uint result;
            lock (iImpl)
            {
                result = iImpl.CreatePreset(aSnapshot);
            }
            InvokeSequenceNumberChange();
            return result;
        }
        public void UpdatePreset(uint aPresetHandle, Snapshot aSnapshot)
        {
            lock (iImpl)
            {
                iImpl.UpdatePreset(aPresetHandle, aSnapshot);
            }
            InvokeSequenceNumberChange();
        }
        public void RemovePreset(uint aPresetHandle)
        {
            lock (iImpl)
            {
                iImpl.RemovePreset(aPresetHandle);
            }
            InvokeSequenceNumberChange();
        }
        public void GetPresetDetails(uint aPresetHandle, out string aPresetId, out Snapshot aPresetSnapshot)
        {
            lock (iImpl)
            {
                iImpl.GetPresetDetails(aPresetHandle, out aPresetId, out aPresetSnapshot);
            }
        }

        public string GetPresetId(uint aPresetHandle)
        {
            lock (iImpl)
            {
                return iImpl.GetPresetId(aPresetHandle);
            }
        }

        public Snapshot GetPresetById(string aId)
        {
            lock (iImpl)
            {
                return iImpl.GetPresetById(aId);
            }
        }
        public void GetSequenceNumbers(out List<uint> aHandles, out List<uint> aSequenceNumbers)
        {
            lock (iImpl)
            {
                aHandles = new List<uint>(iImpl.Handles);
                aSequenceNumbers = new List<uint>(iImpl.SequenceNumbers);
            }
        }
    }

    public class OneNodePresetRepositoryImpl
    {

        private readonly IPresetDiskStore iDiskStore;

        private readonly IdDictionary<string, SequencedValue<Snapshot>> iPresets = new IdDictionary<string, SequencedValue<Snapshot>>();

        public OneNodePresetRepositoryImpl(IPresetDiskStore aDiskStore)
        {
            iDiskStore = aDiskStore;
            foreach (PresetState presetState in iDiskStore.LoadPresetsFromStore())
            {
                uint handle;
                iPresets.TryAdd(presetState.Guid, SequencedValue<Snapshot>.Initial(presetState.Snapshot), out handle);
            }
        }

        public uint CreatePreset(Snapshot aSnapshot)
        {
            bool success;
            uint handle;
            string snapshotId;
            do
            {
                snapshotId = Guid.NewGuid().ToString();
                success = iPresets.TryAdd(snapshotId, SequencedValue<Snapshot>.Initial(aSnapshot), out handle);
            } while (!success);
            iDiskStore.PutPreset(new PresetState {Guid = snapshotId, Snapshot = aSnapshot});
            return handle;
        }

        public void UpdatePreset(uint aPresetHandle, Snapshot aSnapshot)
        {
            SequencedValue<Snapshot> oldValue;
            if (!iPresets.TryGetValueById(aPresetHandle, out oldValue))
            {
                throw new KeyNotFoundException("Specified preset handle not found in repository");
            }
            bool success = iPresets.TryUpdateById(aPresetHandle, oldValue.Updated(aSnapshot));
            Debug.Assert(success);
            iDiskStore.PutPreset(new PresetState {Guid = iPresets.GetKeyForId(aPresetHandle), Snapshot = aSnapshot});
        }

        public void RemovePreset(uint aPresetHandle)
        {
            string presetGuid;
            try
            {
                presetGuid = iPresets.GetKeyForId(aPresetHandle);
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException("Specified preset handle not found in repository");
            }
            if (!iPresets.TryRemoveById(aPresetHandle))
            {
                Debug.Fail("");
            }
            iDiskStore.RemovePreset(presetGuid);
        }

        public void GetPresetDetails(uint aPresetHandle, out string aPresetId, out Snapshot aPresetSnapshot)
        {
            SequencedValue<Snapshot> value;
            if (!iPresets.TryGetValueById(aPresetHandle, out value))
            {
                throw new KeyNotFoundException("Specified preset handle not found in repository");
            }
            aPresetSnapshot = value.Value;
            aPresetId = iPresets.GetKeyForId(aPresetHandle);
        }

        public Snapshot GetPresetById(string aPresetId)
        {
            SequencedValue<Snapshot> snapshot;
            if (!iPresets.TryGetValueByKey(aPresetId, out snapshot))
            {
                throw new KeyNotFoundException("Specified preset ID not found in repository");
            }
            return snapshot.Value;
        }

        public IEnumerable<uint> Handles
        {
            get { return iPresets.ItemsById.Select(aKvp => aKvp.Key); }
        }

        public IEnumerable<uint> SequenceNumbers
        {
            get { return iPresets.ItemsById.Select(aKvp => aKvp.Value.SeqNo); }
        }

        public string GetPresetId(uint aPresetHandle)
        {
            return iPresets.GetKeyForId(aPresetHandle);
        }
    }
}