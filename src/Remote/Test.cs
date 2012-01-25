using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using System.IO;

namespace OpenHome.Os.Remote
{
    public class Program
    {
        static void Main()
        {
            ProxyServer manager = new ProxyServer();
            uint loopback = (1 << 24) | 127;
            manager.Enable(loopback, 52128, "bbc19702-27a7-4fe8-bdd4-335d0a13c009");
            Thread.Sleep(15 * 60 * 1000);
            manager.Dispose();
        }
    }

    public class ProxyServer : IDisposable
    {
        private HttpServer iHttpServer;
        private string iForwardAddress;
        private uint iForwardPort;
        private string iForwardUdn;

        public void Enable(uint aNetworkAdapter, uint aPort, string aUdn)
        {
            iForwardAddress = String.Format("{0}.{1}.{2}.{3}", aNetworkAdapter & 0xff, (aNetworkAdapter >> 8) & 0xff, (aNetworkAdapter >> 16) & 0xff, (aNetworkAdapter >> 24) & 0xff); // assumes little endian
            iForwardPort = aPort;
            iForwardUdn = aUdn;
            iHttpServer = new HttpServer(8);
            iHttpServer.Start(8080, ProcessRequest);
        }
        private void ProcessRequest(HttpListenerContext aContext)
        {
            HttpListenerRequest clientReq = aContext.Request;
            HttpListenerResponse clientResp = aContext.Response;
            string pathAndQuery = clientReq.Url.PathAndQuery;
            bool connectionClose;
            bool connectionKeepAlive;
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
                                // we can't support websockets so reject any handshake attempt to encourage the client to switch to long polling instead
                                clientResp.StatusCode = 404;
                                clientResp.Close();
                                return;
                            }
                        }
                    }
                    if (pathAndQuery.StartsWith("/"))
                        pathAndQuery = pathAndQuery.Remove(0, 1);
                    targetUrl = String.Format("http://{0}:{1}/{2}/Upnp/resource/{3}", iForwardAddress, iForwardPort, iForwardUdn, pathAndQuery);
                    //targetUrl = forwardAddress + resourcePath + pathAndQuery;
                    break;
                case "POST":
                    targetUrl = String.Format("http://{0}:{1}{2}", iForwardAddress, iForwardPort, pathAndQuery);
                    //targetUrl = forwardAddress + pathAndQuery;
                    break;
                default:
                    Console.WriteLine("Unexpected method - {0}", method);
                    clientResp.StatusCode = 404;
                    clientResp.Close();
                    return;
            }
            Console.WriteLine("Method: {0}, url: {1}", clientReq.HttpMethod, targetUrl);
            HttpWebRequest forwardedReq = (HttpWebRequest)WebRequest.Create(targetUrl);
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
            // following can remain commented until we want to proxy websocket connections
            /*if (targetUrl.EndsWith("/Node.js"))
            {
                RewriteNodeJsFile(clientResp, respStream);
            }
            else
            {*/
                if (contentLength > 0) // response may be chunked
                    clientResp.ContentLength64 = contentLength;
                respStream.CopyTo(clientRespStream);
            //}
            clientRespStream.Close();
            // docs suggest following is unnecessary - we only have to close one from clientRespStream / clientResp
            if (connectionClose)
                clientResp.Close();
        }
        static void WriteForwardedRequestHeaders(HttpListenerRequest aClientReq, HttpWebRequest aForwardedReq, out bool aConnectionKeepAlive, out bool aConnectionClose)
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
