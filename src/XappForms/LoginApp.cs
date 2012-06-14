﻿using System;
using System.Collections.Generic;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms
{
    class LoginApp : IApp
    {
        class LoginUser
        {
            public User User { get; set; }
            //public int TabCount { get; set; }
        }

        readonly HashSet<LoginAppTab> iTabs = new HashSet<LoginAppTab>();
        readonly object iLock = new object();
        readonly Dictionary<string, LoginUser> iUsers = new Dictionary<string, LoginUser>();
        int iCounter;
        readonly UserList iUserList;
        readonly AppUrlDispatcher iUrlDispatcher;

        public LoginApp(UserList aUserList)
        {
            iUserList = aUserList;
            iUserList.Updated += OnUserListUpdated;
            iUrlDispatcher = new AppUrlDispatcher();
            iUrlDispatcher.MapPrefixToDirectory(new string[] { }, "login");
        }

        public void ServeWebRequest(IAppWebRequest aRequest)
        {
            iUrlDispatcher.ServeRequest(aRequest);
        }

        public IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser)
        {
            int id = iCounter++;
            var tab = new LoginAppTab(this, aTabProxy, id);
            lock (iLock)
            {
                iTabs.Add(tab);
                foreach (var user in iUsers.Values)
                {
                    tab.NewMessage(
                        new JsonObject
                        {
                            { "type", "user"},
                            { "userid", user.User.Id },
                            { "oldValue", JsonNull.Instance },
                            { "newValue", UserToJson(user.User) } });
                }
            }
            return tab;
        }

        public Dictionary<string, string> GetBrowserDiscriminationMappings()
        {
            return new Dictionary<string, string>{
                {"desktop", "desktop.html"},
                {"mobile", "mobile.html"},
                {"tablet", "tablet.html"},
                {"default", "desktop.html"}};
        }

        internal void RemoveTab(LoginAppTab aTab)
        {
            lock (iLock)
            {
                iTabs.Remove(aTab);
            }
        }

        void Broadcast(JsonValue aMessage)
        {
            List<LoginAppTab> tabs;
            lock (iLock)
            {
                tabs = new List<LoginAppTab>(iTabs);
            }
            foreach (var tab in tabs)
            {
                tab.NewMessage(aMessage);
            }
        }

        public void OnUserListUpdated(object aSender, EventArgs aEventArgs)
        {
            lock (iLock)
            {
                var args = (UserEventArgs)aEventArgs;
                foreach (var userChange in args.Changes)
                {
                    LoginUser chatUser;
                    if (!iUsers.TryGetValue(userChange.UserId, out chatUser) && userChange.NewValue != null)
                    {
                        chatUser = new LoginUser { User = userChange.NewValue };
                        iUsers[userChange.UserId] = chatUser;
                    }
                    Broadcast(
                        new JsonObject {
                            { "type", "user"},
                            { "userid", userChange.UserId },
                            { "oldValue", UserToJson(userChange.OldValue) },
                            { "newValue", UserToJson(userChange.NewValue) } });
                }
            }
        }

        internal static JsonValue UserToJson(User aUser)
        {
            if (aUser == null) return JsonNull.Instance;
            return new JsonObject {
                {"id", aUser.Id},
                {"displayName", aUser.DisplayName},
                {"iconUrl", aUser.IconUrl}};
        }
    }

    class LoginAppTab : IAppTab
    {
        readonly LoginApp iLoginApp;
        readonly IBrowserTabProxy iBrowserTabProxy;
        readonly int iId;

        /// <summary>
        /// Protects iUser.
        /// </summary>
        readonly object iLock = new object();
        string iUserId;

        public LoginAppTab(LoginApp aLoginApp, IBrowserTabProxy aBrowserTabProxy, int aId)
        {
            iLoginApp = aLoginApp;
            iBrowserTabProxy = aBrowserTabProxy;
            iId = aId;
        }

        public int Id
        {
            get { return iId; }
        }

        public string UserId { get { lock (iLock) { return iUserId; } } }

        public void ChangeUser(User aUser)
        {
            lock (iLock)
            {
                if (iUserId == aUser.Id)
                {
                    return;
                }
                iUserId = aUser.Id;
            }
            iBrowserTabProxy.Send(
                new JsonObject{
                    {"type","login"},
                    {"user", LoginApp.UserToJson(aUser)}});
        }

        public void Receive(JsonValue aJsonValue)
        {
            if (aJsonValue.IsObject)
            {
                JsonValue type = aJsonValue.Get("type");
                if (!type.IsString)
                {
                    Console.WriteLine("Bad message from client: {0}", aJsonValue);
                    return;
                }
                switch (type.AsString())
                {
                    case "user":
                        string userid = aJsonValue.Get("id").AsString();
                        iBrowserTabProxy.SwitchUser(userid);
                        break;
                }
            }
        }

        public void NewMessage(JsonValue aMessage)
        {
            Console.WriteLine("Send {0}", aMessage);
            iBrowserTabProxy.Send(aMessage);
        }

        public void TabClosed()
        {
            iLoginApp.RemoveTab(this);
        }
    }
}
