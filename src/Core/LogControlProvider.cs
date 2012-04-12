using System;
using System.Xml;
using System.Xml.Linq;
using OpenHome.Net.Device;
using OpenHome.Os.Platform.Logging;

namespace OpenHome.Os.Core
{
    public interface ILogControlProvider
    {
        void GetRecentLog(out string aLogText);
        void GetLogLevelsXml(out string aLogLevelsXml);
        void SetLogLevelsXml(string aLogLevelsXml);
    }

    public class LogControlProvider : ILogControlProvider
    {
        const int RecentLogBytes = 100000;
        readonly ILogReader iLogReader;
        readonly ILogController iLogController;

        public LogControlProvider(ILogReader aLogReader, ILogController aLogController)
        {
            iLogReader = aLogReader;
            iLogController = aLogController;
        }

        public void GetRecentLog(out string aLogText)
        {
            aLogText = iLogReader.GetLogTail(RecentLogBytes);
        }

        public void GetLogLevelsXml(out string aLogLevelsXml)
        {
            aLogLevelsXml = LogLevelsSerialization.LogLevelsToXml(iLogController.GetLogLevels()).ToString();
        }

        public void SetLogLevelsXml(string aLogLevelsXml)
        {
            try
            {
                XElement logLevelsListElement = XElement.Parse(aLogLevelsXml);
                var logLevels = LogLevelsSerialization.ParseLogXml(logLevelsListElement);
                iLogController.SetLogLevels(logLevels);
            }
            catch (XmlException)
            {
                throw new ActionError();
            }
            catch (ArgumentException)
            {
                throw new ActionError();
            }
        }
    }
}
