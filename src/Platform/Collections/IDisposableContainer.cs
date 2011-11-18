using System;
using System.Collections.Generic;

namespace OpenHome.Os.Platform.Collections
{
    public interface IDisposableContainer<T> : IDisposable, IEnumerable<T>
    {
    }
}