using System;

namespace OpenHome.Os.Platform
{
    public interface ICommandRegistry
    {
        void AddCommand(string aName, Action<string> aAction, string aDescription);
    }
}
