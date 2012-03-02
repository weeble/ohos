using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using log4net;

namespace OpenHome.Os.Platform
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
                var items = (System.Collections.IEnumerable)XElement.XPathEvaluate(aXPath);
                Console.WriteLine("Retval type:{0}", items.GetType());
                var listItems = new List<object>(items.Cast<object>());
                Console.WriteLine("Size:{0}", listItems.Count);
                if (listItems.Count>=1)
                {
                    Console.WriteLine(listItems[0]);
                    Console.WriteLine(listItems[0].GetType());
                }
                return listItems.FirstOrDefault();
                //return ().Cast<object>().FirstOrDefault();
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
        public void AddFile(string aFilename, XElement aContent)
        {
            iConfigFiles.Add(new ConfigFile { Name = aFilename, XElement = aContent });
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
        public IConfigFileCollection GetSubcollection(Func<XElement, XElement> aElementQuery)
        {
            List<ConfigFile> newConfigs = (
                from cf in iConfigFiles
                let newRoot = aElementQuery(cf.XElement)
                where newRoot != null
                select new ConfigFile {Name = cf.Name, XElement = newRoot}).ToList();
            //Console.WriteLine("Path {0} -> {1}", aXPath, newConfigs.Count);
            //Console.WriteLine(newConfigs.Count);
            //Console.WriteLine(newConfigs[0].XElement);
            return new ConfigFileCollection(newConfigs);
        }
        public XAttribute GetAttribute(Func<XElement, XAttribute> aAttributeQuery)
        {
            return SeekNotNull(cf => aAttributeQuery(cf.XElement), (cf, v) => v, null);
        }
        public XElement GetElement(Func<XElement, XElement> aElementQuery)
        {
            return SeekNotNull(cf => aElementQuery(cf.XElement), (cf, v) => v, null);
        }
        public string GetAttributeValue(Func<XElement, XAttribute> aAttributeQuery)
        {
            return SeekNotNull(cf => aAttributeQuery(cf.XElement), (cf,v) => v.Value, null);
        }
        public string GetAttributeAsFilepath(Func<XElement, XAttribute> aAttributeQuery, string aDefault)
        {
            return SeekNotNull(cf => aAttributeQuery(cf.XElement), (cf,v) => cf.ResolveRelativePath(v.Value), aDefault);
        }
        public string GetElementValue(Func<XElement, XElement> aElementQuery)
        {
            return SeekNotNull(cf => aElementQuery(cf.XElement), (cf,v) => v.Value, null);
        }
        public string GetElementValueAsFilepath(Func<XElement, XElement> aElementQuery)
        {
            return SeekNotNull(cf => aElementQuery(cf.XElement), (cf,v) => cf.ResolveRelativePath(v.Value), null);
        }

        public IEnumerable<XElement> GetAllElements(Func<XElement, IEnumerable<XElement>> aElementQuery)
        {
            return iConfigFiles
                .SelectMany(aConfigFile => aElementQuery(aConfigFile.XElement))
                .Where(aElement => aElement != null);
        }

        public int FileCount
        {
            get { return iConfigFiles.Count; }
        }

        public IEnumerable<KeyValuePair<string, IConfigFileCollection>> SplitByFile()
        {
            return iConfigFiles.Select(aConfigFile => new KeyValuePair<string, IConfigFileCollection>(
                aConfigFile.Name,
                new ConfigFileCollection(new[] { aConfigFile })));
        }

        public bool? GetAttributeAsBoolean(Func<XElement, XAttribute> aAttributeQuery)
        {
            return SeekNotNull(
                cf => aAttributeQuery(cf.XElement),
                (cf, v) => TrueStrings.Contains(v.Value),
                (bool?)null);
        }

        public static ConfigFileCollection ReadDirectoryInOrder(DirectoryInfo aDirectoryInfo, string aSearchPattern)
        {
            return new ConfigFileCollection(
                aDirectoryInfo
                    .EnumerateFiles(aSearchPattern)
                    .OrderBy(aFile=>aFile.Name)
                    .Select(aFile=>aFile.FullName));
        }

    }
}
