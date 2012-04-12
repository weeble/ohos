using System;

namespace OpenHome.Widget.Update
{
    public enum BootMode
    {
        eRfs0,
        eRfs1
    }

    public interface IBootControl
    {
        BootMode Current { get; }
        BootMode Pending { get; set; }
    }
}
