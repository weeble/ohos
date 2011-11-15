namespace OpenHome.Widget.Nodes.Logging
{
    public class TextWriterLogger : ILogger
    {
        private System.IO.TextWriter iWriter;
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
