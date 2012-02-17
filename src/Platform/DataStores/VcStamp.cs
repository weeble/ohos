using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Os.Platform.DataStores
{
    public class VcStamp
    {
        readonly Dictionary<string, long> iContents;
        public VcStamp()
        {
            iContents = new Dictionary<string, long>();
        }

        internal VcStamp(Dictionary<string, long> aContents)
        {
            iContents = aContents;
        }

        public VcStamp Clone()
        {
            return new VcStamp(new Dictionary<string, long>(iContents));
        }

        public bool Leq(VcStamp aOther)
        {
            HashSet<string> keys = new HashSet<string>(iContents.Keys);
            keys.UnionWith(aOther.iContents.Keys);
            foreach (string key in keys)
            {
                long valueThis, valueOther;
                // If TryGetValue fails, it sets the output value to default(long),
                // i.e. 0, which is what we want.
                iContents.TryGetValue(key, out valueThis);
                aOther.iContents.TryGetValue(key, out valueOther);
                if (valueThis > valueOther)
                    return false;
            }
            return true;
        }

        public VcStamp Update(VcStamp aOther)
        {
            var newStamp = Clone();
            newStamp.UpdateInPlace(aOther);
            return newStamp;
        }

        public void UpdateInPlace(VcStamp aOther)
        {
            UpdateDictionary(aOther, iContents);
        }

        static void UpdateDictionary(VcStamp aOther, Dictionary<string, long> aContents)
        {
            foreach (var kvp in aOther.iContents)
            {
                long valueThis;
                if (aContents.TryGetValue(kvp.Key, out valueThis))
                {
                    aContents[kvp.Key] = Math.Max(kvp.Value, valueThis);
                }
                else
                {
                    aContents[kvp.Key] = kvp.Value;
                }
            }
        }

        static void AdvanceDictionary(string aId, Dictionary<string, long> aContents)
        {
            long oldValue;
            aContents.TryGetValue(aId, out oldValue);
            aContents[aId] = oldValue + 1;
        }

        public void AdvanceInPlace(string aId)
        {
            AdvanceDictionary(aId, iContents);
        }

        public VcStamp Advance(string aId)
        {
            var newStamp = Clone();
            newStamp.AdvanceInPlace(aId);
            return newStamp;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("{");
            bool addComma = false;
            foreach (var kvp in iContents.OrderBy(aKvp=>aKvp.Key))
            {
                if (addComma)
                    sb.Append(",");
                addComma = true;
                sb.AppendFormat("\"{0}\":{1}", kvp.Key.Replace("\"", "\"\""), kvp.Value);
            }
            sb.Append("}");
            return sb.ToString();
        }
    }
}
