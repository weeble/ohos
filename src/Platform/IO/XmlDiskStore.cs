using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using OpenHome.Widget.Nodes.Logging;

namespace OpenHome.Widget.Nodes.IO
{
    public class XmlDiskStore
    {
        private readonly DirectoryInfo iStoreDirectory;
        private readonly ILogger iLogger;
        private readonly object iLock = new object(); // Required to access the disk.
        private readonly string iFileExtension;

        public XmlDiskStore(DirectoryInfo aStoreDirectory, ILogger aLogger, string aFileExtension)
        {
            iStoreDirectory = aStoreDirectory;
            iLogger = aLogger;
            iFileExtension = aFileExtension;
            iStoreDirectory.Create();
        }

        public IEnumerable<XElement> LoadXmlFiles()
        {
            var xmlElements = new List<XElement>();
            lock (iLock)
            {
                foreach (FileInfo fileInfo in iStoreDirectory.GetFiles("*"+iFileExtension))
                {
                    try
                    {
                        using (var f = File.OpenRead(fileInfo.FullName))
                        {
                            using (var reader = new StreamReader(f))
                            {
                                var element = XElement.Load(reader);
                                xmlElements.Add(element);
                            }
                        }
                    }
                    catch (XmlException e)
                    {
                        iLogger.Log("Failed to load XML file '{0}' with XML exception:\n{1}", fileInfo.FullName, e);
                        // TODO: Retry or delete the file.
                    }
                }
            }
            return xmlElements;
        }
        public void PutXmlFile(string aFilename, XElement aContent)
        {
            string fullpath = Path.Combine(iStoreDirectory.FullName, aFilename+iFileExtension);
            lock (iLock)
            {
                aContent.Save(fullpath);
            }
        }
        public void DeleteXmlFile(string aFilename)
        {
            string fullpath = Path.Combine(iStoreDirectory.FullName, aFilename+iFileExtension);
            lock (iLock)
            {
                File.Delete(fullpath);
            }
        }
    }
}