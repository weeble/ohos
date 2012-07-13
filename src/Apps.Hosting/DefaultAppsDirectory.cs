using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{
    public class DefaultAppsDirectory : IAppsDirectory
    {
        readonly string iDirectoryName;

        public DefaultAppsDirectory(string aDirectoryName)
        {
            iDirectoryName = aDirectoryName;
        }

        static void ValidateSubdirectoryName(string aName)
        {
            if (aName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException("Invalid directory name characters.");
            }
        }
        public bool DoesSubdirectoryExist(string aName)
        {
            ValidateSubdirectoryName(aName);
            return Directory.Exists(Path.Combine(iDirectoryName, aName));
        }

        public void DeleteSubdirectory(string aName, bool aRecursive)
        {
            ValidateSubdirectoryName(aName);
            Directory.Delete(Path.Combine(iDirectoryName, aName), aRecursive);
        }

        public void InstallZipFile(string aZipFile)
        {
            var unzipper = new FastZip();
            unzipper.ExtractZip(aZipFile, iDirectoryName, "");
        }

        public string GetAssemblySubdirectory(Assembly aAssembly)
        {
            string appDir = Path.GetDirectoryName(GetAssemblyCodeBasePath(aAssembly));
            string appDirParent = Path.GetDirectoryName(appDir);
            string appDirName = Path.GetFileName(appDir);
            // MEF has an annoying habit of "normalizing" all paths to uppercase.
            // We use Directory.GetDirectories here to retrieve the actual canonical
            // casing from the filesystem.
            string caseFixedDirName = Path.GetFileName(Directory.GetDirectories(appDirParent, appDirName)[0]);
            if (Path.GetFullPath(appDirParent).ToLowerInvariant() != Path.GetFullPath(iDirectoryName).ToLowerInvariant())
            {
                throw new PluginFoundInWrongDirectoryException(
                    String.Format("Assembly not in add directory: {0} in {1}", aAssembly.FullName, appDir));
            }
            return caseFixedDirName;
        }

        public string GetAbsolutePathForSubdirectory(string aName)
        {
            ValidateSubdirectoryName(aName);
            return Path.Combine(iDirectoryName, aName);
        }

        public IEnumerable<string> GetAppSubdirectories()
        {
            if (!Directory.Exists(iDirectoryName))
            {
                yield break;
            }
            foreach (string dir in Directory.EnumerateDirectories(iDirectoryName).Select(x=>Path.GetFileName(x)))
            {
                if (dir.Contains("~")) continue;
                if (dir.StartsWith(".")) continue;
                if (dir.StartsWith("_")) continue;
                if (dir.StartsWith("addin-")) continue;
                yield return dir;
            }
        }

        private static string GetAssemblyCodeBasePath(Assembly aAssembly)
        {
            // Note: different from Location when shadow-copied. This will return
            // the location the file was copied *from*, while Location would return
            // the shadow copy's path.
            return new Uri(aAssembly.CodeBase).LocalPath;
        }
    }
}