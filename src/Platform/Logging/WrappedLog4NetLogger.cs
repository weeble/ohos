using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace OpenHome.Os.Platform.Logging
{
    public class WrappedLog4NetLogger : ILogger
    {
        readonly ILog iLog;

        public WrappedLog4NetLogger(ILog aLog)
        {
            iLog = aLog;
        }

        public void Log(string aFormatString, params object[] aFormatArgs)
        {
            iLog.DebugFormat(aFormatString, aFormatArgs);
        }
    }
}
