using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Owin;

namespace OpenHome.XappForms
{
    public interface IAppWebRequest
    {
        IDictionary<string, IEnumerable<string>> Query { get; }
        IList<string> RelativePath { get; }
        string Method { get; }
        IWebRequestResponder Responder { get; }
        IAppWebRequest SkipPathSegments(int aCount);
    }

    public interface IServerWebRequestResponder : IWebRequestResponder
    {
        void ServeLongPoll(string aStatus, Dictionary<string, IEnumerable<string>> aHeaders, string aContentType, BodyDelegate aBodyDelegate);
        Dictionary<string, IEnumerable<string>> DefaultResponseHeaders { get; }

        // Note: Should ReadBody move elsewhere? While in terms of data-flow it's
        // part of the request, in terms of actual usage it's much more closely
        // related to the actions on the responder. Really, the partition is not
        // between request and response, but between address and action. The
        // RequestData really identifies what code should handle the request, then
        // the that code performs an action, which might include reading the body
        // and will include sending a response. What we need are better names.
        void ReadBody(Func<ArraySegment<byte>, bool> aWrite, Func<Action, bool> aFlush, Action<Exception> aEnd, CancellationToken aCancellationToken);
    }


    static class StringDictionary
    {
        public static void Add(IDictionary<string, IEnumerable<string>> aDictionary, string aKey, string aValue)
        {
            IEnumerable<string> values;
            if (!aDictionary.TryGetValue(aKey, out values))
            {
                values = new List<string>();
                aDictionary[aKey] = values;
            }
            ((List<string>)values).Add(aValue);
        }
    }

    public class RequestData
    {
        public RequestPath Path { get; private set; }
        public IDictionary<string, IEnumerable<string>> Headers { get; private set; }
        public string Method { get; private set; }
        public RequestCookies Cookies { get; private set; }
        public RequestData(string aMethod, RequestPath aPath, IDictionary<string, IEnumerable<string>> aHeaders)
        {
            Method = aMethod;
            Path = aPath;
            Headers = aHeaders;
            Cookies = new RequestCookies(aHeaders); // TODO: Avoid reparsing cookies on SkipPathSegments.
        }
        public RequestData(string aMethod, string aPath, IDictionary<string, IEnumerable<string>> aHeaders)
            : this(aMethod, new RequestPath(aPath), aHeaders)
        {
        }
        public RequestData SkipPathSegments(int aCount)
        {
            return new RequestData(
                Method,
                Path.SkipPathSegments(aCount),
                Headers);
        }
    }

    public class RequestCookies
    {
        readonly Dictionary<string, IEnumerable<string>> iCookies;
        public IEnumerable<string> this[string aName]
        {
            get { return Get(aName); }
        }
        public IEnumerable<string> Get(string aName)
        {
            IEnumerable<string> value;
            if (iCookies.TryGetValue(aName, out value))
            {
                return value;
            }
            return Enumerable.Empty<string>();
        }
        public RequestCookies(IDictionary<string, IEnumerable<string>> aHeaders)
        {
            iCookies = new Dictionary<string, IEnumerable<string>>();

            IEnumerable<string> cookies;
            if (!aHeaders.TryGetValue("Cookie", out cookies))
            {
                return;
            }
            foreach (string cookieString in cookies)
            {
                foreach (string subCookieString in cookieString.Split(';'))
                {
                    string[] parts = subCookieString.Trim().Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;
                    StringDictionary.Add(iCookies, parts[0], parts[1]);
                }
            }
        }
    }



    public class RequestPath
    {
        public string OriginalUri { get; private set; }
        public IDictionary<string, IEnumerable<string>> Query { get; private set; }
        ArraySlice<string> iPathSegments;

        public IList<string> PathSegments { get { return iPathSegments; } }

        public RequestPath(string aPath, string aQueryString)
            : this(aPath + '?' + aQueryString)
        {
        }

        public RequestPath(string aPath)
        {
            OriginalUri = aPath;
            var uri = new Uri(new Uri("http://dummy/"), aPath);
            string[] pathSegments = uri.Segments.Skip(1).Select(Uri.UnescapeDataString).ToArray();
            iPathSegments = new ArraySlice<string>(pathSegments);
            PopulateQuery(uri.Query);
        }

        RequestPath(
            string aPath,
            ArraySlice<string> aPathSegments,
            IDictionary<string, IEnumerable<string>> aQuery)
        {
            OriginalUri = aPath;
            iPathSegments = aPathSegments;
            Query = aQuery;
        }

        public RequestPath SkipPathSegments(int aCount)
        {
            return new RequestPath(
                OriginalUri,
                iPathSegments.Slice(aCount, int.MaxValue),
                Query);
        }

        void PopulateQuery(string aQueryString)
        {
            Query = new Dictionary<string, IEnumerable<string>>();
            if (aQueryString.Length > 0)
            {
                if (aQueryString[0] == '?')
                {
                    aQueryString = aQueryString.Substring(1);
                }
                string[] fragments = aQueryString.Split('&');
                foreach (string f in fragments)
                {
                    string[] keyAndValue = f.Split(new[] { '=' }, 2).Select(Uri.UnescapeDataString).ToArray();
                    string key, value;
                    if (keyAndValue.Length == 1)
                    {
                        key = "";
                        value = keyAndValue[0];
                    }
                    else
                    {
                        key = keyAndValue[0];
                        value = keyAndValue[1];
                    }
                    StringDictionary.Add(Query, key, value);
                }
            }
        }
    }
}