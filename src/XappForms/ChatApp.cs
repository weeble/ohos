﻿using System;
using System.Collections.Generic;
using OpenHome.XappForms.Json;
using System.Linq;

namespace OpenHome.XappForms
{
    class ChatApp : IAppLayer0
    {
        class ChatUser
        {
            public User User { get; set; }
            public int TabCount { get; set; }
        }

        HashSet<ChatAppTab> iTabs = new HashSet<ChatAppTab>();
        object iLock = new object();
        Dictionary<string, ChatUser> iUsers = new Dictionary<string, ChatUser>();
        int counter = 0;
        UserList iUserList;
        AppUrlDispatcher iUrlDispatcher;

        public ChatApp(UserList aUserList)
        {
            iUserList = aUserList;
            iUserList.Updated += OnUserListUpdated;
            iUrlDispatcher = new AppUrlDispatcher();
            iUrlDispatcher.MapPrefixToDirectory(new string[] { }, "chat");
        }

        public void ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder)
        {
            iUrlDispatcher.ServeRequest(aRequest, aResponder);
        }

        public IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser)
        {
            int id = counter++;
            var tab = new ChatAppTab(this, aTabProxy, id, iUserList);
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
                            { "newValue", UserToJson(user.User, user.TabCount > 0 ? "online" : "offline") } });
                }
            }
            Broadcast(
                new JsonObject{
                    {"type", "connect"},
                    {"sender", id.ToString()},
                });
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

        internal void RemoveTab(ChatAppTab aTab)
        {
            lock (iLock)
            {
                iTabs.Remove(aTab);
            }
            Broadcast(
                new JsonObject{
                    {"type", "disconnect"},
                    {"sender", aTab.Id.ToString()},
                });
            RecountUsers();
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
            List<ChatAppTab> tabs;
            lock (iLock)
            {
                tabs = new List<ChatAppTab>(iTabs);
            }
            foreach (var tab in tabs)
            {
                tab.NewMessage(aMessage);
            }
        }

        public void RecountUsers()
        {
            lock (iLock)
            {
                Dictionary<string, int> newUserCounts = iUsers.Keys.ToDictionary(aUserId=>aUserId, aUserId => 0);
                foreach (var tab in iTabs)
                {
                    string userid = tab.UserId;
                    if (string.IsNullOrEmpty(userid)) continue;
                    int oldcount;
                    if (!newUserCounts.TryGetValue(userid, out oldcount))
                    {
                        oldcount = 0;
                    }
                    newUserCounts[userid] = oldcount + 1;
                }
                foreach (var kvp in newUserCounts)
                {
                    ChatUser user;
                    if (!iUsers.TryGetValue(kvp.Key, out user))
                    {
                        continue;
                    }
                    bool wasOnline = user.TabCount > 0;
                    bool isOnline = kvp.Value > 0;

                    user.TabCount = kvp.Value;
                    
                    Console.WriteLine("USER: {0} TABS: {1} {2}->{3}", kvp.Key, kvp.Value, wasOnline, isOnline);
                    
                    if (isOnline != wasOnline)
                    {
                        // TODO: Avoid recursive lock.
                        Broadcast(
                            new JsonObject {
                                { "type", "user"},
                                { "userid", kvp.Key },
                                { "oldValue", UserToJson(user.User, wasOnline ? "online" : "offline") },
                                { "newValue", UserToJson(user.User, isOnline ? "online" : "offline") } });
                    }
                }
            }
        }

        public void OnUserListUpdated(object aSender, EventArgs aEventArgs)
        {
            lock (iLock)
            {
                var args = (UserEventArgs)aEventArgs;
                //Console.WriteLine("A: {0} B: {1}", args.Changes.Count(), args.SubscriptionEnded);
                foreach (var userChange in args.Changes)
                {
                    ChatUser chatUser;
                    if (!iUsers.TryGetValue(userChange.UserId, out chatUser) && userChange.NewValue != null)
                    {
                        chatUser = new ChatUser { User = userChange.NewValue, TabCount = 0 };
                        iUsers[userChange.UserId] = chatUser;
                    }
                    string status = chatUser == null && chatUser.TabCount > 0 ? "online" : "offline";
                    Broadcast(
                        new JsonObject {
                            { "type", "user"},
                            { "userid", userChange.UserId },
                            { "oldValue", UserToJson(userChange.OldValue, status) },
                            { "newValue", UserToJson(userChange.NewValue, status) } });
                }
            }
        }

        internal static JsonValue UserToJson(User aUser, string aStatus)
        {
            if (aUser == null) return JsonNull.Instance;
            return new JsonObject {
                {"id", aUser.Id},
                {"displayName", aUser.DisplayName},
                {"iconUrl", aUser.IconUrl},
                {"status", aStatus}};
        }
    }

    class ChatAppTab : IAppTab
    {
        readonly ChatApp iChatApp;
        readonly IBrowserTabProxy iBrowserTabProxy;
        readonly int iId;
        //readonly UserList iUserList;

        /// <summary>
        /// Protects iUser.
        /// </summary>
        object iLock = new object();
        string iUserId;

        public ChatAppTab(ChatApp aChatApp, IBrowserTabProxy aBrowserTabProxy, int aId, UserList aUserList)
        {
            iChatApp = aChatApp;
            iBrowserTabProxy = aBrowserTabProxy;
            iId = aId;
            //iUserList = aUserList;
        }




        public int Id
        {
            get { return iId; }
        }

        /*
        void ChangeUser(string aUserId)
        {
            lock (iLock)
            {
                if (aUserId == iUserId)
                {
                    return;
                }
                iUserId = aUserId;
            }
            iChatApp.RecountUsers();
        }*/

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
            iChatApp.RecountUsers();
            iBrowserTabProxy.Send(
                new JsonObject{
                    {"type","login"},
                    {"user", ChatApp.UserToJson(aUser, "online")}});
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
                        iChatApp.NewMessage(userid, aJsonValue.Get("content").AsString());
                        break;
                    case "user":
                        userid = aJsonValue.Get("id").AsString();
                        iBrowserTabProxy.SwitchUser(userid);
                        /*
                        User user;
                        if (!iUserList.TryGetUserById(userid, out user))
                        {
                            Console.WriteLine("Bad user id from client: {0}", userid);
                        }*/
                        //ChangeUser(userid);
                        break;
                }
            } else if (aJsonValue.IsString)
            {
                // Legacy.
                iChatApp.NewMessage(iUserId, aJsonValue.AsString());
            }
        }

        public void NewMessage(JsonValue aMessage)
        {
            Console.WriteLine("Send {0}", aMessage.ToString());
            iBrowserTabProxy.Send(aMessage);
        }

        public void TabClosed()
        {
            iChatApp.RemoveTab(this);
        }
    }
}
