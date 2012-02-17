using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using log4net;
using OpenHome.Os.Platform.Logging;

namespace OpenHome.Os.Platform.IO
{
    public class DiskStore<T> where T:class
    {
        readonly Func<TextReader, T> iReadFunction;
        readonly Action<TextWriter, T> iWriteAction;
        private readonly DirectoryInfo iStoreDirectory;
        private readonly object iLock = new object(); // Required to access the disk.
        private readonly string iFileExtension;

        static readonly ILog Logger = LogManager.GetLogger(typeof(DiskStore<T>));

        public DiskStore(DirectoryInfo aStoreDirectory, string aFileExtension, Func<TextReader, T> aReadFunction, Action<TextWriter, T> aWriteAction)
        {
            iStoreDirectory = aStoreDirectory;
            iFileExtension = aFileExtension;
            iStoreDirectory.Create();
            iReadFunction = aReadFunction;
            iWriteAction = aWriteAction;
        }

        public IEnumerable<T> LoadFiles()
        {
            var fileResults = new List<T>();
            var badFiles = new List<FileInfo>();
            lock (iLock)
            {
                foreach (FileInfo fileInfo in iStoreDirectory.GetFiles("*"+iFileExtension))
                {
                    using (var f = File.OpenRead(fileInfo.FullName))
                    {
                        using (var reader = new StreamReader(f))
                        {
                            var fileContent = iReadFunction(reader);
                            if (fileContent != null)
                            {
                                fileResults.Add(fileContent);
                            }
                            else
                            {
                                // By returning null, the reader indicates that the file
                                // should be deleted.
                                badFiles.Add(fileInfo);
                            }
                        }
                    }
                }
                foreach (FileInfo badFile in badFiles)
                {
                    try
                    {
                        badFile.Delete();
                    }
                    catch (IOException ioe)
                    {
                        Logger.ErrorFormat("Failed to delete file {0} due to exception {1}", badFile.FullName, ioe);
                    }
                }
            }
            return fileResults;
        }

        public void ValidateFilename(string aFilename)
        {
            if (aFilename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(String.Format("Invalid filename: '{0}'.", aFilename));
            }
        }

        public void PutFile(string aFilename, T aContent)
        {
            ValidateFilename(aFilename);
            string fullpath = Path.Combine(iStoreDirectory.FullName, aFilename+iFileExtension);
            lock (iLock)
            {
                using (var f = File.Create(fullpath))
                {
                    using (var writer = new StreamWriter(f))
                    {
                        iWriteAction(writer, aContent);
                    }
                }
            }
        }

        public void DeleteFile(string aFilename)
        {
            ValidateFilename(aFilename);
            string fullpath = Path.Combine(iStoreDirectory.FullName, aFilename+iFileExtension);
            lock (iLock)
            {
                File.Delete(fullpath);
            }
        }

        public T GetFile(string aFilename)
        {
            ValidateFilename(aFilename);
            string fullpath = Path.Combine(iStoreDirectory.FullName, aFilename+iFileExtension);
            lock (iLock)
            {
                try
                {
                    using (var f = File.OpenRead(fullpath))
                    {
                        using (var reader = new StreamReader(f))
                        {
                            return iReadFunction(reader);
                        }
                    }
                }
                catch (IOException)
                {
                    return null;
                }
            }
        }
    }

    public interface IXmlReaderWriter
    {
        void WriteFile(TextWriter aWriter, XElement aElement);
        XElement ReadFile(TextReader aReader);
    }

    public class XmlReaderWriter : IXmlReaderWriter
    {
        readonly XmlSchemaSet iSchemaSet;
        readonly ILog iLog;
        public XmlReaderWriter(XmlSchemaSet aSchemaSet, ILog aLog)
        {
            iSchemaSet = aSchemaSet;
            iLog = aLog;
        }
        public void WriteFile(TextWriter aWriter, XElement aElement)
        {
            aElement.Save(aWriter);
        }

        public XElement ReadFile(TextReader aReader)
        {
            try
            {
                XmlReaderSettings settings = new XmlReaderSettings();
                if (iSchemaSet!=null)
                {
                    settings.ValidationType = ValidationType.Schema;
                    settings.Schemas = iSchemaSet;
                }
                XmlReader xmlReader = XmlReader.Create(aReader, settings);
                return XElement.Load(xmlReader);
            }
            catch (XmlException e)
            {
                if (iLog!=null)
                {
                    iLog.ErrorFormat("Failed to load XML file: {0}", e);
                }
                return null;
            }
            catch (XmlSchemaValidationException e)
            {
                if (iLog!=null)
                {
                    iLog.ErrorFormat("Failed to load XML file: {0}", e);
                }
                return null;
            }
        }
    }

    public class XmlDiskStore
    {
        private readonly DiskStore<XElement> iDiskStore;

        [Obsolete("aLogger will be ignored. Pass an XmlReaderWriter instead to control parsing and logging.")]
        public XmlDiskStore(DirectoryInfo aStoreDirectory, ILogger aLogger, string aFileExtension)
            :this(aStoreDirectory, aFileExtension, new XmlReaderWriter(null, null))
        {
        }
        public XmlDiskStore(DirectoryInfo aStoreDirectory, string aFileExtension, IXmlReaderWriter aXmlReaderWriter)
        {
            iDiskStore = new DiskStore<XElement>(aStoreDirectory, aFileExtension, aXmlReaderWriter.ReadFile, aXmlReaderWriter.WriteFile);
        }


        public IEnumerable<XElement> LoadXmlFiles()
        {
            return iDiskStore.LoadFiles();
        }
        public void PutXmlFile(string aFilename, XElement aContent)
        {
            iDiskStore.PutFile(aFilename, aContent);
        }
        public void DeleteXmlFile(string aFilename)
        {
            iDiskStore.DeleteFile(aFilename);
        }
    }

    /*public class XmlDiskStore
    {
        private readonly DirectoryInfo iStoreDirectory;
        private readonly ILogger iLogger;
        private readonly object iLock = new object(); // Required to access the disk.
        private readonly string iFileExtension;
        private readonly XmlSchemaSet iSchemaSet;

        public XmlDiskStore(DirectoryInfo aStoreDirectory, ILogger aLogger, string aFileExtension, XmlSchemaSet aSchemaSet)
        {
            iStoreDirectory = aStoreDirectory;
            iLogger = aLogger;
            iFileExtension = aFileExtension;
            iStoreDirectory.Create();
            iSchemaSet = aSchemaSet;
        }
        public XmlDiskStore(DirectoryInfo aStoreDirectory, ILogger aLogger, string aFileExtension)
            :this(aStoreDirectory, aLogger, aFileExtension, null)
        {
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
                                XmlReaderSettings settings = new XmlReaderSettings();
                                if (iSchemaSet!=null)
                                {
                                    settings.ValidationType = ValidationType.Schema;
                                    settings.Schemas = iSchemaSet;
                                }
                                XmlReader xmlReader = XmlReader.Create(reader, settings);
                                var element = XElement.Load(xmlReader);
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
    }*/
}