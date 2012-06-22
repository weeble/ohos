using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using Owin;

namespace OpenHome.XappForms
{
    public interface IAppWebRequest
    {
        IDictionary<string, IEnumerable<string>> Query { get; }
        IList<string> RelativePath { get; }
        string Method { get; }
        IWebRequestResponder Responder { get; }
        IAppWebRequest SkipPathSegments(int aCount);
    }

    public interface IWebRequestResponder
    {
        void SendResult(string aStatus, IDictionary<string, IEnumerable<string>> aHeaders, BodyDelegate aBody);
        void SendFile(string aContentType, string aFilepath);
        void SendFile(string aFilepath);
        void Send404NotFound();
        void Send500ServerError();
        void SendPage(string aStatus, IPageSource aPageSource);
        void Send202Accepted();
        void Send400BadRequest();
        //void Send
    }

    public interface IServerWebRequestResponder : IWebRequestResponder
    {
        void ServeLongPoll(string aStatus, Dictionary<string, IEnumerable<string>> aHeaders, string aContentType, BodyDelegate aBodyDelegate);
        Dictionary<string, IEnumerable<string>> DefaultResponseHeaders { get; }

        // Note: Should ReadBody move elsewhere? While in terms of data-flow it's
        // part of the request, in terms of actual usage it's much more closely
        // related to the actions on the responder. Really, the partition is not
        // between request and response, but between address and action. The
        // RequestData really identifies what code should handle the request, then
        // the that code performs an action, which might include reading the body
        // and will include sending a response. What we need are better names.
        void ReadBody(Func<ArraySegment<byte>,bool> aWrite, Func<Action, bool> aFlush, Action<Exception> aEnd, CancellationToken aCancellationToken);
    }


    static class StringDictionary
    {
        public static void Add(IDictionary<string, IEnumerable<string>> aDictionary, string aKey, string aValue)
        {
            IEnumerable<string> values;
            if (!aDictionary.TryGetValue(aKey, out values))
            {
                values = new List<string>();
                aDictionary[aKey] = values;
            }
            ((List<string>) values).Add(aValue);
        }
    }

    public class RequestData
    {
        public RequestPath Path { get; private set; }
        public IDictionary<string, IEnumerable<string>> Headers { get; private set; }
        public string Method { get; private set; }
        public RequestCookies Cookies { get; private set; }
        public RequestData(string aMethod, RequestPath aPath, IDictionary<string, IEnumerable<string>> aHeaders)
        {
            Method = aMethod;
            Path = aPath;
            Headers = aHeaders;
            Cookies = new RequestCookies(aHeaders); // TODO: Avoid reparsing cookies on SkipPathSegments.
        }
        public RequestData(string aMethod, string aPath, IDictionary<string, IEnumerable<string>> aHeaders)
            : this(aMethod, new RequestPath(aPath), aHeaders)
        {
        }
        public RequestData SkipPathSegments(int aCount)
        {
            return new RequestData(
                Method,
                Path.SkipPathSegments(aCount),
                Headers);
        }
    }

    public class RequestCookies
    {
        readonly Dictionary<string, IEnumerable<string>> iCookies;
        public IEnumerable<string> this[string aName]
        {
            get { return Get(aName); }
        }
        public IEnumerable<string> Get(string aName)
        {
            IEnumerable<string> value;
            if (iCookies.TryGetValue(aName, out value))
            {
                return value;
            }
            return Enumerable.Empty<string>();
        }
        public RequestCookies(IDictionary<string, IEnumerable<string>> aHeaders)
        {
            iCookies = new Dictionary<string, IEnumerable<string>>();

            IEnumerable<string> cookies;
            if (!aHeaders.TryGetValue("Cookie", out cookies))
            {
                return;
            }
            foreach (string cookieString in cookies)
            {
                foreach (string subCookieString in cookieString.Split(';'))
                {
                    string[] parts = subCookieString.Trim().Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;
                    StringDictionary.Add(iCookies, parts[0], parts[1]);
                }
            }
        }
    }



    public class RequestPath
    {
        public string OriginalUri { get; private set; }
        public IDictionary<string, IEnumerable<string>> Query { get; private set; }
        ArraySlice<string> iPathSegments;

        public IList<string> PathSegments { get { return iPathSegments; } }

        public RequestPath(string aPath, string aQueryString)
            :this(aPath + '?' + aQueryString)
        {
        }

        public RequestPath(string aPath)
        {
            OriginalUri = aPath;
            var uri = new Uri(new Uri("http://dummy/"), aPath);
            string[] pathSegments = uri.Segments.Skip(1).Select(Uri.UnescapeDataString).ToArray();
            iPathSegments = new ArraySlice<string>(pathSegments);
            PopulateQuery(uri.Query);
        }

        RequestPath(
            string aPath,
            ArraySlice<string> aPathSegments,
            IDictionary<string, IEnumerable<string>> aQuery)
        {
            OriginalUri = aPath;
            iPathSegments = aPathSegments;
            Query = aQuery;
        }

        public RequestPath SkipPathSegments(int aCount)
        {
            return new RequestPath(
                OriginalUri,
                iPathSegments.Slice(aCount,int.MaxValue),
                Query);
        }

        void PopulateQuery(string aQueryString)
        {
            Query = new Dictionary<string, IEnumerable<string>>();
            if (aQueryString.Length>0)
            {
                if (aQueryString[0] == '?')
                {
                    aQueryString = aQueryString.Substring(1);
                }
                string[] fragments = aQueryString.Split('&');
                foreach (string f in fragments)
                {
                    string[] keyAndValue = f.Split(new[]{'='}, 2).Select(Uri.UnescapeDataString).ToArray();
                    string key, value;
                    if (keyAndValue.Length==1)
                    {
                        key = "";
                        value = keyAndValue[0];
                    }
                    else
                    {
                        key = keyAndValue[0];
                        value = keyAndValue[1];
                    }
                    StringDictionary.Add(Query, key, value);
                }
            }
        }
    }
    
    /*
        class RequestPath
        {
            public NameValueCollection Query { get; private set; }
            public IList<string> PathSegments { get; private set; }
            public RequestPath(string path)
            {
                var uri = new Uri(new Uri("http://dummy/"), path);
                Query = HttpUtility.ParseQueryString(uri.Query);
                PathSegments = uri.Segments.Skip(1).Select(seg => HttpUtility.UrlDecode(seg)).ToList<string>().AsReadOnly();
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
    */
    public class AppWebRequest : IAppWebRequest, IServerWebRequestResponder
    {
        public RequestData RequestData { get; private set; }
        public BodyDelegate Body { get; private set; }
        public IWebRequestResponder Responder { get { return this; } }
        public Dictionary<string, IEnumerable<string>> DefaultResponseHeaders { get; private set; }
        readonly ResultDelegate iResult;

        public string Method { get { return RequestData.Method; } }
        public IList<string> RelativePath { get { return RequestData.Path.PathSegments; } }
        public IDictionary<string, IEnumerable<string>> Query { get { return RequestData.Path.Query; } }

        public AppWebRequest(
            RequestData aRequestData,
            Dictionary<string, IEnumerable<string>> aDefaultResponseHeaders,
            ResultDelegate aResult,
            BodyDelegate aBody)
        {
            RequestData = aRequestData;
            DefaultResponseHeaders = aDefaultResponseHeaders;
            iResult = aResult;
            Body = aBody;
        }

        public IAppWebRequest SkipPathSegments(int aCount)
        {
            return new AppWebRequest(
                RequestData.SkipPathSegments(aCount),
                DefaultResponseHeaders,
                iResult,
                Body);
        }

        public void ReadBody(Func<ArraySegment<byte>, bool> aWrite, Func<Action, bool> aFlush, Action<Exception> aEnd, CancellationToken aCancellationToken)
        {
            Body(aWrite, aFlush, aEnd, aCancellationToken);
        }

        public void SendResult(string aStatus, IDictionary<string, IEnumerable<string>> aHeaders, BodyDelegate aBody)
        {
            iResult(aStatus, aHeaders, aBody);
        }

        public void SendFile(string aContentType, string aFilepath)
        {
            ServeFile(aFilepath);
        }

        public void SendFile(string aFilepath)
        {
            ServeFile(aFilepath);
        }

        public void Send404NotFound()
        {
            ServeNotFound();
        }

        public void Send500ServerError()
        {
            ServeError();
        }


        public void SendPage(string aStatus, IPageSource aPageSource)
        {
            Dictionary<string, IEnumerable<string>> headers = new Dictionary<string, IEnumerable<string>>(DefaultResponseHeaders);
            headers["Content-Length"] = new[] { aPageSource.ContentLength.ToString() };
            headers["Content-Type"] = new[] { aPageSource.ContentType };
            iResult(aStatus, headers, aPageSource.Serve());
        }

        public void ServeLongPoll(string aStatus, Dictionary<string, IEnumerable<string>> aHeaders, string aContentType, BodyDelegate aBodyDelegate)
        {
            Dictionary<string, IEnumerable<string>> headers = new Dictionary<string, IEnumerable<string>>(aHeaders);
            //headers["Content-Length"] = new[] { aPageSource.ContentLength.ToString() };
            headers["Content-Type"] = new[] { aContentType };
            iResult(aStatus, headers, aBodyDelegate);
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

        void ServeFile(string aFilename)
        {
            string mimeType = GetMimeType(aFilename);
            try
            {
                var pageSource = PageSource.MakeSourceFromFile(mimeType, aFilename);
                SendPage("200 OK", pageSource);
            }
            catch (FileNotFoundException)
            {
                ServeNotFound();
            }
            catch (DirectoryNotFoundException)
            {
                ServeNotFound();
            }
            catch (IOException ioe)
            {
                Console.WriteLine(ioe);
                SendPage("500 Internal Server Error", PageSource.MakeSourceFromString(StringType.Html, ServerErrorPage));
            }
        }

        const string IndexPageTemplate =
            @"<!DOCTYPE html><html><head>"+
                @"<script src=""/scripts/init.js""></script>" +
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

        void ServeNotFound()
        {
            SendPage("404 Not found", PageSource.MakeSourceFromString(StringType.Html, NotFoundPage));
        }

        void ServeError()
        {
            SendPage("500 Internal server error", PageSource.MakeSourceFromString(StringType.Html, ServerErrorPage));
        }

        public void Send202Accepted()
        {
            SendPage("202 Accepted", PageSource.MakeSourceFromString(StringType.Plain, "202 Accepted"));
        }

        public void Send400BadRequest()
        {
            SendPage("400 Bad request", PageSource.MakeSourceFromString(StringType.Plain, "400 Bad request"));
        }
    }
}