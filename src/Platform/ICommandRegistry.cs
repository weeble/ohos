using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Os.Platform
{
    public interface ICommandRegistry
    {
        void AddCommand(string aName, Action<string> aAction, string aDescription);
    }
}
