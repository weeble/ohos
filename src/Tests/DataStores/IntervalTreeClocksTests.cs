using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using NUnit.Framework;
using OpenHome.Os.Platform.DataStores;

namespace OpenHome.Os.DataStores
{
    public class IntervalTreeClocksTests
    {
        private static ItcStamp ForkLeft(ItcStamp aStamp)
        {
            ItcStamp left, right;
            aStamp.Fork(out left, out right);
            return left;
        }
        private static ItcStamp ForkRight(ItcStamp aStamp)
        {
            ItcStamp left, right;
            aStamp.Fork(out left, out right);
            return right;
        }


        private static void Check(ItcStamp aStamp, string aToString)
        {
            Assert.That(
                aStamp.ToString(),
                Is.EqualTo(aToString));
        }

        public ItcStamp Seed { get { return ItcStamp.Seed; } }
        public ItcStamp SeedForkLeft { get { return ForkLeft(Seed); } }
        public ItcStamp SeedForkRight { get { return ForkRight(Seed); } }

        public ItcStamp SeedForkLeftEvent { get { return ForkLeft(Seed).Event(); } }
        public ItcStamp SeedForkRightEvent { get { return ForkRight(Seed).Event(); } }
        public ItcStamp SeedForkRightEventEvent { get { return ForkRight(Seed).Event().Event(); } }
        public ItcStamp SeedForkLeftEventForkRight { get { return ForkRight(ForkLeft(Seed).Event()); } }

        public ItcStamp FirstJoin
        {
            get
            {
                ItcStamp joinLeft = ForkRight(ForkLeft(Seed).Event());
                ItcStamp joinRight = ForkRight(Seed).Event().Event();
                return joinLeft.Join(joinRight);
            }
        }
        public ItcStamp ForkLeftAfterFirstJoin { get { return ForkLeft(FirstJoin); } }
        public ItcStamp ForkRightAfterFirstJoin { get { return ForkRight(FirstJoin); } }
        public ItcStamp SeedForkLeftEventForkLeft { get { return ForkLeft(ForkLeft(Seed).Event()); } }
        public ItcStamp SeedForkLeftEventForkLeftEvent { get { return ForkLeft(ForkLeft(Seed).Event()).Event(); } }
        
        public ItcStamp SecondJoin
        {
            get
            {
                ItcStamp joinLeft = ForkLeft(ForkLeft(Seed).Event()).Event();
                ItcStamp joinRight = ForkLeft(FirstJoin);
                return joinLeft.Join(joinRight);
            }
        }

        public ItcStamp SecondJoinEvent { get { return SecondJoin.Event(); } }
        public ItcStamp SeedPeek { get { return Seed.Peek(); } }
        public ItcStamp SeedForkLeftEventPeek { get { return SeedForkLeftEvent.Peek(); } }
        public ItcStamp FirstJoinPeek { get { return FirstJoin.Peek(); } }
        public ItcStamp SeedForkLeftEventForkLeftEventPeek { get { return SeedForkLeftEventForkLeftEvent.Peek(); } }
        public ItcStamp PeekJoin { get { return FirstJoinPeek.Join(SeedForkLeftEventForkLeftEventPeek); } }

        public class ToStringTestData
        {
            public Expression<Func<ItcStamp>> PropertyExpression { get; private set; }
            public string StringRepresentation { get; private set; }

            public ToStringTestData(Expression<Func<ItcStamp>> aPropertyExpression, string aStringRepresentation)
            {
                PropertyExpression = aPropertyExpression;
                StringRepresentation = aStringRepresentation;
            }
        }

        public IEnumerable<ToStringTestData> ToStringTestCaseData
        {
            get
            {
                yield return new ToStringTestData(() => Seed, "(1,0)");
                yield return new ToStringTestData(() => SeedForkLeft , "((1,0),0)");
                yield return new ToStringTestData(() => SeedForkRight, "((0,1),0)");
                yield return new ToStringTestData(() => SeedForkLeftEvent, "((1,0),(0,1,0))");
                yield return new ToStringTestData(() => SeedForkRightEvent, "((0,1),(0,0,1))");
                yield return new ToStringTestData(() => SeedForkRightEventEvent, "((0,1),(0,0,2))");
                yield return new ToStringTestData(() => SeedForkLeftEventForkRight, "(((0,1),0),(0,1,0))");
                yield return new ToStringTestData(() => FirstJoin, "(((0,1),1),(1,0,1))");
                yield return new ToStringTestData(() => ForkLeftAfterFirstJoin, "(((0,1),0),(1,0,1))");
                yield return new ToStringTestData(() => ForkRightAfterFirstJoin, "((0,1),(1,0,1))");
                yield return new ToStringTestData(() => SeedForkLeftEventForkLeft, "(((1,0),0),(0,1,0))");
                yield return new ToStringTestData(() => SeedForkLeftEventForkLeftEvent, "(((1,0),0),(0,(1,1,0),0))");
                yield return new ToStringTestData(() => SecondJoin, "((1,0),(1,(0,1,0),1))");
                yield return new ToStringTestData(() => SecondJoinEvent, "((1,0),2)");

                yield return new ToStringTestData(() => SeedPeek, "(0,0)");
                yield return new ToStringTestData(() => SeedForkLeftEventPeek, "(0,(0,1,0))");
                yield return new ToStringTestData(() => FirstJoinPeek, "(0,(1,0,1))");
                yield return new ToStringTestData(() => SeedForkLeftEventForkLeftEventPeek, "(0,(0,(1,1,0),0))");

                yield return new ToStringTestData(() => PeekJoin, "(0,(1,(0,1,0),1))");
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

        private IEnumerable<Expression<Func<ItcStamp>>> StampProperties
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
                IEnumerator<Expression<Func<ItcStamp>>> outerStampIter;
                IEnumerator<Expression<Func<ItcStamp>>> innerStampIter;
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
        public void ToStringTests(Func<ItcStamp> aFunc, string aExpected)
        {
            ItcStamp stamp = aFunc();
            Check(stamp, aExpected);
        }

        [Test]
        [TestCaseSource("LeqTestCases")]
        public void LeqTests(Func<ItcStamp> aFirst, Func<ItcStamp> aSecond, bool aExpected)
        {
            Assert.That(
                aFirst().Leq(aSecond()),
                Is.EqualTo(aExpected));
        }

        [Test]
        [TestCaseSource("ToStringTestCases")]
        public void FromStringTests(Func<ItcStamp> aFunc, string aString)
        {
            ItcStamp expectedStamp = aFunc();
            ItcStamp fromStringStamp = ItcStamp.FromString(aString);
            Assert.That(
                fromStringStamp.ToString(),
                Is.EqualTo(expectedStamp.ToString()));
        }
    }
}
