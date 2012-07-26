using System.Collections.Generic;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    class IndexedControlContainer
    {
        readonly List<IControl> iChildren = new List<IControl>();
        public IControl Get(int aIndex)
        {
            return iChildren[aIndex];
        }
        public List<IControl> Get(Slice aSlice)
        {
            var slice = aSlice.MakeAbsolute(iChildren.Count);
            return iChildren.GetRange(slice.Start, slice.Count);
        }
        public void Set(IInternalControl aThisControl, int aIndex, IControl aChild)
        {
            Set(aThisControl, Slice.Single(aIndex), new List<IControl>{aChild});
        }
        public void Set(IInternalControl aThisControl, Slice aSlice, List<IControl> aChildren)
        {
            var slice = aSlice.MakeAbsolute(iChildren.Count);
            if (slice.Count > 0)
            {
                iChildren.RemoveRange(slice.Start, slice.Count);
            }
            var childIds = new JsonArray();
            if (aChildren != null && aChildren.Count > 0)
            {
                iChildren.InsertRange(slice.Start, aChildren);
                foreach (var child in aChildren)
                {
                    childIds.Add(child.Id);
                }
            }
            aThisControl.BrowserTab.Send(
                new JsonObject {
                    { "type", "xf-bind-slice" },
                    { "start", slice.Start },
                    { "end", slice.End },
                    { "control", aThisControl.Id },
                    { "children", childIds } });
        }
    }
}