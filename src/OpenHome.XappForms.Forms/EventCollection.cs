using System;
using System.Collections.Generic;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    public class EventCollection
    {
        readonly Dictionary<string, EventHandler> iEventHandlers = new Dictionary<string, EventHandler>();
        public void SubscribeEventHandler(IInternalControl aThisControl, string aEventName, EventHandler aHandler)
        {
            EventHandler handler;
            iEventHandlers.TryGetValue(aEventName, out handler);
            if (handler == null)
            {
                aThisControl.BrowserTab.Send(new JsonObject { { "type", "xf-subscribe" }, { "control", aThisControl.Id }, { "event", aEventName } });
            }
            handler += aHandler;
            iEventHandlers[aEventName] = handler;
        }
        public void UnsubscribeEventHandler(IInternalControl aThisControl, string aEventName, EventHandler aHandler)
        {
            var handler = iEventHandlers[aEventName];
            handler -= aHandler;
            iEventHandlers[aEventName] = handler;
            if (handler == null)
            {
                aThisControl.BrowserTab.Send(new JsonObject { { "type", "xf-unsubscribe" }, { "control", aThisControl.Id }, { "event", aEventName } });
            }
        }
        public bool Receive(JsonValue aMessage)
        {
            if (!aMessage.IsObject) return false;
            var type = aMessage.Get("type");
            if (!type.IsString || type.AsString() != "xf-event") return false;
            var ev = aMessage.Get("event");
            if (!ev.IsString) return false;
            EventHandler handler;
            if (!iEventHandlers.TryGetValue(ev.AsString(), out handler)) return false;
            handler(this, EventArgs.Empty);
            return true;
        }
    }
}