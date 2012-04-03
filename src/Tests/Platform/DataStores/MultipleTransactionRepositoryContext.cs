using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OpenHome.Os.Platform.DataStores
{
    public abstract class MultipleTransactionRepositoryContext
    {
        public TransactionRepositoryTestRigUsingItcClock[] TestRigs = new TransactionRepositoryTestRigUsingItcClock[4];

        public ItcClock ClockOmega { get; set; }

        public ItcClock ClockAlpha { get; set; }

        public List<ItcStamp> OmegaStamps { get; set; }

        public List<ItcStamp> AlphaStamps { get; set; }

        [SetUp]
        public void CreateTestRigs()
        {
            List<ItcClock> clocks = new List<ItcClock> {new ItcClock()};
            clocks.Add(clocks[0].Fork());
            clocks.Add(clocks[0].Fork());
            clocks.Add(clocks[1].Fork());
            clocks.Add(clocks[1].Fork());
            clocks.Add(clocks[2].Fork());
            for (int i = 0; i != 4; ++i)
            {
                TestRigs[i] = new TransactionRepositoryTestRigUsingItcClock();
                TestRigs[i].Setup();
                TestRigs[i].ItcClock = clocks[i];
            }
            ClockOmega = clocks[4];
            ClockAlpha = clocks[5];
        }
    }

    public abstract class TransactionSequenceOnMultipleNodesContext : MultipleTransactionRepositoryContext
    {
        protected Transaction A1 { get; private set; }
        protected Transaction A2 { get; private set; }
        protected Transaction B1 { get; private set; }
        protected Transaction B2 { get; private set; }
        protected Transaction C1 { get; private set; }
        protected Transaction C2 { get; private set; }
        protected Transaction D1 { get; private set; }
        protected Transaction D2 { get; private set; }

        protected List<string> iInitialClockStates;

        protected void SaveClocks()
        {
            iInitialClockStates = new List<string>();
            foreach (var rig in TestRigs)
            {
                iInitialClockStates.Add(rig.ItcClock.SaveState());
            }
        }

        protected void RestoreClocks()
        {
            for (int i = 0; i != TestRigs.Length; ++i)
            {
                TestRigs[i].ItcClock.LoadState(iInitialClockStates[i]);
            }
        }

        protected Transaction MkTrans(string aOwner, string aIndex)
        {
            int clockIndex = aOwner[0] - 'A';
            TestRigs[clockIndex].ItcClock.Advance();
            return new Transaction(
                "", aOwner+aIndex, aOwner, new Timestamp(TestRigs[clockIndex].ItcClock.Now.ToString()));
        }

        protected void ClockReceiveMessage(string aOwner, Transaction aTransaction)
        {
            int clockIndex = aOwner[0] - 'A';
            TestRigs[clockIndex].ItcClock.Update(ItcStamp.FromString(aTransaction.Timestamp.Serialization));
        }

        protected Transaction[] MkSeq(params Transaction[] aItems)
        {
            return aItems;
        }

        [SetUp]
        public void SetUp()
        {
            SaveClocks();
            CreateTransactions();
            CreateOtherTimestamps();
            RestoreClocks();
            CreateCommitLogs();
        }

        void CreateCommitLogs()
        {
            TestRigs[0].TransactionRepository.AppendTransactions(MkSeq(A1, C1, B1, A2, B2, D1, C2, D2));
            TestRigs[1].TransactionRepository.AppendTransactions(MkSeq(A1, C1, B1, B2, D1, A2, C2, D2));
            TestRigs[2].TransactionRepository.AppendTransactions(MkSeq(C1, A1, D1, B1, B2, C2, D2, A2));
            TestRigs[3].TransactionRepository.AppendTransactions(MkSeq(C1, D1, A1, B1, D2, B2, C2, A2));
        }

        protected void CreateOtherTimestamps()
        {
            AlphaStamps = new List<ItcStamp> {ClockAlpha.Now};
            foreach (var transaction in MkSeq(A1, C1, B1, A2, B2, D1, C2, D2))
            {
                ClockAlpha.Update(ItcStamp.FromString(transaction.Timestamp.Serialization));
                AlphaStamps.Add(ClockAlpha.Now);
            }
            OmegaStamps = new List<ItcStamp> {ClockOmega.Now};
            foreach (var transaction in MkSeq(C1, D1, A1, B1, D2, B2, C2, A2))
            {
                ClockOmega.Update(ItcStamp.FromString(transaction.Timestamp.Serialization));
                OmegaStamps.Add(ClockOmega.Now);
            }
        }

        protected void CreateTransactions()
        {
            A1 = MkTrans("A", "1");
            C1 = MkTrans("C", "1");
            ClockReceiveMessage("D", C1);
            D1 = MkTrans("D", "1");
            ClockReceiveMessage("B", A1);
            ClockReceiveMessage("B", C1);
            B1 = MkTrans("B", "1");
            B2 = MkTrans("B", "2");
            ClockReceiveMessage("A", B1);
            A2 = MkTrans("A", "2");
            ClockReceiveMessage("C", D1);
            ClockReceiveMessage("C", B1);
            ClockReceiveMessage("C", B2);
            C2 = MkTrans("C", "2");
            ClockReceiveMessage("D", A1);
            ClockReceiveMessage("D", B1);
            D2 = MkTrans("D", "2");
            
        }
    }

    public class WhenFetchingTransactionsRelativeToATimestamp : TransactionSequenceOnMultipleNodesContext
    {
        public void TheRightTransactionsShouldBeReturned(
            int aRepositoryIndex,
            ItcStamp aTimestamp,
            params Transaction[] aExpectedTransactions)
        {
            var transactions = TestRigs[aRepositoryIndex].TransactionRepository.GetMissingTransactions(new Timestamp(aTimestamp.ToString())).ToList();
            Assert.That(transactions, Is.EqualTo(aExpectedTransactions));
        }

        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega0()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[0], A1, C1, B1, B2, D1, A2, C2, D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega1()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[1], A1, B1, B2, D1, A2, C2, D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega2()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[2], A1, B1, B2, A2, C2, D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega3()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[3], B1, B2, A2, C2, D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega4()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[4], B2, A2, C2, D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega5()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[5], B2, A2, C2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega6()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[6], A2, C2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega7()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[7], A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToOmega8()
        {
            TheRightTransactionsShouldBeReturned(1, OmegaStamps[8]);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha0()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[0], A1,C1,B1,B2,D1,A2,C2,D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha1()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[1], C1,B1,B2,D1,A2,C2,D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha2()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[2], B1,B2,D1,A2,C2,D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha3()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[3], B2,D1,A2,C2,D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha4()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[4], B2,D1,C2,D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha5()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[5], D1,C2,D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha6()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[6], C2,D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha7()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[7], D2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromBToAlpha8()
        {
            TheRightTransactionsShouldBeReturned(1, AlphaStamps[8]);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega0()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[0], C1, A1, D1, B1, B2, C2, D2, A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega1()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[1], A1, D1, B1, B2, C2, D2, A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega2()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[2], A1, B1, B2, C2, D2, A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega3()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[3], B1, B2, C2, D2, A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega4()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[4], B2, C2, D2, A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega5()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[5], B2, C2, A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega6()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[6], C2, A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega7()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[7], A2);
        }
        [Test]
        public void TheRightTransactionsShouldBeReturnedFromCToOmega8()
        {
            TheRightTransactionsShouldBeReturned(2, OmegaStamps[8]);
        }

        
    }
}
