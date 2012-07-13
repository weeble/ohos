using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace OpenHome.XappForms.Json
{
    [Serializable]
    public class ParserException : Exception
    {
        public ParserException(string aMessage, Token aToken)
            : base(GenerateMessage(aMessage, aToken))
        {
            Token = aToken;
        }

        public Token Token { get; private set; }

        static string GenerateMessage(string aMessage, Token aToken)
        {
            return String.Format("{0} in \"{1}\" at ({2},{3})", aMessage, aToken.Content, aToken.Line, aToken.Column);
        }

        protected ParserException(
          SerializationInfo aInfo,
          StreamingContext aContext)
            : base(aInfo, aContext)
        {
        }
    }

    /// <summary>
    /// Parse JSON tokens into a JSON object.
    /// </summary>
    public class JsonParser
    {
        readonly IEnumerator<Token> iTokenStream;

        class WhitespaceFilter : IEnumerator<Token>
        {
            readonly IEnumerator<Token> iChild;
            public WhitespaceFilter(IEnumerator<Token> aChild)
            {
                iChild = aChild;
            }

            public void Dispose()
            {
                iChild.Dispose();
            }

            public bool MoveNext()
            {
                if (!iChild.MoveNext()) return false;
                while (iChild.Current.Type == JsonLexer.Whitespace || iChild.Current.Type == JsonLexer.Newline)
                {
                    if (!iChild.MoveNext()) return false;
                }
                return true;
            }

            public void Reset()
            {
                iChild.Reset();
            }

            public Token Current
            {
                get { return iChild.Current; }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }
        }

        public JsonParser(IEnumerator<Token> aTokens)
        {
            iTokenStream = new WhitespaceFilter(aTokens);
        }
        public JsonValue ParseElement()
        {
            return ParseElement(null);
        }
        JsonValue ParseElement(string aCloseChar)
        {
            iTokenStream.MoveNext();
            var token = iTokenStream.Current;
            switch (token.Type)
            {
                case JsonLexer.String:
                    return EvaluateString(token);
                case JsonLexer.Number:
                    return EvaluateNumber(token);
                case JsonLexer.Identifier:
                    switch (token.Content)
                    {
                        case "null": return JsonNull.Instance;
                        case "true": return JsonBool.True;
                        case "false": return JsonBool.False;
                    }
                    throw Throw("Unknown identifier.", token);
                case JsonLexer.Symbol:
                    switch (token.Content)
                    {
                        case "{": return ParseObject();
                        case "[": return ParseArray();
                    }
                    if (!string.IsNullOrEmpty(aCloseChar) && aCloseChar==token.Content)
                    {
                        return null;
                    }
                    throw Throw(String.Format("Bad symbol: '{0}'", token.Content), token);
            }
            throw Throw("Bad token.", token);
        }
        JsonValue ParseObject()
        {
            JsonObject obj = new JsonObject();
            while (true)
            {
                var key = ParseElement("}");
                if (key==null) return obj;
                if (!key.IsString) throw Throw("Keys must be strings.", iTokenStream.Current);
                iTokenStream.MoveNext();
                var token = iTokenStream.Current;
                if (token.Content!=":") throw Throw("Expected ':'", token);
                var value = ParseElement();
                obj.Set(key.AsString(), value);
                iTokenStream.MoveNext();
                token = iTokenStream.Current;
                if (token.Content==",") continue;
                if (token.Content=="}") return obj;
                throw Throw("Expected ',' or '}'", token);
            }
        }
        JsonValue ParseArray()
        {
            JsonArray arr = new JsonArray();
            while (true)
            {
                var value = ParseElement("]");
                if (value==null) return arr;
                arr.Append(value);
                iTokenStream.MoveNext();
                var token = iTokenStream.Current;
                if (token.Content==",") continue;
                if (token.Content=="]") return arr;
                throw Throw("Expected ',' or ']'", token);
            }
        }
        static string DecodeStringToken(Token aStringToken)
        {
            string input = aStringToken.Content;
            StringBuilder sb = new StringBuilder();
            for (int i=1; i<input.Length-1; ++i)
            {
                if (input[i]!='\\')
                {
                    sb.Append(input[i]);
                }
                else
                {
                    i++;
                    switch (input[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'b': sb.Append('\b'); break;
                        case '/': sb.Append('/'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        case 'u':
                            if (i+4>=input.Length-1) throw Throw("String too short after unicode escape.", aStringToken);
                            string hexDigits = input.Substring(i+1, 4);
                            try
                            {
                                string unicodeChar = char.ConvertFromUtf32(Convert.ToInt32(hexDigits, 16));
                                sb.Append(unicodeChar);
                                i += 4;
                                break;
                            }
                            catch (FormatException)
                            {
                                throw Throw("Invalid unicode escape.", aStringToken);
                            }
                            catch (OverflowException)
                            {
                                throw Throw("Invalid unicode escape.", aStringToken);
                            }
                        default: throw Throw("Unexpected escape character.", aStringToken);
                    }
                }
            }
            return sb.ToString();
        }
        static JsonValue EvaluateString(Token aStringToken)
        {
            return new JsonString(DecodeStringToken(aStringToken));
        }

        static JsonValue EvaluateNumber(Token aNumberToken)
        {
            double result;
            if (double.TryParse(aNumberToken.Content, out result))
            {
                return new JsonNumber(aNumberToken.Content);
            }
            throw Throw("Bad number", aNumberToken);
        }

        static Exception Throw(string aDescription, Token aToken)
        {
            return new ParserException(aDescription, aToken);
        }
    }
}