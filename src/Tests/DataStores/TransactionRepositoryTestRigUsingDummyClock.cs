using System.Linq;
using Moq;
using OpenHome.Os.Platform.DataStores;

namespace OpenHome.Os.DataStores
{
    public class TransactionRepositoryTestRigUsingDummyClock : TransactionRepositoryTestRig
    {
        protected override Comparison Compare(Timestamp aFirst, Timestamp aSecond)
        {
            DummyTimestamp first = DummyTimestamp.FromString(aFirst.Serialization);
            DummyTimestamp second = DummyTimestamp.FromString(aSecond.Serialization);
            if (first.Id == second.Id)
            {
                return Comparison.Equal;
            }
            if (first.Predecessors.Contains(second.Id))
            {
                return Comparison.MoreThan;
            }
            if (second.Predecessors.Contains(first.Id))
            {
                return Comparison.LessThan;
            }
            return Comparison.Unordered;
        }


        protected override void SetupClockMock()
        {
            DummyTimestamp now = new DummyTimestamp("x", "");
            ClockMock.Setup(
                aClock => aClock.Update(It.IsAny<Timestamp>())).Callback(
                    (Timestamp aTimestamp) =>
                        {
                            var dummyTimestamp = DummyTimestamp.FromString(aTimestamp.Serialization);
                            now = new DummyTimestamp(
                                now.Id + "x",
                                now.Predecessors.Concat(dummyTimestamp.Predecessors).Concat(
                                    new[] {dummyTimestamp.Id, now.Id}));
                        });
            ClockMock.SetupGet(
                aClock => aClock.Now).Returns(()=>new Timestamp(now.ToString()));
        }
    }
}