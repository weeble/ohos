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
        void AddXapp(string aXappName, IXapp aXapp);
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
        ServerUrlDispatcher iUrlDispatcher;
        Strand iServerStrand;
        List<IXappPageFilter> iFilters;

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
                        RequestData requestData = new RequestData(
                            (string)aEnv["owin.RequestMethod"],
                            new RequestPath((string)aEnv["owin.RequestPath"], (string)aEnv["owin.RequestQueryString"]),
                            headers);

                        var session = GetOrCreateSession(requestData.Cookies);
                        respHeaders["Set-Cookie"] = new[] { "XappSession=" + session.Key + "; Path=/" };

                        Console.WriteLine("Incoming request for: {0}", requestData.Path.OriginalUri);

                        AppWebRequest request = new AppWebRequest(
                            requestData,
                            respHeaders,
                            aResult,
                            (BodyDelegate)aEnv["owin.RequestBody"]);

                        iUrlDispatcher.ServeRequest(requestData, request);

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
            
            ServerUrlDispatcher dispatcher = new ServerUrlDispatcher();
            Console.WriteLine(path("ohj"));
            dispatcher.MapPrefixToDirectory(new[] { "scripts/" }, path("scripts"));
            dispatcher.MapPrefixToDirectory(new[] { "ohj/" }, path("ohj"));
            dispatcher.MapPrefixToDirectory(new[] { "theme/" }, path("theme"));
            dispatcher.MapPrefix(new[] { "poll/" }, HandlePoll);
            dispatcher.MapPrefix(new[] { "send/" }, HandleSend);
            dispatcher.MapPrefix(new string[] { }, HandleOther);
            iUrlDispatcher = dispatcher;
            iServerStrand = aServerStrand;
            iFilters = new List<IXappPageFilter>();
        }

        public void AddXapp(string aXappName, IXapp aXapp)
        {
            if (!Regex.IsMatch(aXappName, @"^(?:[A-Za-z0-9\-\._])+$"))
            {
                throw new ArgumentException("aXappName must consist of ASCII letters, numbers, dash, period or underscore.");
            }
            iServerStrand.ScheduleExclusive(()=>iAppsState.AddApp(aXappName, aXapp));
        }

        public void AddFilter(IXappPageFilter aFilter)
        {
            iServerStrand.ScheduleExclusive(() => iFilters.Add(aFilter));
        }

        void FilterAndServeXappRequest(IXapp aXapp, RequestData aRequest, IServerWebRequestResponder aResponder)
        {
            var request = aRequest;
            foreach (var filter in iFilters)
            {
                request = filter.FilterPageRequest(request, aResponder);
                if (request == null)
                {
                    return;
                }
            }
            aXapp.ServeWebRequest(aRequest, aResponder);
        }

        void HandleOther(RequestData aRequest, IServerWebRequestResponder aResponder)
        {
            var path = aRequest.Path.PathSegments;
            if (path.Count == 0)
            {
                aResponder.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Html, IndexPage));
            }
            else
            {
                var app = iAppsState.GetApp(path[0].TrimEnd('/'));
                if (app != null)
                {
                    FilterAndServeXappRequest(app.App, aRequest.SkipPathSegments(1), aResponder);
                }
                else
                {
                    aResponder.Send404NotFound();
                }
            }
        }

        void HandlePoll(RequestData aRequest, IServerWebRequestResponder aResponder)
        {
            var path = aRequest.Path.PathSegments;
            aResponder.DefaultResponseHeaders["Cache-Control"] = aResponder.DefaultResponseHeaders.GetDefault("Cache-Control",new string[]{}).Concat(new[] { "no-cache" });
            if (path.Count == 1)
            {
                string sessionId = path[0].TrimEnd('/');
                string appname = aRequest.Path.Query["appname"].FirstOrDefault();
                if (appname == null)
                {
                    aResponder.Send404NotFound();
                    return;
                }
                if (aRequest.Method == "POST")
                {
                    var app = iAppsState.GetApp(appname);
                    if (app == null)
                    {
                        aResponder.Send404NotFound();
                        return;
                    }
                    var requestSession = iAppsState.GetSession(sessionId);
                    if (requestSession == null)
                    {
                        aResponder.Send404NotFound();
                        return;
                    }
                    string userid = aRequest.Cookies["xappuser"].FirstOrDefault();
                    var serverTab = requestSession.CreateTab(app, userid);
                    Console.WriteLine("CREATING TAB FOR APP. Session {0}   App {1}   Tab {2}", requestSession.Key, app.Id, serverTab.TabKey);
                    aResponder.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Json,
                        new JsonObject{
                            {"tabUrl", new JsonString(String.Format("/poll/{0}/{1}", requestSession.Key, serverTab.TabKey))},
                            {"tabId", new JsonString(serverTab.TabKey)}}.ToString()));
                    return;
                }
            }
            else if (path.Count == 2)
            {
                string sessionId = path[0].TrimEnd('/');
                string tabId = path[1].TrimEnd('/');

                var serverTab = iAppsState.GetTab(sessionId, tabId);
                if (serverTab == null)
                {
                    aResponder.Send404NotFound();
                    return;
                }
                aResponder.ServeLongPoll("200 OK", aResponder.DefaultResponseHeaders, "application/json", serverTab.Serve());
                return;
            }
            aResponder.Send404NotFound();
        }

        void HandleSend(RequestData aRequest, IServerWebRequestResponder aResponder)
        {
            IList<string> path = aRequest.Path.PathSegments;
            if (path.Count == 2)
            {
                string sessionId = path[0].TrimEnd('/');
                string tabId = path[1].TrimEnd('/');

                var serverTab = iAppsState.GetTab(sessionId, tabId);
                if (serverTab == null)
                {
                    aResponder.Send404NotFound();
                    return;
                }
                SendHandler handler = new SendHandler(serverTab.AppTab, aResponder);
                aResponder.ReadBody(handler.Write, handler.Flush, handler.End, handler.CancellationToken);
                return;
            }
            aResponder.Send404NotFound();
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
                //Console.WriteLine("Write ({0})", aArg.Count);
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
                    //Console.WriteLine("Received JSON: < {0} >", stringPayload);
                    JsonValue json;
                    try
                    {
                        json = JsonValue.FromString(stringPayload);
                        //Console.WriteLine("Parsed JSON: < {0} >", json);
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
