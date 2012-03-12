using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using OpenHome.Os.Platform.DataStores;

namespace OpenHome.Os.DataStores
{
    public abstract class TransactionRepositoryTestRig
    {
        public class MockCommitLogStore : ICommitLogStore
        {
            public class AppendArgs
            {
                private readonly List<CommittedTransaction> iTransactions;

                public AppendArgs(List<CommittedTransaction> aTransactions)
                {
                    iTransactions = aTransactions;
                }

                public List<CommittedTransaction> Transactions
                {
                    get { return iTransactions; }
                }
            }

            private readonly List<AppendArgs> iAppendCalledWith = new List<AppendArgs>();

            public IList<AppendArgs> AppendCalledWith
            {
                get { return iAppendCalledWith.AsReadOnly(); }
            }

            public IEnumerable<CommittedTransaction> ReadCommitLog()
            {
                throw new NotImplementedException();
            }

            public void AppendTransactions(IEnumerable<CommittedTransaction> aTransactions)
            {
                iAppendCalledWith.Add(new AppendArgs(aTransactions.ToList()));
            }

            public void Flush()
            {
                throw new NotImplementedException();
            }

            public void ForgetPreviousCalls()
            {
                iAppendCalledWith.Clear();
            }
        }
        public class MockPlayer : ITransactionPlayer<Transaction>
        {
            public class UpdateArgs
            {
                private readonly List<Transaction> iRolledBack;
                private readonly List<Transaction> iRolledForward;
                private readonly List<Transaction> iEverything;

                public UpdateArgs(List<Transaction> aRolledBack, List<Transaction> aRolledForward, List<Transaction> aEverything)
                {
                    iRolledBack = aRolledBack;
                    iEverything = aEverything;
                    iRolledForward = aRolledForward;
                }

                public IList<Transaction> RolledBack
                {
                    get { return iRolledBack.AsReadOnly(); }
                }
                public IList<Transaction> RolledForward
                {
                    get { return iRolledForward.AsReadOnly(); }
                }
                public IList<Transaction> Everything
                {
                    get { return iEverything.AsReadOnly(); }
                }
            }

            private readonly List<UpdateArgs> iUpdateCalledWith = new List<UpdateArgs>();

            public IList<UpdateArgs> UpdateCalledWith
            {
                get { return iUpdateCalledWith.AsReadOnly(); }
            }

            public void Update(List<Transaction> aRolledBack, IEnumerable<Transaction> aRollForward, IEnumerable<Transaction> aEverything)
            {
                iUpdateCalledWith.Add(new UpdateArgs(aRolledBack, aRollForward.ToList(), aEverything.ToList()));
            }

            public void Reset(IEnumerable<Transaction> aEverything)
            {
                throw new NotImplementedException();
            }

            public void ForgetPreviousCalls()
            {
                iUpdateCalledWith.Clear();
            }

            
        }

        private int Compare(Transaction aFirst, Transaction aSecond)
        {
            return Compare(aFirst.Timestamp, aSecond.Timestamp)
                .ToIntWithDefault(
                    () => String.Compare(aFirst.OwnerUuid, aSecond.OwnerUuid)
                );
        }

        protected abstract Comparison Compare(Timestamp aFirst, Timestamp aSecond);

        public TransactionRepository TransactionRepository { get; private set; }

        public Mock<IDistributedClock<Timestamp>> ClockMock { get; private set; }
        public IDistributedClock<Timestamp> Clock { get { return ClockMock.Object; } }
        public MockPlayer PlayerMock { get; private set; }
        public ITransactionPlayer<Transaction> Player { get { return PlayerMock; } }
        public Mock<IComparer<Transaction>> OrderingMock { get; private set; }
        public IComparer<Transaction> Ordering { get { return OrderingMock.Object; } }

        public MockCommitLogStore CommitLogStoreMock { get; private set; }
        public ICommitLogStore CommitLogStore { get { return CommitLogStoreMock; } }

        public Mock<ITimeStampComparer<Timestamp>> ComparerMock { get; private set; }
        public ITimeStampComparer<Timestamp> Comparer { get { return ComparerMock.Object; } }

        protected abstract void SetupClockMock();

        public void Setup()
        {
            ClockMock = new Mock<IDistributedClock<Timestamp>>();
            SetupClockMock();
            PlayerMock = new MockPlayer();
            OrderingMock = new Mock<IComparer<Transaction>>();
            OrderingMock.Setup(aOrdering => aOrdering.Compare(It.IsAny<Transaction>(), It.IsAny<Transaction>())).Returns
                (
                    (Transaction aFirst, Transaction aSecond) => Compare(aFirst, aSecond));
            CommitLogStoreMock = new MockCommitLogStore();
            ComparerMock = new Mock<ITimeStampComparer<Timestamp>>();
            ComparerMock.Setup(aComparer => aComparer.Compare(It.IsAny<Timestamp>(), It.IsAny<Timestamp>())).Returns
                (
                    (Timestamp aFirst, Timestamp aSecond) => Compare(aFirst, aSecond));

            TransactionRepository = new TransactionRepository(
                Clock, Player, Ordering, CommitLogStore, Comparer);
        }
    }
}