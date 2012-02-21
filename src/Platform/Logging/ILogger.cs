namespace OpenHome.Os.Platform.Logging
{
    public interface ILogger
    {
        void Log(string aFormatString, params object[] aFormatArgs);
    }
}
