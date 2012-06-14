using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenHome.XappForms
{
    interface IUrlDispatcher<T>
    {
        void MapPrefix(string[] aPath, Action<T> aHandlerFunc);
        void MapPath(string[] aPath, Action<T> aHandlerFunc);
        void MapPrefixToDirectory(string[] aPrefix, string aLocalDirectory);
        void MapPathToFile(string[] aPath, string aLocalFile);
    }

    class AppUrlDispatcher : UrlDispatcher<IAppWebRequest> { }
    class ServerUrlDispatcher : UrlDispatcher<IServerWebRequest> { }

    class UrlDispatcher<T> : IUrlDispatcher<T> where T : IAppWebRequest
    {
        class PathBinding
        {
            readonly string[] iPath;
            readonly bool iAllowSuffixes;
            readonly Action<T> iHandler;

            public PathBinding(string[] aPath, bool aAllowSuffixes, Action<T> aHandler)
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

            public Action<T> Handler
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

        public void ServeRequest(T aRequest)
        {
            string[] requestPath = aRequest.RelativePath;
            foreach (var binding in iBindings)
            {
                string[] bindingPath = binding.Path;
                if (bindingPath.Length > requestPath.Length) continue;
                if (!bindingPath.SequenceEqual(requestPath.Take(bindingPath.Length))) continue;
                if (bindingPath.Length < requestPath.Length && !binding.AllowSuffixes) continue;
                aRequest.RelativePath = requestPath.Skip(bindingPath.Length).ToArray();
                binding.Handler(aRequest);
                return;
            }
            aRequest.Send404NotFound();
        }

        public void MapPrefix(string[] aPath, Action<T> aHandlerFunc)
        {
            iBindings.Add(new PathBinding(aPath, true, aHandlerFunc));
        }
        public void MapPath(string[] aPath, Action<T> aHandlerFunc)
        {
            iBindings.Add(new PathBinding(aPath, false, aHandlerFunc));
        }
        public void MapPrefixToDirectory(string[] aPrefix, string aLocalDirectory)
        {
            MapPrefix(aPrefix,
                aAppWebRequest =>
                {
                    string[] path = aAppWebRequest.RelativePath.Select(aSegment => aSegment.TrimEnd('/')).ToArray();
                    if (!ValidatePath(path))
                    {
                        aAppWebRequest.Send404NotFound();
                        return;
                    }
                    string filepath = Path.Combine(aLocalDirectory, Path.Combine(path));
                    string extension = Path.GetExtension(filepath) ?? "";
                    string contentType;
                    if (Server.MimeTypesByExtension.TryGetValue(extension, out contentType))
                    {
                        aAppWebRequest.SendFile(contentType, filepath);
                    }
                    else
                    {
                        aAppWebRequest.Send404NotFound();
                    }
                });
        }
        public void MapPathToFile(string[] aPath, string aLocalFile)
        {
            MapPath(aPath,
                aAppWebRequest =>
                {
                    string extension = Path.GetExtension(aLocalFile) ?? "";
                    string contentType;
                    if (Server.MimeTypesByExtension.TryGetValue(extension, out contentType))
                    {
                        aAppWebRequest.SendFile(contentType, aLocalFile);
                    }
                    else
                    {
                        aAppWebRequest.Send404NotFound();
                    }
                });
        }
        public void MapPathToSubMapping(string[] aPath, UrlDispatcher<T> aSubDispatcher)
        {
            MapPath(aPath, aSubDispatcher.ServeRequest);
        }
    }
}