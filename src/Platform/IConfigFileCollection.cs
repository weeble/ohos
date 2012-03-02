using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace OpenHome.Os.Platform
{
    public interface IConfigFileCollection
    {
        IConfigFileCollection GetSubcollection(Func<XElement, XElement> aElementQuery);
        XAttribute GetAttribute(Func<XElement, XAttribute> aAttributeQuery);
        string GetAttributeValue(Func<XElement, XAttribute> aAttributeQuery);
        string GetAttributeAsFilepath(Func<XElement, XAttribute> aAttributeQuery, string aDefault);
        bool? GetAttributeAsBoolean(Func<XElement, XAttribute> aAttributeQuery);
        XElement GetElement(Func<XElement, XElement> aElementQuery);
        string GetElementValue(Func<XElement, XElement> aElementQuery);
        string GetElementValueAsFilepath(Func<XElement, XElement> aElementQuery);
        int FileCount { get; }
        IEnumerable<KeyValuePair<string, IConfigFileCollection>> SplitByFile();
        IEnumerable<XElement> GetAllElements(Func<XElement, IEnumerable<XElement>> aElementQuery);
    }

    public class NullConfigFileCollection : IConfigFileCollection
    {
        public IConfigFileCollection GetSubcollection(Func<XElement, XElement> aElementQuery)
        {
            return this;
        }

        public XAttribute GetAttribute(Func<XElement, XAttribute> aAttributeQuery) { return null; }
        public string GetAttributeValue(Func<XElement, XAttribute> aAttributeQuery) { return null; }
        public string GetAttributeAsFilepath(Func<XElement, XAttribute> aAttributeQuery, string aDefault) { return null; }
        public bool? GetAttributeAsBoolean(Func<XElement, XAttribute> aAttributeQuery) { return null; }
        public XElement GetElement(Func<XElement, XElement> aElementQuery) { return null; }
        public string GetElementValue(Func<XElement, XElement> aElementQuery) { return null; }
        public string GetElementValueAsFilepath(Func<XElement, XElement> aElementQuery) { return null; }

        public int FileCount
        {
            get { return 0; }
        }

        public IEnumerable<KeyValuePair<string, IConfigFileCollection>> SplitByFile()
        {
            yield break;
        }

        public IEnumerable<XElement> GetAllElements(Func<XElement, IEnumerable<XElement>> aElementQuery)
        {
            yield break;
        }
    }
}