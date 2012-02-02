using System;
using System.Runtime.Serialization;

namespace OpenHome.Os.AppManager
{
    [Serializable]
    public class PluginFoundInWrongDirectoryException : Exception
    {
        public PluginFoundInWrongDirectoryException() { }
        public PluginFoundInWrongDirectoryException(string message) : base(message) { }
        public PluginFoundInWrongDirectoryException(string message, Exception inner) : base(message, inner) { }
        protected PluginFoundInWrongDirectoryException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}