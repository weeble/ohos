using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Os.Platform.DataStores
{
    public class ItcClock : IDistributedClock<ItcStamp>
    {
        private readonly object iCurrentTimeLock = new object();
        private ItcStamp iCurrentTime;
        
        public ItcClock()
        {
            iCurrentTime = ItcStamp.Seed;
        }

        private ItcClock(ItcStamp aStamp)
        {
            iCurrentTime = aStamp;
        }

        public void Update(ItcStamp aTimestamp)
        {
            lock (iCurrentTimeLock)
            {
                iCurrentTime = iCurrentTime.Join(aTimestamp.Peek());
            }
        }

        public ItcStamp Now
        {
            get
            {
                lock (iCurrentTimeLock)
                {
                    return iCurrentTime.Peek();
                }
            }
        }

        public ItcStamp Advance()
        {
            lock (iCurrentTimeLock)
            {
                iCurrentTime = iCurrentTime.Event();
                return iCurrentTime.Peek();
            }
        }

        public string SaveState()
        {
            lock (iCurrentTimeLock)
            {
                return iCurrentTime.ToString();
            }
        }

        public ItcClock Fork()
        {
            ItcStamp otherTime;
            iCurrentTime.Fork(out iCurrentTime, out otherTime);
            return new ItcClock(otherTime);
        }

        public void LoadState(string aSavedState)
        {
            lock (iCurrentTimeLock)
            {
                iCurrentTime = ItcStamp.FromString(aSavedState);
            }
        }
    }

    public class ItcStampComparer : ITimeStampComparer<ItcStamp>
    {
        public Comparison Compare(ItcStamp aFirst, ItcStamp aSecond)
        {
            bool firstLeqSecond = aFirst.Leq(aSecond);
            bool secondLeqFirst = aSecond.Leq(aFirst);
            if (firstLeqSecond)
            {
                return secondLeqFirst ? Comparison.Equal : Comparison.LessThan;
            }
            return secondLeqFirst ? Comparison.MoreThan : Comparison.Unordered;
        }

        static readonly ItcStampComparer Singleton = new ItcStampComparer();
        public static ItcStampComparer Instance { get { return Singleton; } }
    }
}
