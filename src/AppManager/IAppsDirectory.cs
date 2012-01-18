using System;
using System.Reflection;

namespace OpenHome.Os.AppManager
{
    /// <summary>
    /// Manages the directory where ohOs stores installed apps.
    /// </summary>
    public interface IAppsDirectory
    {
        bool DoesSubdirectoryExist(string aName);
        void DeleteSubdirectory(string aName, bool aRecursive);
        void InstallZipFile(string aZipFile);
        string GetAssemblySubdirectory(Assembly aAssembly);
        string GetAbsolutePathForSubdirectory(string aAppDirName);
    }
}