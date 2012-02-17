using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using OpenHome.Net.Device;

namespace OpenHome.Os.Platform.Logging
{
    [Serializable]
    public class LogLevelXmlException : XmlException
    {
        public LogLevelXmlException()
        {
        }

        public LogLevelXmlException(string aMessage) : base(aMessage)
        {
        }

        public LogLevelXmlException(string aMessage, Exception aInner) : base(aMessage, aInner)
        {
        }

        protected LogLevelXmlException(
            SerializationInfo aInfo,
            StreamingContext aContext) : base(aInfo, aContext)
        {
        }
    }

    public static class LogLevelsSerialization
    {
        static public XElement LogLevelsToXml(IEnumerable<KeyValuePair<string, string>> aLogLevels)
        {
            XElement logLevelsListElement = new XElement("logLevelsList");
            foreach (var kvp in aLogLevels)
            {
                logLevelsListElement.Add(
                    new XElement("logger",
                        new XElement("name", kvp.Key),
                        new XElement("level", kvp.Value)));
            }
            return logLevelsListElement;
        }

        static public Dictionary<string, string> ParseLogXml(XElement aLogLevelsListElement)
        {
            Dictionary<string, string> logLevels = new Dictionary<string, string>();
            if (aLogLevelsListElement.Name != "logLevelsList")
            {
                throw new LogLevelXmlException("Bad XML");
            }
            foreach (XElement loggerElement in aLogLevelsListElement.Elements())
            {
                if (loggerElement.Name != "logger")
                {
                    throw new LogLevelXmlException("Bad XML");
                }
                var nameElement = loggerElement.Element("name");
                if (nameElement==null) throw new LogLevelXmlException("Bad XML");
                var levelElement = loggerElement.Element("level");
                if (levelElement==null) throw new LogLevelXmlException("Bad XML");
                logLevels[nameElement.Value] = levelElement.Value;
            }
            return logLevels;
        }
    }
}
