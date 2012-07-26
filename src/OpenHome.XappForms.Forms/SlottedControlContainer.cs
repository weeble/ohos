using System.Collections.Generic;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    class SlottedControlContainer
    {
        Dictionary<string, IControl> iSlots = new Dictionary<string, IControl>();
        public IControl GetSlot(string aName)
        {
            return iSlots[aName];
        }
        public void SetSlot(IInternalControl aThisControl, string aSlot, IControl aChild)
        {
            iSlots[aSlot] = aChild;
            aThisControl.BrowserTab.Send(new JsonObject { { "type", "xf-bind-slot" }, { "control", aThisControl.Id }, {"slot", aSlot}, { "child", aChild.Id } });
        }
    }
}