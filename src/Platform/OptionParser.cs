using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace OpenHome.Os.Platform
{
    public class OptionParser
    {
        public class OptionParserError : Exception
        {
            public OptionParserError(string aMsg)
                : base(aMsg)
            { }
        }

        public abstract class Option
        {
            protected Option(string aShortName, string aLongName)
            {
                if (aShortName == null && aLongName == null)
                    throw new ArgumentException("Option name cannot be null");
                // short names must be of form "-[letter(s)]" e.g "-d"
                if (aShortName != null)
                {
                    if (!aShortName.StartsWith("-"))
                        throw new ArgumentException("short name " + aShortName + " does not start with \"-\"");
                    iShortName = aShortName;
                }
                if (aLongName != null)
                {
                    // long names must be of form "--[word]" e.g "--word"
                    if (!aLongName.StartsWith("--"))
                        throw new ArgumentException("long name " + aLongName + " does not start with \"--\"");
                    if (aLongName.IndexOfAny(new[] { ':', '=' }) != -1)
                        throw new ArgumentException("long name " + aLongName + " contains an illegal character");
                    iLongName = aLongName;
                }
            }
            public bool Match(string aName)
            {
                return (aName == iShortName || aName == iLongName);
            }
            public abstract void Process(string[] aOptArgs);
            public abstract void Reset();
            public abstract int ExpectedArgCount();
            public abstract void AppendHelp(OptionHelp aHelp);

            public string ShortName
            {
                get { return iShortName; }
            }
            public string LongName
            {
                get { return iLongName; }
            }

            public string DisplayName
            {
                get
                {
                    if (ShortName == null) return LongName;
                    if (LongName == null) return ShortName;
                    return ShortName + "/" + LongName;
                }
            }

            protected string iShortName;
            protected string iLongName;
        }

        public class OptionString : Option
        {
            public OptionString(string aShortName, string aLongName, string aDefault, string aHelpDesc, string aHelpMetaVar)
                : base(aShortName, aLongName)
            {
                if (aHelpDesc == null)
                    throw new Exception("aHelpDesc must be non-null");
                if (aHelpDesc == null)
                    throw new Exception("aHelpMetaVar must be non-null");
                iValue = aDefault;
                iDefault = aDefault;
                iHelpDesc = aHelpDesc;
                iHelpMetaVar = aHelpMetaVar;
            }
            public override void Process(string[] aOptArgs)
            {
                if (aOptArgs.Length != 1)
                    throw new ArgumentException("Unexpected number of arguments for option " + ShortName + "/" + LongName);
                iValue = aOptArgs[0];
            }
            public override void Reset()
            {
                iValue = iDefault;
            }
            public override int ExpectedArgCount()
            {
                return 1;
            }
            public override void AppendHelp(OptionHelp aHelp)
            {
                aHelp.Append(iShortName, iLongName, iHelpDesc, iHelpMetaVar);
            }

            public string Value
            {
                get { return iValue; }
            }
            private string iValue;
            private readonly string iDefault;
            private readonly string iHelpDesc;
            private readonly string iHelpMetaVar;
        }

        public class OptionInt : Option
        {
            public OptionInt(string aShortName, string aLongName, int aDefault, string aHelpDesc, string aHelpMetaVar)
                : base(aShortName, aLongName)
            {
                if (aHelpDesc == null)
                    throw new Exception("aHelpDesc must be non-null");
                if (aHelpDesc == null)
                    throw new Exception("aHelpMetaVar must be non-null");
                iValue = aDefault;
                iDefault = aDefault;
                iHelpDesc = aHelpDesc;
                iHelpMetaVar = aHelpMetaVar;
            }
            public override void Process(string[] aOptArgs)
            {
                if (aOptArgs.Length != 1)
                    throw new ArgumentException("Unexpected number of arguments for option " + ShortName + "/" + LongName);
                try
                {
                    iValue = Convert.ToInt32(aOptArgs[0]);
                }
                catch (FormatException)
                {
                    throw new OptionParserError("OptionInt " + DisplayName + " has a non-integer value " + aOptArgs[0]);
                }
                catch (OverflowException)
                {
                    throw new OptionParserError(
                        String.Format(
                            "OptionInt {0} value out of range {1} to {2}: {3}",
                            DisplayName,
                            Int32.MinValue,
                            Int32.MaxValue,
                            aOptArgs[0]));
                }
            }
            public override void Reset()
            {
                iValue = iDefault;
            }
            public override int ExpectedArgCount()
            {
                return 1;
            }
            public override void AppendHelp(OptionHelp aHelp)
            {
                aHelp.Append(iShortName, iLongName, iHelpDesc, iHelpMetaVar);
            }
            public int Value
            {
                get { return iValue; }
            }

            private int iValue;
            private readonly int iDefault;
            private readonly string iHelpDesc;
            private readonly string iHelpMetaVar;
        }

        public class OptionUint : Option
        {
            public OptionUint(string aShortName, string aLongName, uint aDefault, string aHelpDesc, string aHelpMetaVar)
                : base(aShortName, aLongName)
            {
                if (aHelpDesc == null)
                    throw new Exception("aHelpDesc must be non-null");
                if (aHelpDesc == null)
                    throw new Exception("aHelpMetaVar must be non-null");
                iValue = aDefault;
                iDefault = aDefault;
                iHelpDesc = aHelpDesc;
                iHelpMetaVar = aHelpMetaVar;
            }
            public override void Process(string[] aOptArgs)
            {
                if (aOptArgs.Length != 1)
                    throw new ArgumentException("Unexpected number of arguments for option " + ShortName + "/" + LongName);
                try
                {
                    iValue = Convert.ToUInt32(aOptArgs[0]);
                }
                catch (FormatException)
                {
                    throw new OptionParserError("OptionInt " + DisplayName + " has a non-integer value " + aOptArgs[0]);
                }
                catch (OverflowException)
                {
                    throw new OptionParserError(
                        String.Format(
                            "OptionInt {0} value out of range {1} to {2}: {3}",
                            DisplayName,
                            UInt32.MinValue,
                            UInt32.MaxValue,
                            aOptArgs[0]));
                }
            }
            public override void Reset()
            {
                iValue = iDefault;
            }
            public override int ExpectedArgCount()
            {
                return 1;
            }
            public override void AppendHelp(OptionHelp aHelp)
            {
                aHelp.Append(iShortName, iLongName, iHelpDesc, iHelpMetaVar);
            }
            public uint Value
            {
                get { return iValue; }
            }

            private uint iValue;
            private readonly uint iDefault;
            private readonly string iHelpDesc;
            private readonly string iHelpMetaVar;
        }

        public class OptionBool : Option
        {
            public OptionBool(string aShortName, string aLongName, string aHelpDesc)
                : base(aShortName, aLongName)
            {
                if (aHelpDesc == null)
                    throw new Exception("aHelpDesc must be non-null");
                iValue = false;
                iHelpDesc = aHelpDesc;
            }
            public override void Process(string[] aOptArgs)
            {
                if (aOptArgs.Length != 0)
                    throw new ArgumentException("Unexpected number of arguments for option " + ShortName + "/" + LongName);
                iValue = true;
            }
            public override void Reset()
            {
                iValue = false;
            }
            public override int ExpectedArgCount()
            {
                return 0;
            }
            public override void AppendHelp(OptionHelp aHelp)
            {
                aHelp.Append(iShortName, iLongName, iHelpDesc);
            }
            public bool Value
            {
                get { return iValue; }
            }

            private bool iValue;
            private readonly string iHelpDesc;
        }

        public class OptionHelp
        {
            public const int kMaxNameLength = 22;

            public OptionHelp()
            {
                iText = "options:\n";
            }
            public void Append(string aShortName, string aLongName, string aDesc)
            {
                string name = ConstructName(aShortName, aLongName);
                iText += Construct(name, aDesc);
            }
            public void Append(string aShortName, string aLongName, string aDesc, string aMetaVar)
            {
                string name = ConstructName(aShortName, aLongName, aMetaVar);
                iText += Construct(name, aDesc);
            }
            public override string ToString()
            {
                return iText;
            }
            private static string Construct(string aName, string aDesc)
            {
                StringBuilder help = new StringBuilder(aName);
                if (aName.Length > kMaxNameLength)
                {
                    help.Append("\n");
                    help.Append(' ', kMaxNameLength + 2);
                }
                else
                {
                    help.Append(' ', kMaxNameLength + 2 - aName.Length);
                }
                help.Append(aDesc);
                help.Append("\n");
                return help.ToString();
            }
            private static string ConstructName(string aShortName, string aLongName, string aMetaVar)
            {
                string help;
                if (aShortName != null && aLongName != null)
                {
                    help = "  " + aShortName + " " + aMetaVar + ", " + aLongName + "=" + aMetaVar;
                }
                else if (aShortName != null)
                {
                    help = "  " + aShortName + " " + aMetaVar;
                }
                else
                {
                    help = "  " + aLongName + "=" + aMetaVar;
                }
                return help;
            }
            private static string ConstructName(string aShortName, string aLongName)
            {
                string help;
                if (aShortName != null && aLongName != null)
                {
                    help = "  " + aShortName + ", " + aLongName;
                }
                else if (aShortName != null)
                {
                    help = "  " + aShortName;
                }
                else
                {
                    help = "  " + aLongName;
                }
                return help;
            }

            private string iText;
        }

        public OptionParser()
        {
            iOptions = new List<Option>();
            iPosArgs = new List<string>();
            iHelpOption = new OptionBool("-h", "--help", "show this help message and exit");
            AddOption(iHelpOption);
        }
        public OptionParser(string[] aArgs)
            : this()
        {
            iArgs = aArgs;
        }
        public void AddOption(Option aOption)
        {
            if (Find(aOption.ShortName) != null)
                throw new ArgumentException("Short name " + aOption.ShortName + " used by two options");
            if (Find(aOption.LongName) != null)
                throw new ArgumentException("Long name " + aOption.LongName + " used by two options");
            iOptions.Add(aOption);
        }
        public void Parse()
        {
            Parse(iArgs);
        }
        public void Parse(string[] aArgs)
        {
            iPosArgs.Clear();
            foreach (Option opt in iOptions)
            {
                opt.Reset();
            }

            try
            {
                int i = 0;
                while (i < aArgs.Length)
                {
                    string argString = aArgs[i];
                    string[] argStringFragments = argString.Split(new[] { ':', '=' }, 2, StringSplitOptions.None);
                    string optionString, valueString;
                    if (argStringFragments.Length == 1)
                    {
                        optionString = argStringFragments[0];
                        valueString = null;
                    }
                    else
                    {
                        optionString = argStringFragments[0];
                        valueString = argStringFragments[1];
                    }
                    Option opt = Find(optionString);
                    if (opt == null)
                    {
                        // this is not an option - positional argument
                        if (argString.StartsWith("-"))
                        {
                            // this is an unspecified option
                            throw new OptionParserError("No such option: " + aArgs[i]);
                        }
                        iPosArgs.Add(argString);
                        i++;
                    }
                    else
                    {
                        // build an array of the number of arguments this option
                        // is expecting
                        int argumentsToConsume = opt.ExpectedArgCount() - (valueString == null ? 0 : 1);
                        string[] optArgList = new string[opt.ExpectedArgCount()];
                        if (valueString != null)
                        {
                            optArgList[0] = valueString;
                        }
                        try
                        {
                            Array.Copy(aArgs, i + 1, optArgList, valueString == null ? 0 : 1, argumentsToConsume);
                        }
                        catch (ArgumentException)
                        {
                            throw new OptionParserError("Option " + aArgs[i] + " has incorrect arguments");
                        }

                        // check if any of the optArgs are actual options
                        foreach (string arg in optArgList)
                        {
                            if (Find(arg) != null)
                            {
                                throw new OptionParserError("Option " + aArgs[i] + " has incorrect arguments");
                            }
                        }

                        // process this option
                        opt.Process(optArgList);
                        i += 1 + argumentsToConsume;
                    }
                }
            }
            catch (Exception)
            {
                iPosArgs.Clear();
                foreach (Option opt in iOptions)
                {
                    opt.Reset();
                }
                throw;
            }

            // Check if help option has been set
            if (iHelpOption.Value)
            {
                DisplayHelp();
            }
        }
        public bool HelpSpecified()
        {
            return (iHelpOption.Value);
        }
        public string Help()
        {
            OptionHelp help = new OptionHelp();
            foreach (Option opt in iOptions)
            {
                opt.AppendHelp(help);
            }
            return iUsage + "\n" + help;
        }
        public void DisplayHelp()
        {
            Console.WriteLine(Help());
        }
        public string Usage
        {
            get { return iUsage; }
            set { iUsage = value; }
        }
        private Option Find(string aName)
        {
            if (aName == null)
            {
                return null;
            }
            foreach (Option opt in iOptions)
            {
                if (opt.Match(aName))
                {
                    return opt;
                }
            }
            return null;
        }

        public List<string> PosArgs
        {
            get { return iPosArgs; }
        }

        private readonly string[] iArgs;
        private readonly List<Option> iOptions;
        private readonly List<string> iPosArgs;
        private readonly OptionBool iHelpOption;
        private string iUsage = "usage:";
    }
}


