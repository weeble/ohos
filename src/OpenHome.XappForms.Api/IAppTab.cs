using OpenHome.XappForms.Json;

namespace OpenHome.XappForms
{
    public interface IAppTab
    {
        void ChangeUser(User aUser);
        void Receive(JsonValue aJsonValue);
        void TabClosed();
    }
}