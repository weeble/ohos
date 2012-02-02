using System;
using System.Runtime.Serialization;

namespace OpenHome.Os.AppManager
{
    [Serializable]
    public class BadPluginException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public BadPluginException()
        {
        }

        public BadPluginException(string message) : base(message)
        {
        }

        public BadPluginException(string message, Exception inner) : base(message, inner)
        {
        }

        protected BadPluginException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}