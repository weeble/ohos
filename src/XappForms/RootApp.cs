using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms
{
    class RootApp : IAppLayer0
    {
        readonly AppUrlDispatcher iUrlDispatcher;

        public RootApp()
        {
            iUrlDispatcher = new AppUrlDispatcher();
            iUrlDispatcher.MapPrefixToDirectory(new string[] { }, "chat");
        }

        public void ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder)
        {
            iUrlDispatcher.ServeRequest(aRequest, aResponder);
        }

        public IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser)
        {
            return new RootAppTab(this, aTabProxy);
        }

        public Dictionary<string, string> GetBrowserDiscriminationMappings()
        {
            return new Dictionary<string, string>{
                {"desktop", "desktop.html"},
                {"mobile", "mobile.html"},
                {"tablet", "tablet.html"},
                {"default", "desktop.html"}};
        }
    }

    class RootAppTab : IAppTab
    {
        //readonly RootApp iRootApp;
        //readonly IBrowserTabProxy iBrowserTabProxy;

        public RootAppTab(RootApp aRootApp, IBrowserTabProxy aBrowserTabProxy)
        {
            //iRootApp = aRootApp;
            //iBrowserTabProxy = aBrowserTabProxy;
        }

        public void ChangeUser(User aUser)
        {
        }

        public void Receive(JsonValue aMessage)
        {
        }

        public void TabClosed()
        {
        }
    }
}
