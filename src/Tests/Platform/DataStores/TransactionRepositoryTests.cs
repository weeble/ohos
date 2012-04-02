using Moq;
using NUnit.Framework;
using System.Linq;

namespace OpenHome.Os.Platform.DataStores
{
    public class WhenASingleTransactionIsAppendedToAnEmptyRepositoryContext : TransactionRepositoryContextUsingDummyClock
    {
        protected Transaction B1 { get; private set; }
        [SetUp]
        public void AppendSingleTransaction()
        {
            B1 = MkTrans("b", "1", "");
            TestRig.TransactionRepository.AppendTransactions(MkSeq(B1));
        }
    }

    public class WhenASingleTransactionIsAppendedToAnEmptyRepository : WhenASingleTransactionIsAppendedToAnEmptyRepositoryContext
    {
        [Test]
        public void UpdateShouldBeInvokedOnThePlayer()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void NoTransactionsShouldBeRolledBack()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledBack,
                Is.Empty);
        }

        [Test]
        public void OneTransactionShouldBeRolledForward()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledForward,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void OneTransactionShouldBeInTheFullRepository()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].Everything,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void UpdateShouldBeInvokedOnceOnTheClock()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(It.IsAny<Timestamp>()),
                Times.Once());
        }

        [Test]
        public void TheClockShouldBeUpdatedWithTheTransactionTimestamp()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(B1.Timestamp),
                Times.Once());
        }
    }

    public class WhenASecondTransactionIsAppendedToARepositoryContext : WhenASingleTransactionIsAppendedToAnEmptyRepositoryContext
    {
        protected Transaction C1 { get; private set; }
        [SetUp]
        public void AppendSecondTransaction()
        {
            TestRig.PlayerMock.ForgetPreviousCalls();
            C1 = MkTrans("c", "1", "");
            TestRig.TransactionRepository.AppendTransactions(MkSeq(C1));
        }
    }

    public class WhenASecondTransactionIsAppendedToARepository : WhenASecondTransactionIsAppendedToARepositoryContext
    {
        [Test]
        public void UpdateShouldBeInvokedOnThePlayer()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void NoTransactionsShouldBeRolledBack()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledBack,
                Is.Empty);
        }

        [Test]
        public void TheNewTransactionShouldBeRolledForward()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledForward,
                Is.EqualTo(MkSeq(C1)));
        }

        [Test]
        public void TwoTransactionsShouldBeInTheFullRepository()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].Everything,
                Is.EqualTo(MkSeq(B1, C1)));
        }
        
        [Test]
        public void UpdateShouldBeInvokedATotalOfTwiceOnTheClock()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(It.IsAny<Timestamp>()),
                Times.Exactly(2));
        }

        [Test]
        public void TheClockShouldBeUpdatedWithTheTransactionTimestamp()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(C1.Timestamp),
                Times.Once());
        }
    }

    public class WhenASecondTransactionIsPrependedToARepositoryContext : WhenASingleTransactionIsAppendedToAnEmptyRepositoryContext
    {
        protected Transaction A1 { get; private set; }
        [SetUp]
        public void AppendSecondTransaction()
        {
            TestRig.PlayerMock.ForgetPreviousCalls();
            A1 = MkTrans("a", "1", "");
            TestRig.TransactionRepository.AppendTransactions(MkSeq(A1));
        }
    }

    public class WhenASecondTransactionIsPrependedToARepository : WhenASecondTransactionIsPrependedToARepositoryContext
    {
        [Test]
        public void UpdateShouldBeInvokedOnThePlayer()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void OneTransactionShouldBeRolledBack()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledBack,
                Is.EqualTo(MkSeq(B1)));
        }

        [Test]
        public void BothTransactionsShouldBeRolledForward()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledForward,
                Is.EqualTo(MkSeq(A1,B1)));
        }

        [Test]
        public void TwoTransactionsShouldBeInTheFullRepository()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].Everything,
                Is.EqualTo(MkSeq(A1, B1)));
        }
    }

    public class WhenASuiteOfTransactionsAreAppendedToAnEmptyRepositoryContext : TransactionRepositoryContextUsingDummyClock
    {
        protected Transaction A1 { get; private set; }
        protected Transaction A2 { get; private set; }
        protected Transaction A3 { get; private set; }
        protected Transaction A4 { get; private set; }
        protected Transaction B1 { get; private set; }
        protected Transaction B2 { get; private set; }
        protected Transaction B3 { get; private set; }
        protected Transaction B4 { get; private set; }
        protected Transaction C1 { get; private set; }
        protected Transaction C2 { get; private set; }
        [SetUp]
        public void AppendSuiteOfTransactions()
        {
            A1 = MkTrans("a", "1", "");
            A2 = MkTrans("a", "2", "a1-b1");
            A3 = MkTrans("a", "3", "a1-b1-a2-b2");
            A4 = MkTrans("a", "4", "a1-b1-a2-b2-a3-c1-c2");
            B1 = MkTrans("b", "1", "");
            B2 = MkTrans("b", "2", "a1-b1");
            B3 = MkTrans("b", "3", "a1-b1-b2");
            B4 = MkTrans("b", "4", "a1-b1-a2-b2-a3-b3-c1-c2-a4");
            C1 = MkTrans("c", "1", "");
            C2 = MkTrans("c", "2", "c1");
            TestRig.TransactionRepository.AppendTransactions(
                MkSeq(A1,B1,B2,B3));
        }
    }

    public class WhenASuiteOfTransactionsAreAppendedToAnEmptyRepository : WhenASuiteOfTransactionsAreAppendedToAnEmptyRepositoryContext
    {
        [Test]
        public void UpdateShouldBeInvokedOnThePlayer()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void NoTransactionsShouldBeRolledBack()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledBack,
                Is.Empty);
        }

        [Test]
        public void TheNewTransactionsShouldBeRolledForward()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledForward,
                Is.EqualTo(MkSeq(A1,B1,B2,B3)));
        }

        [Test]
        public void FourTransactionsShouldBeInTheFullRepository()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].Everything,
                Is.EqualTo(MkSeq(A1,B1,B2,B3)));
        }
        
        [Test]
        public void TheClockShouldBeUpdatedWithTheLastTimestamp()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(B3.Timestamp));
        }

        [Test]
        public void AppendTransactionsShouldBeCalledOnTheCommitLogStore()
        {
            Assert.That(
                TestRig.CommitLogStoreMock.AppendCalledWith,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void TheCommitLogStoreShouldReceiveAllTheTransactions()
        {
            Assert.That(
                TestRig.CommitLogStoreMock.AppendCalledWith[0].Transactions.Select(aItem=>aItem.Transaction),
                Is.EqualTo(MkSeq(A1, B1, B2, B3)));
        }

        [Test]
        public void TheClockShouldBeUpdatedFourTimes()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(It.IsAny<Timestamp>()),
                Times.Exactly(4));
        }
    }

    public class WhenASuiteOfTransactionsAreAppendedToANonEmptyRepositoryContext : WhenASuiteOfTransactionsAreAppendedToAnEmptyRepositoryContext
    {
        [SetUp]
        public void AppendAnotherSuiteOfTransactions()
        {
            TestRig.PlayerMock.ForgetPreviousCalls();
            TestRig.CommitLogStoreMock.ForgetPreviousCalls();
            TestRig.TransactionRepository.AppendTransactions(
                MkSeq(A3,C1,C2,A4));
        }
    }

    public class WhenASuiteOfTransactionsAreAppendedToANonEmptyRepository : WhenASuiteOfTransactionsAreAppendedToANonEmptyRepositoryContext
    {
        [Test]
        public void UpdateShouldBeInvokedOnThePlayer()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void OneTransactionsShouldBeRolledBack()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledBack,
                Is.EqualTo(MkSeq( B3 )));
        }

        [Test]
        public void TheNewTransactionsShouldBeRolledForward()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledForward,
                Is.EqualTo(MkSeq( A3, B3, C1, C2, A4 )));
        }

        [Test]
        public void NineTransactionsShouldBeInTheFullRepository()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].Everything,
                Is.EqualTo(MkSeq( A1, B1, B2, A3, B3, C1, C2, A4 )));
        }

        [Test]
        public void TheClockShouldBeUpdatedWithTheLastTimestamp()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(A4.Timestamp));
        }

        [Test]
        public void AppendTransactionsShouldBeCalledOnTheCommitLogStore()
        {
            Assert.That(
                TestRig.CommitLogStoreMock.AppendCalledWith,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void TheCommitLogStoreShouldReceiveTheNewTransactions()
        {
            Assert.That(
                TestRig.CommitLogStoreMock.AppendCalledWith[0].Transactions.Select(aItem=>aItem.Transaction),
                Is.EqualTo(MkSeq( A3, B3, C1, C2, A4 )));
        }
    }

    public class WhenAnOverlappingSuiteOfTransactionsAreAppendedToANonEmptyRepositoryContext : WhenASuiteOfTransactionsAreAppendedToAnEmptyRepositoryContext
    {
        [SetUp]
        public void AppendAnothernOverlappingSuiteOfTransactions()
        {
            TestRig.PlayerMock.ForgetPreviousCalls();
            TestRig.CommitLogStoreMock.ForgetPreviousCalls();
            TestRig.TransactionRepository.AppendTransactions(
                MkSeq(A1,A3,B3,C1,C2,A4));
        }
    }

    public class WhenAnOverlappingSuiteOfTransactionsAreAppendedToANonEmptyRepository : WhenAnOverlappingSuiteOfTransactionsAreAppendedToANonEmptyRepositoryContext
    {
        [Test]
        public void UpdateShouldBeInvokedOnThePlayer()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void OneTransactionsShouldBeRolledBack()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledBack,
                Is.EqualTo(MkSeq(B3)));
        }

        [Test]
        public void TheNewTransactionsShouldBeRolledForward()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].RolledForward,
                Is.EqualTo(MkSeq(A3,B3,C1,C2,A4)));
        }

        [Test]
        public void NineTransactionsShouldBeInTheFullRepository()
        {
            Assert.That(
                TestRig.PlayerMock.UpdateCalledWith[0].Everything,
                Is.EqualTo(MkSeq(A1,B1,B2,A3,B3,C1,C2,A4)));
        }
        
        [Test]
        public void TheClockShouldBeUpdatedWithTheLastTimestamp()
        {
            TestRig.ClockMock.Verify(
                aClock => aClock.Update(A4.Timestamp));
        }

        [Test]
        public void AppendTransactionsShouldBeCalledOnTheCommitLogStore()
        {
            Assert.That(
                TestRig.CommitLogStoreMock.AppendCalledWith,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void TheCommitLogStoreShouldReceiveTheNewTransactions()
        {
            Assert.That(
                TestRig.CommitLogStoreMock.AppendCalledWith[0].Transactions.Select(aItem=>aItem.Transaction),
                Is.EqualTo(MkSeq(A3, B3, C1, C2, A4)));
        }
    }
}
