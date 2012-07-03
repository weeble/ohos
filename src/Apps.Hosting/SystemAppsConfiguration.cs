using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenHome.Os.Platform;

namespace OpenHome.Os.Apps
{
    public class SystemApp
    {
        public string Name { get; private set; }
        public bool AutomaticDownload { get; private set; }
        public string DownloadUrl { get; private set; }

        public SystemApp(string aName, bool aAutomaticDownload, string aDownloadUrl)
        {
            Name = aName;
            AutomaticDownload = aAutomaticDownload;
            DownloadUrl = aDownloadUrl;
        }
    }

    public interface ISystemAppsConfiguration
    {
        IEnumerable<SystemApp> Apps { get; }
    }

    class SystemAppsConfiguration : ISystemAppsConfiguration
    {
        public IEnumerable<SystemApp> Apps
        {
            get; private set;
        }

        public SystemAppsConfiguration(IConfigFileCollection aConfigFiles)
        {
            var elements = aConfigFiles.GetAllElements(aElement => new []{ aElement });
            List<SystemApp> apps = new List<SystemApp>();
            foreach (var element in elements)
            {
                string name = (string)element.Element("name");
                bool automaticDownload = (bool?)element.Element("automatic-download") ?? false;
                string url = (string)element.Element("download-url") ?? "";
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }
                apps.Add(
                    new SystemApp(name, automaticDownload, url));
            }
            Apps = apps;
        }
    }
}
