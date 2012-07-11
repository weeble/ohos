using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenHome.XappForms
{
    public class AppPathDispatcher : PathDispatcher<RequestData, IWebRequestResponder> { }

    public class PathDispatcher<TRequest, TResponder>
        where TRequest : IRequestData, IHasWithPath<TRequest>
        where TResponder : IWebRequestResponder
    {
        class PathBinding
        {
            readonly string[] iPath;
            readonly bool iAllowSuffixes;
            readonly Func<TRequest, TResponder, bool> iHandler;

            public PathBinding(string[] aPath, bool aAllowSuffixes, Func<TRequest, TResponder, bool> aHandler)
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

            public Func<TRequest, TResponder, bool> Handler
            {
                get { return iHandler; }
            }
        }

        readonly List<PathBinding> iBindings = new List<PathBinding>();

        public bool ServeRequest(TRequest aRequest, TResponder aResponder)
        {
            IList<string> requestPath = aRequest.Path.PathSegments;
            foreach (var binding in iBindings)
            {
                string[] bindingPath = binding.Path;
                if (bindingPath.Length > requestPath.Count) continue;
                if (!bindingPath.SequenceEqual(requestPath.Take(bindingPath.Length))) continue;
                if (bindingPath.Length < requestPath.Count && !binding.AllowSuffixes) continue;
                return binding.Handler(aRequest.WithPath(aRequest.Path.SkipPathSegments(bindingPath.Length)), aResponder);
            }
            return false;
        }

        public void MapPrefix(string[] aPath, Func<TRequest, TResponder, bool> aHandler)
        {
            iBindings.Add(new PathBinding(aPath, true, aHandler));
        }
        public void MapPath(string[] aPath, Func<TRequest, TResponder, bool> aHandler)
        {
            iBindings.Add(new PathBinding(aPath, false, aHandler));
        }

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
        static bool ValidatePath(IEnumerable<string> aPath)
        {
            return aPath.All(aSeg =>
                aSeg.IndexOfAny(Path.GetInvalidFileNameChars()) == -1
                    && aSeg != ".."
                        && aSeg != "."
                            && aSeg != "") && aPath.Any();
        }

        public void MapPrefixToDirectory(
            string[] aPrefix,
            string aLocalDirectory)
        {
            MapPrefix(
                aPrefix,
                (aRequest, aResponder) =>
                {
                    string[] path = aRequest.Path.PathSegments.Select(aSegment => aSegment.TrimEnd('/')).ToArray();
                    if (!ValidatePath(path))
                    {
                        return false;
                    }
                    string filepath = Path.Combine(aLocalDirectory, Path.Combine(path));
                    aResponder.SendFile(GetMimeType(filepath), filepath);
                    return true;
                });
        }

        public void MapPathToFile(
            string[] aPath,
            string aLocalFile)
        {
            MapPath(aPath,
                (aAppWebRequest, aResponder) =>
                {
                    aResponder.SendFile(GetMimeType(aLocalFile), aLocalFile);
                    return true;
                });
        }
    }
}