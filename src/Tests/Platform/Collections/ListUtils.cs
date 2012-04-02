using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OpenHome.Os.Platform.Collections
{
    public class ListUtilsTests
    {
        private static List<T> Seq<T>(params T[] aItems)
        {
            return aItems.ToList();
        }

        public IEnumerable<List<string>> TestInsertIntoEmptyListTestCases()
        {
            yield return Seq<string>();
            yield return Seq("a");
            yield return Seq("Z", "Y", "X", "W");
        }

        [Test]
        [TestCaseSource("TestInsertIntoEmptyListTestCases")]
        public void TestInsertBeforeEmptyList(List<string> aValues)
        {
            List<string> list = new List<string>();
            ListUtils.MultiInsert(list, Enumerable.Repeat(0, aValues.Count).ToList(), aValues);
            Assert.That(list, Is.EqualTo(aValues));
        }

        [Test]
        public void TestInsertAtStartOfList()
        {
            List<string> list = Seq("a", "b");
            ListUtils.MultiInsert(
                list,
                Seq(0),
                Seq("X"));
            Assert.That(list, Is.EqualTo(
                Seq("X", "a", "b")));
        }

        [Test]
        public void TestInsertAtEndOfList()
        {
            List<string> list = Seq("a", "b");
            ListUtils.MultiInsert(
                list,
                Seq(2),
                Seq("X"));
            Assert.That(list, Is.EqualTo(
                Seq("a", "b", "X")));
        }

        [Test]
        public void TestInterleaveThroughList()
        {
            List<string> list = Seq("a", "b");
            ListUtils.MultiInsert(
                list,
                Seq(0, 1, 1, 2),
                Seq("W", "X", "Y", "Z"));
            Assert.That(list, Is.EqualTo(
                Seq("W", "a", "X", "Y", "b", "Z")));
        }


        public IEnumerable<List<int>> TestBadIndexesTestCases()
        {
            yield return Seq(-1, 0);
            yield return Seq(2, 3);
            yield return Seq(2, 1);
        }

        [Test]
        [TestCaseSource("TestBadIndexesTestCases")]
        public void TestBadIndexes(List<int> aIndexes)
        {
            List<string> list = Seq("a", "b");
            Assert.Throws<ArgumentException>(
                () =>
                ListUtils.MultiInsert(
                    list,
                    aIndexes,
                    Seq("X", "Y")));
        }

        public IEnumerable<List<int>> TestWrongNumberOfIndexes()
        {
            yield return Seq<int>();
            yield return Seq(0);
            yield return Seq(0,0,0);
        }


        [Test]
        [TestCaseSource("TestWrongNumberOfIndexes")]
        public void TestWrongNumberOfIndexes(List<int> aIndexes)
        {
            List<string> list = Seq("a", "b");
            Assert.Throws<ArgumentException>(
                () =>
                ListUtils.MultiInsert(
                    list,
                    aIndexes,
                    Seq("X", "Y")));
        }
    }
}
