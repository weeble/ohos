using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

namespace OpenHome.Os.Apps
{
    public interface IZipReader
    {
        IEnumerable<ZipEntry> Open(string aZipName);
        //void ExtractAll(string aDestination);
    }
}