using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace OpenHome.Os.Platform.Clock
{
    class TimeZoneSerializer
    {
        public static string OffsetToString(TimeSpan aTimeSpan)
        {
            string sign = aTimeSpan.CompareTo(TimeSpan.Zero) >= 0 ? "+" : "-";
            string hours = Math.Abs(aTimeSpan.Hours).ToString();
            string minutes = Math.Abs(aTimeSpan.Minutes).ToString("00");
            return sign + hours + ":" + minutes;
        }
        public static XElement TimeZoneToXml(TimeZoneInfo aTimeZoneInfo, DateTime aNow)
        {
            return new XElement("timezone",
                new XElement("name", aTimeZoneInfo.DisplayName),
                new XElement("daylightName", aTimeZoneInfo.DaylightName),
                new XElement("standardName", aTimeZoneInfo.StandardName),
                new XElement("hasDaylight", aTimeZoneInfo.SupportsDaylightSavingTime ? "yes" : "no"),
                new XElement("currentOffset", OffsetToString(aTimeZoneInfo.GetUtcOffset(aNow))),
                new XElement("isDaylight", aTimeZoneInfo.IsDaylightSavingTime(aNow) ? "yes" : "no"));
        }
        public static XElement TimeZoneListToXml(IEnumerable<TimeZoneInfo> aTimeZoneInfos, DateTime aNow)
        {
            return new XElement("timezoneList",
                aTimeZoneInfos.Select(aZone=>TimeZoneToXml(aZone, aNow)).ToArray());
        }
    }
}