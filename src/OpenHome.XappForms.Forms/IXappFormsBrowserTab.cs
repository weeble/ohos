using System;
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
}