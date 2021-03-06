using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHome.Os.Platform.Threading;
using NUnit.Framework;
using Moq;

namespace OpenHome.Os.AppManager
{
    [TestFixture]
    public class DownloaderTests
    {
        Mock<IDownloadDirectory> iDirectoryMock;
        Mock<IThreadCommunicator> iCommunicator;
        Mock<IPollManager> iPollManager;
        Mock<IUrlFetcher> iUrlFetcher;
        Downloader iDownloader;

        bool iAbandoned;
        Queue<Action<int,ChannelAction[]>> iOnSelectActions;
        Channel<Action<IDownloadThread>> iMessageQueue;

        void OnSelect(int aTimeout, ChannelAction[] aActions)
        {
            iOnSelectActions.Dequeue()(aTimeout, aActions);
        }

        [SetUp]
        public void SetUp()
        {
            iOnSelectActions = new Queue<Action<int,ChannelAction[]>>();
            iAbandoned = false;
            iDirectoryMock = new Mock<IDownloadDirectory>();
            iMessageQueue = new Channel<Action<IDownloadThread>>(10);
            iCommunicator = new Mock<IThreadCommunicator>();
            iPollManager = new Mock<IPollManager>();
            iUrlFetcher = new Mock<IUrlFetcher>();
            iDownloader = new Downloader(iDirectoryMock.Object, iMessageQueue, iPollManager.Object, iUrlFetcher.Object);
            iCommunicator.Setup(x=>x.Abandoned).Returns(()=>iAbandoned);
            iCommunicator.Setup(x=>x.CheckAbandoned()).Returns(()=>iAbandoned);
            iCommunicator.Setup(x=>x.Select(It.IsAny<ChannelAction[]>()))
                .Callback((ChannelAction[] aActions)=>OnSelect(-1, aActions));
            iCommunicator.Setup(x=>x.SelectWithTimeout(It.IsAny<int>(), It.IsAny<ChannelAction[]>()))
                .Callback((int aTimeout, ChannelAction[] aActions) => OnSelect(aTimeout, aActions));
        }

        [Test]
        public void TestTheDownloaderSleepsIndefinitelyWhenThereIsNoWork()
        {
            iPollManager.Setup(x=>x.Empty).Returns(true);
            iOnSelectActions.Enqueue((aTimeout, aActions) => { iAbandoned = true; });

            iDownloader.Run(iCommunicator.Object);

            iCommunicator.Verify(x=>x.SelectWithTimeout(-1, It.IsAny<ChannelAction[]>()), Times.Once());
        }

        [Test]
        public void TestTheDownloaderSleepsForPollingIntervalWhenThereIsPollingToDo()
        {
            iPollManager.Setup(x=>x.Empty).Returns(false);
            iPollManager.Setup(x=>x.PollingInterval).Returns(TimeSpan.FromMilliseconds(1250));
            iOnSelectActions.Enqueue((aTimeout, aActions) => { iAbandoned = true; });

            iDownloader.Run(iCommunicator.Object);

            // Slight time-dependent. A small amount of time will pass between the first poll and
            // the calculation of how long to sleep. The actual time should be very close to 1250.
            // Ideally we would mock out DateTime.UtcNow, but that seems like overkill.
            iCommunicator.Verify(x=>x.SelectWithTimeout(It.IsInRange(1000, 1251, Range.Inclusive), It.IsAny<ChannelAction[]>()), Times.Once());
        }

        [Test]
        public void TestStartingADownloadInvokesUrlFetcher()
        {
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            iUrlFetcher.Verify(x=>x.Fetch("http://test.test", It.IsAny<FileStream>(), It.IsAny<IDownloadListener>()), Times.Once());
        }

        [Test]
        public void TestStartingADownloadAddsItToDownloadStatus()
        {
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            var downloads = iDownloader.GetDownloadStatus().ToList();
            Assert.That(downloads.Count, Is.EqualTo(1));
            Assert.That(downloads[0].Uri, Is.EqualTo("http://test.test"));
        }

        [Test]
        public void TestStartingADownloadSetsItTo0Bytes()
        {
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            var downloads = iDownloader.GetDownloadStatus().ToList();
            Assert.That(downloads.Count, Is.EqualTo(1));
            Assert.That(downloads[0].DownloadedBytes, Is.EqualTo(0));
        }

        [Test]
        public void TestStartingADownloadSetsItToUnknownSize()
        {
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            var downloads = iDownloader.GetDownloadStatus().ToList();
            Assert.That(downloads.Count, Is.EqualTo(1));
            Assert.That(downloads[0].HasTotalBytes, Is.EqualTo(false));
        }

        [Test]
        public void TestStartingADownloadTriggersAnEvent()
        {
            bool gotEvent = false;
            iDownloader.DownloadChanged += (aSender, aEvent) => { gotEvent = true; };
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            Assert.That(gotEvent, Is.True);
        }

        [Test]
        public void TestAFinishedDownloadTriggersAnEvent()
        {
            iUrlFetcher.Setup(x=>x.Fetch(It.IsAny<string>(), It.IsAny<FileStream>(), It.IsAny<IDownloadListener>())).Callback(
                (string aName, FileStream aFile, IDownloadListener aListener) =>
                    {
                        aListener.Complete(new DateTime(2010,10,10));
                    });
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            bool gotEvent = false;
            iDownloader.DownloadChanged += (aSender, aEvent) => { gotEvent = true; };
            iMessageQueue.Receive()(iDownloader);
            Assert.That(gotEvent, Is.True);
        }

        [Test]
        public void TestAFailedDownloadTriggersAnEvent()
        {
            iUrlFetcher.Setup(x=>x.Fetch(It.IsAny<string>(), It.IsAny<FileStream>(), It.IsAny<IDownloadListener>())).Callback(
                (string aName, FileStream aFile, IDownloadListener aListener) =>
                    {
                        aListener.Failed();
                    });
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            bool gotEvent = false;
            iDownloader.DownloadChanged += (aSender, aEvent) => { gotEvent = true; };
            iMessageQueue.Receive()(iDownloader);
            Assert.That(gotEvent, Is.True);
        }

        [Test]
        public void TestDownloadProgressDoesNotTriggerAnEvent()
        {
            iUrlFetcher.Setup(x=>x.Fetch(It.IsAny<string>(), It.IsAny<FileStream>(), It.IsAny<IDownloadListener>())).Callback(
                (string aName, FileStream aFile, IDownloadListener aListener) =>
                    {
                        aListener.Progress(500, 1000);
                    });
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            bool gotEvent = false;
            iDownloader.DownloadChanged += (aSender, aEvent) => { gotEvent = true; };
            iMessageQueue.Receive()(iDownloader);
            Assert.That(gotEvent, Is.False);
        }

        [Test]
        public void TestCancelDownloadDisposesDownload()
        {
            Mock<IDisposable> iDownload = new Mock<IDisposable>();
            iUrlFetcher.Setup(x=>x.Fetch(It.IsAny<string>(), It.IsAny<FileStream>(), It.IsAny<IDownloadListener>())).Returns(iDownload.Object);
            iDownloader.StartDownload(
                "http://test.test",
                (aName, aModified)=>{},
                ()=>{});
            iDownloader.CancelDownload("http://test.test");
            iDownload.Verify(x => x.Dispose());
        }

        [Test]
        public void TestCancelAllDownloadsDisposesAllDownloads()
        {
            Mock<IDisposable>[] downloads = Enumerable.Range(0, 4).Select(x=>new Mock<IDisposable>()).ToArray();
            string[] urls = Enumerable.Range(0, 4).Select(x => String.Format("http://test{0}.test", x)).ToArray();
            foreach (var pair in urls.Zip(downloads, (url, download) => new { Url = url, Download = download }))
            {
                iUrlFetcher.Setup(x => x.Fetch(pair.Url, It.IsAny<FileStream>(), It.IsAny<IDownloadListener>())).Returns(pair.Download.Object);
            }
            foreach (var url in urls)
            {
                iDownloader.StartDownload(url, (aName, aModified) => { }, () => { });
            }
            iDownloader.CancelAllDownloads();
            foreach (var download in downloads)
            {
                download.Verify(x => x.Dispose());
            }
        }

        //[Test]
        //public void Test()
        //{
        //    iDownloader.
        //}
    }
}

