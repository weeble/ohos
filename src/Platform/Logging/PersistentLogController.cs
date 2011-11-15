using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using log4net;

namespace OpenHome.Widget.Nodes.Logging
{
    /// <summary>
    /// Wraps a set of controls for log levels and saves any changes to disk.
    /// </summary>
    class PersistentLogController : ILogController
    {
        readonly static ILog Logger = LogManager.GetLogger(typeof(PersistentLogController));
        readonly ILogController iWrappedLogController;
        readonly string iSettingsFileName;
        readonly object iLock = new object();

        public PersistentLogController(ILogController aWrappedLogController, string aSettingsFileName)
        {
            iWrappedLogController = aWrappedLogController;
            iSettingsFileName = aSettingsFileName;
        }

        public void Reload()
        {
            lock (iLock)
            {
                try
                {
                    using (var reader = new StreamReader(new FileStream(iSettingsFileName, FileMode.Open)))
                    {
                        var element = XElement.Load(reader);
                        var logLevels = LogLevelsSerialization.ParseLogXml(element);
                        iWrappedLogController.SetLogLevels(logLevels);
                    }
                }
                catch (IOException)
                {
                    // That's okay. There is no settings file, so just leave everything
                    // at the defaults.
                    Logger.Info("No saved settings for log levels. Using defaults.");
                }
                catch (XmlException)
                {
                    // There *is* a settings file, but there's something wrong with it.
                    Logger.Error("Log level settings file unreadable. Using defaults.");
                }
            }
        }

        public Dictionary<string, string> GetLogLevels()
        {
            lock (iLock)
            {
                return iWrappedLogController.GetLogLevels();
            }
        }

        public void SetLogLevel(string aLogName, string aLogLevel)
        {
            lock (iLock)
            {
                var logLevels = iWrappedLogController.GetLogLevels();
                logLevels[aLogName] = aLogLevel;
                InternalSetLogLevels(logLevels);
            }
        }

        public void SetLogLevels(Dictionary<string, string> aLogLevels)
        {
            lock (iLock)
            {
                InternalSetLogLevels(aLogLevels);
            }
        }

        void InternalSetLogLevels(Dictionary<string, string> aLogLevels)
        {
            iWrappedLogController.SetLogLevels(aLogLevels);
            var element = LogLevelsSerialization.LogLevelsToXml(aLogLevels);
            Directory.CreateDirectory(Path.GetDirectoryName(iSettingsFileName));
            element.Save(iSettingsFileName);
        }
    }
}
