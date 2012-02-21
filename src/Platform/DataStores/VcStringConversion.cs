using System;
using System.Collections.Generic;
using System.Text;

namespace OpenHome.Os.Platform.DataStores
{
    public static class VcStringConversion
    {
        public static VcClock VcClockFromString(string aString, ref int aIndex)
        {
            if (aIndex >= aString.Length || aIndex < 0)
            {
                throw new FormatException("aString ended unexpectedly.");
            }
            Expect('(', aString, aIndex);
            aIndex++;

            string id = VcIdFromString(aString, ref aIndex);
            Expect(',', aString, aIndex);
            aIndex++;
            VcStamp stamp = VcStampFromString(aString, ref aIndex);
            Expect(')', aString, aIndex);
            aIndex++;
            return new VcClock(id, stamp);
        }

        public static string VcIdFromString(string aString, ref int aIndex)
        {
            if (aIndex >= aString.Length || aIndex < 0)
            {
                throw new FormatException("aString ended unexpectedly.");
            }
            Expect('"', aString, aIndex);
            aIndex++;
            StringBuilder sb = new StringBuilder();
            for (;;)
            {
                while (aIndex < aString.Length && aString[aIndex] != '"')
                {
                    sb.Append(aString[aIndex]);
                    aIndex++;
                }
                if (aIndex >= aString.Length)
                {
                    throw new FormatException("aString ended unexpectedly.");
                }
                aIndex++;
                if (aIndex >= aString.Length || aString[aIndex]!='"')
                {
                    return sb.ToString();
                }
                aIndex++;
            }
        }

        private static long ParseLong(string aString, ref int aIndex)
        {
            int startIndex = aIndex;
            while (aIndex < aString.Length && aString[aIndex] != '}' && aString[aIndex] != ',')
            {
                aIndex++;
            }
            return Int32.Parse(aString.Substring(startIndex, aIndex - startIndex));
        }

        private static void Expect(char aExpected, string aString, int aIndex)
        {
            if (aIndex >= aString.Length)
            {
                throw new FormatException(String.Format(
                    "Bad ITC string. Expected '{0}', but the string ended instead.",
                    aExpected));
            }
            char found = aString[aIndex];
            if (found != aExpected)
            {
                throw new FormatException(String.Format(
                    "Bad ITC string. Expected '{0}', but found '{1}'.",
                    aExpected, found));
            }
        }

        public static VcStamp VcStampFromString(string aString, ref int aIndex)
        {
            if (aIndex >= aString.Length || aIndex < 0)
            {
                throw new FormatException("aString ended unexpectedly.");
            }
            Dictionary<string, long> contents = new Dictionary<string, long>();
            Expect('{', aString, aIndex);
            aIndex++;
            if (aIndex < aString.Length && aString[aIndex] == '}')
            {
                aIndex++;
                return new VcStamp(contents);
            }
            for (; ; )
            {
                string id = VcIdFromString(aString, ref aIndex);
                Expect(':', aString, aIndex);
                aIndex++;
                long value = ParseLong(aString, ref aIndex);
                contents.Add(id, value);
                if (aIndex >= aString.Length)
                {
                    throw new FormatException("Bad Vector Clock string. Expected ':' or '}', but the string ended instead.");
                }
                if (aString[aIndex] == '}')
                {
                    aIndex++;
                    return new VcStamp(contents);
                }
                if (aString[aIndex] == ',')
                {
                    aIndex++;
                    continue;
                }
                throw new FormatException(String.Format("Bad vector clock string. Expected ':' or '}', but got '{0}' instead.", aString[aIndex]));
            }
        }
    }
}
