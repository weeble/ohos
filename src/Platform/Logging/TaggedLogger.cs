namespace OpenHome.Widget.Nodes.Logging
{
    public class TaggedLogger : ILogger
    {
        private ILogger iBaseLogger;
        private string iTag;
        public TaggedLogger(ILogger aBaseLogger, string aTag)
        {
            iBaseLogger = aBaseLogger;
            iTag = aTag;
        }
        public void Log(string aFormatString, params object[] aFormatArgs)
        {
            iBaseLogger.Log(iTag + aFormatString, aFormatArgs);
        }
    }
}
