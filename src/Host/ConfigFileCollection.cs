using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using log4net;
using OpenHome.Os.Platform;

namespace Node
{
    public class ConfigFileCollection : IConfigFileCollection
    {
        static readonly string[] TrueStrings = { "On", "ON", "on", "Yes", "YES", "yes", "True", "TRUE", "true", "1" };
        private class ConfigFile
        {
            public string Name;
            public XElement XElement;
            public object GetNode(string aXPath)
            {
                return ((System.Collections.IEnumerable)XElement.XPathEvaluate(aXPath)).Cast<object>().FirstOrDefault();
            }
            public string GetAttributeValue(string aXPath)
            {
                XAttribute xAttr = GetNode(aXPath) as XAttribute;
                if (xAttr == null)
                {
                    return null;
                }
                return xAttr.Value;
            }
            public string GetElementValue(string aXPath)
            {
                XElement xEl = GetNode(aXPath) as XElement;
                if (xEl == null)
                {
                    return null;
                }
                return xEl.Value;
            }
            public string ResolveRelativePath(string aFilepath)
            {
                if (Path.IsPathRooted(aFilepath)) { return aFilepath; }
                string baseDir = Path.GetDirectoryName(Path.GetFullPath(Name));
                if (baseDir == null) { return aFilepath; }
                string result = Path.GetFullPath(Path.Combine(baseDir, aFilepath));
                Console.WriteLine("ResolveRelativePath({0})[Name={1}]->{2}", aFilepath, Name, result);
                return result;
            }
        }
        readonly List<ConfigFile> iConfigFiles = new List<ConfigFile>();
        readonly List<Exception> iConfigExceptions = new List<Exception>();
        public ConfigFileCollection(IEnumerable<string> aConfigFilenames)
        {
            foreach (string filename in aConfigFilenames)
            {
                try
                {
                    iConfigFiles.Add(new ConfigFile { Name = filename, XElement = XElement.Load(filename) });
                }
                catch (Exception e)
                {
                    iConfigExceptions.Add(e);
                }
            }
        }
        private ConfigFileCollection(IEnumerable<ConfigFile> aConfigFiles)
        {
            iConfigFiles.AddRange(aConfigFiles);
        }
        public void LogErrors(ILog aLog)
        {
            foreach (Exception e in iConfigExceptions)
            {
                aLog.WarnFormat("Failed to load a configuration file: {0}", e);
            }
        }
        T2 SeekNotNull<T1,T2>(Func<ConfigFile, T1> aFunc, Func<ConfigFile, T1, T2> aOutputFunc, T2 aDefault) where T1:class
        {
            foreach (var cf in iConfigFiles)
            {
                var attribute = aFunc(cf);
                if (attribute != null)
                {
                    return aOutputFunc(cf, attribute);
                }
            }
            return aDefault;
        }
        public IConfigFileCollection GetSubcollection(string aXPath)
        {
            List<ConfigFile> newConfigs = (
                from cf in iConfigFiles
                let newRoot = cf.GetNode(aXPath) as XElement
                where newRoot != null
                select new ConfigFile {Name = cf.Name, XElement = newRoot}).ToList();
            //Console.WriteLine("Path {0} -> {1}", aXPath, newConfigs.Count);
            return new ConfigFileCollection(newConfigs);
        }
        public XAttribute GetAttribute(string aXPath)
        {
            return SeekNotNull(cf => cf.GetNode(aXPath) as XAttribute, (cf, v) => v, null);
        }
        public XElement GetElement(string aXPath)
        {
            return SeekNotNull(cf => cf.GetNode(aXPath) as XElement, (cf, v) => v, null);
        }
        public string GetAttributeValue(string aXPath)
        {
            return SeekNotNull(cf=>cf.GetAttributeValue(aXPath), (cf,v)=>v, null);
        }
        public string GetAttributeAsFilepath(string aXPath, string aDefault)
        {
            return SeekNotNull(cf=>cf.GetAttributeValue(aXPath), (cf,v)=>cf.ResolveRelativePath(v), aDefault);
        }
        public string GetElementValue(string aXPath)
        {
            return SeekNotNull(cf=>cf.GetElementValue(aXPath), (cf,v)=>v, null);
        }
        public string GetElementValueAsFilepath(string aXPath)
        {
            return SeekNotNull(cf=>cf.GetElementValue(aXPath), (cf,v)=>cf.ResolveRelativePath(v), null);
        }
        public bool? GetAttributeAsBoolean(string aXPath)
        {
            return SeekNotNull(
                cf => cf.GetAttributeValue(aXPath),
                (cf, v) => TrueStrings.Contains(v),
                (bool?)null);
        }

    }
}