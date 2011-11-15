namespace OpenHome.Widget.Nodes.Logging
{
    public interface ILogger
    {
        void Log(string aFormatString, params object[] aFormatArgs);
    }
}
