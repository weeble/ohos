namespace OpenHome.XappForms
{
    public interface IXapp
    {
        void ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder);
        IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser);
    }
}