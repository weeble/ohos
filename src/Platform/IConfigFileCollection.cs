using System;
using System.Xml.Linq;

namespace OpenHome.Os.Platform
{
    public interface IConfigFileCollection
    {
        IConfigFileCollection GetSubcollection(string aXPath);
        XAttribute GetAttribute(string aXPath);
        XElement GetElement(string aXPath);
        string GetAttributeValue(string aXPath);
        string GetAttributeAsFilepath(string aXPath, string aDefault);
        string GetElementValue(string aXPath);
        string GetElementValueAsFilepath(string aXPath);
        bool? GetAttributeAsBoolean(string aXPath);
    }

    public class NullConfigFileCollection : IConfigFileCollection
    {
        public IConfigFileCollection GetSubcollection(string aXPath)
        {
            return this;
        }

        public XAttribute GetAttribute(string aXPath) { return null; }
        public XElement GetElement(string aXPath) { return null; }
        public string GetAttributeValue(string aXPath) { return null; }
        public string GetAttributeAsFilepath(string aXPath, string aDefault) { return null; }
        public string GetElementValue(string aXPath) { return null; }
        public string GetElementValueAsFilepath(string aXPath) { return null; }
        public bool? GetAttributeAsBoolean(string aXPath) { return null; }
    }
}