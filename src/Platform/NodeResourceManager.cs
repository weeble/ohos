using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenHome.Net.Device;

namespace OpenHome.Os.Platform
{
    public class NodeResourceManager : IResourceManager
    {
        readonly string iUiDir;
        readonly string iUdn;
        const string kResourcesDir = "resources";
        readonly uint iWebSocketPort;

        public NodeResourceManager(string aUiDir, string aUdn, uint aWebSocketPort)
        {
            iUiDir = aUiDir;
            iUdn = aUdn;
            iWebSocketPort = aWebSocketPort;
        }

        public void WriteResource(string aUriTail, uint aIpAddress, List<string> aLanguageList, IResourceWriter aWriter)
        {
            if (iUiDir == null)
            {
                // UI is disabled.
                return;
            }
            if (IsNodeJsPath(aUriTail))
            {
                WriteNodeJs(aWriter);
                return;
            }
            string tail = (aUriTail.Length == 0 ? "index.html" : aUriTail);
            string fullPath = Path.Combine(iUiDir, tail);
            if (Path.GetDirectoryName(fullPath).EndsWith(kResourcesDir))
            {
                string resDir = Path.Combine(iUiDir, kResourcesDir);
                string fileName = Path.GetFileName(aUriTail);
                foreach (string lang in aLanguageList)
                {
                    string language = lang.ToLower();
                    if (language.Contains('-'))
                    {
                        if (TryWriteFile(resDir, language, fileName, aWriter))
                        {
                            return;
                        }
                    }
                    else
                    {
                        string[] paths = Directory.GetDirectories(resDir);
                        foreach (string path in paths)
                        {
                            int index = path.LastIndexOf(Path.DirectorySeparatorChar);
                            string dir = path.Substring(index + 1);
                            if (dir.StartsWith(language))
                            {
                                if (TryWriteFile(resDir, dir, fileName, aWriter))
                                {
                                    return;
                                }
                            }
                        }
                    }
                }
            }
            TryWriteFile(fullPath, aWriter);
        }

        void WriteNodeJs(IResourceWriter aWriter)
        {
            MemoryStream memStream = new MemoryStream();
            StreamWriter writer = new StreamWriter(memStream);
            writer.Write("var nodeUdn = \"" + iUdn + "\";\n");
            writer.Write("var webSocketPort = " + iWebSocketPort + ";\n");
            writer.Write("var isRemote = false;\n");
            writer.Flush();
            aWriter.WriteResourceBegin((int)memStream.Length, "application/x-javascript");
            aWriter.WriteResource(memStream.GetBuffer(), (int)memStream.Length);
            aWriter.WriteResourceEnd();
            writer.Close();
        }

        static bool IsNodeJsPath(string aUriTail)
        {
            if (aUriTail.StartsWith("/"))
            {
                aUriTail = aUriTail.Substring(1);
            }
            bool result = aUriTail == "Node.js";
            return result;
        }

        private static bool TryWriteFile(string aResDir, string aLangDir, string aFileName, IResourceWriter aWriter)
        {
            string fullPath = Path.Combine(Path.Combine(aResDir, aLangDir), aFileName);
            return TryWriteFile(fullPath, aWriter);
        }
        private static bool TryWriteFile(string aFullName, IResourceWriter aWriter)
        {
            if (!File.Exists(aFullName))
                return false;
            string mime = MimeFromFileName(aFullName);
            byte[] data = File.ReadAllBytes(aFullName);
            aWriter.WriteResourceBegin(data.Length, mime);
            aWriter.WriteResource(data, data.Length);
            aWriter.WriteResourceEnd();
            return true;
        }

        private static string MimeFromFileName(string aName)
        {
            string[,] mimeMappings = new [,]{{".html", "text/html"}
                                            ,{".htm",  "text/html"}
                                            ,{".jpg",  "image/jpeg"}
                                            ,{".jpeg", "image/jpeg"}
                                            ,{".gif",  "image/gif"}
                                            ,{".png",  "image/png"}
                                            ,{".bmp",  "image/bmp"}
                                            ,{".xml",  "application/xml"}
                                            ,{".js",   "application/x-javascript"}
                                            ,{".css",  "text/css"}};
            string ext = Path.GetExtension(aName);
            for (int i = 0; i < mimeMappings.Length / 2; i++)
            {
                if (mimeMappings[i, 0] == ext)
                {
                    return mimeMappings[i, 1];
                }
            }
            return "";
        }
    }
}
