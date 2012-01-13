using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;

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
            string forwardAddress = "http://127.0.0.1:57756";
            string resourcePath = "/7b6912d6-8ee8-4702-86ad-980574a5d6ea/Upnp/resource/";
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
                            Console.WriteLine("Unhandled CONNECTION header in request: {0}", clientReq.Headers.GetValues(key)[0]);
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
            foreach (var key in resp.Headers.AllKeys)
            {
                switch (key.ToUpper())
                {
                    case "CONTENT-LENGTH":
                        clientResp.ContentLength64 = Convert.ToInt64(resp.Headers.GetValues(key)[0]);
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
            System.IO.Stream clientRespStream = clientResp.OutputStream;
            resp.GetResponseStream().CopyTo(clientRespStream);
            clientRespStream.Close();
            if (connectionClose)
                clientResp.Close();
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
