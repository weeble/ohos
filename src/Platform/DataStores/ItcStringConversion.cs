using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Widget.Nodes.DataStores
{
    public static class ItcStringConversion
    {
        public static ItcStamp ItcStampFromString(string aString, ref int aIndex)
        {
            if (aIndex >= aString.Length || aIndex < 0)
            {
                throw new ArgumentException("aString ended unexpectedly.");
            }
            Expect('(', aString, aIndex);
            aIndex++;
            ItcId id = ItcIdFromString(aString, ref aIndex);
            Expect(',', aString, aIndex);
            aIndex++;
            ItcEvent ev = ItcEventFromString(aString, ref aIndex);
            Expect(')', aString, aIndex);
            aIndex++;
            return new ItcStamp(id, ev);
        }

        public static ItcId ItcIdFromString(string aString, ref int aIndex)
        {
            if (aIndex >= aString.Length || aIndex < 0)
            {
                throw new ArgumentException("aString ended unexpectedly.");
            }
            if (aString[aIndex] == '(')
            {
                aIndex++;
                ItcId left = ItcIdFromString(aString, ref aIndex);
                Expect(',', aString, aIndex);
                aIndex++;
                ItcId right = ItcIdFromString(aString, ref aIndex);
                Expect(')', aString, aIndex);
                aIndex++;
                return ItcId.Tree(left, right);
            }
            if (aString[aIndex] == '1')
            {
                aIndex++;
                return ItcId.One;
            }
            if (aString[aIndex] == '0')
            {
                aIndex++;
                return ItcId.Zero;
            }
            throw new ArgumentException(String.Format(
                "Unexpected character in id. Expected '(', '0' or '1', but got '{0}'",
                aString[aIndex]));
        }

        private static int ParseInt(string aString, ref int aIndex)
        {
            int startIndex = aIndex;
            while (aIndex < aString.Length && aString[aIndex] != ')' && aString[aIndex] != ',')
            {
                aIndex++;
            }
            return Int32.Parse(aString.Substring(startIndex, aIndex - startIndex));
        }

        private static void Expect(char aExpected, string aString, int aIndex)
        {
            if (aIndex >= aString.Length)
            {
                throw new ArgumentException(String.Format(
                    "Bad ITC string. Expected '{0}', but the string ended instead.",
                    aExpected));
            }
            char found = aString[aIndex];
            if (found != aExpected)
            {
                throw new ArgumentException(String.Format(
                    "Bad ITC string. Expected '{0}', but found '{1}'.",
                    aExpected, found));
            }
        }

        public static ItcEvent ItcEventFromString(string aString, ref int aIndex)
        {
            if (aIndex >= aString.Length || aIndex < 0)
            {
                throw new ArgumentException("aString ended unexpectedly.");
            }
            if (aString[aIndex] == '(')
            {
                aIndex++;
                int value = ParseInt(aString, ref aIndex);
                Expect(',', aString, aIndex);
                aIndex++;
                ItcEvent left = ItcEventFromString(aString, ref aIndex);
                Expect(',', aString, aIndex);
                aIndex++;
                ItcEvent right = ItcEventFromString(aString, ref aIndex);
                Expect(')', aString, aIndex);
                aIndex++;
                return ItcEvent.Tree(value, left, right);
            }
            else
            {
                int value = ParseInt(aString, ref aIndex);
                return ItcEvent.Single(value);
            }
        }
    }
}
