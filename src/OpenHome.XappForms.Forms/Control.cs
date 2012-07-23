using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    public interface IXappFormsBrowserTab
    {
        void Send(JsonObject aJsonObject);
        T CreateControl<T>(Func<long, T> aControlFunc) where T:IControl;
        void DestroyControl(IControl aControl);
        IControl Root { get; set; }
    }

    public interface IControl
    {
        long Id { get; }
        string Class { get; }
        void Receive(JsonObject aMessage);
    }

    public class XappFormsBrowserTab : IXappFormsBrowserTab
    {
        IBrowserTabProxy iTabProxy;
        long iIdCounter = 0;
        Dictionary<long, IControl> iControls = new Dictionary<long, IControl>();
        SlottedControlContainer iRootContainer = new SlottedControlContainer();


        public XappFormsBrowserTab(IBrowserTabProxy aTabProxy)
        {
            iTabProxy = aTabProxy;
            iRootControl = new RootContainer { BrowserTab = this };
        }

        public void Send(JsonObject aJsonObject)
        {
            iTabProxy.Send(aJsonObject);
        }

        public long GenerateId()
        {
            iIdCounter += 1;
            return iIdCounter;
        }

        class RootControl : IControl
        {
            public long Id { get { return 0; } }
            public string Class { get { return "root"; } }
            public void Receive(JsonObject aMessage) { }
        }

        private class RootContainer : IInternalControl
        {
            public long Id { get { return 0; } }
            public string Class { get { return "root"; } }
            public void Receive(JsonObject aMessage) { }
            public IXappFormsBrowserTab BrowserTab { get; set; }
        }
        RootContainer iRootControl;
        IControl iRoot;
        public IControl Root { get { return iRoot; } set { iRootContainer.SetSlot(iRootControl, "root", value); iRoot = value; } }

        public void Receive(JsonValue aValue)
        {
            if (!aValue.IsObject) return;
            long controlId = aValue.Get("control").AsLong();
            IControl control;
            if (iControls.TryGetValue(controlId, out control))
            {
                control.Receive((JsonObject)aValue);
            }
        }

        public T CreateControl<T>(Func<long, T> aControlFunc) where T : IControl
        {
            long controlId = GenerateId();
            T control = aControlFunc(controlId);
            iTabProxy.Send(new JsonObject { { "type", "xf-create" }, { "class", control.Class }, { "control", controlId } });
            iControls[controlId] = control;
            return control;
        }

        public void DestroyControl(IControl aControl)
        {
            long controlId = aControl.Id;
            iControls.Remove(controlId);
            iTabProxy.Send(new JsonObject { { "type", "xf-destroy" }, { "control", controlId } });
        }
    }


    public interface IInternalControl : IControl
    {
        IXappFormsBrowserTab BrowserTab { get; }
    }

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

    struct Slice
    {
        int iStart;
        int iEnd;
        public Slice(int aStart, int aEnd)
        {
            iStart = aStart;
            iEnd = aEnd;
        }
        public int Start { get { return iStart; } }
        public int End { get { return iEnd; } }
        public int Count { get { return iEnd-iStart; } }
        public Slice MakeAbsolute(int aCount)
        {
            var start = iStart;
            var end = iEnd;
            if (start < 0) { start += aCount; }
            if (start < 0) { start = 0; }
            if (end < 0) { end += aCount; }
            if (end < start) { end = start; }
            if (end >= aCount) { end = aCount; }
            if (start >= end) { start = end; }
            return new Slice(start, end);
        }
        public static Slice BeforeStart { get { return new Slice(0,0); } }
        public static Slice AfterEnd { get { return new Slice(int.MaxValue,int.MaxValue); } }
        public static Slice All { get { return new Slice(0, int.MaxValue); } }
        public static Slice Single(int aIndex) { return new Slice(aIndex, aIndex+1); }
    }

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

    abstract class Control : IControl, IInternalControl, IDisposable
    {
        readonly IXappFormsBrowserTab iTab;
        public long Id { get; private set; }
        public Control(IXappFormsBrowserTab aTab, long aId)
        {
            iTab = aTab;
            Id = aId;
        }
        public abstract string Class { get; }
        public IXappFormsBrowserTab BrowserTab { get { return iTab; } }
        public virtual void Receive(JsonObject aObject)
        {
        }
        bool iDisposed;
        public void Dispose()
        {
            if (!iDisposed)
            {
                iDisposed = true;
                iTab.DestroyControl(this);
            }
        }
    }

    class GridControl : Control
    {
        readonly SlottedControlContainer iSlots = new SlottedControlContainer();
        public GridControl(IXappFormsBrowserTab aTab, long aId) : base(aTab, aId)
        {
        }

        public static GridControl Create(IXappFormsBrowserTab aTab)
        {
            return aTab.CreateControl(aId => new GridControl(aTab, aId));
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
        public IControl TopLeft { get { return iSlots.GetSlot("topleft"); } set { iSlots.SetSlot(this, "topleft", value); } }
        public IControl TopRight { get { return iSlots.GetSlot("topright"); } set { iSlots.SetSlot(this, "topright", value); } }
        public IControl BottomLeft { get { return iSlots.GetSlot("bottomleft"); } set { iSlots.SetSlot(this, "bottomleft", value); } }
        public IControl BottomRight { get { return iSlots.GetSlot("bottomright"); } set { iSlots.SetSlot(this, "bottomright", value); } }

        public override string Class
        {
            get { return "grid"; }
        }
    }

    class ButtonControl : Control
    {
        JsonProperties iProperties = new JsonProperties();
        EventCollection iEventHandlers = new EventCollection();
        public ButtonControl(IXappFormsBrowserTab aTab, long aId) : base(aTab, aId)
        {
        }

        public override void Receive(JsonObject aObject)
        {
            iEventHandlers.Receive(aObject);
        }

        public static ButtonControl Create(IXappFormsBrowserTab aTab)
        {
            return aTab.CreateControl(aId => new ButtonControl(aTab, aId));
        }

        public static ButtonControl Create(IXappFormsBrowserTab aTab, string aButtonText)
        {
            return aTab.CreateControl(aId => new ButtonControl(aTab, aId) { Text = aButtonText });
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
            add { iEventHandlers.SubscribeEventHandler(this, "click", value); }
            remove { iEventHandlers.UnsubscribeEventHandler(this, "click", value); }
        }

        public string Text {
            get { return iProperties.GetProperty("text").AsString(); }
            set { iProperties.SetProperty(this, "text", new JsonString(value)); }
        }

        public override string Class
        {
            get { return "button"; }
        }
    }
}
