using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{

    class XappFormsBrowserTab
    {
        IBrowserTabProxy iTabProxy;
        long iIdCounter = 0;
        Dictionary<long, Control> iControls = new Dictionary<long, Control>();

        public XappFormsBrowserTab(IBrowserTabProxy aTabProxy)
        {
            iTabProxy = aTabProxy;
        }

        public void Send(JsonValue aJsonValue)
        {
            iTabProxy.Send(new JsonObject { { "type", "xf-custom" }, { "value", aJsonValue } });
        }

        public long GenerateId()
        {
            iIdCounter += 1;
            return iIdCounter;
        }

        public void SetProperty(long aId, string aName, JsonValue aValue)
        {
            iTabProxy.Send(new JsonObject { { "type", "xf-property" }, { "control", aId }, { "property", aName }, { "value", aValue } });
        }

        public void SetSlot(long aId, string aName, long aChildId)
        {
            iTabProxy.Send(new JsonObject { { "type", "xf-slot" }, { "parent", aId }, { "slot", aName }, { "child", aChildId } });
        }

        public long CreateControl(string aClassName)
        {
            long controlId = GenerateId();
            iTabProxy.Send(new JsonObject { { "type", "xf-control" }, { "class", aClassName }, { "id", controlId } });
            return controlId;
        }

        public void RegisterControl(Control aControl)
        {
            iControls[aControl.Id] = aControl;
        }

        public void Subscribe(long aId, string aEventName)
        {
            iTabProxy.Send(new JsonObject { { "type", "xf-subscribe" }, { "control", aId }, { "event", aEventName } });
        }

        public void Unsubscribe(long aId, string aEventName)
        {
            iTabProxy.Send(new JsonObject { { "type", "xf-unsubscribe" }, { "control", aId }, { "event", aEventName } });
        }

        public void Receive(JsonValue aValue)
        {
            string messageType = aValue.Get("type").AsString();
            switch (messageType)
            {
                case "xf-event":
                    long controlId = aValue.Get("control").AsLong();
                    string eventName = aValue.Get("event").AsString();
                    JsonValue eventObject = aValue.Get("object");
                    Control control;
                    if (iControls.TryGetValue(controlId, out control))
                    {
                        control.HandleEvent(eventName, eventObject);
                    }
                    break;
            }
        }

        public void SetRoot(Control aControl)
        {
            SetSlot(0, "root", aControl.Id);
        }
    }
    class Control
    {
        readonly XappFormsBrowserTab iTab;
        public long Id { get; private set; }
        Control Parent { get; set; }
        bool Placed { get { return Parent != null; } }
        Dictionary<string, Control> iSlots = new Dictionary<string, Control>();
        Dictionary<string, JsonValue> iProperties = new Dictionary<string, JsonValue>();
        Dictionary<string, EventHandler> iEventHandlers = new Dictionary<string, EventHandler>();
        public Control(XappFormsBrowserTab aTab, string aClass)
        {
            iTab = aTab;
            // TODO: Move nasty object-graph assembly into factory.
            Id = iTab.CreateControl(aClass);
            iTab.RegisterControl(this);
        }
        void Eject()
        {
            Parent = null;
        }
        void Emplace(Control aParent)
        {
            if (aParent == null) throw new ArgumentNullException("aParent");
            if (aParent.iTab != iTab) throw new ArgumentException("Parent and child elements must belong to same browser tab.");
            Parent = aParent;
        }
        protected Control GetSlot(string aName)
        {
            return iSlots[aName];
        }
        protected void SetSlot(string aName, Control aControl)
        {
            if (aControl.Placed)
            {
                throw new Exception("Control is already placed.");
            }
            if (iSlots.ContainsKey(aName))
            {
                iSlots[aName].Eject();
            }
            aControl.Emplace(this);
            iSlots[aName] = aControl;
            iTab.SetSlot(Id, aName, aControl.Id);
        }
        protected JsonValue GetProperty(string aName)
        {
            return iProperties[aName];
        }
        protected void SetProperty(string aName, JsonValue aValue)
        {
            iProperties[aName] = aValue;
            iTab.SetProperty(Id, aName, aValue);
        }
        protected void Send(JsonValue aJsonValue)
        {
            iTab.Send(aJsonValue);
        }
        protected void SubscribeEventHandler(string aEventName, EventHandler aHandler)
        {
            EventHandler handler;
            iEventHandlers.TryGetValue(aEventName, out handler);
            if (handler == null)
            {
                iTab.Subscribe(Id, aEventName);
            }
            handler += aHandler;
            iEventHandlers[aEventName] = handler;
            //var eventId = iTab.GenerateId();
        }
        protected void UnsubscribeEventHandler(string aEventName, EventHandler aHandler)
        {
            var handler = iEventHandlers[aEventName];
            handler -= aHandler;
            iEventHandlers[aEventName] = handler;
            if (handler == null)
            {
                iTab.Unsubscribe(Id, aEventName);
            }
        }
        public void HandleEvent(string aEventName, JsonValue aEventObject)
        {
            //TODO: Pass on event object.
            iEventHandlers[aEventName](this, EventArgs.Empty);
        }
    }

    class GridControl : Control
    {
        public GridControl(XappFormsBrowserTab aTab) : base(aTab, "grid")
        {
        }

        public static string HtmlTemplate
        {
            get
            {
                return
                    @"<table id='xf-grid'>" +
                        @"<tr>" +
                            @"<td><span class='xfslot-topleft'></span></td>" +
                            @"<td><span class='xfslot-topright'></span></td>" +
                        @"</tr><tr>" +
                            @"<td><span class='xfslot-bottomleft'></span></td>" +
                            @"<td><span class='xfslot-bottomright'></span></td>" +
                        @"</tr>" +
                    @"</table>";
            }
        }
        public Control TopLeft { get { return GetSlot("topleft"); } set { SetSlot("topleft", value); } }
        public Control TopRight { get { return GetSlot("topright"); } set { SetSlot("topright", value); } }
        public Control BottomLeft { get { return GetSlot("bottomleft"); } set { SetSlot("bottomleft", value); } }
        public Control BottomRight { get { return GetSlot("bottomright"); } set { SetSlot("bottomright", value); } }
    }

    class ButtonControl : Control
    {
        public ButtonControl(XappFormsBrowserTab aTab) : base(aTab, "button")
        {
        }

        public static string HtmlTemplate
        {
            get
            {
                return
                    @"<button id='xf-button'>Click me</span></button>";
            }
        }

        public event EventHandler Clicked
        {
            add { SubscribeEventHandler("click", value); }
            remove { UnsubscribeEventHandler("click", value); }
        }

        public string Text {
            get { return GetProperty("text").AsString(); }
            set { SetProperty("text", new JsonString(value)); }
        }
    }
}
