using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Text;

namespace OpenHome.Os.Remote
{
    public class Program
    {
        static void Main()
        {
            ProxyServer proxy = new ProxyServer();
            const uint adapter = (1 << 24) | 127;
            //const uint adapter = (78 << 24) | (9 << 16) | (2 << 8) | 10;
            proxy.Enable(adapter, 57022, "remote");
            Thread.Sleep(60 * 60 * 1000); // wait for 1 hour
            proxy.Dispose();
        }
    }

    public class ProxyServer : IDisposable
    {
        private HttpServer iHttpServer;
        private string iForwardAddress;
        private uint iForwardPort;
        private string iForwardUdn;
        private Dictionary<string, string> iAuthenticatedClients;
        private const string kAuthCookieName = "remoteId";

        public void Enable(uint aNetworkAdapter, uint aPort, string aUdn)
        {
            iForwardAddress = String.Format("{0}.{1}.{2}.{3}", aNetworkAdapter & 0xff, (aNetworkAdapter >> 8) & 0xff, (aNetworkAdapter >> 16) & 0xff, (aNetworkAdapter >> 24) & 0xff); // assumes little endian
            iForwardPort = aPort;
            iForwardUdn = aUdn;
            iAuthenticatedClients = new Dictionary<string, string>();
            iHttpServer = new HttpServer(8);
            iHttpServer.Start(8082, ProcessRequest);
        }
        private void ProcessRequest(HttpListenerContext aContext)
        {
            HttpListenerRequest clientReq = aContext.Request;
            HttpListenerResponse clientResp = aContext.Response;

            if (IsAuthenticating(clientReq, clientResp))
                return;
            string targetUrl = RewriteUrl(clientReq);
            if (targetUrl == null)
            {
                clientResp.StatusCode = (int)HttpStatusCode.NotFound;
                clientResp.Close();
                return;
            }
            Console.WriteLine("Method: {0}, url: {1}", clientReq.HttpMethod, targetUrl);
            HttpWebRequest forwardedReq = (HttpWebRequest)WebRequest.Create(targetUrl);
            bool connectionClose;
            bool connectionKeepAlive;
            WriteForwardedRequestHeaders(clientReq, forwardedReq, out connectionKeepAlive, out connectionClose);
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)forwardedReq.GetResponse();
            }
            catch (WebException e)
            {
                resp = (HttpWebResponse)e.Response;
                Console.WriteLine("ERROR: {0} for {1}", (int)resp.StatusCode, targetUrl);
            }
            WriteResponse(resp, clientResp);
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
                Console.WriteLine("FIXME - need to actually check username/password");
                Console.WriteLine("...username={0}, password={1}", username, password);

                string guid = Guid.NewGuid().ToString();
                lock (this)
                {
                    iAuthenticatedClients.Add(guid, guid);
                    // TODO: write clients to xml file
                }
                aResponse.AppendCookie(new Cookie(kAuthCookieName, guid));
                aResponse.StatusCode = (int)HttpStatusCode.OK;
                location = aRequest.Headers.GetValues("HOST")[0];
                if (!location.StartsWith("http"))
                    location = "http://" + location;
                aResponse.ContentLength64 = location.Length + 2;
                byte[] buf = Encoding.UTF8.GetBytes(location + "\r\n");
                aResponse.OutputStream.Write(buf, 0, buf.Length);
                Console.WriteLine("Redirecting: {0} to {1}", pathAndQuery, location);
                // just completed authentication.  Redirect client to (assumed) original url
                return true;
            }

            foreach (Cookie cookie in aRequest.Cookies)
            {
                if (cookie.Name == kAuthCookieName && !iAuthenticatedClients.ContainsKey(cookie.Value))
                    Console.WriteLine("WARNING: received old cookie. May need to clear cookies on browser to log in");
                if (cookie.Name == kAuthCookieName && iAuthenticatedClients.ContainsKey(cookie.Value))
                    // already authenticated
                    return false;
            }
            
            if (pathAndQuery == "/login.html" || pathAndQuery.StartsWith("/login/"))
                // allow these requests through, regardless of our authentication state as they're needed to load the login screen
                return false;

            // redirect any other requests to the login page
            location = aRequest.Headers.GetValues("HOST")[0];
            if (!location.StartsWith("http"))
                location = "http://" + location;
            if (!location.EndsWith("/"))
                location += "/";
            location += "login.html";
            aResponse.Redirect(location);
            aResponse.Close();
            Console.WriteLine("Redirecting: {0} to {1}", pathAndQuery, location);
            return true;
        }
        private string RewriteUrl(HttpListenerRequest aRequest)
        {
            string url = null;
            string pathAndQuery = aRequest.Url.PathAndQuery;
            string method = aRequest.HttpMethod.ToUpper();
            switch (method)
            {
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
                    if (pathAndQuery.StartsWith("/"))
                        pathAndQuery = pathAndQuery.Remove(0, 1);
                    url = String.Format("http://{0}:{1}/{2}/Upnp/resource/{3}", iForwardAddress, iForwardPort, iForwardUdn, pathAndQuery);
                    //targetUrl = forwardAddress + resourcePath + pathAndQuery;
                    break;
                case "POST":
                    url = String.Format("http://{0}:{1}{2}", iForwardAddress, iForwardPort, pathAndQuery);
                    //url = forwardAddress + pathAndQuery;
                    break;
                default:
                    Console.WriteLine("Unexpected method - {0}", method);
                    break;
            }
            return url;
        }
        private static void WriteForwardedRequestHeaders(HttpListenerRequest aClientReq, HttpWebRequest aForwardedReq, out bool aConnectionKeepAlive, out bool aConnectionClose)
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
                        //Console.WriteLine("Unhandled CONNECTION header in request: {0}", clientReq.Headers.GetValues(key)[0]);
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
                        Console.WriteLine("Ignored header in request: {0}", key);
                        break;
                }
            }
            if (String.Compare(aClientReq.HttpMethod, "POST", true) == 0)
                aClientReq.InputStream.CopyTo(aForwardedReq.GetRequestStream());
        }
        private static void WriteResponse(HttpWebResponse aProxiedResponse, HttpListenerResponse aResponse)
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
                        Console.WriteLine("Ignored header in response: {0}", key);
                        break;
                }
            }
            Stream clientRespStream = aResponse.OutputStream;
            Stream respStream = aProxiedResponse.GetResponseStream();
            // following can remain commented until we want to proxy websocket connections
            /*if (targetUrl.EndsWith("/Node.js"))
            {
                RewriteNodeJsFile(clientResp, respStream);
            }
            else
            {*/
            if (contentLength > 0) // response may be chunked
                aResponse.ContentLength64 = contentLength;
            respStream.CopyTo(clientRespStream);
            //}
            clientRespStream.Close();
        }
        /*static void RewriteNodeJsFile(HttpListenerResponse aClientResp, Stream aFileStream)
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
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith(wsPortDecl))
                    line = String.Format("{0} {1};", wsPortDecl, 8080); // TODO: remove hard-coding of port
                line += "\r\n";
                contentLength += line.Length;
                byte[] buf = Encoding.UTF8.GetBytes(line);
                outStream.Write(buf, 0, buf.Length);
            }
            aClientResp.ContentLength64 = contentLength;
            outStream.Seek(0, SeekOrigin.Begin);
            outStream.CopyTo(aClientResp.OutputStream);
        }*/
        public void Dispose()
        {
            iHttpServer.Dispose();
        }
    }

    class HttpServer : IDisposable
    {
        private readonly HttpListener iListener;
        private readonly Thread iListenerThread;
        private readonly Thread[] iWorkers;
        private readonly ManualResetEvent iStop;
        private readonly ManualResetEvent iReady;
        private readonly Queue<HttpListenerContext> iQueue;
        private Action<HttpListenerContext> iRequestHandler;

        public HttpServer(int aMaxThreads)
        {
            iWorkers = new Thread[aMaxThreads];
            iQueue = new Queue<HttpListenerContext>();
            iStop = new ManualResetEvent(false);
            iReady = new ManualResetEvent(false);
            iListener = new HttpListener();
            iListenerThread = new Thread(HandleRequests);
        }
        public void Start(int aPort, Action<HttpListenerContext> aRequestHandler)
        {
            iRequestHandler = aRequestHandler;
            iListener.Prefixes.Add(String.Format(@"http://+:{0}/", aPort));
            iListener.Start();
            iListenerThread.Start();

            for (int i = 0; i < iWorkers.Length; i++)
            {
                iWorkers[i] = new Thread(Worker);
                iWorkers[i].Start();
            }
        }
        public void Dispose()
        {
            Stop();
        }
        public void Stop()
        {
            iStop.Set();
            iListenerThread.Join();
            foreach (Thread worker in iWorkers)
                worker.Join();
            iListener.Stop();
        }
        private void HandleRequests()
        {
            while (iListener.IsListening)
            {
                var context = iListener.BeginGetContext(ContextReady, null);
                if (0 == WaitHandle.WaitAny(new[] { iStop, context.AsyncWaitHandle }))
                    return;
            }
        }
        private void ContextReady(IAsyncResult ar)
        {
            try
            {
                lock (iQueue)
                {
                    iQueue.Enqueue(iListener.EndGetContext(ar));
                    iReady.Set();
                }
            }
            catch { return; }
        }
        private void Worker()
        {
            WaitHandle[] wait = new[] { iReady, iStop };
            while (0 == WaitHandle.WaitAny(wait))
            {
                HttpListenerContext context;
                lock (iQueue)
                {
                    if (iQueue.Count > 0)
                        context = iQueue.Dequeue();
                    else
                    {
                        iReady.Reset();
                        continue;
                    }
                }

                try
                {
                    iRequestHandler(context);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
