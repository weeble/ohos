namespace OpenHome.Widget.Nodes.Logging
{
    public class NullLogger : ILogger
    {
        public void Log(string aFormatString, params object[] aFormatArgs)
        {
        }
    }
}
