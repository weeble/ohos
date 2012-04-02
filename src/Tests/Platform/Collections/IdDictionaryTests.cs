using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OpenHome.Os.Platform.Collections
{
    public class WhenAnIdDictionaryIsEmptyContext
    {
        protected IdDictionary<string, string> iIdDictionary;
        [SetUp]
        public void CreateEmptyIdDictionary()
        {
            iIdDictionary = new IdDictionary<string, string>();
        }
    }

    public class WhenAnIdDictionaryHasThreeElementsContext : WhenAnIdDictionaryIsEmptyContext
    {
        protected List<uint> iReturnedIds;

        [SetUp]
        public void AddSomeEntries()
        {
            iReturnedIds = new List<uint>();
            uint id;
            iIdDictionary.TryAdd("1", "ONE", out id);
            iReturnedIds.Add(id);
            iIdDictionary.TryAdd("2", "TWO", out id);
            iReturnedIds.Add(id);
            iIdDictionary.TryAdd("3", "THREE", out id);
            iReturnedIds.Add(id);
        }
    }


    [TestFixture]
    public class WhenAnIdDictionaryIsEmpty : WhenAnIdDictionaryIsEmptyContext
    {
        public static TestCaseData[] KeysThatAreNotInTheDictionary =
        {
            new TestCaseData("1"),
            new TestCaseData("2"),
            new TestCaseData(""),
            new TestCaseData("0"),
            new TestCaseData("ONE"),
        };
        public static TestCaseData[] IdsThatAreNotInTheDictionary =
        {
            new TestCaseData((uint)0), 
            new TestCaseData((uint)1), 
            new TestCaseData(uint.MaxValue), 
        };

        [Test]
        public void CountShouldBeZero()
        {
            Assert.That(iIdDictionary.Count, Is.EqualTo(0));
        }
        [Test]
        public void AddingAnItemShouldIncreaseTheCount()
        {
            uint id;
            iIdDictionary.TryAdd("1", "ONE", out id);
            Assert.That(iIdDictionary.Count, Is.EqualTo(1));
        }
        [Test]
        public void AddingAnItemShouldReturnTrue()
        {
            uint id;
            bool success = iIdDictionary.TryAdd("1", "ONE", out id);
            Assert.That(success, Is.True);
        }
        [TestCaseSource("KeysThatAreNotInTheDictionary")]
        public void TryGetValueByKeyShouldAlwaysReturnFalse(string key)
        {
            string value;
            bool success = iIdDictionary.TryGetValueByKey(key, out value);
            Assert.That(success, Is.False);
        }
        [TestCaseSource("IdsThatAreNotInTheDictionary")]
        public void TryGetValueByIdShouldAlwaysReturnFalse(uint id)
        {
            string value;
            bool success = iIdDictionary.TryGetValueById(id, out value);
            Assert.That(success, Is.False);
        }
        [TestCaseSource("KeysThatAreNotInTheDictionary")]
        public void ContainsKeyShouldAlwaysReturnFalse(string key)
        {
            Assert.That(iIdDictionary.ContainsKey(key), Is.False);
        }
        public void ItemsByKeyShouldBeEmpty()
        {
            Assert.That(iIdDictionary.ItemsByKey, Is.Empty);
        }
        public void ItemsByIdShouldBeEmpty()
        {
            Assert.That(iIdDictionary.ItemsById, Is.Empty);
        }
    }

    [TestFixture]
    public class WhenAnIdDictionaryHasThreeElements : WhenAnIdDictionaryHasThreeElementsContext
    {
        [Test]
        public void CountShouldBeThree()
        {
            Assert.That(iIdDictionary.Count, Is.EqualTo(3));
        }
        [Test]
        public void AddingANewItemShouldIncreaseTheCount()
        {
            uint id;
            iIdDictionary.TryAdd("4", "FOUR", out id);
            Assert.That(iIdDictionary.Count, Is.EqualTo(4));
        }
        [Test]
        public void AddingANewItemShouldReturnTrue()
        {
            uint id;
            bool success = iIdDictionary.TryAdd("4", "FOUR", out id);
            Assert.That(success, Is.True);
        }
        [TestCase("1", "ONE")]
        [TestCase("2", "TWO")]
        [TestCase("3", "THREE")]
        public void FetchingAnExistingItemShouldReturnTrueWithTheCorrectValue(string key, string expectedValue)
        {
            string value;
            bool success = iIdDictionary.TryGetValueByKey(key, out value);
            Assert.That(success, Is.True);
            Assert.That(value, Is.EqualTo(expectedValue));
        }
        [TestCase(0, "ONE")]
        [TestCase(1, "TWO")]
        [TestCase(2, "THREE")]
        public void FetchingAnExistingItemByIdShouldReturnTrueWithTheCorrectValue(int index, string expectedValue)
        {
            string value;
            bool success = iIdDictionary.TryGetValueById(iReturnedIds[index], out value);
            Assert.That(success, Is.True);
            Assert.That(value, Is.EqualTo(expectedValue));
        }
        [TestCase("1")]
        [TestCase("2")]
        [TestCase("3")]
        public void AddingAnExistingItemShouldNotIncreaseTheCount(string key)
        {
            uint id;
            iIdDictionary.TryAdd(key, "IRRELEVANT", out id);
            Assert.That(iIdDictionary.Count, Is.EqualTo(3));
        }
        [TestCase("1")]
        [TestCase("2")]
        [TestCase("3")]
        public void AddingAnExistingItemShouldReturnFalse(string key)
        {
            uint id;
            bool success = iIdDictionary.TryAdd(key, "IRRELEVANT", out id);
            Assert.That(success, Is.EqualTo(false));
        }
        [Test]
        public void ItemsByKeyShouldContainTheCorrectElements()
        {
            Assert.That(
                iIdDictionary.ItemsByKey,
                Is.EquivalentTo(
                    new[] {
                        new KeyValuePair<string, string>("1", "ONE"),
                        new KeyValuePair<string, string>("2", "TWO"),
                        new KeyValuePair<string, string>("3", "THREE"),
                    }));
        }
        [Test]
        public void ItemsByIdShouldContainTheCorrectElements()
        {
            Assert.That(
                iIdDictionary.ItemsById.ToList(),
                Is.EquivalentTo(
                    new[] {
                        new KeyValuePair<uint, string>(iReturnedIds[0], "ONE"),
                        new KeyValuePair<uint, string>(iReturnedIds[1], "TWO"),
                        new KeyValuePair<uint, string>(iReturnedIds[2], "THREE"),
                    }));
        }
    }

    [TestFixture]
    public class WhenSomeElementsHaveBeenDeletedFromAnIdDictionaryContext : WhenAnIdDictionaryHasThreeElementsContext
    {
        [SetUp]
        public void DeleteSomeEntries()
        {
            iIdDictionary.TryRemoveByKey("2");
            iIdDictionary.TryRemoveByKey("1");
        }
    }

    [TestFixture]
    public class WhenSomeElementsHaveBeenDeletedFromAnIdDictionary : WhenSomeElementsHaveBeenDeletedFromAnIdDictionaryContext
    {
        [Test]
        public void CountShouldBeOne()
        {
            Assert.That(iIdDictionary.Count, Is.EqualTo(1));
        }

        [TestCase("1", Result=false)]
        [TestCase("2", Result=false)]
        public bool GettingTheDeletedElementsByKeyShouldReturnFalse(string key)
        {
            string value;
            return iIdDictionary.TryGetValueByKey(key, out value);
        }

        [TestCase("3", "THREE")]
        public void GettingTheRemainingElementsByKeyShouldReturnTrueWithTheCorrectValue(string key, string expectedValue)
        {
            string value;
            bool success = iIdDictionary.TryGetValueByKey(key, out value);
            Assert.That(success, Is.True);
            Assert.That(value, Is.EqualTo(expectedValue));
        }

        [TestCase(0, Result=false)]
        [TestCase(1, Result=false)]
        public bool GettingTheDeletedElementsByIdShouldReturnFalse(int index)
        {
            string value;
            return iIdDictionary.TryGetValueById(iReturnedIds[index], out value);
        }

        [TestCase(2, "THREE")]
        public void GettingTheRemainingElementsByIdShouldReturnTrueWithTheCorrectValue(int index, string expectedValue)
        {
            string value;
            bool success = iIdDictionary.TryGetValueById(iReturnedIds[index], out value);
            Assert.That(success, Is.True);
            Assert.That(value, Is.EqualTo(expectedValue));
        }

        [TestCase("1", Result=true)]
        [TestCase("2", Result=true)]
        public bool AddingBackTheDeletedKeysShouldReturnTrue(string key)
        {
            uint id;
            return iIdDictionary.TryAdd(key, "IRRELEVANT", out id);
        }

        [TestCase("1", -1, ExpectedException = typeof(KeyNotFoundException))]
        [TestCase("2", -1, ExpectedException = typeof(KeyNotFoundException))]
        [TestCase("3", 2)]
        [TestCase("6", -1, ExpectedException = typeof(KeyNotFoundException))]
        public void GetIdForKeyShouldReturnOnlyForTheRemainingItems(string key, int index)
        {
            uint id = iIdDictionary.GetIdForKey(key);
            Assert.That(id, Is.EqualTo(iReturnedIds[index]));
        }

        [TestCase(0), TestCase(1)]
        public void GetKeyForIdShouldThrowForIdsRemovedFromTheDictionary(int index)
        {
            Assert.Throws(
                typeof (KeyNotFoundException),
                () => iIdDictionary.GetKeyForId(iReturnedIds[index]));
        }
        
        [TestCase(2, Result="3")]
        public string GetKeyForIdShouldReturnForIdsRemainingInTheDictionary(int index)
        {
            return iIdDictionary.GetKeyForId(iReturnedIds[index]);
        }
    }
}
