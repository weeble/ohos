using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenHome.Net.Device;

namespace OpenHome.Os.Platform.Clock
{
    public interface ISystemClockProvider
    {
        void GetTime(IDvInvocation aInvocation, out string aUtcTime, out string aLocalOffset, out string aTimeZoneName);
        void GetAvailableTimeZones(IDvInvocation aInvocation, out string aTimeZoneList);
        void SetTimeZone(IDvInvocation aInvocation, string aTimeZoneName);
        void SetUtcTime(IDvInvocation aInvocation, string aTime);
    }

    /// <summary>
    /// Wraps an ISystemClock in order to handle instructions passed over UPnP.
    /// </summary>
    public class SystemClockProvider : ISystemClockProvider
    {
        private const string kTimeFormat = "yyyy-MM-ddTHH:mm:ss";
        private readonly ISystemClock iSystemClock;

        public SystemClockProvider(ISystemClock aSystemClock)
        {
            iSystemClock = aSystemClock;
        }

        public void GetAvailableTimeZones(IDvInvocation aInvocation, out string aTimeZoneList)
        {
            aTimeZoneList = TimeZoneSerializer.TimeZoneListToXml(iSystemClock.AvailableTimeZones, iSystemClock.Now).ToString();
        }

        public void GetTime(IDvInvocation aInvocation, out string aUtcTime, out string aLocalOffset, out string aTimeZoneName)
        {
            DateTime now = iSystemClock.Now;
            aUtcTime = now .ToString(kTimeFormat);
            var timeZone = iSystemClock.TimeZone;
            TimeSpan offset;
            try
            {
                offset = timeZone.GetUtcOffset(now);
            }
            catch (NullReferenceException)
            {
                // Work around a Mono bug. See https://github.com/mono/mono/pull/72
                offset = new TimeSpan(0,0,0);
            }
            aLocalOffset = TimeZoneSerializer.OffsetToString(offset);
            aTimeZoneName = timeZone.DisplayName;
        }

        public void SetTimeZone(IDvInvocation aInvocation, string aTimeZoneName)
        {
            List<TimeZoneInfo> timeZones = iSystemClock.AvailableTimeZones.ToList();
            TimeZoneInfo tzi = timeZones.FirstOrDefault(aTzi => aTzi.DisplayName == aTimeZoneName);
            if (tzi == null)
                throw new ActionError("Unrecognized time zone name.");
            iSystemClock.TimeZone = tzi;
        }

        public void SetUtcTime(IDvInvocation aInvocation, string aTime)
        {
            DateTime parsedTime;
            bool ok = DateTime.TryParseExact(aTime, kTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal, out parsedTime);
            if (!ok)
                throw new ActionError("Unrecognized time.");
            try
            {
                iSystemClock.Now = parsedTime;
            }
            catch (InvalidOperationException ioe)
            {
                throw new ActionError(ioe.Message);
            }
        }

    }
}