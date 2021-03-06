using System;
using System.Net;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Xml.Linq;
using System.Text;
using log4net;

namespace OpenHome.Os.Remote
{
    public interface ILoginValidator
    {
        bool ValidateCredentials(string aUserName, string aPassword);
    }
    
    public class ProxyServer : IDisposable
    {
        public uint Port { get { return kRemoteAccessPort; } }
        public string LoginPath { get { return kLoginPath; } }

        private const string kAuthCookieName = "remoteId";
        private const string kLoginPath = "/login.html";
        private HttpServer iHttpServer;
        private readonly string iForwardAddress;
        private uint iForwardPort;
        private string iForwardUdn;
        private readonly Dictionary<string, string> iAuthenticatedClients;
        private ILoginValidator iLoginValidator;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ProxyServer));
        private ServicePoint iServicePoint;

        private const int kNumServerThreads = 8;
        private const int kRemoteAccessPort = 55170; // TODO: hard-coded port.  Can we be sure no ohNet server will have been allocated this before we run?

        public ProxyServer(string aNetworkAdapter)
        {
            iForwardAddress = aNetworkAdapter;
            iAuthenticatedClients = new Dictionary<string, string>();
        }
        public void AddApp(uint aPort, string aUdn)
        {
            // should support multiple apps but only allow one at present
            iForwardPort = aPort;
            iForwardUdn = aUdn;

            // .NET doesn't seem to care about ConnectionLimit for calls to our own address; mono does
            string host = String.Format("http://{0}:{1}", iForwardAddress, aPort);
            iServicePoint = ServicePointManager.FindServicePoint(new Uri(host));
            iServicePoint.ConnectionLimit = kNumServerThreads;
        }
        public void Start(ILoginValidator aLoginValidator)
        {
            iLoginValidator = aLoginValidator;
            iHttpServer = new HttpServer(kNumServerThreads);
            iHttpServer.Start(kRemoteAccessPort, ProcessRequest);
            Logger.InfoFormat("Started proxy server on port {0}", kRemoteAccessPort);
        }
        public void Stop()
        {
            if (iHttpServer != null)
            {
                iHttpServer.Dispose();
                iHttpServer = null;
            }
        }
        public void ClearAuthenticatedClients()
        {
            lock (this)
            {
                iAuthenticatedClients.Clear();
            }
        }
        private void ProcessRequest(HttpListenerContext aContext)
        {
            HttpListenerRequest clientReq = aContext.Request;
            HttpListenerResponse clientResp = aContext.Response;

            if (clientReq.Url.PathAndQuery == "/favicon.ico")
            {   // we don't support favicons
                clientResp.StatusCode = (int)HttpStatusCode.NotFound;
                clientResp.Close();
                return;
            }
            if (IsAuthenticating(clientReq, clientResp))
                return;
            string targetUrl = RewriteUrl(clientReq);
            if (targetUrl == null)
            {
                clientResp.StatusCode = (int)HttpStatusCode.NotFound;
                clientResp.Close();
                return;
            }
            Logger.InfoFormat("Method: {0}, url: {1}, thread: {2}", clientReq.HttpMethod, targetUrl, Thread.CurrentThread.ManagedThreadId);
            HttpWebRequest forwardedReq = (HttpWebRequest)WebRequest.Create(targetUrl);
            bool connectionClose;
            bool connectionKeepAlive;
            WriteForwardedRequest(clientReq, forwardedReq, out connectionKeepAlive, out connectionClose);
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)forwardedReq.GetResponse();
            }
            catch (WebException e)
            {
                resp = (HttpWebResponse)e.Response;
                Logger.ErrorFormat("ERROR: {0} for {1}", (int)resp.StatusCode, targetUrl);
            }
            if (clientReq.HttpMethod == "POST")
                Logger.InfoFormat("Completing request.  url: {0}, thread: {1}", targetUrl, Thread.CurrentThread.ManagedThreadId);
            WriteResponse(targetUrl, resp, clientResp);
            // docs suggest following is unnecessary - we only have to close one from clientRespStream / clientResp
            if (connectionClose)
                clientResp.Close();
        }
        private bool IsAuthenticating(HttpListenerRequest aRequest, HttpListenerResponse aResponse)
        {
            string pathAndQuery = aRequest.Url.PathAndQuery;
            string location;
            if (String.Compare(aRequest.HttpMethod, "POST", true) == 0 && pathAndQuery == "/loginService")
            {
                MemoryStream memStream = new MemoryStream();
                aRequest.InputStream.CopyTo(memStream);
                byte[] bytes = memStream.ToArray();
                XElement tree = XElement.Parse(Encoding.UTF8.GetString(bytes));
                string username = tree.Element("username").Value;
                string password = tree.Element("password").Value;
                if (!iLoginValidator.ValidateCredentials(username, password))
                {
                    aResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                    aResponse.Close();
                    return true;
                }

                string guid = Guid.NewGuid().ToString();
                lock (this)
                {
                    iAuthenticatedClients.Add(guid, guid);
                    // TODO: write clients to xml file (iff not using session cookies)
                }
                aResponse.AppendCookie(new Cookie(kAuthCookieName, guid));
                aResponse.StatusCode = (int)HttpStatusCode.OK;
                location = "/";
                byte[] buf = Encoding.UTF8.GetBytes(location + "\r\n");
                aResponse.OutputStream.Write(buf, 0, buf.Length);
                Logger.InfoFormat("Authenticated! Redirecting: {0} to {1}", pathAndQuery, location);
                // just completed authentication.  Redirect client to (assumed) original url
                aResponse.Close();
                return true;
            }

            foreach (Cookie cookie in aRequest.Cookies)
            {
                if (cookie.Name == kAuthCookieName && iAuthenticatedClients.ContainsKey(cookie.Value))
                {
                    // already authenticated.
                    // A path of /{iForwardUdn} is a special case (see docs on our use of HaProxy) which needs to be redirected to "/"
                    if (pathAndQuery == String.Format("/{0}", iForwardUdn))
                    {
                        aResponse.Redirect("/");
                        aResponse.Close();
                        return true;
                    }
                    return false;
                }
            }

            if (pathAndQuery == kLoginPath || pathAndQuery.StartsWith("/login/"))
                // allow these requests through, regardless of our authentication state as they're needed to load the login screen
                return false;

            // redirect any other requests to the login page
            location = kLoginPath;
            aResponse.Redirect(location);
            aResponse.Close();
            Logger.InfoFormat("Redirecting: {0} to {1}", pathAndQuery, location);
            return true;
        }
        private string RewriteUrl(HttpListenerRequest aRequest)
        {
            string url = null;
            string pathAndQuery = aRequest.Url.PathAndQuery;
            string method = aRequest.HttpMethod.ToUpper();
            switch (method)
            {
                case "HEAD":
                case "GET":
                    if (pathAndQuery == "/")
                    {
                        foreach (string key in aRequest.Headers.AllKeys)
                        {
                            if (key.ToUpper() == "UPGRADE" && aRequest.Headers.GetValues(key)[0].ToUpper() == "WEBSOCKET")
                            {
                                // we can't support websockets so reject any handshake attempt to encourage the client to switch to long polling instead
                                return null;
                            }
                        }
                    }
                    if (pathAndQuery.Contains("/Upnp/resource/"))
                    {
                        url = String.Format("http://{0}:{1}{2}", iForwardAddress, iForwardPort, pathAndQuery);
                    }
                    else
                    {
                        if (pathAndQuery.StartsWith("/"))
                            pathAndQuery = pathAndQuery.Remove(0, 1);
                        url = String.Format("http://{0}:{1}/{2}/Upnp/resource/{3}", iForwardAddress, iForwardPort,
                                            iForwardUdn, pathAndQuery);
                    }
                    break;
                case "POST":
                    url = String.Format("http://{0}:{1}{2}", iForwardAddress, iForwardPort, pathAndQuery);
                    break;
                default:
                    Logger.InfoFormat("Unexpected method - {0}", method);
                    break;
            }
            return url;
        }
        private static void WriteForwardedRequest(HttpListenerRequest aClientReq, HttpWebRequest aForwardedReq, out bool aConnectionKeepAlive, out bool aConnectionClose)
        {
            aConnectionKeepAlive = aConnectionClose = false;
            aForwardedReq.Method = aClientReq.HttpMethod;
            foreach (string key in aClientReq.Headers.AllKeys)
            {
                switch (key.ToUpper())
                {
                    case "HOST":
                        aForwardedReq.Host = aClientReq.Headers.GetValues(key)[0];
                        break;
                    case "CONTENT-LENGTH":
                        aForwardedReq.ContentLength = Convert.ToInt32(aClientReq.Headers.GetValues(key)[0]);
                        break;
                    case "TRANSFER-ENCODING":
                        aForwardedReq.TransferEncoding = aClientReq.Headers.GetValues(key)[0];
                        break;
                    case "CACHE-CONTROL":
                        // TODO
                        //forwardedReq.CachePolicy = clientReq.Headers.GetValues(key)[0];
                        break;
                    case "CONTENT-TYPE":
                        aForwardedReq.ContentType = aClientReq.Headers.GetValues(key)[0];
                        break;
                    case "CONNECTION":
                        string value = aClientReq.Headers.GetValues(key)[0];
                        if (String.Compare(value, "keep-alive", true) == 0)
                            aConnectionKeepAlive = true;
                        else if (String.Compare(value, "close", true) == 0)
                            aConnectionClose = true;
                        else
                            aForwardedReq.Connection = aClientReq.Headers.GetValues(key)[0];
                        //Logger.InfoFormat("Unhandled CONNECTION header in request: {0}", clientReq.Headers.GetValues(key)[0]);
                        aForwardedReq.KeepAlive = aConnectionKeepAlive;
                        break;
                    case "ACCEPT":
                        aForwardedReq.Accept = aClientReq.Headers.GetValues(key)[0];
                        break;
                    case "ACCEPT-CHARSET":
                    case "ACCEPT-ENCODING":
                    case "ACCEPT-LANGUAGE":
                    case "ORIGIN":
                    case "SOAPACTION":
                        string[] values = aClientReq.Headers.GetValues(key);
                        foreach (string val in values)
                            aForwardedReq.Headers.Add(key, val);
                        break;
                    case "USER-AGENT":
                    case "REFERER":
                    case "COOKIE":
                        // we deliberately don't pass these on
                        break;
                    default:
                        Logger.InfoFormat("Ignored header in request: {0}", key);
                        break;
                }
            }
            if (String.Compare(aClientReq.HttpMethod, "POST", true) == 0)
            {
                using (Stream stream = aForwardedReq.GetRequestStream())
                {
                    aClientReq.InputStream.CopyTo(stream);
                }
            }
        }
        private static void WriteResponse(string aUrl, HttpWebResponse aProxiedResponse, HttpListenerResponse aResponse)
        {
            aResponse.StatusCode = (int)aProxiedResponse.StatusCode;
            aResponse.StatusDescription = aProxiedResponse.StatusDescription;
            int contentLength = 0;
            foreach (var key in aProxiedResponse.Headers.AllKeys)
            {
                switch (key.ToUpper())
                {
                    case "CONTENT-LENGTH":
                        // don't set aResponse.ContentLength64 yet as we may re-write some content below (if we're serving Node.js)
                        contentLength = Convert.ToInt32(aProxiedResponse.Headers.GetValues(key)[0]);
                        break;
                    case "CONTENT-TYPE":
                    case "EXT":
                    case "SERVER":
                        string[] values = aProxiedResponse.Headers.GetValues(key);
                        foreach (string val in values)
                            aResponse.Headers.Add(key, val);
                        break;
                    case "TRANSFER-ENCODING":
                        aResponse.SendChunked = (String.Compare(aProxiedResponse.Headers.GetValues(key)[0], "chunked", true) == 0);
                        break;
                    case "CONNECTION":
                        aResponse.Headers.Add(key, aProxiedResponse.Headers.GetValues(key)[0]);
                        break;
                    default:
                        Logger.InfoFormat("Ignored header in response: {0}", key);
                        break;
                }
            }
            bool useGzip = false;
            if (!aResponse.SendChunked && contentLength > 512 /* no point in zipping tiny responses */ &&
                aProxiedResponse.ContentType != null && 
                !(aProxiedResponse.ContentType.Contains("image/png") || aProxiedResponse.ContentType.Contains("image/jpeg")))
                // no point in wasting time zipping a format that is already compressed
                useGzip = true;
            Stream clientRespStream = aResponse.OutputStream;
            using (Stream respStream = aProxiedResponse.GetResponseStream())
            {
                if (aUrl.EndsWith("/Node.js"))
                {
                    RewriteNodeJsFile(aResponse, respStream);
                }
                else if (!useGzip)
                {
                    if (contentLength > 0) // response may be chunked
                        aResponse.ContentLength64 = contentLength;
                    respStream.CopyTo(clientRespStream);
                }
                else
                {
                    aResponse.AddHeader("Content-Encoding", "gzip");
                    MemoryStream zip = new MemoryStream();
                    using (var zipper = new GZipStream(zip, CompressionMode.Compress, true))
                    {
                        respStream.CopyTo(zipper);
                    }
                    zip.Seek(0, SeekOrigin.Begin);
                    aResponse.ContentLength64 = zip.Length;
                    //Console.WriteLine("Compressed {0} to {1} bytes", contentLength, zip.Length);
                    zip.CopyTo(clientRespStream);
                }
            }
            clientRespStream.Close();
        }
        static void RewriteNodeJsFile(HttpListenerResponse aClientResp, Stream aFileStream)
        {
            int contentLength = 0;
            MemoryStream memStream = new MemoryStream();
            MemoryStream outStream = new MemoryStream();
            aFileStream.CopyTo(memStream);
            byte[] bytes = memStream.ToArray();
            memStream = new MemoryStream(bytes);
            StreamReader reader = new StreamReader(memStream);
            string line;
            const string wsPortDecl = "var webSocketPort =";
            const string isRemoteDecl = "var isRemote =";
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(wsPortDecl))
                    line = String.Format("{0} {1};", wsPortDecl, kRemoteAccessPort);
                else if (line.StartsWith(isRemoteDecl))
                    line = String.Format("{0} {1};", isRemoteDecl, "true");
                line += "\r\n";
                contentLength += line.Length;
                byte[] buf = Encoding.UTF8.GetBytes(line);
                outStream.Write(buf, 0, buf.Length);
            }
            aClientResp.ContentLength64 = contentLength;
            outStream.Seek(0, SeekOrigin.Begin);
            outStream.CopyTo(aClientResp.OutputStream);
        }
        public void Dispose()
        {
            if (iHttpServer != null)
                iHttpServer.Dispose();
        }
    }
}
