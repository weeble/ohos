using System.Collections.Generic;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    public class JsonProperties
    {
        readonly Dictionary<string, JsonValue> iProperties = new Dictionary<string, JsonValue>();
        public JsonValue GetProperty(string aName)
        {
            return iProperties[aName];
        }
        public void SetProperty(IInternalControl aThisControl, string aName, JsonValue aValue)
        {
            iProperties[aName] = aValue;
            aThisControl.BrowserTab.Send(new JsonObject { { "type", "xf-set-property" }, { "control", aThisControl.Id }, { "property", aName }, { "value", aValue } });
        }
    }
}