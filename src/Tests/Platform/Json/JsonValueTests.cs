using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenHome.XappForms.Json;


namespace UnitTests
{
    public class JsonValueTests
    {
        [TestCase(@"""""", "")]
        [TestCase(@"""a""", "a")]
        [TestCase(@"""a""        ", "a")]
        [TestCase(@"""ABC XYZ abc xyz 012 789""", "ABC XYZ abc xyz 012 789")]
        [TestCase(@"""\""""", "\"")]
        [TestCase(@"""\r""", "\r")]
        [TestCase(@"""\b""", "\b")]
        [TestCase(@"""\t""", "\t")]
        [TestCase(@"""\n""", "\n")]
        [TestCase(@"""\f""", "\f")]
        [TestCase(@"""\\""", "\\")]
        [TestCase(@"""\/""", "/")]
        // See https://bugzilla.xamarin.com/show_bug.cgi?id=5732
        // This Mono bug prevents the following test from working. The code
        // under test is fine, the test just does the wrong thing under Mono.
        //[TestCase(@"""\u0000""", "\x0000")]
        [TestCase(@"""\u2593""", "\x2593")]
        [TestCase(@"""\u00ae""", "\x00ae")]
        [TestCase(@"""\u00AE""", "\x00ae")]
        public void TestParseString(string aJsonLiteral, string aExpectedValue)
        {
            string actualValue = JsonValue.FromString(aJsonLiteral).AsString();
            Assert.That(actualValue, Is.EqualTo(aExpectedValue));
        }

        [TestCase(@"""""")]
        [TestCase(@"""a""")]
        [TestCase(@"""ABC XYZ abc xyz 012 789""")]
        [TestCase(@"""\""""")]
        [TestCase(@"""\r""")]
        [TestCase(@"""\b""")]
        [TestCase(@"""\t""")]
        [TestCase(@"""\n""")]
        [TestCase(@"""\f""")]
        [TestCase(@"""\\""")]
        [TestCase(@"""/""")]
        [TestCase(@"""\u0000""")] // 00 - 1f are control characters and must be escaped
        [TestCase(@"""\u001f""")]
        [TestCase(@"""\u007f""")] // 7f - 9f are control characters, but 7f is backspace
        [TestCase(@"""\u0080""")]
        [TestCase(@"""\u009f""")]
        [TestCase(@"""\u0100""")] // 100+ must be
        [TestCase(@"""\u2593""")]
        [TestCase(@"1234")]
        [TestCase(@"[]")]
        [TestCase(@"{}")]
        [TestCase(@"[1,2,3]")]
        [TestCase(@"{""A"":1,""B"":2,""C"":3}")]
        public void TestToString(string aRoundTripJsonLiteral)
        {
            string actualValue = JsonValue.FromString(aRoundTripJsonLiteral).ToString();
            Assert.That(actualValue, Is.EqualTo(aRoundTripJsonLiteral));
        }

        [TestCase(@"""")]
        [TestCase(@"""a")]
        [TestCase(@"""a""b")]
        [TestCase(@"""\""")]
        [TestCase(@"""\R""")]
        [TestCase(@"""\B""")]
        [TestCase(@"""\T""")]
        [TestCase(@"""\N""")]
        [TestCase(@"""\F""")]
        [TestCase(@"""\u000""")]
        [TestCase(@"""\u000 """)]
        [TestCase(@"""\u""")]
        [TestCase(@"""\u 0000""")]
        [TestCase(@"""\U0000""")]
        [TestCase(@"""\U2593""")]
        [TestCase(@"""\U00ae""")]
        [TestCase(@"""\U00AE""")]
        public void TestInvalidString(string aJsonLiteral)
        {
            Assert.Throws<ArgumentException>(() => JsonValue.FromString(aJsonLiteral));
        }


        [TestCase("1", 1)]
        [TestCase("0", 0)]
        [TestCase("999999", 999999)]
        [TestCase("-1", -1)]
        [TestCase("-777555", -777555)]
        [TestCase("600500400300200100", 600500400300200100L)]
        public void TestLong(string aJsonLiteral, long aExpectedValue)
        {
            long actualValue = JsonValue.FromString(aJsonLiteral).AsLong();
            Assert.That(actualValue, Is.EqualTo(aExpectedValue));
        }

        [TestCase("1.0", 1.0)]
        [TestCase("1", 1.0)]
        [TestCase("1.0e5", 1.0e5)]
        [TestCase("1.0E5", 1.0e5)]
        [TestCase("1.e5", 1.0e5)]
        [TestCase("1e5", 1.0e5)]
        [TestCase("-1e5", -1.0e5)]
        [TestCase("1.00035e-20", 1.00035e-20)]
        [TestCase("1.00035e+20", 1.00035e20)]
        [TestCase("998877.66", 998877.66)]
        public void TestDouble(string aJsonLiteral, double aExpectedValue)
        {
            double actualValue = JsonValue.FromString(aJsonLiteral).AsDouble();
            Assert.That(actualValue, Is.EqualTo(aExpectedValue));
        }

        [TestCase("001")]
        [TestCase("01")]
        [TestCase("01.1")]
        [TestCase("-01")]
        [TestCase("1L")]
        [TestCase("1.1.1")]
        public void TestInvalidNumber(string aJsonLiteral)
        {
            Assert.Throws<ArgumentException>(() => JsonValue.FromString(aJsonLiteral));
        }

        [TestCase("true", true)]
        [TestCase("false", false)]
        public void TestBool(string aJsonLiteral, bool aExpectedValue)
        {
            bool actualValue = JsonValue.FromString(aJsonLiteral).AsBool();
            Assert.That(actualValue, Is.EqualTo(aExpectedValue));
        }

        [Test]
        public void TestNull()
        {
            JsonValue jsonNull = JsonValue.FromString("null");
            Assert.That(jsonNull.IsNull, Is.True);
        }


        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(10)]
        [TestCase(100)]
        public void TestArray(int aLength)
        {
            string jsonString = "[" + String.Join(",", Enumerable.Range(0, aLength).Select(aNumber=>aNumber.ToString())) +"]";
            JsonValue jsonValue = JsonValue.FromString(jsonString);
            List<JsonValue> arrayContents = jsonValue.EnumerateArray().ToList();
            List<long> arrayOfLongs = arrayContents.Select(aJsonValue => aJsonValue.AsLong()).ToList();
            Assert.That(arrayOfLongs, Is.EqualTo(Enumerable.Range(0, aLength)));
        }
    }
}
