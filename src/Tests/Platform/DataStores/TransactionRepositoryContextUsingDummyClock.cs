using System.Collections.Generic;
using NUnit.Framework;

namespace OpenHome.Os.Platform.DataStores
{
    public abstract class TransactionRepositoryContextUsingDummyClock
    {
        TransactionRepositoryTestRigUsingDummyClock iTestRig;
        protected TransactionRepositoryTestRigUsingDummyClock TestRig { get { return iTestRig; } }

        protected static Transaction MkTrans(string aOwner, string aIndex, string aPredecessors)
        {
            return new Transaction(
                "", aOwner+aIndex, aOwner, new Timestamp(new DummyTimestamp(aOwner+aIndex, aPredecessors).ToString()));
        }

        [SetUp]
        public void CreateRepository()
        {
            iTestRig = new TransactionRepositoryTestRigUsingDummyClock();
            iTestRig.Setup();
        }

        protected static IEnumerable<T> MkSeq<T>(params T[] aItems)
        {
            return aItems;
        }
    }
}