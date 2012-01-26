using System;
using System.Net;
using System.Threading;
using System.Collections.Generic;

namespace OpenHome.Os.Remote
{
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
