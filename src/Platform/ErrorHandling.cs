using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenHome.Widget.Utils
{
    public static class ErrorHandling
    {
        [DllImport("Kernel32")]
        private static extern UInt32 SetErrorMode(UInt32 uMode);

        private const UInt32 SEM_FAILCRITICALERRORS = 0x0001;
        private const UInt32 SEM_NOGPFAULTERRORBOX = 0x0002;
        private const UInt32 SEM_NOOPENFILEERRORBOX = 0x8000;

        public static void SuppressWindowsErrorDialogs()
        {
            SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += ExceptionHandler;
        }

        static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = (Exception) args.ExceptionObject;
            int pid = Process.GetCurrentProcess().Id;
            string exceptionReport = exception.ToString();
            string prefix = String.Format("{0}: ", pid);
            string prefixedReport = prefix + exceptionReport.Replace("\n", "\n" + prefix);
            Console.Error.WriteLine(prefix + "Unhandled exception\n");
            Console.Error.WriteLine(prefixedReport);
            Console.Error.WriteLine();
            Console.Error.WriteLine(prefix + "Terminating process '" + Environment.CommandLine + "'");
            Environment.Exit(77);
        }
    }
}
