using System;
using System.Linq;
using System.Text;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
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


    class TextboxControl : Control
    {
        JsonProperties iProperties = new JsonProperties();
        EventCollection iEventHandlers = new EventCollection();
        public TextboxControl(IXappFormsBrowserTab aTab, long aId)
            : base(aTab, aId)
        {
        }

        public override void Receive(JsonObject aObject)
        {
            iEventHandlers.Receive(aObject);
        }

        public static TextboxControl Create(IXappFormsBrowserTab aTab)
        {
            return aTab.CreateControl(aId => new TextboxControl(aTab, aId));
        }

        public static TextboxControl Create(IXappFormsBrowserTab aTab, string aText)
        {
            var tb = aTab.CreateControl(aId => new TextboxControl(aTab, aId));
            tb.Text = aText;
            return tb;
        }

        public event EventHandler KeyPress
        {
            add { iEventHandlers.SubscribeEventHandler(this, "keypress", value); }
            remove { iEventHandlers.UnsubscribeEventHandler(this, "keypress", value); }
        }

        public static string HtmlTemplate
        {
            get
            {
                return
                    @"<input type='textbox' id='xf-textbox' />";
            }
        }

        public string Text
        {
            get { return iProperties.GetProperty("text").AsString(); }
            set { iProperties.SetProperty(this, "text", new JsonString(value)); }
        }

        public override string Class
        {
            get { return "textbox"; }
        }
    }
}
