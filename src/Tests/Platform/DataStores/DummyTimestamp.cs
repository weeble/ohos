using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHome.Os.Platform.DataStores
{
    public class DummyTimestamp
    {
        private readonly string iId;
        private readonly HashSet<string> iPredecessors;

        public DummyTimestamp(string aId, string aPredecessors)
        {
            iId = aId;
            iPredecessors = new HashSet<string>(aPredecessors.Split(new[]{'-'}));
        }

        public DummyTimestamp(string aId, IEnumerable<string> aPredecessors)
        {
            iId = aId;
            iPredecessors = new HashSet<string>(aPredecessors);
        }

        public string Id
        {
            get { return iId; }
        }

        public HashSet<string> Predecessors
        {
            get { return iPredecessors; }
        }
        public override string ToString()
        {
            return String.Format("{0}--{1}", iId, String.Join("-", iPredecessors.OrderBy(aX => aX).ToArray()));
        }
        public static DummyTimestamp FromString(string aString)
        {
            string[] fragments = aString.Split(new[]{'-'});
            if (fragments.Length < 3) throw new Exception(String.Format("Bad timestamp [{0}]", aString));
            if (fragments[1] != "") throw new Exception("Bad timestamp");
            string id = fragments[0];
            if (fragments.Length==3 && fragments[2] == "")
            {
                return new DummyTimestamp(id, Enumerable.Empty<string>());
            }
            return new DummyTimestamp(id, fragments.Skip(2));

        }
    }
}