using System;

namespace OpenHome.Os.Platform.DataStores
{
    public class VcClock : IDistributedClock<VcStamp>
    {
        string iId;

        public string Id
        {
            get { return iId; }
        }

        public VcStamp CurrentTime
        {
            get { return iCurrentTime; }
        }

        VcStamp iCurrentTime;

        public VcClock()
        {
            iId = Guid.NewGuid().ToString();
            iCurrentTime = new VcStamp();
        }

        public VcClock(string aId, VcStamp aCurrentTime)
        {
            iId = aId;
            iCurrentTime = aCurrentTime.Clone();
        }

        public void Update(VcStamp aTimestamp)
        {
            iCurrentTime.UpdateInPlace(aTimestamp);
        }

        public VcStamp Now
        {
            get { return iCurrentTime.Clone(); }
        }

        public VcStamp Advance()
        {
            iCurrentTime.AdvanceInPlace(iId);
            return Now;
        }

        public void LoadState(string aClockState)
        {
            int index = 0;
            var clock = VcStringConversion.VcClockFromString(aClockState, ref index);
            iId = clock.iId;
            iCurrentTime = clock.iCurrentTime;
        }

        public string SaveState()
        {
            return ToString();
        }

        public override string ToString()
        {
            return string.Format("(\"{0}\",{1})", iId, iCurrentTime);
        }
    }
}