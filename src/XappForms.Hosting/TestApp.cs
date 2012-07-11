using System;
using System.Collections.Generic;
using OpenHome.XappForms.Json;
using System.Linq;

namespace OpenHome.XappForms
{
    class TestApp : IXapp
    {
        HashSet<TestAppTab> iTabs = new HashSet<TestAppTab>();
        object iLock = new object();
        int counter = 0;
        AppPathDispatcher iPathDispatcher;

        public TestApp()
        {
            iPathDispatcher = new AppPathDispatcher();
            iPathDispatcher.MapPrefixToDirectory(new string[] { }, "test");
        }

        public bool ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder)
        {
            return iPathDispatcher.ServeRequest(aRequest, aResponder);
        }

        public IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser)
        {
            int id = counter++;

            var tab = new TestAppTab(this, aTabProxy, id);
            lock (iLock)
            {
                iTabs.Add(tab);
            }
            return tab;
        }

        public Dictionary<string, string> GetBrowserDiscriminationMappings()
        {
            return new Dictionary<string, string>{
                {"desktop","desktop.html"},
                {"mobile","mobile.html"},
                {"tablet","tablet.html"},
                {"default","desktop.html"}
            };
        }

        internal void RemoveTab(TestAppTab aTab)
        {
            lock (iLock)
            {
                iTabs.Remove(aTab);
            }
        }

        internal void NewMessage(string aUserId, string aS)
        {
            Broadcast(
                new JsonObject{
                    {"type", "message"},
                    {"sender", aUserId==null ? (JsonValue)JsonNull.Instance : new JsonString(aUserId)},
                    {"content", aS},
                });
        }

        void Broadcast(JsonValue aMessage)
        {
            List<TestAppTab> tabs;
            lock (iLock)
            {
                tabs = new List<TestAppTab>(iTabs);
            }
            foreach (var tab in tabs)
            {
                tab.NewMessage(aMessage);
            }
        }
    }

    class TestAppTab : IAppTab
    {
        readonly TestApp iTestApp;
        readonly IBrowserTabProxy iBrowserTabProxy;
        readonly int iId;

        /// <summary>
        /// Protects iUser.
        /// </summary>
        object iLock = new object();
        string iUserId;

        public TestAppTab(TestApp aTestApp, IBrowserTabProxy aBrowserTabProxy, int aId)
        {
            iTestApp = aTestApp;
            iBrowserTabProxy = aBrowserTabProxy;
            iId = aId;
            iUserId = "guest";
        }


        public int Id
        {
            get { return iId; }
        }

        public void ChangeUser(User aUser)
        {
        }

        public void Receive(JsonValue aJsonValue)
        {
            if (aJsonValue.IsObject)
            {
                JsonValue aType = aJsonValue.Get("type");
                if (!aType.IsString)
                {
                    Console.WriteLine("Bad message from client: {0}", aJsonValue);
                    return;
                }
                string userid;
                switch (aType.AsString())
                {
                    case "message":
                        lock (iLock)
                        {
                            userid = iUserId;
                        }
                        iTestApp.NewMessage(userid, aJsonValue.Get("content").AsString());
                        break;
                }
            } else if (aJsonValue.IsString)
            {
                // Legacy.
                iTestApp.NewMessage(iUserId, aJsonValue.AsString());
            }
        }

        public void NewMessage(JsonValue aMessage)
        {
            Console.WriteLine("Send {0}", aMessage.ToString());
            iBrowserTabProxy.Send(aMessage);
        }

        public void TabClosed()
        {
            iTestApp.RemoveTab(this);
        }
    }
}
