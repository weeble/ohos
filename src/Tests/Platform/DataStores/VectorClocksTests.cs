using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;

namespace OpenHome.Os.Platform.DataStores
{
    public class VectorClocksTests
    {
        private static VcClock ForkLeft(VcClock aStamp)
        {
            return new VcClock(aStamp.Id + "L", aStamp.CurrentTime);
        }
        private static VcClock ForkRight(VcClock aStamp)
        {
            return new VcClock(aStamp.Id + "R", aStamp.CurrentTime);
        }

        private static void Check(VcClock aStamp, string aToString)
        {
            Assert.That(
                aStamp.ToString(),
                Is.EqualTo(aToString));
        }

        private static VcClock Event(VcClock aClock)
        {
            return new VcClock(aClock.Id, aClock.CurrentTime.Advance(aClock.Id));
        }

        private static VcClock Join(VcClock aLeft, VcClock aRight)
        {
            return new VcClock(aLeft.Id, aLeft.CurrentTime.Update(aRight.CurrentTime));

        }

        public VcClock Seed { get { return new VcClock("SEED", new VcStamp()); } }
        public VcClock SeedForkLeft { get { return ForkLeft(Seed); } }
        public VcClock SeedForkRight { get { return ForkRight(Seed); } }

        public VcClock SeedForkLeftEvent { get { return Event(ForkLeft(Seed)); } }
        public VcClock SeedForkRightEvent { get { return Event(ForkRight(Seed)); } }
        public VcClock SeedForkRightEventEvent { get { return Event(Event(ForkRight(Seed))); } }
        public VcClock SeedForkLeftEventForkRight { get { return ForkRight(Event(ForkLeft(Seed))); } }

        public VcClock FirstJoin
        {
            get
            {
                VcClock joinLeft = ForkRight(Event(ForkLeft(Seed)));
                VcClock joinRight = Event(Event(ForkRight(Seed)));
                return Join(joinLeft, joinRight);
            }
        }
        public VcClock ForkLeftAfterFirstJoin { get { return ForkLeft(FirstJoin); } }
        public VcClock ForkRightAfterFirstJoin { get { return ForkRight(FirstJoin); } }
        public VcClock SeedForkLeftEventForkLeft { get { return ForkLeft(Event(ForkLeft(Seed))); } }
        public VcClock SeedForkLeftEventForkLeftEvent { get { return Event(ForkLeft(Event(ForkLeft(Seed)))); } }
        
        public VcClock SecondJoin
        {
            get
            {
                VcClock joinLeft = Event(ForkLeft(Event(ForkLeft(Seed))));
                VcClock joinRight = ForkLeft(FirstJoin);
                return Join(joinLeft, joinRight);
            }
        }

        public VcClock SecondJoinEvent { get { return Event(SecondJoin); } }
        
        
        /*public VcClock SeedPeek { get { return Seed.Peek(); } }
        public VcClock SeedForkLeftEventPeek { get { return SeedForkLeftEvent.Peek(); } }
        public VcClock FirstJoinPeek { get { return FirstJoin.Peek(); } }
        public VcClock SeedForkLeftEventForkLeftEventPeek { get { return SeedForkLeftEventForkLeftEvent.Peek(); } }
        public VcClock PeekJoin { get { return FirstJoinPeek.Join(SeedForkLeftEventForkLeftEventPeek); } }*/

        public class ToStringTestData
        {
            public Expression<Func<VcClock>> PropertyExpression { get; private set; }
            public string StringRepresentation { get; private set; }

            public ToStringTestData(Expression<Func<VcClock>> aPropertyExpression, string aStringRepresentation)
            {
                PropertyExpression = aPropertyExpression;
                StringRepresentation = aStringRepresentation;
            }
        }

        public IEnumerable<ToStringTestData> ToStringTestCaseData
        {
            get
            {
                yield return new ToStringTestData(() => Seed, @"(""SEED"",{})");
                yield return new ToStringTestData(() => SeedForkLeft , @"(""SEEDL"",{})");
                yield return new ToStringTestData(() => SeedForkRight, @"(""SEEDR"",{})");
                yield return new ToStringTestData(() => SeedForkLeftEvent, @"(""SEEDL"",{""SEEDL"":1})");
                yield return new ToStringTestData(() => SeedForkRightEvent, @"(""SEEDR"",{""SEEDR"":1})");
                yield return new ToStringTestData(() => SeedForkRightEventEvent, @"(""SEEDR"",{""SEEDR"":2})");
                yield return new ToStringTestData(() => SeedForkLeftEventForkRight, @"(""SEEDLR"",{""SEEDL"":1})");
                yield return new ToStringTestData(() => FirstJoin, @"(""SEEDLR"",{""SEEDL"":1,""SEEDR"":2})");
                yield return new ToStringTestData(() => ForkLeftAfterFirstJoin, @"(""SEEDLRL"",{""SEEDL"":1,""SEEDR"":2})");
                yield return new ToStringTestData(() => ForkRightAfterFirstJoin, @"(""SEEDLRR"",{""SEEDL"":1,""SEEDR"":2})");
                yield return new ToStringTestData(() => SeedForkLeftEventForkLeft, @"(""SEEDLL"",{""SEEDL"":1})");
                yield return new ToStringTestData(() => SeedForkLeftEventForkLeftEvent, @"(""SEEDLL"",{""SEEDL"":1,""SEEDLL"":1})");
                yield return new ToStringTestData(() => SecondJoin, @"(""SEEDLL"",{""SEEDL"":1,""SEEDLL"":1,""SEEDR"":2})");
                yield return new ToStringTestData(() => SecondJoinEvent, @"(""SEEDLL"",{""SEEDL"":1,""SEEDLL"":2,""SEEDR"":2})");

                //yield return new ToStringTestData(() => SeedPeek, "(0,0)");
                //yield return new ToStringTestData(() => SeedForkLeftEventPeek, "(0,(0,1,0))");
                //yield return new ToStringTestData(() => FirstJoinPeek, "(0,(1,0,1))");
                //yield return new ToStringTestData(() => SeedForkLeftEventForkLeftEventPeek, "(0,(0,(1,1,0),0))");

                //yield return new ToStringTestData(() => PeekJoin, "(0,(1,(0,1,0),1))");
            }
        }

        public IEnumerable<TestCaseData> ToStringTestCases
        {
            get
            {
                // To make the test output a bit nicer, we dig into the property expression
                // to find out the name of the property, and then embed it into the test name.
                // It's a lot more meaningful than a bunch of tests all named "LambdaExpression..."
                return
                    from item in ToStringTestCaseData
                    select
                        new TestCaseData(
                            item.PropertyExpression.Compile(),
                            item.StringRepresentation
                        ).SetName(
                            String.Format(
                                "ToStringTests({0},\"{1}\")",
                                ((MemberExpression)item.PropertyExpression.Body).Member.Name,
                                item.StringRepresentation));
            }
        }

        public IEnumerable<TestCaseData> FromStringTestCases
        {
            get
            {
                // To make the test output a bit nicer, we dig into the property expression
                // to find out the name of the property, and then embed it into the test name.
                // It's a lot more meaningful than a bunch of tests all named "LambdaExpression..."
                return
                    from item in ToStringTestCaseData
                    select
                        new TestCaseData(
                            item.PropertyExpression.Compile(),
                            item.StringRepresentation
                        ).SetName(
                            String.Format(
                                "FromStringTests({0},\"{1}\")",
                                ((MemberExpression)item.PropertyExpression.Body).Member.Name,
                                item.StringRepresentation));
            }
        }

        private IEnumerable<Expression<Func<VcClock>>> StampProperties
        {
            get
            {
                yield return () => Seed;
                yield return () => SeedForkLeft;
                yield return () => SeedForkRight;
                yield return () => SeedForkLeftEvent;
                yield return () => SeedForkRightEvent;
                yield return () => SeedForkRightEventEvent;
                yield return () => SeedForkLeftEventForkRight;
                yield return () => FirstJoin;
                yield return () => ForkLeftAfterFirstJoin;
                yield return () => ForkRightAfterFirstJoin;
                yield return () => SeedForkLeftEventForkLeft;
                yield return () => SeedForkLeftEventForkLeftEvent;
                yield return () => SecondJoin;
                yield return () => SecondJoinEvent;
            }
        }

        private static readonly string[] ExpectedLeqResults =
            {
                "11111111111111",
                "11111111111111",
                "11111111111111",
                "00010011111111",
                "00001101110011",
                "00000101110011",
                "00010011111111",
                "00000001110011",
                "00000001110011",
                "00000001110011",
                "00010011111111",
                "00000000000111",
                "00000000000011",
                "00000000000001"
            };

        public IEnumerable<TestCaseData> LeqTestCases
        {
            get
            {
                // The following constructs 14*14 test cases for the Leq method, by
                // making every different pair of 2 out of the 14 timestamps. The
                // ExpectedLeqResults strings encode the expected results - each 1
                // or 0 indicates an expected true or false return value from 
                // StampProperties[row]().Leq(StampProperties[column]()).

                // Maintenance note: Enumerable.Zip from .NET 4.0 would greatly
                // simplify the for loops. We could either re-implement it or move
                // to .NET 4.0 and tidy this up.
                IEnumerator<Expression<Func<VcClock>>> outerStampIter;
                IEnumerator<Expression<Func<VcClock>>> innerStampIter;
                IEnumerator<string> outerResultsIter;
                IEnumerator<char> innerResultsIter;
                for (
                    outerStampIter = StampProperties.GetEnumerator(),
                    outerResultsIter = ExpectedLeqResults.AsEnumerable().GetEnumerator();
                    outerStampIter.MoveNext() && outerResultsIter.MoveNext();)
                {
                    for (
                        innerStampIter = StampProperties.GetEnumerator(),
                        innerResultsIter = outerResultsIter.Current.GetEnumerator();
                        innerStampIter.MoveNext() && innerResultsIter.MoveNext(); )
                    {
                        var first = outerStampIter.Current;
                        var second = innerStampIter.Current;
                        bool expected = innerResultsIter.Current == '1';
                        yield return new TestCaseData(
                            first.Compile(),
                            second.Compile(),
                            expected)
                            .SetName(
                                String.Format(
                                    "LeqTest({0}, {1}, {2})",
                                    ((MemberExpression) first.Body).Member.Name,
                                    ((MemberExpression) second.Body).Member.Name,
                                    expected));
                    }
                }
            }
        }

        [Test]
        [TestCaseSource("ToStringTestCases")]
        public void ToStringTests(Func<VcClock> aFunc, string aExpected)
        {
            VcClock stamp = aFunc();
            Check(stamp, aExpected);
        }

        [Test]
        [TestCaseSource("LeqTestCases")]
        public void LeqTests(Func<VcClock> aFirst, Func<VcClock> aSecond, bool aExpected)
        {
            Assert.That(
                aFirst().CurrentTime.Leq(aSecond().CurrentTime),
                Is.EqualTo(aExpected));
        }

        [Test]
        [TestCaseSource("ToStringTestCases")]
        public void FromStringTests(Func<VcClock> aFunc, string aString)
        {
            VcClock expectedStamp = aFunc();
            VcClock fromStringStamp = new VcClock();
            fromStringStamp.LoadState(aString);
            Assert.That(
                fromStringStamp.ToString(),
                Is.EqualTo(expectedStamp.ToString()));
        }
    }
}
