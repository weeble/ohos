using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace OpenHome.Os.Platform.IO
{
    /// <summary>
    /// Base class that does correct cleanup after failing tests.
    /// </summary>
    /// <remarks>
    /// NUnit's [TearDown] mechanism is badly broken. Teardown methods can be invoked
    /// without their [SetUp] having run, and one failing [TearDown] can prevent all
    /// others from running. By replacing all [TearDown] in sub-classes with a call
    /// to CleanupAfterTest in the SetUp or the test, you can avoid all these problems.
    /// </remarks>
    public class TestContext
    {
        private List<Action> iCleanupMethods;
        [SetUp]
        public void PrepareListOfCleanupMethods()
        {
            iCleanupMethods = new List<Action>();
        }
        protected void CleanupAfterTest(Action aCleanupMethod)
        {
            iCleanupMethods.Add(aCleanupMethod);
        }
        protected IDisposable CleanupAfterTest(IDisposable aDisposable)
        {
            iCleanupMethods.Add(aDisposable.Dispose);
            return aDisposable;
        }
        [TearDown]
        public void InvokeCleanupMethods()
        {
            List<Exception> cleanupExceptions = new List<Exception>();
            foreach (Action action in iCleanupMethods)
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception during cleanup:\n"+e.Message);
                    Console.WriteLine(e.StackTrace);
                    cleanupExceptions.Add(e);
                }
            }
            if (cleanupExceptions.Count==1)
            {
                throw cleanupExceptions[1];
            }
            if (cleanupExceptions.Count>1)
            {
                throw new Exception("Multiple exceptions occured during cleanup:\n" + String.Join("\n", cleanupExceptions.Select(e=>e.Message).ToArray()));
            }
        }
    }

    [Category("Integration")]
    public class TempDirectoryContext : TestContext
    {
        protected DirectoryInfo iTempDirectory;
        [SetUp]
        public void CreateTemporaryDirectory()
        {
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            iTempDirectory = Directory.CreateDirectory(tempDirectoryPath);
            CleanupAfterTest(()=>iTempDirectory.Delete(true));
        }
    }

    public class WhenAnEmptyXmlDiskStoreIsCreatedContext : TempDirectoryContext
    {
        protected XmlDiskStore iXmlDiskStore;
        [SetUp]
        public void CreateXmlDiskStore()
        {
            iXmlDiskStore = new XmlDiskStore(iTempDirectory, ".test.xml", new XmlReaderWriter(null, null));
        }
    }

    [TestFixture]
    public class WhenAnEmptyXmlDiskStoreIsCreated : WhenAnEmptyXmlDiskStoreIsCreatedContext
    {
        [Test]
        public void ThereAreNoItemsFoundInAnEmptyDirectory()
        {
            Assert.That(iXmlDiskStore.LoadXmlFiles().ToList(), Is.Empty);
        }
        [Test]
        public void PutXmlFileCreatesAFileWithTheCorrectExtension()
        {
            iXmlDiskStore.PutXmlFile("testname", new XElement("foobar"));
            List<string> filenames = iTempDirectory.GetFiles().Select(finfo => finfo.Name).ToList();
            Assert.That(filenames, Is.EquivalentTo(new List<string> { "testname.test.xml" }));
        }
    }

    public class WhenTwoFilesArePutInAnXmlDiskStoreContext : WhenAnEmptyXmlDiskStoreIsCreatedContext
    {
        [SetUp]
        public void PutTwoFiles()
        {
            iXmlDiskStore.PutXmlFile("file1", new XElement("file1content"));
            iXmlDiskStore.PutXmlFile("file2", new XElement("file2content"));
        }
    }

    [Category("Integration")]
    public class WhenTwoFilesArePutInAnXmlDiskStore : WhenTwoFilesArePutInAnXmlDiskStoreContext
    {
        [Test]
        public void RemovingOneFileLeavesOnlyTheOther()
        {
            iXmlDiskStore.DeleteXmlFile("file2");
            List<string> filenames = iTempDirectory.GetFiles().Select(finfo => finfo.Name).ToList();
            Assert.That(filenames, Is.EquivalentTo(new List<string> { "file1.test.xml" }));
        }
        [Test]
        public void FilesCanBeOverwritten()
        {
            iXmlDiskStore.PutXmlFile("file1", new XElement("differentcontent"));
            string elementName = XElement.Load(Path.Combine(iTempDirectory.FullName, "file1.test.xml")).Name.LocalName;
            Assert.That(elementName, Is.EqualTo("differentcontent"));
        }
        [Test]
        public void LoadXmlFilesReturnsTheFileContents()
        {
            List<string> elementNames = iXmlDiskStore.LoadXmlFiles().Select(element => element.Name.LocalName).ToList();
            Assert.That(elementNames, Is.EquivalentTo(new[]{"file1content", "file2content"}));
        }
    }
}
