using System;

namespace OpenHome.Os.Platform.DataStores
{
    public abstract class ItcId
    {
        private static readonly ItcId OneSingleton = new ItcIdOne();
        private static readonly ItcId ZeroSingleton = new ItcIdZero();

        public static ItcId One { get { return OneSingleton; } }
        public static ItcId Zero { get { return ZeroSingleton; } }
        public static ItcId Tree(ItcId aLeft, ItcId aRight)
        {
            if (aLeft == One && aRight == One)
            {
                return One;
            }
            if (aLeft == Zero && aRight == Zero)
            {
                return Zero;
            }
            return new ItcIdTree(aLeft, aRight);
        }

        public abstract void Split(out ItcId aLeft, out ItcId aRight);
        public abstract ItcId Sum(ItcId aOther);
        public abstract ItcEvent Fill(ItcEvent aEvent);
        internal abstract ItcEvent InternalGrow(ItcEvent.ItcEventTree aEvent, out long aCost);

        public static ItcId FromString(string aString)
        {
            int index = 0;
            return ItcStringConversion.ItcIdFromString(aString, ref index);
        }

        private class ItcIdOne : ItcId
        {
            public override void Split(out ItcId aLeft, out ItcId aRight)
            {
                aLeft = Tree(One, Zero);
                aRight = Tree(Zero, One);
            }
            public override ItcId Sum(ItcId aOther)
            {
                if (aOther != Zero)
                {
                    throw new Exception("Over unity.");
                }
                return this;
            }

            public override ItcEvent Fill(ItcEvent aEvent)
            {
                return ItcEvent.Single(aEvent.Max);
            }

            internal override ItcEvent InternalGrow(ItcEvent.ItcEventTree aEvent, out long aCost)
            {
                throw new InvalidOperationException("Tried to Grow when Fill would have worked.");
            }

            public override string ToString()
            {
                return "1";
            }
        }
        private class ItcIdZero : ItcId
        {
            public override void Split(out ItcId aLeft, out ItcId aRight)
            {
                aLeft = Zero;
                aRight = Zero;
            }
            public override ItcId Sum(ItcId aOther)
            {
                return aOther;
            }

            public override ItcEvent Fill(ItcEvent aEvent)
            {
                return aEvent;
            }

            internal override ItcEvent InternalGrow(ItcEvent.ItcEventTree aEvent, out long aCost)
            {
                throw new InvalidOperationException("Anonymous clocks cannot Grow.");
            }

            public override string ToString()
            {
                return "0";
            }
        }
        private class ItcIdTree : ItcId
        {
            private readonly ItcId iLeft;
            private readonly ItcId iRight;

            public ItcIdTree(ItcId aLeft, ItcId aRight)
            {
                iLeft = aLeft;
                iRight = aRight;
            }

            public override void Split(out ItcId aLeft, out ItcId aRight)
            {
                if (Zero == iLeft)
                {
                    ItcId sub1, sub2;
                    iRight.Split(out sub1, out sub2);
                    aLeft = Tree(Zero, sub1);
                    aRight = Tree(Zero, sub2);
                }
                else if (Zero == iRight)
                {
                    ItcId sub1, sub2;
                    iLeft.Split(out sub1, out sub2);
                    aLeft = Tree(sub1, Zero);
                    aRight = Tree(sub2, Zero);
                }
                else
                {
                    aLeft = Tree(iLeft, Zero);
                    aRight = Tree(Zero, iRight);
                }
            }

            public override ItcId Sum(ItcId aOther)
            {
                if (Zero == aOther)
                {
                    return this;
                }
                if (One == aOther)
                {
                    throw new Exception("Over unity.");
                }
                var otherTree = (ItcIdTree) aOther;
                return Tree(iLeft.Sum(otherTree.iLeft), iRight.Sum(otherTree.iRight));
            }

            public override ItcEvent Fill(ItcEvent aEvent)
            {
                return aEvent.Fill(iLeft, iRight);
            }

            internal override ItcEvent InternalGrow(ItcEvent.ItcEventTree aEvent, out long aCost)
            {
                return aEvent.InternalGrow(iLeft, iRight, out aCost);
            }

            public override string ToString()
            {
                return String.Format("({0},{1})", iLeft, iRight);
            }
        }
    }
}