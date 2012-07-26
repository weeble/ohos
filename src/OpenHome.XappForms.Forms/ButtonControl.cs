using System;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
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
            var button = aTab.CreateControl(aId => new ButtonControl(aTab, aId));
            button.Text = aButtonText;
            return button;
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