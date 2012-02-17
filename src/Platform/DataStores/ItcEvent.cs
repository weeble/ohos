using System;
using System.Diagnostics;

namespace OpenHome.Os.Platform.DataStores
{
    public abstract class ItcEvent
    {
        private const long GrowConstant = 0x100000000L;
        private static readonly ItcEvent ZeroSingleton = new ItcEventSingle(0);
        public static ItcEvent Zero { get { return ZeroSingleton; } }
        public static ItcEvent Single(int aValue)
        {
            return new ItcEventSingle(aValue);
        }
        public static ItcEvent Tree(int aValue, ItcEvent aLeft, ItcEvent aRight)
        {
            var leftSingle = aLeft as ItcEventSingle;
            var rightSingle = aRight as ItcEventSingle;
            if (leftSingle != null && rightSingle != null && leftSingle.Min == rightSingle.Min)
            {
                return new ItcEventSingle(aValue + leftSingle.Min);
            }
            int sinkAmount = Math.Min(aLeft.Min, aRight.Min);
            return new ItcEventTree(aValue + sinkAmount, aLeft.Sink(sinkAmount), aRight.Sink(sinkAmount));
        }

        public abstract bool Leq(ItcEvent aOther);

        private readonly int iValue;

        protected ItcEvent(int aValue)
        {
            iValue = aValue;
        }

        public int Min
        {
            get { return iValue; }
        }

        public abstract int Max { get; }

        public abstract ItcEvent Left { get; }
        public abstract ItcEvent Right { get; }

        public abstract ItcEvent Join(ItcEvent aOther);

        public ItcEvent Sink(int aAmount)
        {
            return Lift(-aAmount);
        }

        public abstract ItcEvent Lift(int aAmount);

        internal abstract ItcEvent Fill(ItcId aLeft, ItcId aRight);
        public abstract ItcEvent Grow(ItcId aId, out long aCost);

        public ItcEvent Grow(ItcId aId)
        {
            long ignore;
            return Grow(aId, out ignore);
        }

        public static ItcEvent FromString(string aString)
        {
            int index = 0;
            return ItcStringConversion.ItcEventFromString(aString, ref index);
        }

        private class ItcEventSingle : ItcEvent
        {
            public ItcEventSingle(int aValue) : base(aValue)
            {
            }

            public override int Max
            {
                get { return iValue; }
            }

            public override ItcEvent Left
            {
                get { return this; }
            }

            public override ItcEvent Right
            {
                get { return this; }
            }

            public override ItcEvent Join(ItcEvent aOther)
            {
                var otherSingle = aOther as ItcEventSingle;
                if (otherSingle != null)
                {
                    if (otherSingle.iValue > iValue)
                    {
                        return otherSingle;
                    }
                    return this;
                }
                return aOther.Join(this);
            }

            public override ItcEvent Lift(int aAmount)
            {
                return new ItcEventSingle(iValue + aAmount);
            }

            internal override ItcEvent Fill(ItcId aLeft, ItcId aRight)
            {
                return this;
            }

            public override ItcEvent Grow(ItcId aId, out long aCost)
            {
                if (aId == ItcId.One)
                {
                    aCost = 0;
                    return Single(iValue + 1);
                }
                long cost;
                ItcEvent evPrime = new ItcEventTree(iValue, Zero, Zero).Grow(aId, out cost);
                aCost = cost + GrowConstant;
                return evPrime;
            }

            public override bool Leq(ItcEvent aOther)
            {
                return iValue <= aOther.Min;
            }
            public override string ToString()
            {
                return iValue.ToString();
            }
        }
        public class ItcEventTree : ItcEvent
        {
            private readonly ItcEvent iLeft;
            private readonly ItcEvent iRight;

            public ItcEventTree(int aValue, ItcEvent aLeft, ItcEvent aRight)
                :base(aValue)
            {
                iLeft = aLeft;
                iRight = aRight;
            }

            public override int Max
            {
                get { return Math.Max(iLeft.Max, iRight.Max); }
            }

            public override ItcEvent Left
            {
                get { return iLeft.Lift(iValue); }
            }

            public override ItcEvent Right
            {
                get { return iRight.Lift(iValue); }
            }

            public override ItcEvent Join(ItcEvent aOther)
            {
                return Tree(
                    0,
                    Left.Join(aOther.Left),
                    Right.Join(aOther.Right));
            }

            public override ItcEvent Lift(int aAmount)
            {
                return new ItcEventTree(iValue + aAmount, iLeft, iRight);
            }

            internal override ItcEvent Fill(ItcId aLeft, ItcId aRight)
            {
                if (aLeft == ItcId.One)
                {
                    ItcEvent rightPrime = aRight.Fill(iRight);
                    return Tree(
                        iValue,
                        Single(Math.Max(iLeft.Max, rightPrime.Min)),
                        rightPrime);
                }
                if (aRight == ItcId.One)
                {
                    ItcEvent leftPrime = aLeft.Fill(iLeft);
                    return Tree(
                        iValue,
                        leftPrime,
                        Single(Math.Max(iRight.Max, leftPrime.Min)));
                }
                return Tree(
                    iValue,
                    aLeft.Fill(iLeft),
                    aRight.Fill(iRight));
            }

            public override ItcEvent Grow(ItcId aId, out long aCost)
            {
                return aId.InternalGrow(this, out aCost);
            }

            public override bool Leq(ItcEvent aOther)
            {
                return
                    iValue <= aOther.Min &&
                    Left.Leq(aOther.Left) &&
                    Right.Leq(aOther.Right);
            }
            public override string ToString()
            {
                return String.Format("({0},{1},{2})", iValue, iLeft, iRight);
            }

            public ItcEvent InternalGrow(ItcId aLeft, ItcId aRight, out long aCost)
            {
                long costLeft, costRight;
                ItcEvent growLeft, growRight;

                if (aRight != ItcId.Zero)
                {
                    growRight = iRight.Grow(aRight, out costRight);
                }
                else
                {
                    growRight = null;
                    costRight = long.MaxValue;
                }

                if (aLeft != ItcId.Zero)
                {
                    growLeft = iLeft.Grow(aLeft, out costLeft);
                }
                else
                {
                    growLeft = null;
                    costLeft = long.MaxValue;
                }

                Debug.Assert(growLeft != null || growRight != null);

                if (costLeft < costRight)
                {
                    aCost = costLeft + 1;
                    return Tree(iValue, growLeft, iRight);
                }
                aCost = costRight + 1;
                return Tree(iValue, iLeft, growRight);
            }
        }

    }
}
