using System;
using System.Collections.Generic;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
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
}