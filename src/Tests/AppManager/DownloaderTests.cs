using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.Os.Platform.Threading;
using NUnit.Framework;
using Moq;

namespace OpenHome.Os.AppManager
{
    [TestFixture]
    public class DownloaderTests
    {
        Mock<IDownloadDirectory> iDirectoryMock;
        Channel<DownloadInstruction> iInstructionChannel;
        Channel<PollInstruction> iPollInstructionChannel;
        Mock<IThreadCommunicator> iCommunicator;
        Mock<IPollManager> iPollManager;
        Mock<IUrlFetcher> iUrlFetcher;
        Downloader iDownloader;

        bool iAbandoned;
        Queue<Action<int>> iOnSelectActions;

        void OnSelect(int aTimeout, ChannelAction[] iActions)
        {
            iOnSelectActions.Dequeue()(aTimeout);
        }

        [SetUp]
        public void SetUp()
        {
            iOnSelectActions = new Queue<Action<int>>();
            iAbandoned = false;
            iDirectoryMock = new Mock<IDownloadDirectory>();
            iInstructionChannel = new Channel<DownloadInstruction>(10);
            iPollInstructionChannel = new Channel<PollInstruction>(10);
            iCommunicator = new Mock<IThreadCommunicator>();
            iPollManager = new Mock<IPollManager>();
            iUrlFetcher = new Mock<IUrlFetcher>();
            iDownloader = new Downloader(iDirectoryMock.Object, iInstructionChannel, iPollInstructionChannel, iPollManager.Object, iUrlFetcher.Object);
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
            iOnSelectActions.Enqueue(aTimeout => { iAbandoned = true; });

            iDownloader.Run(iCommunicator.Object);

            iCommunicator.Verify(x=>x.SelectWithTimeout(-1, It.IsAny<ChannelAction[]>()), Times.Once());
        }

        [Test]
        public void TestTheDownloaderSleepsForPollingIntervalWhenThereIsPollingToDo()
        {
            iPollManager.Setup(x=>x.Empty).Returns(false);
            iPollManager.Setup(x=>x.PollingInterval).Returns(TimeSpan.FromMilliseconds(1250));
            iOnSelectActions.Enqueue(aTimeout => { iAbandoned = true; });

            iDownloader.Run(iCommunicator.Object);

            iCommunicator.Verify(x=>x.SelectWithTimeout(1250, It.IsAny<ChannelAction[]>()), Times.Once());
        }

        //[Test]
        //public void Test()
        //{
        //    iDownloader.
        //}
    }
}

