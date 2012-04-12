using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace OpenHome.Os.Platform
{
    [TestFixture]
    class OptionParserTests
    {
        OptionParser iComplexParser;
        OptionParser iEmptyParser;
        OptionParser.OptionString iOptionAlpha;
        OptionParser.OptionBool iOptionBravo;
        OptionParser.OptionUint iOptionCharlie;
        OptionParser.OptionInt iOptionDelta;

        OptionParser.OptionString iOptionQuebec;
        OptionParser.OptionString iOptionXRay;

        [SetUp]
        public void SetUp()
        {
            iComplexParser = new OptionParser();
            iEmptyParser = new OptionParser();

            iOptionAlpha = new OptionParser.OptionString("-a", "--alpha", "ALPHA_DEFAULT", "ALPHA_DESC", "ALPHA_METAVAR");
            iOptionBravo = new OptionParser.OptionBool("-b", "--bravo", "BRAVO_DESC");
            iOptionCharlie = new OptionParser.OptionUint("-c", "--charlie", 12345, "CHARLIE_DESC", "CHARLIE_METAVAR");
            iOptionDelta = new OptionParser.OptionInt("-d", "--delta", -54321, "DELTA_DESC", "DELTA_METAVAR");

            iOptionQuebec = new OptionParser.OptionString(null, "--quebec", "QUEBEC_DEFAULT", "QUEBEC_DESC", "QUEBEC_METAVAR");
            iOptionXRay = new OptionParser.OptionString("-x", null, "XRAY_DEFAULT", "XRAY_DESC", "XRAY_METAVAR");

            iComplexParser.AddOption(iOptionAlpha);
            iComplexParser.AddOption(iOptionBravo);
            iComplexParser.AddOption(iOptionCharlie);
            iComplexParser.AddOption(iOptionDelta);
            iComplexParser.AddOption(iOptionQuebec);
            iComplexParser.AddOption(iOptionXRay);
        }

        [Test]
        public void TestPosArgsInComplexParser()
        {
            iComplexParser.Parse(new[]{"foo", "bar", "baz"});
            Assert.That(iComplexParser.PosArgs, Is.EqualTo(new[]{"foo", "bar", "baz"}));
        }

        [Test]
        public void TestPosArgsInEmptyParser()
        {
            iEmptyParser.Parse(new[]{"foo", "bar", "baz"});
            Assert.That(iEmptyParser.PosArgs, Is.EqualTo(new[]{"foo", "bar", "baz"}));
        }

        [TestCase(new[]{"-a", "testvalue"}, "testvalue")]
        [TestCase(new[]{"--alpha", "testvalue"}, "testvalue")]
        [TestCase(new[]{"-a:testvalue"}, "testvalue")]
        [TestCase(new[]{"--alpha:testvalue"}, "testvalue")]
        [TestCase(new[]{"-a=testvalue"}, "testvalue")]
        [TestCase(new[]{"--alpha=testvalue"}, "testvalue")]
        [TestCase(new[]{"-a", ""}, "")]
        [TestCase(new[]{"--alpha", ""}, "")]
        [TestCase(new[]{"-a:"}, "")]
        [TestCase(new[]{"--alpha:"}, "")]
        [TestCase(new[]{"-a="}, "")]
        [TestCase(new[]{"--alpha="}, "")]
        public void TestSpaceSeparatedStringOption(string[] aArgs, string aExpectedValue)
        {
            iComplexParser.Parse(aArgs);
            Assert.That(iOptionAlpha.Value, Is.EqualTo(aExpectedValue));
        }

        [Test]
        public void TestDefaultStringValue()
        {
            iComplexParser.Parse(new string[] { });
            Assert.That(iOptionAlpha.Value, Is.EqualTo("ALPHA_DEFAULT"));
        }

        [Test]
        public void TestDefaultBoolValue()
        {
            iComplexParser.Parse(new string[] { });
            Assert.That(iOptionBravo.Value, Is.False);
        }

        [Test]
        public void TestDefaultUintValue()
        {
            iComplexParser.Parse(new string[] { });
            Assert.That(iOptionCharlie.Value, Is.EqualTo(12345));
        }

        [Test]
        public void TestDefaultIntValue()
        {
            iComplexParser.Parse(new string[] { });
            Assert.That(iOptionDelta.Value, Is.EqualTo(-54321));
        }

        [TestCase("-b")]
        [TestCase("--bravo")]
        public void TestSettingBoolValue(string aOptionString)
        {
            iComplexParser.Parse(new[] { aOptionString });
            Assert.That(iOptionBravo.Value, Is.True);
        }

        [TestCase("-c", "7777", (uint)7777)]
        [TestCase("--charlie", "7777", (uint)7777)]
        [TestCase("-c", "0", (uint)0)]
        [TestCase("-c", "4294967295", 4294967295)]
        public void TestSettingUintValue(string aOptionString, string aValueString, uint aValue)
        {
            iComplexParser.Parse(new[] { aOptionString, aValueString });
            Assert.That(iOptionCharlie.Value, Is.EqualTo(aValue));
        }

        [TestCase("-d", "7777", 7777)]
        [TestCase("--delta", "7777", 7777)]
        [TestCase("-d", "0", 0)]
        [TestCase("-d", "-7777", -7777)]
        [TestCase("-d", "2147483647", 2147483647)]
        [TestCase("-d", "-2147483648", -2147483648)]
        public void TestSettingIntValue(string aOptionString, string aValueString, int aValue)
        {
            iComplexParser.Parse(new[] { aOptionString, aValueString });
            Assert.That(iOptionDelta.Value, Is.EqualTo(aValue));
        }

        [TestCase(new[]{"-a:foo", "pos1", "--charlie", "9999", "pos2", "pos3", "-x", "xray"}, new[]{"pos1","pos2","pos3"})]
        [TestCase(new[]{"-b", "pos1", "pos2"}, new[]{"pos1","pos2"})]
        [TestCase(new[]{"-a:foo", "-x=xray"}, new string[]{})]
        public void TestPosArgsWithMultipleOptions(string[] aArgs, string[] aPosArgs)
        {
            iComplexParser.Parse(aArgs);
            Assert.That(iComplexParser.PosArgs, Is.EqualTo(aPosArgs));
        }

        [TestCase(new[]{"-y"}, "No such option",              TestName = "TestInvalidArguments(\"-y\")")]
        [TestCase(new[]{"--unknown"}, "No such option",       TestName = "TestInvalidArguments(\"--unknown\")")]
        [TestCase(new[]{"-a"}, "incorrect arguments",         TestName = "TestInvalidArguments(\"-a\")")]
        [TestCase(new[]{"--alpha"}, "incorrect arguments",    TestName = "TestInvalidArguments(\"--alpha\")")]
        [TestCase(new[]{"-c", "not a number"}, "non-integer", TestName = "TestInvalidArguments(\"-c \"not a number\"\")")]
        [TestCase(new[]{"-c", "-7777"}, "out of range",       TestName = "TestInvalidArguments(\"-c -7777\")")]
        [TestCase(new[]{"-c", "5000000000"}, "out of range",  TestName = "TestInvalidArguments(\"-c 5000000000\")")]
        [TestCase(new[]{"-c"}, "incorrect arguments",         TestName = "TestInvalidArguments(\"-c\")")]
        [TestCase(new[]{"--charlie"}, "incorrect arguments",  TestName = "TestInvalidArguments(\"--charlie\")")]
        [TestCase(new[]{"-d", "not a number"}, "non-integer", TestName = "TestInvalidArguments(\"-d \"not a number\"\")")]
        [TestCase(new[]{"-d", "3000000000"}, "out of range",  TestName = "TestInvalidArguments(\"-d 3000000000\")")]
        [TestCase(new[]{"-d", "-3000000000"}, "out of range", TestName = "TestInvalidArguments(\"-d -3000000000\")")]
        [TestCase(new[]{"-d"}, "incorrect arguments",         TestName = "TestInvalidArguments(\"-d\")")]
        [TestCase(new[]{"--delta"}, "incorrect arguments",    TestName = "TestInvalidArguments(\"--delta\")")]
        public void TestInvalidArguments(string[] aArgs, string aMessageSubstring)
        {
            Assert.That(
                ()=>iComplexParser.Parse(aArgs),
                Throws
                    .TypeOf<OptionParser.OptionParserError>()
                    .With.Property("Message").StringContaining(aMessageSubstring));
        }
    }
}
