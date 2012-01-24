using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net.Sockets;

namespace OpenHome.Os.Remote
{
    public class Program
    {
        static void Main()
        {
            HttpServer server = new HttpServer(8);
            server.Start(8080, ProcessRequest);
            Thread.Sleep(15 * 60 * 1000);
            server.Dispose();
        }

        private static void ProcessRequest(HttpListenerContext aContext)
        {
            const string kAppName = "/ohWidget";
            string forwardAddress = "http://127.0.0.1:50610"; // TODO: should be specified by client
            string resourcePath = "/6ad23af3-6ccd-4460-982d-34bb93f62a09/Upnp/resource/"; // TODO: should be specified by client
            HttpListenerRequest clientReq = aContext.Request;
            HttpListenerResponse clientResp = aContext.Response;
            string pathAndQuery = clientReq.Url.PathAndQuery;
            bool connectionClose;
            bool connectionKeepAlive = connectionClose = false;
            string targetUrl;
            string method = clientReq.HttpMethod.ToUpper();
            switch (method)
            {
                case "GET":
                    if (pathAndQuery == "/")
                    {
                        foreach (string key in clientReq.Headers.AllKeys)
                        {
                            if (key.ToUpper() == "UPGRADE" && clientReq.Headers.GetValues(key)[0].ToUpper() == "WEBSOCKET")
                            {
                                Console.WriteLine("!!! Ws connect attempt !!!");
                                HandleWebSocketConnection(aContext);
                                return;
                            }
                        }
                    }
                    if (pathAndQuery.StartsWith(kAppName))
                        pathAndQuery = pathAndQuery.Remove(0, kAppName.Length);
                    if (pathAndQuery.StartsWith("/"))
                        pathAndQuery = pathAndQuery.Remove(0, 1);
                    targetUrl = forwardAddress + resourcePath + pathAndQuery;
                    break;
                case "POST":
                    targetUrl = forwardAddress + pathAndQuery;
                    break;
                default:
                    Console.WriteLine("Unexpected method - {0}", method);
                    clientResp.StatusCode = 404;
                    clientResp.Close();
                    return;
            }
            Console.WriteLine("Method: {0}, url: {1}", clientReq.HttpMethod, targetUrl);
            HttpWebRequest forwardedReq = (HttpWebRequest)WebRequest.Create(targetUrl);
            forwardedReq.Method = clientReq.HttpMethod;
            foreach (string key in clientReq.Headers.AllKeys)
            {
                switch (key.ToUpper())
                {
                    case "HOST":
                        forwardedReq.Host = clientReq.Headers.GetValues(key)[0];
                        break;
                    case "CONTENT-LENGTH":
                        forwardedReq.ContentLength = Convert.ToInt32(clientReq.Headers.GetValues(key)[0]);
                        break;
                    case "TRANSFER-ENCODING":
                        forwardedReq.TransferEncoding = clientReq.Headers.GetValues(key)[0];
                        break;
                    case "CACHE-CONTROL":
                        // TODO
                        //forwardedReq.CachePolicy = clientReq.Headers.GetValues(key)[0];
                        break;
                    case "CONTENT-TYPE":
                        forwardedReq.ContentType = clientReq.Headers.GetValues(key)[0];
                        break;
                    case "CONNECTION":
                        string value = clientReq.Headers.GetValues(key)[0];
                        if (String.Compare(value, "keep-alive", true) == 0)
                            connectionKeepAlive = true;
                        else if (String.Compare(value, "close", true) == 0)
                            connectionClose = true;
                        else
                            forwardedReq.Connection = clientReq.Headers.GetValues(key)[0];
                            //Console.WriteLine("Unhandled CONNECTION header in request: {0}", clientReq.Headers.GetValues(key)[0]);
                        forwardedReq.KeepAlive = connectionKeepAlive;
                        break;
                    case "ACCEPT":
                        forwardedReq.Accept = clientReq.Headers.GetValues(key)[0];
                        break;
                    case "ACCEPT-CHARSET":
                    case "ACCEPT-ENCODING":
                    case "ACCEPT-LANGUAGE":
                    case "ORIGIN":
                    case "SOAPACTION":
                    //case "SEC-WEBSOCKET-PROTOCOL":
                    //case "SEC-WEBSOCKET-KEY":
                    //case "SEC-WEBSOCKET-VERSION":
                    //case "UPGRADE":
                        string[] values = clientReq.Headers.GetValues(key);
                        foreach (string val in values)
                            forwardedReq.Headers.Add(key, val);
                        break;
                    case "USER-AGENT":
                    case "REFERER":
                        // we deliberately don't pass these on
                        break;
                    default:
                        Console.WriteLine("Ignored header in request: {0}", key);
                        break;
                }
            }
            if (String.Compare(clientReq.HttpMethod, "POST", true) == 0)
                clientReq.InputStream.CopyTo(forwardedReq.GetRequestStream());

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
            clientResp.StatusCode = (int)resp.StatusCode;
            clientResp.StatusDescription = resp.StatusDescription;
            int contentLength = 0;
            foreach (var key in resp.Headers.AllKeys)
            {
                switch (key.ToUpper())
                {
                    case "CONTENT-LENGTH":
                        // don't set clientResp.ContentLength64 yet as we may re-write some content below (if we're serving Node.js)
                        contentLength = Convert.ToInt32(resp.Headers.GetValues(key)[0]);
                        break;
                    case "CONTENT-TYPE":
                    case "EXT":
                    case "SERVER":
                        string[] values = resp.Headers.GetValues(key);
                        foreach (string val in values)
                            clientResp.Headers.Add(key, val);
                        break;
                    case "TRANSFER-ENCODING":
                        clientResp.SendChunked = (String.Compare(resp.Headers.GetValues(key)[0], "chunked", true) == 0);
                        break;
                    case "CONNECTION":
                        clientResp.Headers.Add(key, resp.Headers.GetValues(key)[0]);
                        break;
                    default:
                        Console.WriteLine("Ignored header in response: {0}", key);
                        break;
                }
            }
            Stream clientRespStream = clientResp.OutputStream;
            Stream respStream = resp.GetResponseStream();
            if (!targetUrl.EndsWith("/Node.js"))
            {
                if (contentLength > 0) // response may be chunked
                    clientResp.ContentLength64 = contentLength;
                respStream.CopyTo(clientRespStream);
            }
            else
            {
                contentLength = 0; // we're re-writing content so ignore the received length
                MemoryStream memStream = new MemoryStream();
                MemoryStream outStream = new MemoryStream();
                respStream.CopyTo(memStream);
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
                clientResp.ContentLength64 = contentLength;
                outStream.Seek(0, SeekOrigin.Begin);
                outStream.CopyTo(clientRespStream);
            }
            clientRespStream.Close();
            // docs suggest following is unnecessary - we only have to close one from clientRespStream / clientResp
            if (connectionClose)
                clientResp.Close();
        }
        private static void HandleWebSocketConnection(HttpListenerContext aContext)
        {
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IPv4);
            socket.Connect("http://127.0.0.1", 8080); // TODO: remove hard-coding of address / port
            const string newline = "\r\n";
            string request = "GET / HTTP/1.1" + newline;
            System.Collections.Specialized.NameValueCollection headers = aContext.Request.Headers;
            foreach (string key in headers.AllKeys)
            {
                string header = String.Format("{0}: {1}{2}", key, headers.GetValues(key)[0], newline);
                request += header;
            }
            request += newline;
            socket.Send(Encoding.UTF8.GetBytes(request));
            byte[] serverMsg = new byte[4*1024];
            socket.BeginReceive(serverMsg, 0, serverMsg.Length, SocketFlags.None, WebSocketServerEvent, aContext);




            Semaphore blocker = new Semaphore(0, 1);
            blocker.WaitOne(); // TODO: allow exit when either side closes their socket

            socket.Shutdown(SocketShutdown.Receive);
            socket.Close();
            aContext.Response.Close();
        }
        private static void WebSocketServerEvent(IAsyncResult aAr)
        {
        }
    }

    class WebSocketConnection
    {
        private readonly HttpListenerContext iContext;
        private readonly Socket iSocket;

        public WebSocketConnection(HttpListenerContext aContext)
        {
            iContext = aContext;
            iSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IPv4);
        }
        public void Start()
        {
            iSocket.Connect("http://127.0.0.1", 8080); // TODO: remove hard-coding of address / port
            const string newline = "\r\n";
            string request = "GET / HTTP/1.1" + newline;
            System.Collections.Specialized.NameValueCollection headers = iContext.Request.Headers;
            foreach (string key in headers.AllKeys)
            {
                string header = String.Format("{0}: {1}{2}", key, headers.GetValues(key)[0], newline);
                request += header;
            }
            request += newline;
            iSocket.Send(Encoding.UTF8.GetBytes(request));
            byte[] serverMsg = new byte[4 * 1024];
            iSocket.BeginReceive(serverMsg, 0, serverMsg.Length, SocketFlags.None, WebSocketServerEvent, this);
        }
        private static void WebSocketServerEvent(IAsyncResult aAr)
        {
            WebSocketConnection self = (WebSocketConnection)aAr.AsyncState;
            SocketError error;
            self.iSocket.EndReceive(aAr, out error);
            if (error != SocketError.Success)
            {
                // TODO: do something to close connections
            }
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
