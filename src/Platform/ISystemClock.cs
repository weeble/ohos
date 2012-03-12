using System;
using System.Collections.Generic;

namespace OpenHome.Os.Platform
{
    public interface ISystemClock
    {
        event Action SystemClockChanged;
        DateTime Now { get; set; }
        TimeZoneInfo TimeZone { get; set; }
        IEnumerable<TimeZoneInfo> AvailableTimeZones { get; }
    }
}