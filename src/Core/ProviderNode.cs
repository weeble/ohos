using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using OpenHome.Os.Platform.Clock;

namespace OpenHome.Os.Core
{
    public class ProviderNode : DvProviderOpenhomeOrgNode1
    {
        readonly ISystemClockProvider iSystemClockProvider;
        readonly ILogControlProvider iLogControlProvider;

        public ProviderNode(DvDevice aDevice, ISystemClockProvider aSystemClockProvider, ILogControlProvider aLogControlProvider)
            : base(aDevice)
        {
            EnableActionWriteFile();
            EnableActionGetAvailableTimeZones();
            EnableActionGetTime();
            EnableActionSetTimeZone();
            EnableActionSetUtcTime();
            EnableActionGetRecentLog();
            EnableActionGetLogLevelsXml();
            EnableActionSetLogLevelsXml();
            EnablePropertyTimeSequenceNumber();
            EnablePropertyTimeSequenceNumber();
            SetPropertyTimeSequenceNumber(0);
            iSystemClockProvider = aSystemClockProvider;
            iLogControlProvider = aLogControlProvider;
        }
        protected override void WriteFile(IDvInvocation aInvocation, byte[] aData, string aFileFullName)
        {
            System.IO.File.WriteAllBytes(aFileFullName, aData);
        }
        protected override void GetAvailableTimeZones(IDvInvocation aInvocation, out string aTimeZoneList)
        {
            iSystemClockProvider.GetAvailableTimeZones(out aTimeZoneList);
        }
        protected override void GetTime(IDvInvocation aInvocation, out string aUtcTime, out string aLocalOffset, out string aTimeZoneName)
        {
            iSystemClockProvider.GetTime(out aUtcTime, out aLocalOffset, out aTimeZoneName);
        }
        protected override void SetTimeZone(IDvInvocation aInvocation, string aTimeZoneName)
        {
            iSystemClockProvider.SetTimeZone(aTimeZoneName);
            SetPropertyTimeSequenceNumber(PropertyTimeSequenceNumber() + 1);
        }
        protected override void SetUtcTime(IDvInvocation aInvocation, string aTime)
        {
            iSystemClockProvider.SetUtcTime(aTime);
            SetPropertyTimeSequenceNumber(PropertyTimeSequenceNumber() + 1);
        }
        protected override void GetRecentLog(IDvInvocation aInvocation, out string aLogText)
        {
            iLogControlProvider.GetRecentLog(out aLogText);
        }
        protected override void GetLogLevelsXml(IDvInvocation aInvocation, out string aLogLevelsXml)
        {
            iLogControlProvider.GetLogLevelsXml(out aLogLevelsXml);
        }
        protected override void SetLogLevelsXml(IDvInvocation aInvocation, string aLogLevelsXml)
        {
            iLogControlProvider.SetLogLevelsXml(aLogLevelsXml);
        }
    }
}
