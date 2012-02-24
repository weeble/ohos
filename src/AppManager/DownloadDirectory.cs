using System;
using System.IO;

namespace OpenHome.Os.AppManager
{
    public interface IDownloadDirectory
    {
        void Clear();
        void CreateFile(out FileStream aFile, out string aName);
    }

    public class DownloadDirectory : IDownloadDirectory
    {
        string iPath;
        //XmlDiskStore iStore;

        public DownloadDirectory(string aPath)
        {
            iPath = aPath;
            if (!Directory.Exists(iPath))
                Directory.CreateDirectory(iPath);
            //iStore = aStore;
            //iStore.LoadXmlFiles(
        }

        public void Clear()
        {
            foreach (var fname in Directory.GetFiles(iPath, "*.download"))
            {
                File.Delete(fname);
            }
        }

        public void CreateFile(out FileStream aFile, out string aName)
        {
            string filepath = Path.Combine(iPath, Guid.NewGuid() + ".download");
            aFile = File.Create(filepath);
            aName = filepath;
        }
    }
}