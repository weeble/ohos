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
        public RawRequestData RawRequestData { get; private set; }
        public BodyDelegate Body { get; private set; }
        public IWebRequestResponder Responder { get { return this; } }
        public Dictionary<string, IEnumerable<string>> DefaultResponseHeaders { get; private set; }
        readonly ResultDelegate iResult;

        public string Method { get { return RawRequestData.Method; } }
        public IList<string> RelativePath { get { return RawRequestData.Path.PathSegments; } }
        public IDictionary<string, IEnumerable<string>> Query { get { return RawRequestData.Path.Query; } }

        public AppWebRequest(
            RawRequestData aRawRequestData,
            Dictionary<string, IEnumerable<string>> aDefaultResponseHeaders,
            ResultDelegate aResult,
            BodyDelegate aBody)
        {
            RawRequestData = aRawRequestData;
            DefaultResponseHeaders = aDefaultResponseHeaders;
            iResult = aResult;
            Body = aBody;
        }

        public IAppWebRequest SkipPathSegments(int aCount)
        {
            return new AppWebRequest(
                RawRequestData.WithPath(RawRequestData.Path.SkipPathSegments(aCount)),
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