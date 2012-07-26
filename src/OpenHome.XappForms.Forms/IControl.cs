using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    public interface IControl
    {
        long Id { get; }
        string Class { get; }
        void Receive(JsonObject aMessage);
    }
}