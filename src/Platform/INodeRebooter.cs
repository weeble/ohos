using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Os.Platform
{
    public interface INodeRebooter
    {
        void RebootNode();
        void SoftRestartNode();
    }
}
