using System;
using System.Collections.Generic;
using System.Diagnostics;
using log4net;

namespace OpenHome.Os.Platform.Clock
{
    /// <summary>
    /// Handles observation and manipulation of the system clock.
    /// </summary>
    public class LinuxSystemClock : ISystemClock
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(LinuxSystemClock));
        private const string kTimeFormat = "yyyy-MM-dd HH:mm:ss";
        
        public DateTime Now
        {
            get { return DateTime.Now; }
            set
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    Logger.InfoFormat("New time: {0}", value.ToUniversalTime());
                    ProcessStartInfo psi = new ProcessStartInfo(
                        "/bin/sh",
                        String.Format(
                            "-c 'set -e & date --utc -s \"{0}\"'",
                            value.ToUniversalTime().ToString(kTimeFormat)+"Z"
                        )
                    ) {RedirectStandardOutput = false, RedirectStandardError = true, UseShellExecute = false};
                    var process = Process.Start(psi);
                    string errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        Logger.ErrorFormat("Failed to set time. Exit code from 'date {0}': {1}\n{2}", psi.Arguments, process.ExitCode, errors);
                        throw new InvalidOperationException("Cannot set time.");
                    }
                }
                else
                {
                    Logger.ErrorFormat("Tried to set time, not running on Unix. New time = {0}", value.ToUniversalTime());
                }
            }
        }

        public TimeZoneInfo TimeZone
        {
            get { return TimeZoneInfo.Local; }
            set
            {
                Logger.ErrorFormat("Tried to set time zone. Not supported. Time zone = {0}", value.DisplayName);
            }
        }

        public IEnumerable<TimeZoneInfo> AvailableTimeZones
        {
            get { return TimeZoneInfo.GetSystemTimeZones(); }
        }
    }
}