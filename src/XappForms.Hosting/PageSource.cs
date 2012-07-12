using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Owin;

namespace OpenHome.XappForms
{
    public static class PageSource
    {
        public static BodyDelegate ServeFile(string filename)
        {
            return ServeBytes(File.ReadAllBytes(filename));
        }

        public static BodyDelegate ServeUtf8(string data)
        {
            return ServeBytes(Encoding.UTF8.GetBytes(data));
        }

        public static BodyDelegate ServeBytes(byte[] bytes)
        {
            return ServeBytes(new ArraySegment<byte>(bytes));
        }

        public static BodyDelegate ServeBytes(ArraySegment<byte> bytes)
        {
            // Note: This seems to trip a genuine type-inference bug in the compiler.
            // The workaround is to specify the types explicitly.
            // It looks like it's very similar to this:
            // http://stackoverflow.com/questions/4466859/delegate-system-action-does-not-take-0-arguments-is-this-a-c-sharp-compiler
            return
                (Func<ArraySegment<byte>, bool> aWrite, Func<Action, bool> aFlush, Action<Exception> aEnd,
                    CancellationToken aCancelToken) =>
                    //(aWrite, aFlush, aEnd, aCancelToken) =>
                {
                    try
                    {
                        aWrite(bytes); // <- This trips up the compiler if the types are inferred.
                        aEnd(null);
                    }
                    catch (Exception e)
                    {
                        aEnd(e);
                    }
                };
        }
        public static IPageSource MakeSourceFromFile(string aContentType, string aFilename)
        {
            return new BytePageSource(aContentType, new ArraySegment<byte>(File.ReadAllBytes(aFilename)));
        }
        public static IPageSource MakeSourceFromString(StringType aStringType, string aString)
        {
            return new BytePageSource(TextEncoding[aStringType], new ArraySegment<byte>(Encoding.UTF8.GetBytes(aString)));
        }

        static readonly Dictionary<StringType, string> TextEncoding = new Dictionary<StringType, string> {
            { StringType.Plain, "text/plain; charset=utf-8" },
            { StringType.Html, "text/html; charset=utf-8" },
            { StringType.Xml, "text/xml; charset=utf-8" },
            { StringType.Css, "text/css; charset=utf-8" },
            { StringType.Json, "application/json; charset=utf-8" },
            { StringType.Javascript, "application/javascript; charset=utf-8" },
        };
    }

    public enum StringType
    {
        Plain,
        Html,
        Xml,
        Css,
        Json,
        Javascript
    }

    class BytePageSource : IPageSource
    {
        ArraySegment<byte> iByteSegment;
        string iContentType;

        public BytePageSource(string aContentType, ArraySegment<byte> aByteSegment)
        {
            iContentType = aContentType;
            iByteSegment = aByteSegment;
        }
        public long ContentLength { get { return iByteSegment.Count; } }
        public string ContentType { get { return iContentType; } }
        public BodyDelegate Serve()
        {
            return PageSource.ServeBytes(iByteSegment);
        }
    }
}