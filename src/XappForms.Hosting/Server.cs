using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenHome.XappForms.Json;
using Owin;

namespace OpenHome.XappForms
{
    static class Extensions
    {
        public static TValue GetDefault<TKey,TValue>(
            this Dictionary<TKey,TValue> aDict, TKey aKey, TValue aDefaultValue)
        {
            TValue value;
            return aDict.TryGetValue(aKey, out value) ? value : aDefaultValue;
        }
    }
    public interface ITabStatusListener
    {
        void NewTab(string aSessionId, string aTabId, string aUserId, string aId);
        void TabClosed(string aSessionId, string aTabId);
        void UpdateTabStatus(string aSessionId, string aTabId, string aUserId, int aQueueLength, DateTime aLastRead, bool aHasListener);
    }

    public interface IXappServer
    {
        void AddXapp(string aXappName, IRawXapp aRawXapp);
        void AddXapp(string aXappName, IXapp aRawXapp);
    }

    public class Server : IDisposable, IXappServer
    {
        static void ServePage(ResultDelegate aResult, string aStatus, Dictionary<string, IEnumerable<string>> aHeaders, IPageSource aPageSource)
        {
            Dictionary<string, IEnumerable<string>> headers = new Dictionary<string, IEnumerable<string>>(aHeaders);
            headers["Content-Length"] = new[] { aPageSource.ContentLength.ToString() };
            headers["Content-Type"] = new[] { aPageSource.ContentType };
            aResult(aStatus, headers, aPageSource.Serve());
        }


        const string IndexPage =
            @"<!DOCTYPE html><html><head>"+
                //@"<script>window.xapp_discrimination_mapping = {0};</script>"+
                @"<script src=""/scripts/browser.js""></script>" +
            @"</head></html>";

        const string ServerErrorPage =
            @"<!DOCTYPE html><html><body><h1>500 Internal Server Error</h1>"+
            @"<p>Ummmm... errr... Did you...</p>"+
            @"<p>I... I don't know what happened. Something went terribly wrong. I'm "+
            @"not saying it was <em>definitely</em> you, but, err, it might have been. "+
            @"I'm j<small>U</small>st saying... Look, I won""t tell anybody if you don,t. We can just "+
            @"pre-tend nothing has happened and nobodu needs to get refonma<sub>tt</sub>ed. "+
            @"O<sup>ka</sup>y. Rel<sub>A</sub>X. No<sub>THING</sub> to s333333 h^</p>"+
            @"<p>E&lt;/p&gt;</p></body></html>";

        AppsState iAppsState;
        ServerPathDispatcher iPathDispatcher;
        Strand iServerStrand;
        Func<IXapp, IRawXapp> iXappAdapter;

        static string GetSessionFromCookie(RequestCookies aCookies)
        {
            return aCookies["XappSession"].FirstOrDefault();
        }

        SessionRecord GetOrCreateSession(RequestCookies aCookies)
        {
            lock (iAppsState)
            {
                string session = GetSessionFromCookie(aCookies);
                return iAppsState.FindOrCreateSession(session);
            }
        }

        public void HandleRequest(IDictionary<string, object> aEnv, ResultDelegate aResult, Action<Exception> aFault)
        {
            iServerStrand.ScheduleExclusive(
                () =>
                {
                    Dictionary<string, IEnumerable<string>> respHeaders = new Dictionary<string, IEnumerable<string>>();
                    try
                    {
                        var headers = (IDictionary<string, IEnumerable<string>>)aEnv["owin.RequestHeaders"];
                        RawRequestData rawRequestData = new RawRequestData(
                            (string)aEnv["owin.RequestMethod"],
                            new RequestPath((string)aEnv["owin.RequestPath"], (string)aEnv["owin.RequestQueryString"]),
                            headers);

                        var session = GetOrCreateSession(rawRequestData.Cookies);
                        respHeaders["Set-Cookie"] = new[] { "XappSession=" + session.Key + "; Path=/" };

                        AppWebRequest request = new AppWebRequest(
                            rawRequestData,
                            respHeaders,
                            aResult,
                            (BodyDelegate)aEnv["owin.RequestBody"]);

                        if (!iPathDispatcher.ServeRequest(rawRequestData, request))
                        {
                            request.Send404NotFound();
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        ServePage(aResult, "500 Internal Server Error", respHeaders, PageSource.MakeSourceFromString(StringType.Html, ServerErrorPage));
                    }
                });
        }

        static string GravatarUrl(string aEmail)
        {
            var normalizedEmail = aEmail.Trim().ToLowerInvariant();
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(normalizedEmail);
            byte[] hash = md5.ComputeHash(inputBytes);
            string gravatarHash = String.Join("", hash.Select(b=>b.ToString("x2")));
            return String.Format("http://www.gravatar.com/avatar/{0}?s=60", gravatarHash);
        }

        public Server(AppsState aAppsState, Strand aServerStrand, string aHttpDirectory)
        {
            iAppsState = aAppsState;

            Func<string, string> path = p => Path.Combine(aHttpDirectory, p);
            
            ServerPathDispatcher dispatcher = new ServerPathDispatcher();
            dispatcher.MapPrefixToDirectory(new[] { "scripts/" }, path("scripts"));
            dispatcher.MapPrefixToDirectory(new[] { "ohj/" }, path("ohj"));
            dispatcher.MapPrefixToDirectory(new[] { "theme/" }, path("theme"));
            dispatcher.MapPrefix(new[] { "poll/" }, HandlePoll);
            dispatcher.MapPrefix(new[] { "send/" }, HandleSend);
            dispatcher.MapPrefix(new string[] { }, HandleOther);
            iPathDispatcher = dispatcher;
            iServerStrand = aServerStrand;
        }

        public void AddXapp(string aXappName, IRawXapp aRawXapp)
        {
            if (!Regex.IsMatch(aXappName, @"^(?:[A-Za-z0-9\-\._])+$"))
            {
                throw new ArgumentException("aXappName must consist of ASCII letters, numbers, dash, period or underscore.");
            }
            iServerStrand.ScheduleExclusive(()=>iAppsState.AddApp(aXappName, aRawXapp));
        }

        public void AddXapp(string aXappName, IXapp aXapp)
        {
            if (!Regex.IsMatch(aXappName, @"^(?:[A-Za-z0-9\-\._])+$"))
            {
                throw new ArgumentException("aXappName must consist of ASCII letters, numbers, dash, period or underscore.");
            }
            iServerStrand.ScheduleExclusive(() =>
                {
                    Console.WriteLine("ADDING... {0}", aXappName);
                    var xapp = iXappAdapter(aXapp);
                    Console.WriteLine("ADDING#2... {0}", aXappName);
                    iAppsState.AddApp(aXappName, iXappAdapter(aXapp));
                    Console.WriteLine("ADDING#3... {0}", aXappName);
                });
        }

        public void SetXappAdapter(Func<IXapp, IRawXapp> aAdapter)
        {
            iServerStrand.ScheduleExclusive(() => iXappAdapter = aAdapter);
        }

        bool HandleOther(RawRequestData aRawRequest, IServerWebRequestResponder aResponder)
        {
            var path = aRawRequest.Path.PathSegments;
            if (path.Count == 0)
            {
                aResponder.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Html, IndexPage));
                return true;
            }
            var app = iAppsState.GetApp(path[0].TrimEnd('/'));
            if (app != null)
            {
                app.App.ServeWebRequest(aRawRequest.WithPath(aRawRequest.Path.SkipPathSegments(1)), aResponder);
                return true;
            }
            return false;
        }

        bool HandlePoll(RawRequestData aRawRequest, IServerWebRequestResponder aResponder)
        {
            var path = aRawRequest.Path.PathSegments;
            aResponder.DefaultResponseHeaders["Cache-Control"] = aResponder.DefaultResponseHeaders.GetDefault("Cache-Control",new string[]{}).Concat(new[] { "no-cache" });
            if (path.Count == 1)
            {
                string sessionId = path[0].TrimEnd('/');
                string appname = aRawRequest.Path.Query["appname"].FirstOrDefault();
                if (appname == null)
                {
                    return false;
                }
                if (aRawRequest.Method == "POST")
                {
                    var app = iAppsState.GetApp(appname);
                    if (app == null)
                    {
                        return false;
                    }
                    var requestSession = iAppsState.GetSession(sessionId);
                    if (requestSession == null)
                    {
                        return false;
                    }
                    string userid = aRawRequest.Cookies["xappuser"].FirstOrDefault();
                    var serverTab = requestSession.CreateTab(app, userid);
                    aResponder.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Json,
                        new JsonObject{
                            {"tabUrl", new JsonString(String.Format("/poll/{0}/{1}", requestSession.Key, serverTab.TabKey))},
                            {"tabId", new JsonString(serverTab.TabKey)}}.ToString()));
                    return true;
                }
            }
            else if (path.Count == 2)
            {
                string sessionId = path[0].TrimEnd('/');
                string tabId = path[1].TrimEnd('/');

                var serverTab = iAppsState.GetTab(sessionId, tabId);
                if (serverTab == null)
                {
                    return false;
                }
                aResponder.ServeLongPoll("200 OK", aResponder.DefaultResponseHeaders, "application/json", serverTab.Serve());
                return true;
            }
            return false;
        }

        bool HandleSend(RawRequestData aRawRequest, IServerWebRequestResponder aResponder)
        {
            IList<string> path = aRawRequest.Path.PathSegments;
            if (path.Count == 2)
            {
                string sessionId = path[0].TrimEnd('/');
                string tabId = path[1].TrimEnd('/');

                var serverTab = iAppsState.GetTab(sessionId, tabId);
                if (serverTab == null)
                {
                    return false;
                }
                SendHandler handler = new SendHandler(serverTab.AppTab, aResponder);
                aResponder.ReadBody(handler.Write, handler.Flush, handler.End, handler.CancellationToken);
                return true;
            }
            return false;
        }

        private class SendHandler
        {
            readonly IAppTab iAppTab;
            readonly IServerWebRequestResponder iResponder;
            public CancellationToken CancellationToken { get; private set; }
            List<byte> content = new List<byte>();

            public SendHandler(IAppTab aAppTab, IServerWebRequestResponder aResponder)
            {
                iAppTab = aAppTab;
                iResponder = aResponder;
                CancellationToken = new CancellationToken();
            }

            public bool Write(ArraySegment<byte> aArg)
            {
                content.AddRange(aArg.Array.Skip(aArg.Offset).Take(aArg.Count));
                return false;
            }

            public bool Flush(Action aArg)
            {
                return false;
            }

            public void End(Exception aObj)
            {
                if (aObj == null)
                {
                    string stringPayload = Encoding.UTF8.GetString(content.ToArray());
                    stringPayload = stringPayload.Trim();
                    JsonValue json;
                    try
                    {
                        json = JsonValue.FromString(stringPayload);
                    }
                    catch (ArgumentException ae)
                    {
                        Console.WriteLine("Parse error: {0}", ae);
                        iResponder.Send400BadRequest();
                        return;
                    }
                    iResponder.Send202Accepted();
                    iAppTab.Receive(json);
                    return;
                }
                else
                {
                    iResponder.Send400BadRequest();
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
