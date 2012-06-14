using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Owin;

namespace OpenHome.XappForms
{
    public interface IAppWebRequest
    {
        Dictionary<string, IEnumerable<string>> DefaultResponseHeaders { get; }
        NameValueCollection Query { get; }
        string[] RelativePath { get; set; }
        string Method { get; }
        void SendResult(string aStatus, IDictionary<string, IEnumerable<string>> aHeaders, BodyDelegate aBody);
        void SendFile(string aContentType, string aFilepath);
        void Send404NotFound();
        void Send500ServerError();
        void SendPage(string aStatus, IPageSource aPageSource);
        void Send202Accepted();
        void Send400BadRequest();
    }

    public interface IServerWebRequest : IAppWebRequest
    {
        void ServeLongPoll(string aStatus, Dictionary<string, IEnumerable<string>> aHeaders, string aContentType, BodyDelegate aBodyDelegate);
        BodyDelegate Body { get; }
    }

    public class AppWebRequest : IServerWebRequest
    {
        public string Method { get; private set; }
        public string[] RelativePath { get; set; }
        public Dictionary<string, IEnumerable<string>> DefaultResponseHeaders { get; private set; }
        ResultDelegate iResult;
        public NameValueCollection Query { get; private set; }
        public BodyDelegate Body { get; private set; }

        public AppWebRequest(string aMethod, string[] aRelativePath, Dictionary<string, IEnumerable<string>> aDefaultResponseHeaders, ResultDelegate aResult, NameValueCollection aQuery, BodyDelegate aBody)
        {
            Method = aMethod;
            RelativePath = aRelativePath;
            DefaultResponseHeaders = aDefaultResponseHeaders;
            iResult = aResult;
            Query = aQuery;
            Body = aBody;
        }

        public void SendResult(string aStatus, IDictionary<string, IEnumerable<string>> aHeaders, BodyDelegate aBody)
        {
            iResult(aStatus, aHeaders, aBody);
        }

        public void SendFile(string aContentType, string aFilepath)
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