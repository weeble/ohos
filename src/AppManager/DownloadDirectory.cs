using System;
using System.IO;

namespace OpenHome.Os.AppManager
{
    public interface IDownloadDirectory
    {
        void Clear();
        FileStream CreateFile();
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
            foreach (var fname in Directory.GetFiles(iPath))
            {
                File.Delete(fname);
            }
        }

        public FileStream CreateFile()
        {
            string filepath = Path.Combine(iPath, Guid.NewGuid() + ".download");
            return File.Create(filepath);
        }
    }
}