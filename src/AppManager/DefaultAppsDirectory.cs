using System;
using System.IO;
using System.Reflection;
using ICSharpCode.SharpZipLib.Zip;
using OpenHome.Os.Platform;

namespace OpenHome.Os.AppManager
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
            if (aName.Split(Path.GetInvalidFileNameChars()).Length > 1)
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
            if (Path.GetFullPath(appDirParent) != Path.GetFullPath(iDirectoryName))
            {
                throw new PluginFoundInWrongDirectoryException(
                    String.Format("Assembly not in add directory: {0} in {1}", aAssembly.FullName, appDir));
            }
            return appDirName;
        }

        public string GetAbsolutePathForSubdirectory(string aName)
        {
            ValidateSubdirectoryName(aName);
            return Path.Combine(iDirectoryName, aName);
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