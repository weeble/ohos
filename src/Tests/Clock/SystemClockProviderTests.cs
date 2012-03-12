using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using OpenHome.Net.Device;
using OpenHome.Os.Platform;
using OpenHome.Os.Platform.Clock;

namespace OpenHome.Os.Clock
{
    public class SystemClockProviderContext
    {
        protected Mock<ISystemClock> MockSystemClock { get; set; }
        protected SystemClockProvider ClockProvider { get; set; }

        protected readonly static Dictionary<string, TimeZoneInfo> CustomTimeZones =
            new Dictionary<string, TimeZoneInfo>{
                {"Zone1",
                    // Define CustomZone1 so that daylight time is currently in effect.
                    TimeZoneInfo.CreateCustomTimeZone(
                        "Zone1Id",
                        new TimeSpan(3, 0, 0),
                        "Zone1Name",
                        "Zone1StandardTime",
                        "Zone1DaylightTime",
                        new[] {
                            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                                DateTime.MinValue.Date,
                                DateTime.MaxValue.Date,
                                TimeSpan.FromHours(+1),
                                // Starts at 2AM on the 15th of April:
                                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                                    new DateTime(1,1,1,2,0,0),
                                    4,
                                    15),
                                // Ends at 2AM on the 15th of September:
                                TimeZoneInfo.TransitionTime.CreateFixedDateRule(
                                    new DateTime(1,1,1,2,0,0),
                                    9,
                                    15))},
                        false) },
                {"Zone2",
                    TimeZoneInfo.CreateCustomTimeZone(
                        "Zone2Id",
                        new TimeSpan(-5, -30, 0),
                        "Zone2Name",
                        "Zone2StandardTime",
                        "Zone2DaylightTime",
                        new TimeZoneInfo.AdjustmentRule[] { },
                        false)}};

        [SetUp]
        public void SetUp()
        {
            MockSystemClock = new Mock<ISystemClock>();
            ClockProvider = new SystemClockProvider(MockSystemClock.Object);
        }
    }
    public class WhenInvokingGetAvailableTimezonesAndOnlyZone1Exists : SystemClockProviderContext
    {
        string iResult;
        [SetUp]
        public void CallGetAvailableTimezones()
        {
            MockSystemClock.SetupGet(
                aSystemClock => aSystemClock.Now)
                .Returns(new DateTime(2010, 6, 15));
            MockSystemClock.SetupGet(
                aSystemClock => aSystemClock.AvailableTimeZones)
                .Returns(
                    new[] { CustomTimeZones["Zone1"] });
            ClockProvider.GetAvailableTimeZones(null, out iResult);
        }
        [Test]
        public void AListShouldBeReturned()
        {
            Assert.That(iResult, Is.StringMatching("(?s)<timezoneList>.*</timezoneList>"));
        }
        [Test]
        public void ATimeZoneShouldBeReturned()
        {
            Assert.That(iResult, Is.StringMatching("(?s)<timezone>.*</timezone>"));
        }
        [Test]
        public void TheNameShouldBeZone1Name()
        {
            Assert.That(iResult, Is.StringMatching("<name>Zone1Name</name>"));
        }
        [Test]
        public void TheDaylightNameShouldBeZone1DaylightTime()
        {
            Assert.That(iResult, Is.StringMatching("<daylightName>Zone1DaylightTime</daylightName>"));
        }
        [Test]
        public void TheStandardNameShouldBeZone1StandardTime()
        {
            Assert.That(iResult, Is.StringMatching("<standardName>Zone1StandardTime</standardName>"));
        }
        [Test]
        public void DaylightShouldBeEnabled()
        {
            Assert.That(iResult, Is.StringMatching("<hasDaylight>yes</hasDaylight>"));
        }
        [Test]
        public void DaylightShouldBeActive()
        {
            Assert.That(iResult, Is.StringMatching("<isDaylight>yes</isDaylight>"));
        }
        [Test]
        public void CurrentOffsetShouldBePlus4Hours()
        {
            Assert.That(iResult, Is.StringMatching(@"<currentOffset>\+4:00</currentOffset>"));
        }
    }

    class WhenInvokingGetTime : SystemClockProviderContext
    {
        string iUtcTime;
        string iLocalOffset;
        string iTimeZoneName;

        private void PrepareMocks(DateTime aCurrentTime, TimeZoneInfo aCurrentTimeZone)
        {
            MockSystemClock.SetupGet(
                aClock => aClock.Now)
                .Returns(aCurrentTime);
            MockSystemClock.SetupGet(
                aClock => aClock.TimeZone)
                .Returns(aCurrentTimeZone);
        }

        [Test]
        [TestCaseSource("GetTestCases1")]
        public void UtcTimeShouldBeTheCurrentTimeRegardlessOfTimeZone(string aTimeZone, DateTime aCurrentTime, string aExpectedTime)
        {
            PrepareMocks(aCurrentTime, CustomTimeZones[aTimeZone]);
            ClockProvider.GetTime(null, out iUtcTime, out iLocalOffset, out iTimeZoneName);
            Assert.That(iUtcTime, Is.EqualTo(aExpectedTime));
        }
        public IEnumerable<TestCaseData> GetTestCases1()
        {
            yield return new TestCaseData("Zone1", new DateTime(2010, 11, 17, 9, 29, 50, DateTimeKind.Utc), "2010-11-17T09:29:50");
            yield return new TestCaseData("Zone2", new DateTime(2010, 11, 17, 9, 29, 50, DateTimeKind.Utc), "2010-11-17T09:29:50");
            yield return new TestCaseData("Zone1", new DateTime(2011, 1, 1, 21, 29, 1, DateTimeKind.Utc), "2011-01-01T21:29:01");
            yield return new TestCaseData("Zone2", new DateTime(2011, 1, 1, 21, 29, 1, DateTimeKind.Utc), "2011-01-01T21:29:01");
        }

        [Test]
        [TestCaseSource("GetTestCases2")]
        public void LocalOffsetShouldBeCorrectForTheCurrentTimeAndZone(string aTimeZone, DateTime aCurrentTime, string aExpectedOffset)
        {
            PrepareMocks(aCurrentTime, CustomTimeZones[aTimeZone]);
            ClockProvider.GetTime(null, out iUtcTime, out iLocalOffset, out iTimeZoneName);
            Assert.That(iLocalOffset, Is.EqualTo(aExpectedOffset));
        }
        public IEnumerable<TestCaseData> GetTestCases2()
        {
            yield return new TestCaseData("Zone1", new DateTime(2010, 11, 17, 9, 29, 50, DateTimeKind.Utc), "+3:00");
            yield return new TestCaseData("Zone2", new DateTime(2010, 11, 17, 9, 29, 50, DateTimeKind.Utc), "-5:30");
            yield return new TestCaseData("Zone1", new DateTime(2011, 6, 1, 21, 29, 1, DateTimeKind.Utc), "+4:00");
            yield return new TestCaseData("Zone2", new DateTime(2011, 6, 1, 21, 29, 1, DateTimeKind.Utc), "-5:30");
        }

        [Test]
        [TestCaseSource("GetTestCases3")]
        public void TimeZoneNameShouldBeCorrectRegardlessOfCurrentDate(string aTimeZone, DateTime aCurrentTime, string aExpectedTimeZoneName)
        {
            PrepareMocks(aCurrentTime, CustomTimeZones[aTimeZone]);
            ClockProvider.GetTime(null, out iUtcTime, out iLocalOffset, out iTimeZoneName);
            Assert.That(iTimeZoneName, Is.EqualTo(aExpectedTimeZoneName));
        }
        public IEnumerable<TestCaseData> GetTestCases3()
        {
            yield return new TestCaseData("Zone1", new DateTime(2010, 11, 17, 9, 29, 50, DateTimeKind.Utc), "Zone1Name");
            yield return new TestCaseData("Zone2", new DateTime(2010, 11, 17, 9, 29, 50, DateTimeKind.Utc), "Zone2Name");
            yield return new TestCaseData("Zone1", new DateTime(2011, 6, 1, 21, 29, 1, DateTimeKind.Utc), "Zone1Name");
            yield return new TestCaseData("Zone2", new DateTime(2011, 6, 1, 21, 29, 1, DateTimeKind.Utc), "Zone2Name");
        }
    }

    class SetTimeZoneTests : SystemClockProviderContext
    {
        [Test]
        public void SetTimeZoneShouldThrowActionErrorIfTheTimeZoneDoesNotExists()
        {
            Assert.Throws<ActionError>(() => ClockProvider.SetTimeZone(null, "foobar"));
        }
        [TestCase("Zone1Name", "Zone1")]
        [TestCase("Zone2Name", "Zone2")]
        public void SetTimeZoneShouldUpdateSystemTimeZone(string aZoneName, string aZoneKey)
        {
            MockSystemClock.SetupGet(
                aSystemClock => aSystemClock.AvailableTimeZones)
                .Returns(
                    new[] { CustomTimeZones["Zone1"], CustomTimeZones["Zone2"] });
            ClockProvider.SetTimeZone(null, aZoneName);
            MockSystemClock.VerifySet(
                aClock => aClock.TimeZone = CustomTimeZones[aZoneKey]);
        }
    }

    class SetUtcTimeTests : SystemClockProviderContext
    {
        [Test]
        [TestCase("oisudfngoen")]
        [TestCase("2000-11-11 01:01:01")]
        [TestCase("2000-11-11T01:01.01")]
        [TestCase("99-11-11T01:01:01")]
        [TestCase("2000/11/11T01:01:01")]
        [TestCase("11/32/2000T01:01:01")]
        [TestCase("2000-11-11")]
        [TestCase("01:01:01")]
        [TestCase("2000-02-30T01:01:01")]
        [TestCase("2000-13-01T01:01:01")]
        [TestCase("2000-00-01T01:01:01")]
        [TestCase("2000-01-01T25:01:01")]
        public void SetUtcTimeShouldThrowActionErrorIfTheFormatIsBad(string aUtcTime)
        {
            Assert.Throws<ActionError>(() => ClockProvider.SetUtcTime(null, aUtcTime));
        }

        static bool CompareTimes(DateTime aFirst, DateTime aSecond)
        {
            int comparison = aFirst.CompareTo(aSecond);
            Console.WriteLine("cmp({0},{1}) -> {2}", aFirst, aSecond, comparison);
            return comparison==0;
        }

        [TestCaseSource("TestCaseData1")]
        public void SetUtcTimeShouldUpdateTheSystemTime(string aUtcTime, DateTime aExpectedTime)
        {
            ClockProvider.SetUtcTime(null, aUtcTime);
            MockSystemClock.VerifySet(
                aClock => aClock.Now = It.Is<DateTime>(aActualTime => CompareTimes(aExpectedTime, aActualTime)));
        }
        public IEnumerable<TestCaseData> TestCaseData1()
        {
            yield return new TestCaseData("2010-01-30T11:59:00", new DateTime(2010, 1, 30, 11, 59, 0, DateTimeKind.Utc));
            yield return new TestCaseData("2017-07-04T23:59:59", new DateTime(2017, 7, 4, 23, 59, 59, DateTimeKind.Utc));
            yield return new TestCaseData("2010-01-30T13:14:15", new DateTime(2010, 1, 30, 13, 14, 15, DateTimeKind.Utc));
            yield return new TestCaseData("2010-01-30T01:02:03", new DateTime(2010, 1, 30, 1, 2, 3, DateTimeKind.Utc));
        }
    }
}
