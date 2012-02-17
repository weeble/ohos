namespace OpenHome.Os.Platform.Logging
{
    public class TaggedLogger : ILogger
    {
        private readonly ILogger iBaseLogger;
        private readonly string iTag;
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
