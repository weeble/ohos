using System;
using System.IO;

namespace OpenHome.Os.Apps
{
    public class DefaultStoreDirectory : IStoreDirectory
    {
        const string AppsDirectory = "apps";
        readonly string iDirectory;

        public DefaultStoreDirectory(string aDirectory)
        {
            iDirectory = aDirectory;
        }

        static void ValidateSubdirectoryName(string aAppName)
        {
            if (aAppName.Split(Path.GetInvalidFileNameChars()).Length > 1)
            {
                throw new ArgumentException("Invalid directory name characters.");
            }
        }

        public void EnsureAppDirectoryExists(string aAppName)
        {
            string path = GetAbsolutePathForAppDirectory(aAppName);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public void DeleteAppDirectory(string aAppName)
        {
            string path = GetAbsolutePathForAppDirectory(aAppName);
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }

        public string GetAbsolutePathForAppDirectory(string aAppName)
        {
            ValidateSubdirectoryName(aAppName);
            return Path.Combine(Path.Combine(iDirectory, AppsDirectory), aAppName);
        }
    }
}