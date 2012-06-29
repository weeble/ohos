using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenHome.XappForms;

namespace UnitTests
{
    class BinaryHeapTests
    {
        static readonly int[] RandomOrder = { 77, 66, 10, 2, 100, 8, 1, 99, 34, 27, 12, 44, 78 };

        public class TestData
        {
            public Func<BinaryHeap<int>> HeapCreator { get; set; }
            public BinaryHeap<int> GetHeap()
            {
                return HeapCreator();
            }
            public int ExpectedCount { get; set; }
            public int ExpectedMin { get; set; }
            public string Description { get; set; }
        }

        public static TestCaseData IncreasingTestCase(int aCount)
        {
            return new TestCaseData(
                new TestData
                {
                    HeapCreator =
                        () =>
                        {
                            BinaryHeap<int> heap = new BinaryHeap<int>(Comparer<int>.Default);
                            for (int i = 0; i != aCount; ++i)
                            {
                                heap.Insert(i);
                            }
                            return heap;
                        },
                    ExpectedCount = aCount,
                    ExpectedMin = 0,
                    Description = "increasing"
                });
        }

        public static TestCaseData DecreasingTestCase(int aCount)
        {
            return new TestCaseData(
                new TestData
                {
                    HeapCreator =
                        () =>
                        {
                            BinaryHeap<int> heap = new BinaryHeap<int>(Comparer<int>.Default);
                            for (int i = 0; i != aCount; ++i)
                            {
                                heap.Insert(aCount - 1 - i);
                            }
                            return heap;
                        },
                    ExpectedCount = aCount,
                    ExpectedMin = 0,
                    Description = "decreasing"
                });
        }

        public static TestCaseData RandomTestCase(int aCount)
        {
            return new TestCaseData(
                new TestData
                {
                    HeapCreator =
                        () =>
                        {
                            BinaryHeap<int> heap = new BinaryHeap<int>(Comparer<int>.Default);
                            for (int i = 0; i != aCount; ++i)
                            {
                                heap.Insert(RandomOrder[i]);
                            }
                            return heap;
                        },
                    ExpectedCount = aCount,
                    ExpectedMin = RandomOrder.Take(aCount).Min(),
                    Description = "random"
                });
        }


        public static IEnumerable<TestCaseData> TestCases
        {
            get
            {
                yield return IncreasingTestCase(1);
                yield return IncreasingTestCase(2);
                yield return IncreasingTestCase(3);
                yield return IncreasingTestCase(10);
                yield return DecreasingTestCase(2);
                yield return DecreasingTestCase(3);
                yield return DecreasingTestCase(10);
                yield return RandomTestCase(1);
                yield return RandomTestCase(2);
                yield return RandomTestCase(3);
                yield return RandomTestCase(10);
            }
        }
        
        [TestCaseSource("TestCases")]
        public void TestCountIsCorrect(TestData aData)
        {
            var heap = aData.GetHeap();
            Assert.That(heap.Count, Is.EqualTo(aData.ExpectedCount));
        }

        [TestCaseSource("TestCases")]
        public void TestPeekReturnsCorrectValue(TestData aData)
        {
            var heap = aData.GetHeap();
            Assert.That(heap.Peek().Value, Is.EqualTo(aData.ExpectedMin));
        }

        [TestCaseSource("TestCases")]
        public void TestPeekDoesntModifyCount(TestData aData)
        {
            var heap = aData.GetHeap();
            heap.Peek();
            Assert.That(heap.Count, Is.EqualTo(aData.ExpectedCount));
        }

        [Test]
        public void TestPeekOnAnEmptyHeapThrowsInvalidException()
        {
            Assert.Throws<InvalidOperationException>(()=>new BinaryHeap<int>(Comparer<int>.Default).Peek());
        }


        [TestCaseSource("TestCases")]
        public void TestPopReturnsCorrectValue(TestData aData)
        {
            var heap = aData.GetHeap();
            if (aData.ExpectedCount == 0)
            {
                Assert.Ignore();
            }
            Assert.That(heap.Pop(), Is.EqualTo(aData.ExpectedMin));
        }

        [TestCaseSource("TestCases")]
        public void TestPopReducesCountByOne(TestData aData)
        {
            var heap = aData.GetHeap();
            if (aData.ExpectedCount == 0)
            {
                Assert.Ignore();
            }
            heap.Pop();
            Assert.That(heap.Count, Is.EqualTo(aData.ExpectedCount-1));
        }

        [Test]
        public void TestPopOnAnEmptyHeapThrowsInvalidException()
        {
            Assert.Throws<InvalidOperationException>(()=>new BinaryHeap<int>(Comparer<int>.Default).Pop());
        }

        [TestCaseSource("TestCases")]
        public void TestInsertIncreasedCountByOne(TestData aData)
        {
            var heap = aData.GetHeap();
            heap.Insert(aData.ExpectedMin - 1);
            Assert.That(heap.Count, Is.EqualTo(aData.ExpectedCount + 1));
        }

        [TestCaseSource("TestCases")]
        public void TestInsertLowerValueAffectedMin(TestData aData)
        {
            var heap = aData.GetHeap();
            heap.Insert(aData.ExpectedMin - 1);
            Assert.That(heap.Peek().Value, Is.EqualTo(aData.ExpectedMin - 1));
        }

        [TestCaseSource("TestCases")]
        public void TestInsertHigherValueAffectedMin(TestData aData)
        {
            var heap = aData.GetHeap();
            heap.Insert(aData.ExpectedMin + 1);
            Assert.That(heap.Peek().Value, Is.EqualTo(aData.ExpectedMin));
        }

        [TestCaseSource("TestCases")]
        public void TestInsertAndRemoveLowerValueRestoredMin(TestData aData)
        {
            var heap = aData.GetHeap();
            heap.Insert(aData.ExpectedMin - 1).Remove();
            Assert.That(heap.Peek().Value, Is.EqualTo(aData.ExpectedMin));
        }

        [TestCaseSource("TestCases")]
        public void TestAdjustingValueDown(TestData aData)
        {
            var heap = aData.GetHeap();
            var node = heap.Insert(aData.ExpectedMin + 30);
            node.Value = aData.ExpectedMin - 1;
            Assert.That(heap.Peek(), Is.EqualTo(node));
        }

        [TestCaseSource("TestCases")]
        public void TestAdjustingValueUp(TestData aData)
        {
            var heap = aData.GetHeap();
            var node = heap.Insert(aData.ExpectedMin - 1);
            node.Value = aData.ExpectedMin + 30;
            Assert.That(heap.Peek().Value, Is.EqualTo(aData.ExpectedMin));
        }

        [Test]
        public void TestRemovingMiddleValuePreservesOrder()
        {
            var heap = new BinaryHeap<int>(Comparer<int>.Default);
            /* var node33 = */ heap.Insert(33);
            /* var node12 = */ heap.Insert(12);
            var node44 =       heap.Insert(44);
            /* var node50 = */ heap.Insert(50);
            /* var node49 = */ heap.Insert(49);
            /* var node17 = */ heap.Insert(17);
            /* var node99 = */ heap.Insert(99);
            /* var node88 = */ heap.Insert(88);
            /* var node75 = */ heap.Insert(75);
            node44.Remove();
            List<int> results = new List<int>();
            while (heap.Count > 0) { results.Add(heap.Pop()); }
            Assert.That(results, Is.EqualTo(new[] { 12, 17, 33, 49, 50, 75, 88, 99 }));
        }
    }
}
