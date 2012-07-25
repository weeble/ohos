using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using OpenHome.Net.Device;
using OpenHome.Os.Platform;
using OpenHome.XappForms.Json;

namespace OpenHome.XappForms.Forms
{
    [Export(typeof(IApp))]
    class App : IApp
    {
        FormsApp iXapp;
        public void Dispose()
        {
        }

        public bool PublishesNodeServices
        {
            get { return false; }
        }

        public IResourceManager ResourceManager
        {
            get { return null; }
        }

        public AppVersion Version
        {
            get { return new AppVersion(0, 0, 0); }
        }

        public string IconUri
        {
            // TODO: Sort out the port here.
            get { return "/forms/appicon.png"; }
        }

        public string DescriptionUri
        {
            get { return "http://invalid/"; }
        }

        public void Start(IAppContext aAppContext)
        {
            iXapp = new FormsApp(aAppContext.Services.UserList, Path.Combine(aAppContext.StaticPath, "http"));
            aAppContext.PublishXapp("forms", iXapp);
            aAppContext.Device.SetAttribute("Upnp.PresentationUrl", ":12921/forms/"); // TODO: Remove need for hard-coded port.
        }

        public void Stop()
        {
            // TODO: Stop the app again.
        }
    }

    class FormsApp : IXapp
    {
        /*class FormsUser
        {
            public User User { get; set; }
            public int TabCount { get; set; }
        }*/

        HashSet<FormsAppTab> iTabs = new HashSet<FormsAppTab>();
        object iLock = new object();
        //Dictionary<string, FormsUser> iUsers = new Dictionary<string, FormsUser>();
        int counter = 0;
        UserList iUserList;
        readonly string iHttpDirectory;
        AppPathDispatcher iUrlDispatcher;

        const string IndexTemplate =
@"<!DOCTYPE html>
<html lang='en'>
<head>
    <!--<script src='../scripts/browser.js'></script>-->
    <link href='/ohj/scroller/ohj.scroller.pointer.css' rel='stylesheet'>
    <link href='/theme/default/bootstrap.min.css' rel='stylesheet'>
    <link href='css/main.css' rel='stylesheet'>
    <link href='/ohj/ohj.ui.css' rel='stylesheet'>
    <link href='/ohj/list/ohj.list.css' rel='stylesheet'>

    <style>
    
    .nano > .pane > .slider
    {{
        background-color:#BBB;
    }}
    </style>
</head>
<body data-appname='forms'>
    <span id='root' class='xfslot-root'></span>
    <div class='templates' style='display:none'>{0}</div>
    <script src='../scripts/lib/jquery-1.7.2.min.js'></script>
    <script src='../scripts/xapp.js'></script>
    <script src='../ohj/ohj.util.js'></script>
    <script src='../ohj/ohj.ui.js'></script>
    <script src='../ohj/ohj.ui.bridge.js'></script>
    <script src='../ohj/navbar/ohj.navbar.js'></script>
    <script src='../ohj/scroller/ohj.scroller.js'></script>
    <script src='../ohj/scroller/ohj.scroller.touch.js'></script>
    <script src='../ohj/scroller/ohj.scroller.pointer.js'></script>
    <script src='../ohj/page/ohj.page.js'></script>
    <script src='../ohj/contentslider/ohj.contentslider.js'></script> 
    <script src='../ohj/button/ohj.button.js'></script>
    <script src='../ohj/grid/ohj.grid.js'></script>
    <script src='../ohj/textbox/ohj.textbox.js'></script>
    <script src='../ohj/list/ohj.list.js'></script>
    <script src='js/common.js'></script>
    <script src='js/desktop.js'></script>

    <script type='text/html' id='tplUser'>
    <li id='usr_<#= id #>'>
        <img src='<#= avatar #>' />
        <h3><#= name #></h3>
        <span class='ohjlist-info ohjlist-info-top '><span class='status label <#= (status==='ONLINE' ? 'label-success' : '') #>'><#= status #></span></span>
    </li>
    </script>

    <script type='text/html' id='tplMessage'>
     <div class='chatmessage clearfix'>
        <div class='chatmessage-avatar'>
                <img src='<#= avatar #>' />
            <p class='ellipsis'><strong><#= name #></strong></p>
        </div>
        <div class='chatmessage-arrow'></div>
        <div class='message'>
            <#= message #>
        </div>
    </div>
    </script>
</body>
</html>";

        string GenerateHtml()
        {
            string controlFragments = GridControl.HtmlTemplate + ButtonControl.HtmlTemplate;
            return String.Format(IndexTemplate, controlFragments);
        }

        public FormsApp(UserList aUserList, string aHttpDirectory)
        {
            iUserList = aUserList;
            iHttpDirectory = aHttpDirectory;
            //iUserList.Updated += OnUserListUpdated;
            iUrlDispatcher = new AppPathDispatcher();
            iUrlDispatcher.MapPath( new string[] { }, ServeAppHtml);
            iUrlDispatcher.MapPrefixToDirectory(new string[] { }, aHttpDirectory);
        }

        string GetPath(string aFilename)
        {
            return Path.Combine(iHttpDirectory, aFilename);
        }

        bool ServeAppHtml(RequestData aRequest, IWebRequestResponder aResponder)
        {
            //return GenerateHtml();
            aResponder.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Html, GenerateHtml()));
            return true;
            /*string browser = aRequest.BrowserClass;
            string filename = GetBrowserDiscriminationMappings()[browser];
            aResponder.SendFile(GetPath(filename));
            return true;*/
        }

        public bool ServeWebRequest(RequestData aRequest, IWebRequestResponder aResponder)
        {
            //Console.WriteLine("Serving {0} from forms app.", aRequest.Path.OriginalUri);
            return iUrlDispatcher.ServeRequest(aRequest, aResponder);
        }

        public IAppTab CreateTab(IBrowserTabProxy aTabProxy, User aUser)
        {
            int id = counter++;
            var tab = new FormsAppTab(this, aTabProxy, id, iUserList, aUser == null ? null : aUser.Id);
            lock (iLock)
            {
                iTabs.Add(tab);
                /*foreach (var user in iUsers.Values)
                {
                    tab.NewMessage(
                        new JsonObject
                        {
                            { "type", "user"},
                            { "userid", user.User.Id },
                            { "oldValue", JsonNull.Instance },
                            { "newValue", UserToJson(user.User, user.TabCount > 0 ? "online" : "offline") } });
                }*/
            }
            //tab.NewMessage(
            //    new JsonObject{
            //        {"type","login"},
            //        {"user", UserToJson(aUser, "online")}});
            //RecountUsers();
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

        internal void RemoveTab(FormsAppTab aTab)
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
            //RecountUsers();
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
            List<FormsAppTab> tabs;
            lock (iLock)
            {
                tabs = new List<FormsAppTab>(iTabs);
            }
            foreach (var tab in tabs)
            {
                tab.NewMessage(aMessage);
            }
        }

        /*public void RecountUsers()
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
                    FormsUser user;
                    if (!iUsers.TryGetValue(kvp.Key, out user))
                    {
                        continue;
                    }
                    bool wasOnline = user.TabCount > 0;
                    bool isOnline = kvp.Value > 0;

                    user.TabCount = kvp.Value;
                    
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
                    FormsUser formsUser;
                    if (!iUsers.TryGetValue(userChange.UserId, out formsUser) && userChange.NewValue != null)
                    {
                        formsUser = new FormsUser { User = userChange.NewValue, TabCount = 0 };
                        iUsers[userChange.UserId] = formsUser;
                    }
                    string status = formsUser == null && formsUser.TabCount > 0 ? "online" : "offline";
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
        }*/
    }

    class FormsAppTab : IAppTab
    {
        readonly XappFormsBrowserTab iXFTab;
        readonly FormsApp iFormsApp;
        readonly IBrowserTabProxy iBrowserTabProxy;
        readonly int iId;

        ButtonControl iButtonFour;

        public FormsAppTab(FormsApp aFormsApp, IBrowserTabProxy aBrowserTabProxy, int aId, UserList aUserList, string aUserId)
        {
            iXFTab = new XappFormsBrowserTab(aBrowserTabProxy);


            var grid = GridControl.Create(iXFTab);

            grid.TopLeft = ButtonControl.Create(iXFTab, "First");
            grid.TopRight = ButtonControl.Create(iXFTab, "Beta");
            grid.BottomLeft = ButtonControl.Create(iXFTab, "Charlie");
            iButtonFour = ButtonControl.Create(iXFTab, "FOUR");

            grid.BottomRight = iButtonFour;
            iButtonFour.Clicked += OnButtonFourClicked;

            iXFTab.Root = grid;

            iFormsApp = aFormsApp;
            iBrowserTabProxy = aBrowserTabProxy;
            iId = aId;
        }

        void OnButtonFourClicked(object sender, EventArgs e)
        {
            Console.WriteLine("Click!");
            iButtonFour.Text = iButtonFour.Text == "CLICKED" ? "clicked" : "CLICKED";
        }




        public int Id
        {
            get { return iId; }
        }


        public void ChangeUser(User aUser)
        {
            // throw new NotImplementedException();
        }

        public void Receive(JsonValue aJsonValue)
        {
            iXFTab.Receive(aJsonValue);
        }

        public void NewMessage(JsonValue aMessage)
        {
            iBrowserTabProxy.Send(aMessage);
        }

        public void TabClosed()
        {
            iFormsApp.RemoveTab(this);
        }
    }
}