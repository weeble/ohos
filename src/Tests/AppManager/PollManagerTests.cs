﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using NUnit.Framework;

namespace OpenHome.Os.AppManager
{

    [TestFixture]
    public class PollManagerTests
    {
        Mock<IUrlPoller> iMockUrlPoller;
        PollManager iPollManager;

        [SetUp]
        public void SetUp()
        {
            iMockUrlPoller = new Mock<IUrlPoller>();
            iPollManager = new PollManager(iMockUrlPoller.Object);
        }

        protected void StartPollingApp(string aAppName, string aUrl, DateTime aDateTime, out Mock<IAction> aAvailableActionMock, out Mock<IAction> aFailedActionMock)
        {
            var availableAction = new ActionMock();
            var failedAction = new ActionMock();
            iPollManager.StartPollingApp(aAppName, aUrl, aDateTime, availableAction.Action, failedAction.Action);
            aAvailableActionMock = availableAction.Mock;
            aFailedActionMock = failedAction.Mock;
        }
        
        protected void StartPollingApp(string aAppName, string aUrl, DateTime aDateTime)
        {
            Mock<IAction> dummy1;
            Mock<IAction> dummy2;
            StartPollingApp(aAppName, aUrl, aDateTime, out dummy1, out dummy2);
        }

        protected void CancelPollingApp(string aAppName)
        {
            iPollManager.CancelPollingApp(aAppName);
        }

        protected void PollNext()
        {
            iPollManager.PollNext();
        }

        [Test]
        public void TestNewPollManagerIsEmpty()
        {
            Assert.That(iPollManager.Empty, Is.True);
        }

        [Test]
        public void TestAddingAUrlMakesThePollManagerNonEmpty()
        {
            StartPollingApp("appfoo", "http://domain.invalid", new DateTime(2000, 06, 01));
            Assert.That(iPollManager.Empty, Is.False);
        }

        [Test]
        public void TestAddingThenRemovingAUrlMakesThePollManagerEmpty()
        {
            StartPollingApp("appfoo", "http://domain.invalid", new DateTime(2000, 06, 01));
            CancelPollingApp("appfoo");
            Assert.That(iPollManager.Empty, Is.True);
        }
        
        [Test]
        public void TestAvailableActionIsInvokedIfTheDownloadIsAvailable()
        {
            Mock<IAction> availableMock;
            Mock<IAction> failedMock;
            StartPollingApp("appfoo", "http://domain.invalid", new DateTime(2000, 06, 01), out availableMock, out failedMock);

            iMockUrlPoller.Setup(x => x.Poll("http://domain.invalid", new DateTime(2000, 06, 01))).Returns(DownloadAvailableState.Available);
            PollNext();
            
            availableMock.Verify(x => x.Invoke(), Times.Once());
        }

        [Test]
        public void TestFailedActionIsInvokedIfTheRequestFails()
        {
            Mock<IAction> availableMock;
            Mock<IAction> failedMock;
            StartPollingApp("appfoo", "http://domain.invalid", new DateTime(2000, 06, 01), out availableMock, out failedMock);

            iMockUrlPoller.Setup(x => x.Poll("http://domain.invalid", new DateTime(2000, 06, 01))).Returns(DownloadAvailableState.Error);
            PollNext();
            
            failedMock.Verify(x => x.Invoke(), Times.Once());
        }

        [Test]
        public void TestNoActionIsInvokedIfNoNewVersionIsAvailable()
        {
            Mock<IAction> availableMock;
            Mock<IAction> failedMock;
            StartPollingApp("appfoo", "http://domain.invalid", new DateTime(2000, 06, 01), out availableMock, out failedMock);

            iMockUrlPoller.Setup(x => x.Poll("http://domain.invalid", new DateTime(2000, 06, 01))).Returns(DownloadAvailableState.NotAvailable);
            PollNext();
            
            availableMock.Verify(x => x.Invoke(), Times.Never());
            failedMock.Verify(x => x.Invoke(), Times.Never());
        }

        [Test]
        public void TestOnlyOneAppIsPolledAtATime()
        {
            Mock<IAction>[] availableMocks = new Mock<IAction>[3];
            Mock<IAction>[] failedMocks = new Mock<IAction>[3];
            StartPollingApp("appzero", "http://domain.invalid", new DateTime(2000, 06, 01), out availableMocks[0], out failedMocks[0]);
            StartPollingApp("appone", "http://domain.invalid", new DateTime(2000, 06, 01), out availableMocks[1], out failedMocks[1]);
            StartPollingApp("apptwo", "http://domain.invalid", new DateTime(2000, 06, 01), out availableMocks[2], out failedMocks[2]);

            iMockUrlPoller.Setup(x => x.Poll(It.IsAny<string>(), It.IsAny<DateTime>())).Returns(DownloadAvailableState.Available);
            PollNext();

            availableMocks[0].Verify(x => x.Invoke(), Times.Once());
            availableMocks[1].Verify(x => x.Invoke(), Times.Never());
            availableMocks[2].Verify(x => x.Invoke(), Times.Never());
        }

        [Test]
        public void TestAppsArePolledInOrder()
        {
            Mock<IAction>[] availableMocks = new Mock<IAction>[3];
            Mock<IAction>[] failedMocks = new Mock<IAction>[3];
            StartPollingApp("appzero", "http://appone.invalid", new DateTime(2000, 06, 01), out availableMocks[0], out failedMocks[0]);
            StartPollingApp("appone", "http://apptwo.invalid", new DateTime(2000, 06, 01), out availableMocks[1], out failedMocks[1]);
            StartPollingApp("apptwo", "http://appthree.invalid", new DateTime(2000, 06, 01), out availableMocks[2], out failedMocks[2]);

            List<int> order = new List<int>();
            for (int i = 0; i != 3; ++i)
            {
                int index = i;
                availableMocks[i].Setup(x => x.Invoke()).Callback(() => order.Add(index));
            }
            PollNext();
            PollNext();
            PollNext();

            Assert.That(order, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void TestPollTimeForZeroApps()
        {
            iPollManager.MaxAppPollingInterval = TimeSpan.FromSeconds(100);
            Assert.That(iPollManager.PollingInterval, Is.EqualTo(TimeSpan.FromSeconds(100)));
        }

        [Test]
        public void TestPollTimeForOneApps()
        {
            iPollManager.MinPollingInterval = TimeSpan.FromSeconds(10);
            iPollManager.MaxAppPollingInterval = TimeSpan.FromSeconds(100);
            StartPollingApp("appfoo", "http://domain.invalid", new DateTime(2000, 06, 01));
            Assert.That(iPollManager.PollingInterval, Is.EqualTo(TimeSpan.FromSeconds(100)));
        }

        [Test]
        public void TestPollTimeForTwoApps()
        {
            iPollManager.MinPollingInterval = TimeSpan.FromSeconds(10);
            iPollManager.MaxAppPollingInterval = TimeSpan.FromSeconds(100);
            StartPollingApp("app1", "http://domain.invalid", new DateTime(2000, 06, 01));
            StartPollingApp("app2", "http://domain.invalid", new DateTime(2000, 06, 01));
            Assert.That(iPollManager.PollingInterval, Is.EqualTo(TimeSpan.FromSeconds(50)));
        }

        [Test]
        public void TestAppsAreNotPolledAfterRemoval()
        {
            Mock<IAction>[] availableMocks = new Mock<IAction>[3];
            Mock<IAction>[] failedMocks = new Mock<IAction>[3];
            StartPollingApp("appzero", "http://appone.invalid", new DateTime(2000, 06, 01), out availableMocks[0], out failedMocks[0]);
            StartPollingApp("appone", "http://apptwo.invalid", new DateTime(2000, 06, 01), out availableMocks[1], out failedMocks[1]);
            StartPollingApp("apptwo", "http://appthree.invalid", new DateTime(2000, 06, 01), out availableMocks[2], out failedMocks[2]);
            CancelPollingApp("appzero");
            CancelPollingApp("apptwo");
            iMockUrlPoller.Setup(x => x.Poll(It.IsAny<string>(), It.IsAny<DateTime>())).Returns(DownloadAvailableState.Available);
            PollNext();
            PollNext();
            availableMocks[0].Verify(x => x.Invoke(), Times.Never());
            availableMocks[1].Verify(x => x.Invoke(), Times.Exactly(2));
            availableMocks[2].Verify(x => x.Invoke(), Times.Never());
        }
    }
}
