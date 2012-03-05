using System;
using System.IO;
using System.Net;
using log4net;
using OpenHome.Os.Platform.Threading;

namespace OpenHome.Os.AppManager
{
    public enum DownloadAvailableState
    {
        Available,
        NotAvailable,
        Error
    };

    public interface IUrlPoller
    {
        DownloadAvailableState Poll(string aUrl, DateTime aLastModified);
    }

    class DefaultUrlPoller : IUrlPoller
    {
        static readonly ILog Logger = LogManager.GetLogger(typeof(DefaultUrlPoller));
        public DownloadAvailableState Poll(string aUrl, DateTime aLastModified)
        {
            try
            {
                Logger.DebugFormat("Polling URL for app update: {0}", aUrl);
                var request = WebRequest.Create(aUrl);
                request.Method = "HEAD";
                request.Timeout = 20000; // 20s
                using (var response = request.GetResponse())
                {
                    var httpResponse = response as HttpWebResponse;
                    if (httpResponse == null)
                    {
                        return DownloadAvailableState.Error;
                    }
                    Logger.DebugFormat("Poll succeeded, available={0}, have={1}, same={2}", httpResponse.LastModified, aLastModified, httpResponse.LastModified == aLastModified);
                    if (httpResponse.LastModified != aLastModified)
                    {
                        return DownloadAvailableState.Available;
                    }
                }
            }
            catch (WebException)
            {
                return DownloadAvailableState.Error;
            }
            return DownloadAvailableState.NotAvailable;
        }
    }

    public interface IDownloadListener
    {
        void Complete(DateTime aLastModified);
        void Failed();
        void Progress(int aBytes, int aBytesTotal);
    }

    public interface IUrlFetcher
    {
        IDisposable Fetch(string aUrl, FileStream aFileStream, IDownloadListener aListener);
    }

    class DefaultUrlFetcher : IUrlFetcher
    {
        class Download : IDisposable
        {
            readonly string iUrl;
            readonly FileStream iOutStream;
            readonly byte[] iBuffer;
            long iOffset;
            HttpWebResponse iResponse;
            Stream iResponseStream;
            DateTime iLastModified;
            //public Action<string, DateTime> CompletedCallback { get; private set; }
            //public Action FailedCallback { get; private set; }
            IDownloadListener iListener;

            public Download(string aUrl, int aBufferSize, FileStream aOutStream, IDownloadListener aListener)
            {
                iListener = aListener;
                iUrl = aUrl;
                iOutStream = aOutStream;
                iBuffer = new byte[aBufferSize];
            }

            public void Start()
            {
                try
                {
                    WebRequest request = WebRequest.Create(iUrl);
                    iResponse = request.GetResponse() as HttpWebResponse;
                    if (iResponse == null)
                    {
                        iListener.Failed();
                        return;
                    }
                    iLastModified = iResponse.LastModified;
                    iResponseStream = iResponse.GetResponseStream();
                    iOffset = 0;
                    BeginRead();
                }
                catch (Exception)
                {
                    iListener.Failed();
                    if (iResponseStream != null)
                    {
                        iResponseStream.Dispose();
                    }
                }
            }

            public void Dispose()
            {
                // One of four situations exist:
                //    1. Start failed and sent a failure message already.
                //    2. The download has already completed and success was sent.
                //    3. Start completed, but a read failed, the stream was closed and a failure was sent.
                //    4. Start completed, reads are ongoing.
                // In 1-3, a double-dispose is safe and harmless.
                // In 4, the dispose will trigger OnReadComplete to finish, will cause an exception in
                // EndRead, and that will send a failure message.
                if (iResponseStream != null)
                {
                    iResponseStream.Dispose();
                }
                iOutStream.Dispose();
            }

            void BeginRead()
            {
                iResponseStream.BeginRead(iBuffer, 0, iBuffer.Length, OnReadComplete, null);
            }

            void OnReadComplete(IAsyncResult aAr)
            {
                int count;
                try
                {
                    count = iResponseStream.EndRead(aAr);
                }
                catch (Exception)
                {
                    iResponseStream.Close();
                    iOutStream.Close();
                    iListener.Failed();
                    return;
                }
                if (count == 0)
                {
                    iResponseStream.Dispose();
                    iOutStream.Close();
                    iListener.Complete(iLastModified);
                }
                else
                {
                    iOutStream.Write(iBuffer, 0, count);
                    iOffset += count;
                    iListener.Progress((int)iOffset, (int)iResponse.ContentLength);
                    BeginRead();
                }
            }
        }

        public IDisposable Fetch(string aUrl, FileStream aFileStream, IDownloadListener aListener)
        {
            var download = new Download(
                aUrl,
                4096,
                aFileStream,
                aListener);
            download.Start();
            return download;
        }

    }

}