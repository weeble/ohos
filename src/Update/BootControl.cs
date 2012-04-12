using System.IO;
using System.Diagnostics;

namespace OpenHome.Os.Update
{
    public class BootControlSheeva : IBootControl
    {
        private const string kBootModeFile = "/var/run/bootmode";
        private readonly BootMode iCurrentBootMode;

        public class CommandFailureException : System.Exception
        {
            private string iMsg;
            
            public CommandFailureException( string aMsg )
            {
                iMsg = aMsg;
            }

            public override string ToString()
            {
                return string.Format("CommandFailureException: {0}", iMsg);
            }
        }

        public BootControlSheeva()
        {
            if (File.Exists(kBootModeFile))
            {
                string btMode = File.ReadAllText(kBootModeFile).Trim();
                iCurrentBootMode = btMode.Equals("0") ? BootMode.eRfs0 : BootMode.eRfs1;
            }
            else
            {
                iCurrentBootMode = Pending;
                File.WriteAllText(kBootModeFile, (iCurrentBootMode == BootMode.eRfs0) ? "0\n" : "1\n");
            }
        }

        public BootMode Current
        {
            get
            {
                return iCurrentBootMode;
            }
        }
        
        public BootMode Pending
        {            
            get
            {
                byte[] data = new byte[4];
                var psi = new ProcessStartInfo("/usr/local/bin/smarties_get_bootmode")
                              {
                                  UseShellExecute = false,
                                  RedirectStandardOutput = true
                              };

                Process bmProcess = Process.Start(psi);
                Stream s = bmProcess.StandardOutput.BaseStream;
                s.Read(data, 0, 4);

                bmProcess.WaitForExit();

                if (bmProcess.ExitCode != 0)
                    throw new CommandFailureException(psi.FileName);

                if ( data[0] == 0xff    &&
                     data[1] == 0xff    &&
                     data[2] == 0xff    &&
                     data[3] == 0xff )
                    return BootMode.eRfs0;
                return BootMode.eRfs1;
            }
            
            set
            {
                if ( value == Pending )
                    return;   
                                         
                string bmArgs;
                switch ( value )
                {
                    case BootMode.eRfs0:
                    {
                        bmArgs = "0";
                        break;
                    }
                    default:
                    {
                        bmArgs = "1";
                        break;
                    }
                }

                var psi = new ProcessStartInfo("/usr/local/bin/smarties_set_bootmode", bmArgs);
                Process bmProcess = Process.Start(psi);
                bmProcess.WaitForExit();

                if (bmProcess.ExitCode != 0)
                    throw new CommandFailureException(psi.FileName);
            }
        }
    }
}
