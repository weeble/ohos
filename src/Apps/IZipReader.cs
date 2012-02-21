using System;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip;

namespace OpenHome.Os.Apps
{
    public interface IZipReader
    {
        IZipContent Open(string aZipName);
        //void ExtractAll(string aDestination);
    }

    public interface IZipContent : IEnumerable<ZipEntry>, IDisposable
    {
    }
}