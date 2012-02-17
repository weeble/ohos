namespace OpenHome.Os.Platform.Logging
{
    public class NullLogger : ILogger
    {
        public void Log(string aFormatString, params object[] aFormatArgs)
        {
        }
    }
}
