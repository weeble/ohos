using Moq;

namespace OpenHome.Os.Platform.DataStores
{
    public class TransactionRepositoryTestRigUsingItcClock : TransactionRepositoryTestRig
    {
        ItcStampComparer iComparer;
        ItcClock iClock;
        //ItcClock[] iRemoteClocks;
        //protected virtual int RemoteClockCount { get { return 4; } }

        public ItcClock ItcClock { get { return iClock; } set { iClock = value; } }

        protected override Comparison Compare(Timestamp aFirst, Timestamp aSecond)
        {
            return iComparer.Compare(ItcStamp.FromString(aFirst.Serialization), ItcStamp.FromString(aSecond.Serialization));
        }

        protected override void SetupClockMock()
        {
            iComparer = new ItcStampComparer();
            iClock = new ItcClock();
            //iRemoteClocks = new ItcClock[RemoteClockCount];
            //iRemoteClocks[0] = iClock.Fork();
            //for (int i = 1; i != RemoteClockCount-1; ++i)
            //{
            //    iRemoteClocks[i] = iRemoteClocks[(1+i)/2-1].Fork();
            //}

            ClockMock.Setup(
                aClock => aClock.Update(It.IsAny<Timestamp>())).Callback(
                    (Timestamp aTimestamp) => iClock.Update(ItcStamp.FromString(aTimestamp.Serialization)));
            ClockMock.Setup(
                aClock => aClock.Advance()).Callback(
                    () => iClock.Advance());
            ClockMock.SetupGet(
                aClock => aClock.Now).Returns(()=>new Timestamp(iClock.Now.ToString()));
        }
    }
}