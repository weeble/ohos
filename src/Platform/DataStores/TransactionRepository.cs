using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Linq;
using OpenHome.Os.Platform.Collections;

namespace OpenHome.Widget.Nodes.DataStores
{

    // TODO : Review use of generics - Graham isn't keen on these.

    public interface IDistributedClock<TTimestamp>
    {
        void Update(TTimestamp aTimestamp);
        TTimestamp Now { get; }
        TTimestamp Advance();
    }

    public interface IDistributedClock : IDistributedClock<Timestamp>
    {
    }

    public class Timestamp
    {
        public string Serialization { get; private set; }

        public Timestamp(string aSerialization)
        {
            Serialization = aSerialization;
        }
        public override string ToString()
        {
            return String.Format("Timestamp(\"{0}\")", Serialization);
        }
    }

    public class Transaction : ITransaction
    {
        public Transaction(string aSerialization, string aUuid, string aOwnerUuid, Timestamp aTimestamp)
        {
            Serialization = aSerialization;
            Uuid = aUuid;
            OwnerUuid = aOwnerUuid;
            Timestamp = aTimestamp;
        }

        public string Serialization { get; private set; }
        public string Uuid { get; private set; }
        public string OwnerUuid { get; private set; }
        public Timestamp Timestamp { get; private set; }
        public override string ToString()
        {
            return String.Format("Transaction(\"{0}\", \"{1}\", \"{2}\", {3})", Serialization, Uuid, OwnerUuid, Timestamp);
        }
        public XElement ToXml()
        {
            return new XElement("transaction",
                new XElement("uuid", Uuid),
                new XElement("owner", OwnerUuid),
                new XElement("timestamp", Timestamp.Serialization),
                new XElement("data", Serialization));
        }
        static string GetValueOrThrow(XElement aXElement, string aChildName)
        {
            var child = aXElement.Element(aChildName);
            if (child == null)
            {
                throw new BadTransactionXmlException();
            }
            return child.Value;
        }
        public static Transaction FromXml(XElement aXElement)
        {
            if (aXElement.Name != "transaction")
            {
                throw new BadTransactionXmlException();
            }
            string uuid = GetValueOrThrow(aXElement,"uuid");
            string owner = GetValueOrThrow(aXElement,"owner");
            string timestamp = GetValueOrThrow(aXElement,"timestamp");
            string data = GetValueOrThrow(aXElement, "data");
            return new Transaction(data, uuid, owner, new Timestamp(timestamp));
        }
        public static IEnumerable<Transaction> SequenceFromXml(XElement aXElement)
        {
            if (aXElement.Name != "transactionList")
            {
                throw new BadTransactionXmlException();
            }
            foreach (var transactionElement in aXElement.Elements("transaction"))
            {
                yield return FromXml(transactionElement);
            }
        }
    }


    [Serializable]
    public class BadTransactionXmlException : XmlException
    {
        public BadTransactionXmlException()
        {
        }

        public BadTransactionXmlException(string aMessage) : base(aMessage)
        {
        }

        public BadTransactionXmlException(string aMessage, Exception aInner) : base(aMessage, aInner)
        {
        }

        protected BadTransactionXmlException(
            SerializationInfo aInfo,
            StreamingContext aContext) : base(aInfo, aContext)
        {
        }
    }

    public interface ITransactionPlayer<TTransaction>
    {
        void Update(
            List<TTransaction> aRolledBack,
            IEnumerable<TTransaction> aRollForward,
            IEnumerable<TTransaction> aEverything);

        /// <summary>
        /// Roll-back all transactions and then apply the
        /// new transaction sequence.
        /// </summary>
        void Reset(
            IEnumerable<TTransaction> aEverything);
    }

    public interface ITransactionPlayer : ITransactionPlayer<Transaction>
    {
    }

    public interface ITransaction<TTimestamp>
    {
        string Uuid { get; }
        string OwnerUuid { get; }
        TTimestamp Timestamp { get; }
    }

    public interface ITransaction : ITransaction<Timestamp>
    {
    }

    public interface ITransactionOrdering<TTransaction>
    {
        int Compare(TTransaction aFirst, TTransaction aSecond);
    }

    public interface ITimeStampComparer<TTimestamp>
    {
        Comparison Compare(TTimestamp aFirst, TTimestamp aSecond);
    }

    public interface ICommitLogStore
    {
        IEnumerable<CommittedTransaction> ReadCommitLog();
        void AppendTransactions(IEnumerable<CommittedTransaction> aTransactions);
        void Flush();
    }

    public class CommittedTransaction
    {
        /// <summary>
        /// The body of the transaction.
        /// </summary>
        public Transaction Transaction { get; private set; }
        public Timestamp CommitTime { get; private set; }

        public CommittedTransaction(Transaction aTransaction, Timestamp aCommitTime)
        {
            Transaction = aTransaction;
            CommitTime = aCommitTime;
        }
    }

    public class CommitLog
    {
        readonly List<CommittedTransaction> iCommitLog;
        readonly ITimeStampComparer<Timestamp> iComparer;
        Timestamp iLastCommitTime;
        readonly object iLock = new object();

        public CommitLog(ITimeStampComparer<Timestamp> aComparer)
        {
            iCommitLog = new List<CommittedTransaction>();
            iComparer = aComparer;
        }

        public void Append(CommittedTransaction aTransaction)
        {
            lock (iLock)
            {
                if (iCommitLog.Count > 0 && iComparer.Compare(iLastCommitTime, aTransaction.CommitTime) == Comparison.MoreThan)
                {
                    throw new ArgumentException("Cannot append to commit log using an earlier commit time.");
                }
                iCommitLog.Add(aTransaction);
                iLastCommitTime = aTransaction.CommitTime;
            }
        }

        /// <summary>
        /// Find the index of the first item not earlier than the given timestamp.
        /// </summary>
        /// <param name="aTime"></param>
        /// <returns></returns>
        public int FindFirstNotBefore(Timestamp aTime)
        {
            lock (iLock)
            {
                int lo = 0;
                int hi = iCommitLog.Count;
                while (lo < hi)
                {
                    int mid = (lo + hi) / 2;
                    var cmp = iComparer.Compare(iCommitLog[mid].CommitTime, aTime);
                    if (cmp == Comparison.LessThan)
                    {
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid;
                    }
                }
                return lo;
            }
        }

        public CommittedTransaction this[int aIndex]
        {
            get
            {
                lock (iLock)
                {
                    return iCommitLog[aIndex];
                }
            }
        }

        public IEnumerable<CommittedTransaction> GetFromIndex(int aStartIndex)
        {
            for (int i = aStartIndex; ; ++i)
            {
                CommittedTransaction transaction;
                lock (iLock)
                {
                    if (i >= iCommitLog.Count)
                    {
                        yield break;
                    }
                    transaction = iCommitLog[i];
                }
                yield return transaction;
            }
        }

        public int Count
        {
            get
            {
                lock (iLock)
                {
                    return iCommitLog.Count;
                }
            }
        }

        public void Clear()
        {
            lock (iLock)
            {
                iCommitLog.Clear();
                iLastCommitTime = default(Timestamp);
            }
        }

        public void AddRange(IEnumerable<CommittedTransaction> aTransactions)
        {
            lock (iLock)
            {
                foreach (var item in aTransactions)
                {
                    Append(item);
                }
            }
        }
    }

    public interface ITransactionRepository
    {
        Timestamp CurrentTime { get; }
        void Reload();
        void AppendTransactions(IEnumerable<Transaction> aTransactions);
        IEnumerable<Transaction> GetMissingTransactions(Timestamp aKnownTime);
    }

    public class TransactionRepository : ITransactionRepository
    {
        readonly IDistributedClock<Timestamp> iClock;
        readonly CommitLog iCommitLog;
        readonly List<int> iTransactionOrder;

        readonly ITransactionPlayer<Transaction> iPlayer;
        readonly TransactionComparer iComparer;
        readonly ITimeStampComparer<Timestamp> iTimestampComparer;
        readonly IComparer<Transaction> iTransactionOrdering;
        readonly ICommitLogStore iCommitLogStore;

        /// <summary>
        /// Used to detect if an iterator is used when no longer valid.
        /// </summary>
        private int iInternalSequence;

        private Timestamp iCurrentTime;

        public TransactionRepository(
            IDistributedClock<Timestamp> aClock,
            ITransactionPlayer<Transaction> aPlayer,
            IComparer<Transaction> aTransactionOrdering,
            ICommitLogStore aCommitLogStore, ITimeStampComparer<Timestamp> aTimestampComparer)
        {
            iClock = aClock;
            iTimestampComparer = aTimestampComparer;
            iCommitLogStore = aCommitLogStore;
            iTransactionOrdering = aTransactionOrdering;
            iPlayer = aPlayer;
            iCommitLog = new CommitLog(iTimestampComparer);
            iTransactionOrder = new List<int>();
            iComparer = new TransactionComparer(this);
        }

        public Timestamp CurrentTime
        {
            get
            {
                if (iCurrentTime == null)
                {
                    iCurrentTime = iClock.Now;
                }
                return iCurrentTime;
            }
        }

        private int CompareTransactions(Transaction aX, Transaction aY)
        {
            return iTransactionOrdering.Compare(aX, aY);
        }

        private class TransactionComparer : IComparer<int>
        {
            private readonly TransactionRepository iParent;

            public TransactionComparer(TransactionRepository aParent)
            {
                iParent = aParent;
            }

            public int Compare(int aX, int aY)
            {
                return iParent.CompareTransactions(iParent.iCommitLog[aX].Transaction, iParent.iCommitLog[aY].Transaction);
            }

        }

        public void Reload()
        {
            iInternalSequence += 1;
            iCommitLog.Clear();
            iTransactionOrder.Clear();
            // TODO : Reload clock from... somewhere...
            // NOTE : Reading the whole commit log would be a good time to prune.
            iCommitLog.AddRange(iCommitLogStore.ReadCommitLog());
            // NOTE : Here we sort the entire commit log. This will need consideration if we
            // later want to avoid loading the entire log into memory at once.
            iTransactionOrder.AddRange(Enumerable.Range(0, iCommitLog.Count));
            iTransactionOrder.Sort(iComparer);
            iPlayer.Reset(GetTransactions(0, iInternalSequence));
        }


        private IEnumerable<CommittedTransaction> GetCommittedTransactions(int aStartIndex, int aInternalSequence)
        {
            for (int i = aStartIndex; i < iTransactionOrder.Count; ++i)
            {
                if (aInternalSequence != iInternalSequence)
                {
                    throw new InvalidOperationException(
                        "Transaction repository changed during iteration. "+
                        "This probably indicates the enumerable was retained "+
                        "until after it was valid.");
                }
                yield return iCommitLog[iTransactionOrder[i]];
            }
        }

        private IEnumerable<Transaction> GetTransactions(int aStartIndex, int aInternalSequence)
        {
            return GetCommittedTransactions(aStartIndex, aInternalSequence).Select(aCommitted => aCommitted.Transaction);
        }

        public void AppendTransactions(IEnumerable<Transaction> aTransactions)
        {
            CheckOrdered(aTransactions);
            iInternalSequence += 1;
            List<int> transactionOrderIndexes;
            List<int> commitLogIndexes;
            AppendToCommitLog(aTransactions, out commitLogIndexes, out transactionOrderIndexes);
            if (transactionOrderIndexes.Count == 0)
            {
                return;
            }
            int minimumInsertIndex;
            List<Transaction> rolledBack;
            UpdateTransactionOrder(transactionOrderIndexes, commitLogIndexes, out minimumInsertIndex, out rolledBack);

            iCommitLogStore.AppendTransactions(
                GetCommittedTransactions(minimumInsertIndex, iInternalSequence));

            // TODO: Should we do the disk I/O asynchronously and only update the player when it has completed?
            // (Same with the exchanger, when we implement it.)

            iPlayer.Update(
                rolledBack,
                GetTransactions(minimumInsertIndex, iInternalSequence),
                GetTransactions(0, iInternalSequence));
            iInternalSequence += 1;
        }

        public IEnumerable<Transaction> GetMissingTransactions(Timestamp aKnownTime)
        {
            // We want everything in the commit log with a creation time not less than or equal to aKnownTime.
            // We first find the earliest commit with commit time not less than aKnownTime. Then we
            // filter everything after this by creation time.
            int startIndex = iCommitLog.FindFirstNotBefore(aKnownTime);
            int initialSequence = iInternalSequence;
            foreach (var item in iCommitLog.GetFromIndex(startIndex))
            {
                if (iInternalSequence != initialSequence)
                {
                    throw new InvalidOperationException(
                        "Transaction repository changed during iteration. "+
                        "This probably indicates the enumerable was retained "+
                        "until after it was valid.");
                }
                if (iTimestampComparer.Compare(item.Transaction.Timestamp, aKnownTime).ToIntWithDefault(1) > 0)
                {
                    yield return item.Transaction;
                }
            }
        }

        private void UpdateTransactionOrder(List<int> aTransactionOrderIndexes, List<int> aCommitLogIndexes, out int aMinimumInsertIndex, out List<Transaction> aRolledBack)
        {
            //var lastNewTransaction = iCommitLog[iCommitLog.Count - 1];
            //iClock.Update(lastNewTransaction.Timestamp);
            aMinimumInsertIndex = aTransactionOrderIndexes[0];
            aRolledBack = new List<Transaction>(iTransactionOrder.Count - aMinimumInsertIndex);
            for (int i = iTransactionOrder.Count - 1; i >= aMinimumInsertIndex; --i)
            {
                aRolledBack.Add(iCommitLog[iTransactionOrder[i]].Transaction);
            }
            ListUtils.MultiInsert(iTransactionOrder, aTransactionOrderIndexes, aCommitLogIndexes);
            iCurrentTime = null;
        }

        /// <summary>
        /// Of the transactions provided, for those that are not already in the commit log,
        /// append them in order. Return the indexes at which they've been appended, and
        /// the indexes in the transaction order where they should be inserted.
        /// </summary>
        /// <param name="aTransactions"></param>
        /// <param name="aCommitLogIndexes"></param>
        /// <param name="aTransactionOrderIndexes">
        /// Indexes in the transaction order where the transactions referenced by
        /// aCommitLogIndexes should be inserted.
        /// </param>
        private void AppendToCommitLog(IEnumerable<Transaction> aTransactions, out List<int> aCommitLogIndexes, out List<int> aTransactionOrderIndexes)
        {
            aTransactionOrderIndexes = new List<int>();
            aCommitLogIndexes = new List<int>();
            int previousIndex = -1;
            foreach (var transaction in aTransactions)
            {
                if (iTimestampComparer.Compare(transaction.Timestamp, CurrentTime) == Comparison.LessThan)
                {
                    continue;
                }
                int commitLogIndex = iCommitLog.Count;
                iClock.Update(transaction.Timestamp);
                iCommitLog.Append(new CommittedTransaction(transaction, iClock.Now));
                int insertIndex = iTransactionOrder.BinarySearch(commitLogIndex, iComparer);
                if (insertIndex < 0)
                {
                    insertIndex = ~insertIndex;
                }
                if (insertIndex < previousIndex)
                {
                    // The search ignores the other items we're adding to the list. We must
                    // take care not to re-order them amongst themselves.
                    insertIndex = previousIndex;
                }
                previousIndex = insertIndex;
                aCommitLogIndexes.Add(commitLogIndex);
                aTransactionOrderIndexes.Add(insertIndex);
            }
        }

        private void CheckOrdered(IEnumerable<Transaction> aTransactions)
        {
            Transaction previous = null;
            foreach (var transaction in aTransactions)
            {
                if (previous != null && iTimestampComparer.Compare(transaction.Timestamp, previous.Timestamp) == Comparison.LessThan)
                {
                    throw new ArgumentException("aTransactions must be ordered with non-decreasing timestamp.");
                }
                previous = transaction;
            }
        }
    }
}
