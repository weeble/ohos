namespace OpenHome.XappForms
{
    public interface IRawXapp
    {
        bool ServeWebRequest(RawRequestData aRawRequest, IWebRequestResponder aResponder);
        IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser);
    }

    public interface IXapp
    {
        bool ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder);
        IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser);
    }
}