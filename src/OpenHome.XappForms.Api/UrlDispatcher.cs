using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenHome.XappForms
{
    public interface IUrlDispatcher<T>
    {
        void MapPrefix(string[] aPath, Action<RequestData, T> aHandlerFunc);
        void MapPath(string[] aPath, Action<RequestData, T> aHandlerFunc);
        void MapPrefixToDirectory(string[] aPrefix, string aLocalDirectory);
        void MapPathToFile(string[] aPath, string aLocalFile);
    }

    public class AppUrlDispatcher : UrlDispatcher<IWebRequestResponder> { }
    //class ServerUrlDispatcher : UrlDispatcher<IServerWebRequestResponder> { }

    public class UrlDispatcher<T> : IUrlDispatcher<T> where T : IWebRequestResponder
    {
        static readonly internal Dictionary<string, string> MimeTypesByExtension = new Dictionary<string, string>{
            {".js", "application/javascript; charset=utf-8" },
            {".css", "text/css; charset=utf-8" },
            {".json", "application/json; charset=utf-8" },
            {".html", "text/html; charset=utf-8" },
            {".htm", "text/html; charset=utf-8" },
            {".xml", "text/xml; charset=utf-8" },
            {".txt", "text/plain; charset=utf-8" },
            {".png", "image/png" },
            {".gif", "image/gif" },
            {".jpeg", "image/jpeg" },
            {".jpg", "image/jpeg" },
            {".svg", "image/svg+xml; charset=utf-8" },
            {".ico", "image/vnd.microsoft.icon" },
        };

        static internal string GetMimeType(string aFilename)
        {
            foreach (var kvp in MimeTypesByExtension)
            {
                if (aFilename.EndsWith(kvp.Key))
                {
                    return kvp.Value;
                }
            }
            return "application/octet-stream";
        }
        class PathBinding
        {
            readonly string[] iPath;
            readonly bool iAllowSuffixes;
            readonly Action<RequestData, T> iHandler;

            public PathBinding(string[] aPath, bool aAllowSuffixes, Action<RequestData, T> aHandler)
            {
                iPath = aPath;
                iAllowSuffixes = aAllowSuffixes;
                iHandler = aHandler;
            }

            public string[] Path
            {
                get { return iPath; }
            }

            public bool AllowSuffixes
            {
                get { return iAllowSuffixes; }
            }

            public Action<RequestData, T> Handler
            {
                get { return iHandler; }
            }
        }

        readonly List<PathBinding> iBindings = new List<PathBinding>();

        static bool ValidatePath(IEnumerable<string> aPath)
        {
            return aPath.All(aSeg =>
                aSeg.IndexOfAny(Path.GetInvalidFileNameChars()) == -1
                    && aSeg != ".."
                        && aSeg != "."
                            && aSeg != "") && aPath.Any();
        }

        public void ServeRequest(RequestData aRequest, T aResponder)
        {
            IList<string> requestPath = aRequest.Path.PathSegments;
            foreach (var binding in iBindings)
            {
                string[] bindingPath = binding.Path;
                if (bindingPath.Length > requestPath.Count) continue;
                if (!bindingPath.SequenceEqual(requestPath.Take(bindingPath.Length))) continue;
                if (bindingPath.Length < requestPath.Count && !binding.AllowSuffixes) continue;
                binding.Handler(aRequest.SkipPathSegments(bindingPath.Length), aResponder);
                return;
            }
            aResponder.Send404NotFound();
        }

        public void MapPrefix(string[] aPath, Action<RequestData, T> aHandlerFunc)
        {
            iBindings.Add(new PathBinding(aPath, true, aHandlerFunc));
        }
        public void MapPath(string[] aPath, Action<RequestData, T> aHandlerFunc)
        {
            iBindings.Add(new PathBinding(aPath, false, aHandlerFunc));
        }
        public void MapPrefixToDirectory(string[] aPrefix, string aLocalDirectory)
        {
            MapPrefix(aPrefix,
                (aAppWebRequest, aResponder) =>
                {
                    string[] path = aAppWebRequest.Path.PathSegments.Select(aSegment => aSegment.TrimEnd('/')).ToArray();
                    if (!ValidatePath(path))
                    {
                        aResponder.Send404NotFound();
                        return;
                    }
                    string filepath = Path.Combine(aLocalDirectory, Path.Combine(path));
                    aResponder.SendFile(GetMimeType(filepath), filepath);
                });
        }
        public void MapPathToFile(string[] aPath, string aLocalFile)
        {
            MapPath(aPath,
                (aAppWebRequest, aResponder) => aResponder.SendFile(GetMimeType(aLocalFile), aLocalFile));
        }
        public void MapPathToSubMapping(string[] aPath, UrlDispatcher<T> aSubDispatcher)
        {
            MapPath(aPath, aSubDispatcher.ServeRequest);
        }
    }
}