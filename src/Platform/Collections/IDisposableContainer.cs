using System;
using System.Collections.Generic;

namespace OpenHome.Widget.Nodes.Collections
{
    public interface IDisposableContainer<T> : IDisposable, IEnumerable<T>
    {
    }
}