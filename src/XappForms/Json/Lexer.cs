﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace OpenHome.XappForms.Json
{
    /// <summary>
    /// Language token parsed out by a Lexer.
    /// </summary>
    public class Token
    {
        /// <summary>
        /// What kind of token this is. Valid values depend on language.
        /// For JSON, these are "whitespace", "newline", "symbol", "string", "number" and "identifier".
        /// See JsonLexer for more details.
        /// </summary>
        public string Type { get; private set; }
        /// <summary>
        /// The verbatim content of the token. E.g. for a string token this includes all the quotes
        /// and escape characters.
        /// </summary>
        public string Content { get; private set; }
        readonly string iOriginalString;
        public int Index { get; private set; }
        int iLine = -1;
        int iColumn = -1;
        public int Line
        {
            get
            {
                LazyCalcLineAndColumn();
                return iLine;
            }
        }
        public int Column
        {
            get
            {
                LazyCalcLineAndColumn();
                return iColumn;
            }
        }
        void LazyCalcLineAndColumn()
        {
            if (iLine == -1)
            {
                StringUtils.GetLineAndColumn(iOriginalString, Index, out iLine, out iColumn);
            }
        }
        public Token(string aKind, string aContent, string aOriginalString, int aIndex)
        {
            Type = aKind;
            Content = aContent;
            iOriginalString = aOriginalString;
            Index = aIndex;
        }
    }

    [Serializable]
    public class LexerException : Exception
    {
        readonly int iLine;
        readonly int iColumn;

        public LexerException(string aMessage, string aString, int aIndex)
            : base(GenerateMessage(aMessage, aString, aIndex))
        {
            String = aString;
            Index = aIndex;
            StringUtils.GetLineAndColumn(aString, aIndex, out iLine, out iColumn);
        }

        public int Column { get { return iColumn; } }
        public int Line { get { return iLine; } }
        public int Index { get; private set; }
        public string String { get; private set; }

        static string GenerateMessage(string aMessage, string aString, int aIndex)
        {
            int line, column;
            StringUtils.GetLineAndColumn(aString, aIndex, out line, out column);
            return String.Format("{0} at ({1},{2})", aMessage, line, column);
        }

        protected LexerException(
          SerializationInfo aInfo,
          StreamingContext aContext)
            : base(aInfo, aContext) { }
    }


    /// <summary>
    /// Helper class for constructing a Lexer.
    /// </summary>
    /// <remarks>
    /// A Lexer is immutable and contains a compiled Regex object. The LexerBuilder
    /// gives us a convenient mutable interface to describe the Lexer we want to
    /// construct.
    /// </remarks>
    public class LexerBuilder
    {
        readonly List<string> iTokenKinds = new List<string>();
        readonly List<string> iTokenRegexes = new List<string>();
        public void AddTokenKind(string aKind, string aRegex)
        {
            iTokenKinds.Add(aKind);
            iTokenRegexes.Add(aRegex);
        }
        public string RegexString
        {
            get
            {
                return String.Join(
                    "|",                                    // Make a series of alternatives, one for each token type.
                    Enumerable
                        .Range(0,iTokenKinds.Count)
                        .Select(
                            aIndex=>String.Format(
                                "(?<{0}>\\G{1})",           // Named capture group matching token kind.
                                                            // \G ensures we don't skip any characters to find a match.
                                iTokenKinds[aIndex],
                                iTokenRegexes[aIndex]))
                        .ToArray());
            }
        }
        public IEnumerable<string> TokenKinds { get { return iTokenKinds.AsEnumerable(); } }

        public Lexer Build()
        {
            return new Lexer(TokenKinds, RegexString);
        }
    }
    
    static class StringUtils
    {
        /// <summary>
        /// Determine the line and column that a given character index in the string
        /// occurs, based on the newlines embedded in the string.
        /// </summary>
        /// <param name="aString"></param>
        /// <param name="aIndex"></param>
        /// <param name="aLine">The line number, starting from 1 at the top.</param>
        /// <param name="aColumn">The column, starting from 1 at the left.</param>
        public static void GetLineAndColumn(string aString, int aIndex, out int aLine, out int aColumn)
        {
            aLine = 1 + aString.Substring(0, aIndex).Count(aCh=>aCh=='\n');
            if (aIndex == 0)
            {
                aColumn = 1;
            }
            else
            {
                aColumn = aIndex - aString.LastIndexOf('\n', aIndex - 1, aIndex);
            }
        }
    }

    /// <summary>
    /// Scans a string to produce a stream of tokens.
    /// </summary>
    /// <remarks>
    /// Use LexerBuilder to construct an instance.
    /// </remarks>
    public class Lexer
    {
        readonly List<string> iTokenKinds;
        readonly Regex iRegex;
        internal Lexer(IEnumerable<string> aTokenKinds, string aRegexString)
        {
            iTokenKinds = new List<string>(aTokenKinds);
            iRegex = new Regex(aRegexString, RegexOptions.IgnorePatternWhitespace);
        }
        public IEnumerable<Token> Lex(string aString)
        {
            return Lex(aString, 0);
        }
        public IEnumerable<Token> Lex(string aString, int aStartIndex)
        {
            int index = aStartIndex;
            while (index<aString.Length)
            {
                Match match = iRegex.Match(aString, index);
                if (!match.Success)
                {
                    int line, column;
                    StringUtils.GetLineAndColumn(aString, index, out line, out column);
                    throw new LexerException("Unrecognized token", aString, index);
                }
                foreach (string tokenType in iTokenKinds)
                {
                    if (match.Groups[tokenType].Success)
                    {
                        yield return new Token(tokenType, match.Value, aString, index);
                        break;
                    }
                }
                // This isn't a LexerException because it doesn't indicate a problem with
                // the input string, but with the lexer's token definitions. If you get
                // this at run-time, there's a bug in whatever code assembled the lexer,
                // e.g. JsonLexer.
                if (match.Length==0) throw new Exception("Lexer matched zero-length token.");
                index += match.Length;
            }
        }
    }

    /// <summary>
    /// Lexer for the Json data language.
    /// </summary>
    public static class JsonLexer
    {
        public const string Whitespace = "whitespace";
        public const string Newline = "newline";
        public const string Symbol = "symbol";
        public const string Identifier = "identifier";
        public const string String = "string";
        public const string Number = "number";
        
        const string WhitespaceRegex = @"[\t ]+";
        const string NewlineRegex    = @"\r?\n";
        const string SymbolRegex     = @"[][{}:,]";
        const string IdentifierRegex = @"[A-Za-z]+";
        const string StringRegex    = @"
            \""                              # Opening quote
            (?:
              \\u[0-9a-fA-F]{4}           |  # Unicode escape
              \\b | \\f | \\n | \\r | \\t |  # Backspace, form-feed, newline, carriage-return, tab
              \\\\ | \\/ |                   # Escaped backslash, slash
              \\"" |                         # Escaped quotes
              [^\p{Cc}\\""]                  # Any unicode character except a control code, a backslash or a double quote
            )*                               # Zero of more of any of the previous classes
            \""                              # Closing quote
            ";
        const string NumberRegex     = @"
            -?                             # Optional minus
            (?:
              0 |                          # Leading zero only allowed on its own (e.g. 0, 0.1, 0e1)
              [1-9][0-9]*                   # Otherwise leading digit must be non-zero (so no 0123, 0001)
            )
            (?:[.][0-9]*)?                 # Optional fraction
            (?:[Ee][+-]?[0-9]+)?           # Optional exponent
            (?![A-Za-z0-9_])               # Must not be followed by an alphanumeric character
            ";
        //const string CommentRegex    = @"[;#][^\n]*\n";

        public static LexerBuilder MkBuilder()
        {
            LexerBuilder builder = new LexerBuilder();
            builder.AddTokenKind(Whitespace, WhitespaceRegex);
            builder.AddTokenKind(Newline, NewlineRegex);
            builder.AddTokenKind(Symbol, SymbolRegex);
            builder.AddTokenKind(Identifier, IdentifierRegex);
            builder.AddTokenKind(String, StringRegex);
            builder.AddTokenKind(Number, NumberRegex);
            return builder;
        }
        static readonly Lexer StaticInstance = MkBuilder().Build();
        public static Lexer Instance { get { return StaticInstance; } }
        public static IEnumerable<Token> Lex(string aString)
        {
            return Instance.Lex(aString);
        }
        public static IEnumerable<Token> Lex(string aString, int aStartIndex)
        {
            return Instance.Lex(aString, aStartIndex);
        }
    }

}