namespace OpenHome.XappForms
{
    //class ServerUrlDispatcher : UrlDispatcher<IServerWebRequestResponder> { }
    class ServerPathDispatcher : PathDispatcher<RawRequestData, IServerWebRequestResponder> { }
}