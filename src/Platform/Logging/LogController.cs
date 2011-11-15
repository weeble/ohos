using System;
using System.Collections.Generic;
using System.Linq;
using log4net.Core;
using log4net.Repository;
using log4net.Repository.Hierarchy;

namespace OpenHome.Widget.Nodes.Logging
{
    /// <summary>
    /// Provides controls to inspect and change the log4net log levels.
    /// </summary>
    public class LogController : ILogController
    {
        const string RootLoggerName = "ROOT";
        const string InheritLevelName = "INHERIT";
        readonly ILoggerRepository iHierarchy;
        readonly Logger iRootLogger;
        public LogController(Hierarchy aHierarchy, Logger aRootLogger)
        {
            iHierarchy = aHierarchy;
            iRootLogger = aRootLogger;
        }
        public Dictionary<string, string> GetLogLevels()
        {
            Dictionary<string, string> levels =
                iHierarchy.GetCurrentLoggers()
                    .Cast<Logger>()
                    .ToDictionary(
                        aLogger => aLogger.Name,
                        aLogger => aLogger.Level==null ? InheritLevelName : aLogger.Level.Name);
            levels.Add(RootLoggerName, iRootLogger.Level.Name);
            return levels;
        }
        public void SetLogLevels(Dictionary<string, string> aLogLevels)
        {
            foreach (var kvp in aLogLevels)
            {
                SetLogLevel(kvp.Key, kvp.Value);
            }
        }
        public void SetLogLevel(string aLogName, string aLogLevel)
        {
            Logger logger = aLogName == RootLoggerName ?
                                                           iRootLogger :
                                                                           (Logger)iHierarchy.GetLogger(aLogName);
            // Note that we GetLogger *creates* the logger if it doesn't exist.
            // This is intended. We want to be able to configure log levels
            // even if the component involved hasn't started yet.
            Level level;
            if (aLogLevel == InheritLevelName)
            {
                level = null;
            }
            else
            {
                level = iHierarchy.LevelMap[aLogLevel];
                if (level == null)
                    throw new ArgumentException("No such level.");
            }
            logger.Level = level;
        }
    }
}