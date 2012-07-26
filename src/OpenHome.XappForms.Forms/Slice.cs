namespace OpenHome.XappForms.Forms
{
    struct Slice
    {
        int iStart;
        int iEnd;
        public Slice(int aStart, int aEnd)
        {
            iStart = aStart;
            iEnd = aEnd;
        }
        public int Start { get { return iStart; } }
        public int End { get { return iEnd; } }
        public int Count { get { return iEnd-iStart; } }
        public Slice MakeAbsolute(int aCount)
        {
            var start = iStart;
            var end = iEnd;
            if (start < 0) { start += aCount; }
            if (start < 0) { start = 0; }
            if (end < 0) { end += aCount; }
            if (end < start) { end = start; }
            if (end >= aCount) { end = aCount; }
            if (start >= end) { start = end; }
            return new Slice(start, end);
        }
        public static Slice BeforeStart { get { return new Slice(0,0); } }
        public static Slice AfterEnd { get { return new Slice(int.MaxValue,int.MaxValue); } }
        public static Slice All { get { return new Slice(0, int.MaxValue); } }
        public static Slice Single(int aIndex) { return new Slice(aIndex, aIndex+1); }
    }
}