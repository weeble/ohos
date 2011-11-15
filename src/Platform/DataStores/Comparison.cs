using System;

namespace OpenHome.Widget.Nodes.DataStores
{
    public enum Comparison
    {
        Unordered = int.MinValue,
        LessThan = -1,
        Equal = 0,
        MoreThan = 1
    }

    public static class ComparisonExtensions
    {
        public static int ToIntWithDefault(this Comparison aComparison, int aDefaultWhenUnordered)
        {
            if (aComparison == Comparison.Unordered)
            {
                return aDefaultWhenUnordered;
            }
            return (int) aComparison;
        }
        public static int ToIntWithDefault(this Comparison aComparison, Func<int> aDefaultWhenUnordered)
        {
            if (aComparison == Comparison.Unordered)
            {
                return aDefaultWhenUnordered();
            }
            return (int) aComparison;
        }
    }
}