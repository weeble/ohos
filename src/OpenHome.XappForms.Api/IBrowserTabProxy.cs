using OpenHome.XappForms.Json;

namespace OpenHome.XappForms
{
    public interface IBrowserTabProxy
    {
        void Send(JsonValue aJsonValue);
        void SwitchUser(string aUserId);
        string SessionId { get; }
        string TabId { get; }
        void SetCookie(string aName, string aValue, CookieAttributes aAttributes);
        void ReloadPage();
    }
}