using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHome.Os.Platform.Collections
{
    public static class ListUtils
    {
        public static void MultiInsert<T>(List<T> aTargetList, List<int> aIndexes, List<T> aValues)
        {
            VerifyIndexes(aIndexes, aTargetList.Count, aValues.Count);
            // aIndexes must be in order!
            int oldListCount = aTargetList.Count;
            int newItemCount = aIndexes.Count;
            aTargetList.AddRange(Enumerable.Repeat(default(T), newItemCount));
            
            int inserted = 0;
            int sourceSlot = oldListCount - 1;
            for (int targetSlot = aTargetList.Count - 1; inserted != newItemCount ; --targetSlot)
            {
                if (sourceSlot == aIndexes[newItemCount - inserted - 1] - 1)
                {
                    aTargetList[targetSlot] = aValues[newItemCount - inserted - 1];
                    inserted += 1;
                }
                else
                {
                    aTargetList[targetSlot] = aTargetList[sourceSlot];
                    sourceSlot -= 1;
                }
            }
        }

        private static void VerifyIndexes(List<int> aIndexes, int aTargetListCount, int aValuesCount)
        {
            if (aIndexes.Count != aValuesCount)
            {
                throw new ArgumentException("aIndexes must have same size as list of values to insert.");
            }
            int previous = -1;
            foreach (int index in aIndexes)
            {
                if (index < 0 || index > aTargetListCount || index < previous)
                {
                    throw new ArgumentException("aIndexes must be non-decreasing and have indexes 0 <= index <= length of target list.");
                }
                previous = index;
            }
        }
    }
}
