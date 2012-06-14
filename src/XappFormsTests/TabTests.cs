using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using OpenHome.XappForms;
using OpenHome.XappForms.Json;

namespace UnitTests
{
    public interface IPageWriter
    {
        bool Write(ArraySegment<byte> aBytes);
        bool Flush(Action aAction);
        void End(Exception aException);
    }

    class ThreadStandin : ISoftThread
    {
        public Task ScheduleExclusive(Action a)
        {
            a();
            return null;
        }

        public Task<T> ScheduleExclusive<T>(Func<T> a)
        {
            a();
            return null;
        }
    }

    class TabTests
    {
        //Mock<IJsonEventQueue> iMockEventQueue;
        Mock<ITabStatusListener> iMockStatusListener;
        Mock<IPageWriter> iMockPageWriter;
        Mock<ITimerThread> iMockTimerThread;
        Mock<ITimerCallback> iMockTimerCallback;
        Mock<ISession> iMockSession;
        ISoftThread iSoftThread;
        IPageWriter PageWriter { get { return iMockPageWriter.Object; } }
        ServerTab iServerTab;
        DateTime iNow = new DateTime(2000,1,1,0,0,0);

        [SetUp]
        public void SetUp()
        {
            //iMockEventQueue = new Mock<IJsonEventQueue>();
            iMockStatusListener = new Mock<ITabStatusListener>();
            iMockPageWriter = new Mock<IPageWriter>();
            ServerTabTimeoutPolicy timeoutPolicy = new ServerTabTimeoutPolicy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
            iMockTimerCallback = new Mock<ITimerCallback>();
            iMockTimerThread = new Mock<ITimerThread>();
            iMockTimerThread.Setup(x => x.RegisterCallback(It.IsAny<Action>())).Returns(iMockTimerCallback.Object);
            iMockSession = new Mock<ISession>();
            iSoftThread = new ThreadStandin();
            iServerTab = new ServerTab(
                "session7", "tab9", iMockStatusListener.Object, ()=>iNow,
                timeoutPolicy, iMockTimerThread.Object, iSoftThread, iMockSession.Object);
        }
        
        [Test]
        public void WhenServeIsInvokedBeforeSend_TheServedDocumentShouldContainTheEvent()
        {
            const string jsonPayloadString = @"{""foo"":""bar""}";
            const string expectedDocumentString = @"[{""type"":""event"",""value"":{""foo"":""bar""}}]";
            JsonValue jsonPayload = JsonValue.FromString(jsonPayloadString);
            ArraySegment<byte> actualDocument = new ArraySegment<byte>();
            iMockPageWriter.Setup(aX => aX.Write(It.IsAny<ArraySegment<byte>>())).Callback<ArraySegment<byte>>(
                arraySegment => actualDocument = arraySegment);
            iServerTab.Serve()(PageWriter.Write, PageWriter.Flush, PageWriter.End, CancellationToken.None);
            iServerTab.Send(jsonPayload);
            iMockPageWriter.Verify(aX => aX.Write(It.IsAny<ArraySegment<byte>>()));
            Assert.That(
                Encoding.UTF8.GetString(actualDocument.Array, actualDocument.Offset, actualDocument.Count),
                Is.EqualTo(expectedDocumentString));
        }

        static IEnumerable<T> SegmentToSequence<T>(ArraySegment<T> aArraySegment)
        {
            var result = aArraySegment.Array.AsEnumerable().Skip(aArraySegment.Offset).Take(aArraySegment.Count);
            Console.WriteLine(String.Join(",", result));
            return result;
        }

        [Test]
        public void WhenSendIsInvokedBeforeServe_TheServedDocumentShouldContainTheEvent()
        {
            const string jsonPayloadString = @"{""foo"":""bar""}";
            const string expectedDocumentString = @"[{""type"":""event"",""value"":{""foo"":""bar""}}]";
            JsonValue jsonPayload = JsonValue.FromString(jsonPayloadString);
            ArraySegment<byte> actualDocument = new ArraySegment<byte>();
            iMockPageWriter.Setup(aX => aX.Write(It.IsAny<ArraySegment<byte>>())).Callback<ArraySegment<byte>>(
                arraySegment => actualDocument = arraySegment);
            iServerTab.Send(jsonPayload);
            iServerTab.Serve()(PageWriter.Write, PageWriter.Flush, PageWriter.End, CancellationToken.None);
            iMockPageWriter.Verify(aX => aX.Write(It.IsAny<ArraySegment<byte>>()));
            Assert.That(
                Encoding.UTF8.GetString(actualDocument.Array, actualDocument.Offset, actualDocument.Count),
                Is.EqualTo(expectedDocumentString));
        }

        [Test]
        public void WhenSendIsInvokedMultipleTimes_TheServedDocumentShouldContainTheEventsInOrder()
        {
            string[] jsonPayloadStrings = {
                @"{""foo"":""bar""}",
                @"{""abc"":""def""}",
                @"{""ninety-nine"":99}" };
            const string expectedDocumentString =
                "["+
                    @"{""type"":""event"",""value"":{""foo"":""bar""}}," +
                    @"{""type"":""event"",""value"":{""abc"":""def""}}," +
                    @"{""type"":""event"",""value"":{""ninety-nine"":99}}" +
                "]";
            var jsonPayloads = jsonPayloadStrings.Select(JsonValue.FromString);
            ArraySegment<byte> actualDocument = new ArraySegment<byte>();
            iMockPageWriter.Setup(aX => aX.Write(It.IsAny<ArraySegment<byte>>())).Callback<ArraySegment<byte>>(
                arraySegment => actualDocument = arraySegment);
            foreach (var payload in jsonPayloads)
            {
                iServerTab.Send(payload);
            }
            iServerTab.Serve()(PageWriter.Write, PageWriter.Flush, PageWriter.End, CancellationToken.None);
            iMockPageWriter.Verify(aX => aX.Write(It.IsAny<ArraySegment<byte>>()));
            Assert.That(
                Encoding.UTF8.GetString(actualDocument.Array, actualDocument.Offset, actualDocument.Count),
                Is.EqualTo(expectedDocumentString));
        }

        /* TODO : Provide equivalent tests. We removed ShouldExpire when making the ServerTab responsible
         * scheduling its own maintenance. Probably the easiest way to do that is to create a substitute
         * TimerThread that lets us manually say "run, in order, everything scheduled up until time X".
        [Test]
        public void WhenNoPollIsOutstanding_ShouldExpireReturnsTrue_IfEnoughTimeHasPassed()
        {

            iNow += TimeSpan.FromSeconds(11);
            Assert.That(iServerTab.ShouldExpire(TimeSpan.FromSeconds(10)), Is.True);
        }

        [Test]
        public void WhenNoPollIsOutstanding_ShouldExpireReturnsFalse_IfNotEnoughTimeHasPassed()
        {
            iNow += TimeSpan.FromSeconds(9);
            Assert.That(iServerTab.ShouldExpire(TimeSpan.FromSeconds(10)), Is.False);
        }

        [Test]
        public void WhenAPollIsOutstanding_ShouldExpireReturnsFalse_EvenIfTimeHasPassed()
        {
            iServerTab.Serve()(PageWriter.Write, PageWriter.Flush, PageWriter.End, CancellationToken.None);
            iNow += TimeSpan.FromSeconds(11);
            Assert.That(iServerTab.ShouldExpire(TimeSpan.FromSeconds(10)), Is.False);
        }*/

        [Test]
        public void WhenSendIsInvoked_TheListenerShouldBeNotified()
        {
            iMockSession.Setup(x => x.UserId).Returns("vladimir");
            iServerTab.Send(new JsonString("test"));
            iMockStatusListener.Verify(x => x.UpdateTabStatus("session7", "tab9", "vladimir", 1, iNow, false));
        }

        [Test]
        public void WhenServeIsInvoked_TheListenerShouldBeNotified()
        {
            iMockSession.Setup(x => x.UserId).Returns("vladimir");
            iServerTab.Serve()(PageWriter.Write, PageWriter.Flush, PageWriter.End, CancellationToken.None);
            iMockStatusListener.Verify(x => x.UpdateTabStatus("session7", "tab9", "vladimir", 0, iNow, true));
        }

        [Test]
        public void WhenTabClosedIsInvoked_TheListenerShouldBeNotified()
        {
            iServerTab.TabClosed();
            iMockStatusListener.Verify(x => x.TabClosed("session7", "tab9"));
        }

        [Test]
        public void WhenTabClosedIsInvoked_TheAppShouldBeNotified_IfItIsAttached()
        {
            var mockApp = new Mock<IAppTab>();
            iServerTab.AppTab = mockApp.Object;
            iServerTab.TabClosed();
            mockApp.Verify(x => x.TabClosed());
        }

        [Test]
        public void WhenDoMaintenanceRuns_TheRequestIsFulfilled_IfEnoughTimeHasPassed()
        {
            const string expectedDocumentString = "[]";
            ArraySegment<byte> actualDocument = new ArraySegment<byte>();
            iMockPageWriter.Setup(aX => aX.Write(It.IsAny<ArraySegment<byte>>())).Callback<ArraySegment<byte>>(
                arraySegment => actualDocument = arraySegment);
            iServerTab.Serve()(PageWriter.Write, PageWriter.Flush, PageWriter.End, CancellationToken.None);
            iNow += TimeSpan.FromSeconds(11);
            iServerTab.DoMaintenance();
            iMockPageWriter.Verify(aX => aX.Write(It.IsAny<ArraySegment<byte>>()));
            Assert.That(
                Encoding.UTF8.GetString(actualDocument.Array, actualDocument.Offset, actualDocument.Count),
                Is.EqualTo(expectedDocumentString));
        }

        [Test]
        public void WhenDoMaintenanceRuns_TheRequestIsNotFulfilled_IfTooLittleTimeHasPassed()
        {
            iServerTab.Serve()(PageWriter.Write, PageWriter.Flush, PageWriter.End, CancellationToken.None);
            iNow += TimeSpan.FromSeconds(9);
            iServerTab.DoMaintenance();
            iMockPageWriter.Verify(aX => aX.Write(It.IsAny<ArraySegment<byte>>()), Times.Never());
        }

        [Test]
        public void WhenDoMaintenanceRuns_TheTabIsExpired_IfEnoughTimeHasPassed()
        {
            iNow += TimeSpan.FromSeconds(11);
            iServerTab.DoMaintenance();
            iMockSession.Verify(x => x.NotifyTabExpired("tab9"));
        }

        [Test]
        public void WhenDoMaintenanceRuns_TheTabIsNotExpired_IfTooLittleTimeHasPassed()
        {
            iNow += TimeSpan.FromSeconds(9);
            iServerTab.DoMaintenance();
            iMockSession.Verify(x => x.NotifyTabExpired("tab9"), Times.Never());
        }
    }
}