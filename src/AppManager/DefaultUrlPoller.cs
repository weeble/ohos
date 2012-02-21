using System;
using System.Net;
using log4net;

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
                request.Timeout = 5000;
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

}