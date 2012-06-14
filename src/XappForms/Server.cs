using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using OpenHome.XappForms.Json;
using Owin;

namespace OpenHome.XappForms
{
    public interface ITabStatusListener
    {
        void NewTab(string aSessionId, string aTabId, string aUserId, string aId);
        void TabClosed(string aSessionId, string aTabId);
        void UpdateTabStatus(string aSessionId, string aTabId, string aUserId, int aQueueLength, DateTime aLastRead, bool aHasListener);
    }

    public class Server : IDisposable
    {
        class RequestPath
        {
            public NameValueCollection Query { get; private set; }
            public IList<string> PathSegments { get; private set; }
            public RequestPath(string path)
            {
                var uri = new Uri(new Uri("http://dummy/"), path);
                Query = HttpUtility.ParseQueryString(uri.Query);
                //Query = uri.Query == "" ? "" : HttpUtility.UrlDecode(uri.Query.Substring(1));
                //HttpUtility.ParseQueryString(uri.Query);
                PathSegments = uri.Segments.Skip(1).Select(seg => HttpUtility.UrlDecode(seg)).ToList<string>().AsReadOnly();
                //Console.WriteLine("Path:{0}",path);
                //Console.WriteLine("Segments:{0}", String.Join("///", PathSegments));
                //Console.WriteLine("Query:{0}", uri.Query);
                //Console.WriteLine("Parsed query:{0}", Query.AllKeys.Select(key => String.Format("{0}={1}", key, Query[key])));
            }
            private RequestPath(NameValueCollection query, IEnumerable<string> path)
            {
                Query = query;
                PathSegments = path.ToList();
            }
            public RequestPath Skip(int n)
            {
                return new RequestPath(Query, PathSegments.Skip(n));
            }
        }

        /*
        static Dictionary<string, IEnumerable<string>> MakeHeaders(params string[][] headers)
        {
            var result = new Dictionary<string, IEnumerable<string>>();
            foreach (string[] header in headers)
            {
                result[header[0]] = header.Skip(1);
            }
            return result;
        }*/

        static void ServePage(ResultDelegate aResult, string aStatus, Dictionary<string, IEnumerable<string>> aHeaders, IPageSource aPageSource)
        {
            Dictionary<string, IEnumerable<string>> headers = new Dictionary<string, IEnumerable<string>>(aHeaders);
            headers["Content-Length"] = new[] { aPageSource.ContentLength.ToString() };
            headers["Content-Type"] = new[] { aPageSource.ContentType };
            aResult(aStatus, headers, aPageSource.Serve());
        }

        static void ServeLongPoll(ResultDelegate aResult, string aStatus, Dictionary<string, IEnumerable<string>> aHeaders, string aContentType, BodyDelegate aBodyDelegate)
        {
            Dictionary<string, IEnumerable<string>> headers = new Dictionary<string, IEnumerable<string>>(aHeaders);
            //headers["Content-Length"] = new[] { aPageSource.ContentLength.ToString() };
            headers["Content-Type"] = new[] { aContentType };
            aResult(aStatus, headers, aBodyDelegate);
        }

        static readonly internal Dictionary<string, string> MimeTypesByExtension = new Dictionary<string, string>{
            {".js", "application/javascript; charset=utf-8" },
            {".css", "text/css; charset=utf-8" },
            {".json", "application/json; charset=utf-8" },
            {".html", "text/html; charset=utf-8" },
            {".htm", "text/html; charset=utf-8" },
            {".xml", "text/xml; charset=utf-8" },
            {".txt", "text/plain; charset=utf-8" },
            {".png", "image/png" },
            {".gif", "image/gif" },
            {".jpeg", "image/jpeg" },
            {".jpg", "image/jpeg" },
            {".svg", "image/svg+xml; charset=utf-8" },
            {".ico", "image/vnd.microsoft.icon" },
        };

        static string GetMimeType(string aFilename)
        {
            foreach (var kvp in MimeTypesByExtension)
            {
                if (aFilename.EndsWith(kvp.Key))
                {
                    return kvp.Value;
                }
            }
            return "application/octet-stream";
        }

        static void ServeFile(ResultDelegate aResult, Dictionary<string, IEnumerable<string>> aHeaders, string aFilename)
        {
            string mimeType = GetMimeType(aFilename);
            try
            {
                var pageSource = PageSource.MakeSourceFromFile(mimeType, aFilename);
                ServePage(aResult, "200 OK", aHeaders, pageSource);
            }
            catch (FileNotFoundException)
            {
                ServeNotFound(aResult, aHeaders);
            }
            catch (DirectoryNotFoundException)
            {
                ServeNotFound(aResult, aHeaders);

            }
            catch (IOException ioe)
            {
                Console.WriteLine(ioe);
                ServePage(aResult, "500 Internal Server Error", aHeaders, PageSource.MakeSourceFromString(StringType.Html, ServerErrorPage));
            }
        }


        const string IndexPageTemplate =
            @"<!DOCTYPE html><html><head>"+
                @"<script>window.xapp_discrimination_mapping = {0};</script>"+
                @"<script src=""/scripts/browser.js""></script>" +
            @"</head></html>";
        const string NotFoundPage =
            @"<!DOCTYPE html><html><body><h1>404 Not found</h1>"+
            @"<p>Listen, sit yourself down. There's no easy way to tell you this. "+
            @"Please imagine a crying robot or cute and bewildered animal delivering "+
            @"this message to you, because otherwise this information could crush your "+
            @"fragile psyche.</p>"+
            @"<p>The page you are looking for does not exist. Perhaps it never did. "+
            @"Perhaps it was only ever a figment of your fractured imagination. You "+
            @"need to stop looking for the page. It doesn't exist. Let it go. Chasing "+
            @"this non-existent page is only hurting you and those close to you. Please "+
            @"get yourself the help you badly need.</p></body></html>";
        const string ServerErrorPage =
            @"<!DOCTYPE html><html><body><h1>500 Internal Server Error</h1>"+
            @"<p>Ummmm... errr... Did you...</p>"+
            @"<p>I... I don't know what happened. Something went terribly wrong. I'm "+
            @"not saying it was <em>definitely</em> you, but, err, it might have been. "+
            @"I'm j<small>U</small>st saying... Look, I won""t tell anybody if you don,t. We can just "+
            @"pre-tend nothing has happened and nobodu needs to get refonma<sub>tt</sub>ed. "+
            @"O<sup>ka</sup>y. Rel<sub>A</sub>X. No<sub>THING</sub> to s333333 h^</p>"+
            @"<p>E&lt;/p&gt;</p></body></html>";

        static System.Security.Cryptography.RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

        static void ServeNotFound(ResultDelegate aResult, Dictionary<string, IEnumerable<string>> aHeaders)
        {
            ServePage(aResult, "404 Not found", aHeaders, PageSource.MakeSourceFromString(StringType.Html, NotFoundPage));
        }

        AppsState iAppsState;
        ServerUrlDispatcher iUrlDispatcher;

        static bool ValidatePath(IEnumerable<string> aPath)
        {
            return aPath.All(aSeg =>
                aSeg.IndexOfAny(Path.GetInvalidFileNameChars()) == -1
                    && aSeg != ".."
                        && aSeg != ".");
        }

        string GetSessionFromCookie(IEnumerable<string> aCookies)
        {
            foreach (string cookieString in aCookies)
            {
                foreach (string subCookieString in cookieString.Split(';'))
                {
                    string[] parts = subCookieString.Trim().Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && parts[0] == "XappSession")
                    {
                        return parts[1];
                    }
                }
            }
            return null;
        }

        Task<SessionRecord> GetOrCreateSession(IEnumerable<string> aCookies)
        {
            lock (iAppsState)
            {
                string session = GetSessionFromCookie(aCookies);
                return iAppsState.FindOrCreateSession(session);
            }
        }

        public void HandleRequest(IDictionary<string, object> aEnv, ResultDelegate aResult, Action<Exception> aFault)
        {
            Dictionary<string, IEnumerable<string>> respHeaders = new Dictionary<string, IEnumerable<string>>();
            try
            {
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                IDictionary<string,IEnumerable<string>> headers = (IDictionary<string, IEnumerable<string>>) aEnv["owin.RequestHeaders"];
                List<string> cookies = headers.ContainsKey("Cookie") ?
                    headers["Cookie"].ToList() :
                    new List<string>();

                //foreach (var c in cookies) Console.WriteLine(c);

                GetOrCreateSession(cookies).ContinueWith(
                    task =>
                    {
                        var session = task.Result;
                        respHeaders["Set-Cookie"] = new[] { "XappSession=" + session.Key + "; Path=/" };

                        Console.WriteLine("Incoming request for: {0}, {1}", aEnv["owin.RequestPath"], aEnv["owin.RequestMethod"]);
                        RequestPath path = new RequestPath((string)aEnv["owin.RequestPath"] + "?" + (string)aEnv["owin.RequestQueryString"]);

                        AppWebRequest request = new AppWebRequest(
                            (string)aEnv["owin.RequestMethod"],
                            path.PathSegments.ToArray(),
                            respHeaders,
                            aResult,
                            path.Query,
                            (BodyDelegate)aEnv["owin.RequestBody"]);

                        iUrlDispatcher.ServeRequest(request);
                    });

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                ServePage(aResult, "500 Internal Server Error", respHeaders, PageSource.MakeSourceFromString(StringType.Html, ServerErrorPage));
            }
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

        public Server()
        {
            var userList = new UserList();
            userList.SetUser(new User("chrisc", "Chris Cheung", GravatarUrl("chris.cheung@linn.co.uk")));
            userList.SetUser(new User("andreww", "Andrew Wilson", GravatarUrl("andrew.wilson@linn.co.uk")));
            userList.SetUser(new User("simonc", "Simon Chisholm", GravatarUrl("simon.chisholm@linn.co.uk")));
            userList.SetUser(new User("grahamd", "Graham Darnell", GravatarUrl("graham.darnell@linn.co.uk")));
            userList.SetUser(new User("stathisv", "Stathis Voukelatos", GravatarUrl("stathis.voukelatos@linn.co.uk")));
            var serverHealthApp = new ServerHealthApp();
            var timeoutPolicy = new ServerTabTimeoutPolicy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            var appsStateFactory = new AppsStateFactory(serverHealthApp, () => DateTime.Now, timeoutPolicy, userList);
            iAppsState = appsStateFactory.CreateAppsState();
            iAppsState.AddApp("chat", new ChatApp(userList));
            iAppsState.AddApp("test", new TestApp());
            iAppsState.AddApp("root", new RootApp());
            iAppsState.AddApp("login", new LoginApp(userList));
            iAppsState.AddApp("serverhealth", serverHealthApp);
            
            ServerUrlDispatcher dispatcher = new ServerUrlDispatcher();
            dispatcher.MapPrefixToDirectory(new[] { "scripts/" }, "scripts");
            dispatcher.MapPrefixToDirectory(new[] { "ohj/" }, "ohj");
            dispatcher.MapPrefixToDirectory(new[] { "theme/" }, "theme");
            dispatcher.MapPrefix(new[] { "poll/" }, HandlePoll);
            dispatcher.MapPrefix(new[] { "send/" }, HandleSend);
            dispatcher.MapPrefix(new[] { "apps/" }, HandleAppQuery);
            dispatcher.MapPrefix(new string[] { }, HandleOther);
            iUrlDispatcher = dispatcher;
        }

        void HandleAppQuery(IServerWebRequest aRequest)
        {
            string[] path = aRequest.RelativePath;
            if (path.Length != 1)
            {
                aRequest.Send404NotFound();
            }
            string appname = path[0].TrimEnd('/');
            iAppsState.GetApp(appname).ContinueWith(
                task =>
                {
                    if (task.Result == null)
                    {
                        aRequest.Send404NotFound();
                        return;
                    }
                    var mappings = task.Result.App.GetBrowserDiscriminationMappings();
                    var mappingObj = DiscriminationMappingsToJson(mappings);
                    var appObject = new JsonObject{
                        {"appid", appname},
                        {"browserTypes", mappingObj}
                    };
                    aRequest.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Json, appObject.ToString()));
                });
        }

        JsonObject DiscriminationMappingsToJson(Dictionary<string, string> aMappings)
        {
            var mappingObj = new JsonObject();
            foreach (var platformToUrl in aMappings)
            {
                mappingObj.Set(platformToUrl.Key, new JsonString(platformToUrl.Value));
            }
            return mappingObj;
        }

        void HandleOther(IServerWebRequest aRequest)
        {
            string[] path = aRequest.RelativePath;
            if (path.Length == 0)
            {
                aRequest.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Html, String.Format(IndexPageTemplate, "")));
            }
            else
            {
                //AppRecord app = ;
                iAppsState.GetApp(path[0].TrimEnd('/')).ContinueWith(
                    task =>
                    {
                        var app = task.Result;
                        if (app != null)
                        {
                            if (path.Length == 1)
                            {
                                Dictionary<string, string> mappings = app.App.GetBrowserDiscriminationMappings();
                                var mappingObj = DiscriminationMappingsToJson(mappings);
                                aRequest.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Html, String.Format(IndexPageTemplate, mappingObj.ToString())));
                            }
                            else
                            {
                                aRequest.RelativePath = aRequest.RelativePath.Skip(1).ToArray();
                                app.App.ServeWebRequest(aRequest);
                            }
                        }
                        else
                        {
                            aRequest.Send404NotFound();
                        }
                    });
            }
        }

        void HandlePoll(IServerWebRequest aRequest)
        {
            string[] path = aRequest.RelativePath;
            aRequest.DefaultResponseHeaders["Cache-Control"] = aRequest.DefaultResponseHeaders.GetDefault("Cache-Control",new string[]{}).Concat(new[] { "no-cache" });
            if (path.Length == 1)
            {
                string sessionId = path[0].TrimEnd('/');
                string appname = aRequest.Query.Get("appname");
                //Console.WriteLine(string.Join(", ", aRequest.Query.Keys.Cast<string>()));
                if (aRequest.Method == "POST")
                {
                    //AppRecord app;
                    //SessionRecord requestSession;
                    
                    iAppsState.GetApp(appname).ContinueWith(
                        task =>
                        {
                            var app = task.Result;
                            if (app == null)
                            {
                                aRequest.Send404NotFound();
                                return;
                            }
                            iAppsState.GetSession(sessionId).ContinueWith(
                                task2 =>
                                {
                                    var requestSession = task2.Result;
                                    if (requestSession == null)
                                    {
                                        aRequest.Send404NotFound();
                                        return;
                                    }
                                    requestSession.CreateTab(app).ContinueWith(
                                        task3 =>
                                        {
                                            var serverTab = task3.Result;
                                            //Console.WriteLine("CREATING TAB FOR APP. Session {0}   App {1}   Tab {2}", requestSession.Key, app.Id, tab.TabKey);
                                            aRequest.SendPage("200 OK", PageSource.MakeSourceFromString(StringType.Json,
                                                new JsonObject{
                                                    {"tabUrl", new JsonString(String.Format("/poll/{0}/{1}", requestSession.Key, serverTab.TabKey))},
                                                    {"tabId", new JsonString(serverTab.TabKey)}}.ToString()));
                                        });
                                });
                        });
                    return;
                }
            }
            else if (path.Length == 2)
            {
                string sessionId = path[0].TrimEnd('/');
                string tabId = path[1].TrimEnd('/');

                iAppsState.GetTab(sessionId, tabId).ContinueWith(
                    task =>
                    {
                        var serverTab = task.Result;
                        if (serverTab == null)
                        {
                            aRequest.Send404NotFound();
                            return;
                        }
                        aRequest.ServeLongPoll("200 OK", aRequest.DefaultResponseHeaders, "application/json", serverTab.Serve());
                    });
                return;
            }
            aRequest.Send404NotFound();
        }

        void HandleSend(IServerWebRequest aRequest)
        {
            string[] path = aRequest.RelativePath;
            if (path.Length == 2)
            {
                string sessionId = path[0].TrimEnd('/');
                string tabId = path[1].TrimEnd('/');
                //Console.WriteLine("HandleSend({0}, {1})", sessionId, tabId);

                iAppsState.GetTab(sessionId, tabId).ContinueWith(
                    task =>
                    {
                        ServerTab serverTab = task.Result;
                        if (serverTab == null)
                        {
                            aRequest.Send404NotFound();
                            return;
                        }
                        SendHandler handler = new SendHandler(serverTab.AppTab, aRequest);
                        aRequest.Body(handler.Write, handler.Flush, handler.End, handler.CancellationToken);
                    });
                return;
            }
            aRequest.Send404NotFound();
        }

        private class SendHandler
        {
            readonly IAppTab iAppTab;
            readonly IServerWebRequest iRequest;
            public CancellationToken CancellationToken { get; private set; }
            List<byte> content = new List<byte>();

            public SendHandler(IAppTab aAppTab, IServerWebRequest aRequest)
            {
                iAppTab = aAppTab;
                iRequest = aRequest;
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
                        iRequest.Send400BadRequest();
                        return;
                    }
                    iRequest.Send202Accepted();
                    iAppTab.Receive(json);
                    return;
                }
                else
                {
                    iRequest.Send400BadRequest();
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
