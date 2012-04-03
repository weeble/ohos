using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace OpenHome.Os.Platform.Threading
{
    public class ChannelTests
    {
        Channel<string> iStringChannel;
        Channel<uint> iUintChannel;
        [SetUp]
        public void SetUp()
        {
            iStringChannel = new Channel<string>(3);
            iUintChannel = new Channel<uint>(3);
        }
        [TearDown]
        public void TearDown()
        {
            iStringChannel.Dispose();
            iUintChannel.Dispose();
        }
        [Test]
        public void SendingThenReceivingWorks()
        {
            iStringChannel.Send("message");
            string message = iStringChannel.Receive();
            Assert.That(message, Is.EqualTo("message"));
        }
        [Test]
        public void SendingSeveralItemsThenReceivingWorks()
        {
            iStringChannel.Send("alpha");
            iStringChannel.Send("bravo");
            iStringChannel.Send("charlie");
            string message1 = iStringChannel.Receive();
            string message2 = iStringChannel.Receive();
            string message3 = iStringChannel.Receive();
            Assert.That(new[]{message1,message2,message3}, Is.EqualTo(new[]{"alpha","bravo","charlie"}));
        }
        [Test]
        public void SendingBlocksWhenTheQueueIsFull()
        {
            iStringChannel.Send("alpha");
            iStringChannel.Send("bravo");
            iStringChannel.Send("charlie");
            object lockObj = new object();
            bool sent = false;
            Thread thread = new Thread(
                () =>
                {
                    iStringChannel.Send("delta");
                    lock (lockObj)
                    {
                        sent = true;
                    }
                });
            thread.Start();
            Thread.Sleep(250);
            bool wasSent;
            lock (lockObj)
            {
                wasSent = sent;
            }
            /* string message1 = */ iStringChannel.Receive();
            /* string message2 = */ iStringChannel.Receive();
            /* string message3 = */ iStringChannel.Receive();
            string message4 = iStringChannel.Receive();
            thread.Join();
            Assert.That(wasSent, Is.False);
            Assert.That(message4, Is.EqualTo("delta"));
        }
        [Test]
        public void ReceivingBlocksWhenTheQueueIsEmpty()
        {
            iStringChannel.Send("alpha");
            object lockObj = new object();
            bool received = false;
            string message2 = "";
            Thread thread = new Thread(
                () =>
                {
                    iStringChannel.Receive();
                    message2 = iStringChannel.Receive();
                    lock (lockObj)
                    {
                        received = true;
                    }
                });
            thread.Start();
            Thread.Sleep(250);
            bool wasReceived;
            lock (lockObj)
            {
                wasReceived = received;
            }
            iStringChannel.Send("bravo");
            thread.Join();
            Assert.That(wasReceived, Is.False);
            Assert.That(message2, Is.EqualTo("bravo"));
        }
        [Test]
        public void SelectPicksTheRightChannelForWriting()
        {
            Channel.Select(
                iStringChannel.CaseReceive(s=>Assert.Fail("Shouldn't receive from empty channel.")),
                iUintChannel.CaseReceive(u=>Assert.Fail("Shouldn't receive from empty channel.")),
                iStringChannel.CaseSend("message")
                );
            string message = iStringChannel.Receive();
            Assert.That(message, Is.EqualTo("message"));
        }
        [Test]
        public void SelectPicksTheRightChannelForReading()
        {
            iStringChannel.Send("one");
            iStringChannel.Send("two");
            iStringChannel.Send("three");
            Channel.Select(
                iUintChannel.CaseReceive(u=>Assert.Fail("Shouldn't receive from empty channel.")),
                iStringChannel.CaseSend("message"),
                iUintChannel.CaseSend(1234)
                );
            uint number = iUintChannel.Receive();
            Assert.That(number, Is.EqualTo(1234));
        }
    }
    public class ChannelPipelineTests
    {
        private class ClassifiedNumber
        {
            public uint Value { get; set; }
            public string Category { get; set; }
        }
        [Test]
        [Description(
            "Slightly more thorough test that creates a small pipeline with a split and a "+
            "merge and makes sure that the channels don't unexpectedly deadlock, drop or "+
            "rearrange message.")]
        public void TestSeveralChannelsInAPipeline()
        {
            Channel<uint> incoming = new Channel<uint>(5);
            Channel<uint> filteredEven = new Channel<uint>(9);
            Channel<uint> filteredDivisibleBy3 = new Channel<uint>(4);
            Channel<ClassifiedNumber> classifiedNumbers = new Channel<ClassifiedNumber>(10);
            Channel<int> quitChannel = new Channel<int>(2);
            Thread feederThread = new Thread(
                ()=>
                    {
                        for (uint i = 0; i!=90; ++i)
                        {
                            incoming.Send(i);
                        }
                    });
            Thread filterThread = new Thread(
                ()=>
                    {
                        bool done = false;
                        while (!done)
                        {
                            Channel.Select(
                                incoming.CaseReceive(
                                    v =>
                                    {
                                        if (v%2==0)
                                        {
                                            filteredEven.Send(v);
                                        }
                                        else if (v%3==0)
                                        {
                                            filteredDivisibleBy3.Send(v);
                                        }
                                    }
                                ),
                                quitChannel.CaseReceive(
                                    v => done = true
                                ));
                        }
                    });
            Thread mergeThread = new Thread(
                () =>
                    {
                        bool done = false;
                        while (!done)
                        {
                            Channel.Select(
                                filteredEven.CaseReceive(
                                    v=>classifiedNumbers.Send(new ClassifiedNumber{Value=v, Category = "even"})),
                                filteredDivisibleBy3.CaseReceive(
                                    v=>classifiedNumbers.Send(new ClassifiedNumber{Value=v, Category = "div3"})),
                                quitChannel.CaseReceive(
                                    v=>done = true));
                        }
                    });
            feederThread.Start();
            filterThread.Start();
            mergeThread.Start();
            List<uint> outputEvenNumbers = new List<uint>();
            List<uint> outputDivisibleBy3Numbers = new List<uint>();
            for (int idx=0; idx != 60; ++idx)
            {
                var number = classifiedNumbers.Receive();
                switch (number.Category)
                {
                    case "even": outputEvenNumbers.Add(number.Value); break;
                    case "div3": outputDivisibleBy3Numbers.Add(number.Value); break;
                }
            }
            quitChannel.Send(0);
            quitChannel.Send(0);
            feederThread.Join();
            filterThread.Join();
            mergeThread.Join();
            Assert.That(
                outputEvenNumbers, Is.EqualTo(new List<uint>{0,2,4,6,8,10,12,14,16,18,20,22,24,26,28,30,32,34,36,38,40,42,44,46,48,50,52,54,56,58,60,62,64,66,68,70,72,74,76,78,80,82,84,86,88}));
            Assert.That(
                outputDivisibleBy3Numbers, Is.EqualTo(new List<uint>{3,9,15,21,27,33,39,45,51,57,63,69,75,81,87}));
        }
    }
}
