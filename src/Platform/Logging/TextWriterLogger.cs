namespace OpenHome.Os.Platform.Logging
{
    public class TextWriterLogger : ILogger
    {
        private readonly System.IO.TextWriter iWriter;
        public TextWriterLogger(System.IO.TextWriter aWriter)
        {
            iWriter = aWriter;
        }
        public void Log(string aFormatString, params object[] aFormatArgs)
        {
            iWriter.WriteLine(aFormatString, aFormatArgs);
        }
    }
}
