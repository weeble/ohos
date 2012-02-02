namespace OpenHome.Os.AppManager
{
    /// <summary>
    /// Manages the ohOs store directory.
    /// </summary>
    public interface IStoreDirectory
    {
        void EnsureAppDirectoryExists(string aAppName);
        void DeleteAppDirectory(string aAppName);
        string GetAbsolutePathForAppDirectory(string aAppName);
    }
}