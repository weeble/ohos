using System;
using System.Collections.Generic;
using System.IO;
using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace OpenHome.Os.Platform.Logging
{
    public class LogSystem
    {
        public ILogReader LogReader { get; private set; }
        public ILogController LogController { get; private set; }
        public LogSystem(ILogReader aLogReader, ILogController aLogController)
        {
            LogReader = aLogReader;
            LogController = aLogController;
        }
    }

    public interface ILogReader
    {
        string GetLogTail(int aMaxBytes);
    }

    public interface ILogController
    {
        Dictionary<string, string> GetLogLevels();
        void SetLogLevel(string aLogName, string aLogLevel);
        void SetLogLevels(Dictionary<string, string> aLogLevels);
    }

    public static class Log4Net
    {
        static readonly PatternLayout DefaultPatternLayout = new PatternLayout("%date [%thread] %-5level %logger - %message%newline");

        private class LogReader : ILogReader
        {
            readonly string iLogFileName;

            public LogReader(string aLogFileName)
            {
                iLogFileName = aLogFileName;
            }

            public string GetLogTail(int aMaxBytes)
            {
                FileStream fs = new FileStream(
                    iLogFileName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                long seekLocation = Math.Max(0, fs.Length - aMaxBytes);
                fs.Seek(seekLocation, SeekOrigin.Begin);
                using (TextReader tr = new StreamReader(fs))
                {
                    return tr.ReadToEnd();
                }
            }
        }


        public static string GetDefaultLogFile(string aAppName)
        {
            // 128 was returned by Mono on Unix platforms before PlatformID.Unix was defined.
            if (Environment.OSVersion.Platform == PlatformID.Unix || (int)Environment.OSVersion.Platform == 128)
            {
                return String.Format("/var/log/{0}", aAppName);
            }
            return Path.Combine(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    aAppName),
                aAppName+".log");
        }

        public static LogSystem SetupLog4NetLogging(string aXmlConfigFile, string aLogFile, string aLogSettingsFile)
        {
            SetupUserConfigurableLogging(aXmlConfigFile);
            var logReader = SetupLoggingToFile(aLogFile);
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var logController = new LogController(hierarchy, hierarchy.Root);
            var persistentLogController = new PersistentLogController(logController, aLogSettingsFile);
            persistentLogController.Reload();
            return new LogSystem(
                logReader,
                persistentLogController);
        }

        static ILogReader SetupLoggingToFile(string aLogFile)
        {
            var patternLayout = DefaultPatternLayout;
            patternLayout.ActivateOptions();
            FileAppender fa = new FileAppender
                                  {
                                      File = aLogFile,
                                      Layout = patternLayout,
                                      AppendToFile = true,
                                      LockingModel = new FileAppender.MinimalLock()
                                  };
            fa.ActivateOptions();
            //Console.WriteLine(fa.File);
            ((Hierarchy)LogManager.GetRepository()).Root.AddAppender(fa);
            return new LogReader(aLogFile);
        }

        static void SetupUserConfigurableLogging(string aXmlConfigFile)
        {
            if (!File.Exists(aXmlConfigFile))
            {
                log4net.Config.BasicConfigurator.Configure(new ConsoleAppender { Layout = DefaultPatternLayout });
            }
            else
            {
                log4net.Config.XmlConfigurator.Configure(new FileInfo(aXmlConfigFile));
            }
        }
    }
}
