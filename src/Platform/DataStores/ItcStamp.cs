using System;

namespace OpenHome.Os.Platform.DataStores
{
    /// <summary>
    /// Implementation of Interval Tree Clocks algorithm (Almeida, Baquero, Fonte 2008)
    /// Immutable
    /// </summary>
    public sealed class ItcStamp
    {
        private static readonly ItcStamp SeedSingleton = new ItcStamp(ItcId.One, ItcEvent.Zero);

        private readonly ItcId iId;
        private readonly ItcEvent iEvent;

        public ItcId IdComponent
        {
            get { return iId; }
        }

        public ItcEvent EventComponent
        {
            get { return iEvent; }
        }

        public static ItcStamp Anonymous(ItcEvent aEvent)
        {
            return new ItcStamp(ItcId.Zero, aEvent);
        }

        /// <summary>
        /// Seed timestamp for starting a new clock system.
        /// </summary>
        public static ItcStamp Seed { get { return SeedSingleton; } }

        /// <summary>
        /// Compare this timestamp to another. Return true if the other timestamp
        /// occurs causally after this one or is equal to it. Return false if the
        /// other timestamp is unordered or occurs causally before this one.
        /// </summary>
        /// <param name="aOther"></param>
        /// <returns></returns>
        public bool Leq(ItcStamp aOther)
        {
            return iEvent.Leq(aOther.iEvent);
        }

        /// <summary>
        /// Fork this clock into two pieces that can be advanced independently and
        /// successfully joined with any other clock in the system.
        /// </summary>
        /// <param name="aLeft"></param>
        /// <param name="aRight"></param>
        public void Fork(out ItcStamp aLeft, out ItcStamp aRight)
        {
            ItcId leftId, rightId;
            iId.Split(out leftId, out rightId);
            aLeft = new ItcStamp(leftId, iEvent);
            aRight = new ItcStamp(rightId, iEvent);
        }

        /// <summary>
        /// Fork an anonymous copy of this clock. The copy cannot be advanced with
        /// Event(), but it can be joined with another clock.
        /// </summary>
        /// <returns></returns>
        public ItcStamp Peek()
        {
            return Anonymous(iEvent);
        }

        /// <summary>
        /// Join two clocks. Result will compare greater or equal than everything
        /// previously forked from those clocks, but not ordered with anything
        /// </summary>
        /// <param name="aOther"></param>
        /// <returns></returns>
        public ItcStamp Join(ItcStamp aOther)
        {
            return new ItcStamp(
                iId.Sum(aOther.iId),
                iEvent.Join(aOther.iEvent));
        }

        /// <summary>
        /// Advance this clock so that the returned value compares greater than the
        /// current and previous values of the clock.
        /// </summary>
        /// <returns></returns>
        public ItcStamp Event()
        {
            ItcEvent filledEvent = iId.Fill(iEvent);
            if (filledEvent.Leq(iEvent) && iEvent.Leq(filledEvent))
            {
                return new ItcStamp(iId, iEvent.Grow(iId));
            }
            return new ItcStamp(iId, filledEvent);
        }

        /// <summary>
        /// Format as a string that uniquely describes the state of the clock.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("({0},{1})", iId, iEvent);
        }

        public static ItcStamp FromString(string aString)
        {
            int index = 0;
            return ItcStringConversion.ItcStampFromString(aString, ref index);
        }

        internal ItcStamp(ItcId aId, ItcEvent aEvent)
        {
            iId = aId;
            iEvent = aEvent;
        }
    }
}