using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;

namespace OpenHome.Os.Apps
{
    public interface IZipVerifier
    {
        /// <summary>
        /// Verify that the plugin installs to a single subdirectory,
        /// and return the name of that subdirectory.
        /// </summary>
        /// <param name="aZipFile"></param>
        /// <returns></returns>
        string VerifyPluginZip(string aZipFile);
    }

    public class ZipVerifier : IZipVerifier
    {
        readonly IZipReader iZipReader;

        public ZipVerifier(IZipReader aZipReader)
        {
            iZipReader = aZipReader;
        }

        /// <summary>
        /// Verify that the plugin installs to a single subdirectory,
        /// and return the name of that subdirectory.
        /// </summary>
        /// <param name="aZipFile"></param>
        /// <returns></returns>
        public string VerifyPluginZip(string aZipFile)
        {
            var zf = iZipReader.Open(aZipFile);
            HashSet<string> topLevelDirectories = new HashSet<string>();
            try
            {
                foreach (ZipEntry entry in zf)
                {
                    string fname = entry.Name;
                    Debug.Assert(fname != null); // Zip library should assure this.
                    if (Path.IsPathRooted(fname))
                    {
                        throw new BadPluginException("Bad plugin: contains absolute paths.");
                    }

                    string topLevelDirectory = VerifyPluginZipEntry(fname);
                    topLevelDirectories.Add(topLevelDirectory);
                }
            }
            catch (NotSupportedException)
            {
                throw new BadPluginException("Bad plugin: filenames contain illegal characters.");
            }
            if (topLevelDirectories.Count != 1)
            {
                throw new BadPluginException("Bad plugin: doesn't have exactly 1 subdirectory.");
            }
            return topLevelDirectories.First();
        }

        /// <summary>
        /// Verify that the given filename in a plugin zip-file:
        ///     1. Isn't absolute.
        ///     2. Contains no ".." segments.
        ///     3. Is a file (not a directory).
        ///     4. Isn't a file at the top-level.
        /// </summary>
        /// <param name="aFname"></param>
        /// <returns>The top-level directory that contains the file.</returns>
        static string VerifyPluginZipEntry(string aFname)
        {
            string path = aFname;
            bool isTerminalComponent = true;
            while (true)
            {
                string component = Path.GetFileName(path);
                Debug.Assert(component != null);
                // Path.GetFileName can only return null
                // if path is null. (Which it's not.)

                if (component==".." || component==".")
                {
                    throw new BadPluginException("Bad plugin: contains special path components.");
                }

                string parent = Path.GetDirectoryName(path);
                Debug.Assert(parent != null);
                // Path.GetDirectoryName can only return
                // null if path is null or a root
                // directory. (It's not.)

                if (parent=="")
                {
                    if (component=="")
                    {
                        // Zip files use entries like "foo\" to indicate an empty
                        // directory called foo. The top level directory should not
                        // be empty.
                        throw new BadPluginException("Bad plugin: empty directory entry.");
                    }
                    if (isTerminalComponent)
                    {
                        throw new BadPluginException("Bad plugin: contains file at top-level.");
                    }
                    return component;
                }
                path = parent;
                isTerminalComponent = false;
            }
        }
    }
}