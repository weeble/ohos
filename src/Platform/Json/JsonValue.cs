using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.XappForms.Json
{
    static class Extensions
    {
        public static TValue GetDefault<TKey,TValue>(
            this Dictionary<TKey,TValue> aDict, TKey aKey, TValue aDefaultValue)
        {
            TValue value;
            return aDict.TryGetValue(aKey, out value) ? value : aDefaultValue;
        }
    }

    /// <summary>
    /// A JSON value, such as a number, string, array, object, boolean or null.
    /// </summary>
    public abstract class JsonValue
    {
        public static JsonValue FromString(string aJsonString)
        {
            try
            {
                var tokenStream = JsonLexer.Lex(aJsonString).GetEnumerator();
                var parser = new JsonParser(tokenStream);
                var value = parser.ParseElement();
                while (tokenStream.MoveNext())
                {
                    if (tokenStream.Current.Type != JsonLexer.Whitespace &&
                        tokenStream.Current.Type != JsonLexer.Newline)
                    {
                        throw new ArgumentException(String.Format(
                            "Bad JSON string - trailing token at ({0},{1}).",
                            tokenStream.Current.Line, tokenStream.Current.Column));
                    }
                }
                return value;
            }
            catch (LexerException le)
            {
                throw new ArgumentException("Cannot lex JSON string", "aJsonString", le);
            }
            catch (ParserException pe)
            {
                throw new ArgumentException("Cannot parse JSON string", "aJsonString", pe);
            }
        }
        public override bool Equals(object aOther)
        {
            return aOther is JsonValue && ToString()==aOther.ToString();
        }
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
        virtual public JsonValue Get(int aIndex) { return JsonError.Instance; }
        virtual public JsonValue Get(string aName) { return JsonError.Instance; }
        virtual public void Extend(JsonValue aNewItems) { throw new Exception(); }
        virtual public void Append(JsonValue aElement) { throw new Exception(); }
        virtual public void Set(int aIndex, JsonValue aElement) { throw new Exception(); }
        virtual public void Set(string aName, JsonValue aElement) { throw new Exception(); }
        virtual public string AsString() { throw new Exception(); }
        virtual public bool AsBool() { throw new Exception(); }
        virtual public double AsDouble() { throw new Exception(); }
        virtual public int AsInt() { throw new Exception(); }
        virtual public long AsLong() { throw new Exception(); }
        virtual public IEnumerable<KeyValuePair<string, JsonValue>> EnumerateObject() { throw new Exception(); }
        virtual public IEnumerable<JsonValue> EnumerateArray() { throw new Exception(); }
        virtual public bool Exists { get { return true; } }
        virtual public bool IsError { get { return false; } }
        virtual public bool IsNull { get { return false; } }
        virtual public bool IsString { get { return false; } }
        virtual public bool IsArray { get { return false; } }
        virtual public bool IsObject { get { return false; } }
        virtual public bool IsNumber { get { return false; } }
        virtual public bool IsBool { get { return false; } }
    }




    public class JsonError : JsonValue
    {
        static readonly JsonError StaticInstance = new JsonError();
        static public JsonError Instance { get { return StaticInstance; } }
        override public bool Exists { get { return false; } }
        override public bool IsError { get { return true; } }
        override public string ToString()
        {
            return "ERROR";
        }
    }

    public class Undefined : JsonValue
    {
        static readonly Undefined StaticInstance = new Undefined();
        static public Undefined Instance { get { return StaticInstance; } }
        override public JsonValue Get(int aIndex) { return this; }
        override public JsonValue Get(string aName) { return this; }
        override public bool Exists { get { return false; } }
        override public string ToString()
        {
            return "undefined";
        }
    }

    public class JsonObject : JsonValue, IEnumerable<KeyValuePair<string, JsonValue>>
    {
        readonly Dictionary<string, JsonValue> iContents = new Dictionary<string, JsonValue>();
        override public bool IsObject { get { return true; } }
        override public JsonValue Get(string aName) { return iContents.GetDefault(aName, Undefined.Instance); }
        override public void Set(string aName, JsonValue aElement) { iContents[aName] = aElement; }

        // Convenience methods that make C# initializer syntax nice:

        public void Add(string aName, JsonValue aElement)
        {
            iContents.Add(aName, aElement);
        }
        public void Add(string aName, string aString)
        {
            if (aString == null)
            {
                iContents.Add(aName, JsonNull.Instance);
            }
            else
            {
                iContents.Add(aName, new JsonString(aString));
            }
        }
        public void Add(string aName, bool aBool)
        {
            iContents.Add(aName, new JsonBool(aBool));
        }
        public void Add(string aName, long aLong)
        {
            iContents.Add(aName, new JsonNumber(aLong.ToString()));
        }
        public void Add(string aName, double aDouble)
        {
            iContents.Add(aName, new JsonNumber(aDouble.ToString()));
        }

        override public void Extend(JsonValue aNewItems)
        {
            var figObject = aNewItems as JsonObject;
            if (figObject == null) throw new Exception("Expected JsonObject");
            foreach (var kvp in figObject.iContents)
            {
                iContents.Add(kvp.Key, kvp.Value);
            }
        }

        public IEnumerator<KeyValuePair<string, JsonValue>> GetEnumerator()
        {
            return iContents.GetEnumerator();
        }

        override public string ToString()
        {
            return String.Format(
                "{{{0}}}",
                String.Join(
                    ",",
                    iContents
                        .OrderBy(aKvp => aKvp.Key)
                        .Select(aKvp =>
                            String.Format(
                                "{0}:{1}",
                                JsonUtils.EncodeJsonString(aKvp.Key), aKvp.Value)).ToArray()));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        override public IEnumerable<KeyValuePair<string, JsonValue>> EnumerateObject()
        {
            return iContents;
        }
    }

    public class JsonArray : JsonValue, IEnumerable<JsonValue>
    {
        readonly List<JsonValue> iContents = new List<JsonValue>();
        override public bool IsArray { get { return true; } }
        override public JsonValue Get(int aIndex)
        {
            if (aIndex<0 || aIndex >= iContents.Count) return Undefined.Instance;
            return iContents[aIndex];
        }
        override public void Set(int aIndex, JsonValue aElement)
        {
            if (aIndex<0 || aIndex > iContents.Count) throw new Exception();
            if (aIndex == iContents.Count)
            {
                Append(aElement);
            }
            else
            {
                iContents[aIndex] = aElement;
            }
        }
        override public void Append(JsonValue aElement)
        {
            iContents.Add(aElement);
        }

        // Convenience methods to make it easy to use C# initializer syntax:

        public void Add(JsonValue aElement)
        {
            Append(aElement);
        }
        public void Add(string aString)
        {
            Append(new JsonString(aString));
        }
        public void Add(bool aBool)
        {
            Append(new JsonBool(aBool));
        }
        public void Add(long aLong)
        {
            Append(new JsonNumber(aLong.ToString()));
        }
        public void Add(double aDouble)
        {
            Append(new JsonNumber(aDouble.ToString()));
        }
        override public void Extend(JsonValue aNewItems)
        {
            var figArray = aNewItems as JsonArray;
            if (figArray == null) throw new Exception("Expected JsonArray");
            iContents.AddRange(figArray.iContents);
        }

        public IEnumerator<JsonValue> GetEnumerator()
        {
            return iContents.GetEnumerator();
        }

        override public string ToString()
        {
            return String.Format(
                "[{0}]",
                String.Join(
                    ",",
                    iContents.Select(aItem=>aItem.ToString()).ToArray()));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        override public IEnumerable<JsonValue> EnumerateArray()
        {
            return iContents;
        }
    }

    public class JsonString : JsonValue
    {
        readonly string iValue;
        public JsonString(string aValue)
        {
            if (aValue == null)
            {
                throw new ArgumentNullException("aValue");
            }
            iValue = aValue;
        }
        override public bool IsString { get { return true; } }
        override public string AsString() { return iValue; }
        override public string ToString()
        {
            return JsonUtils.EncodeJsonString(iValue);
        }
    }

    public class JsonNumber : JsonValue
    {
        readonly string iValue;
        public JsonNumber(string aValue) { iValue = aValue; }
        override public double AsDouble() { return double.Parse(iValue); }
        override public int AsInt() { return int.Parse(iValue); }
        override public long AsLong() { return long.Parse(iValue); }
        override public bool IsNumber { get { return true; } }
        override public string ToString()
        {
            return iValue;
        }
    }

    public class JsonBool : JsonValue
    {
        static readonly JsonBool StaticTrue = new JsonBool(true);
        static readonly JsonBool StaticFalse = new JsonBool(false);
        static public JsonBool True { get { return StaticTrue; } }
        static public JsonBool False { get { return StaticFalse; } }
        readonly bool iValue;
        public JsonBool(bool aValue) { iValue = aValue; }
        override public bool AsBool() { return iValue; }
        override public bool IsBool { get { return true; } }
        override public string ToString()
        {
            return iValue?"true":"false";
        }
    }

    public class JsonNull : JsonValue
    {
        static readonly JsonNull StaticInstance = new JsonNull();
        static public JsonNull Instance { get { return StaticInstance; } }
        override public bool IsNull { get { return true; } }
        override public string ToString()
        {
            return "null";
        }
    }

    static class JsonUtils
    {
        public static string EncodeJsonString(string aContent)
        {
            if (aContent == null)
            {
                throw new ArgumentNullException("aContent");
            }
            StringBuilder sb = new StringBuilder("\"");
            foreach (char ch in aContent)
            {
                switch (ch)
                {
                    case '\r':sb.Append(@"\r"); break;
                    case '\f':sb.Append(@"\f"); break;
                    case '\t':sb.Append(@"\t"); break;
                    case '\n':sb.Append(@"\n"); break;
                    case '\b':sb.Append(@"\b"); break;
                    case '\\':sb.Append(@"\\"); break;
                    case '"':sb.Append(@"\"""); break;
                    default:
                        if (char.IsControl(ch) || ch>255)
                        {
                            sb.Append(@"\u");
                            sb.Append(Convert.ToInt32(ch).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(ch);
                        }
                        break;
                }
            }
            sb.Append("\"");
            return sb.ToString();
        }
    }
}
