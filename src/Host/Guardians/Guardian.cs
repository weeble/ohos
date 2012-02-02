using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using log4net;

namespace OpenHome.Os.Host.Guardians
{
    public enum ExitBehaviour
    {
        Exit,
        Retry,
        Repeat,
    }
    public class GuardianChild
    {
        private object iLock = new object();
        private bool iGuardianTerminated;
        private EventHandler iWhenGuardianTerminates;
        string iFifoDirectory;
        public TimeSpan ConnectionTimeout { get; set; }
        public GuardianChild(string aFifoDirectory)
        {
            ConnectionTimeout = TimeSpan.FromSeconds(5);
            iFifoDirectory = aFifoDirectory;
        }
        public event EventHandler WhenGuardianTerminates
        {
            add
            {
                bool shouldInvoke;
                lock (iLock)
                {
                    if (iGuardianTerminated)
                    {
                        shouldInvoke = true;
                    }
                    else
                    {
                        shouldInvoke = false;
                        iWhenGuardianTerminates += value;
                    }
                }
                if (shouldInvoke)
                {
                    value(this, EventArgs.Empty);
                }
            }
            remove
            {
                lock (iLock)
                {
                    iWhenGuardianTerminates -= value;
                }
            }
        }
        IPipedProcessChild iPipeChild;
        StreamWriter iParentWriter;
        Thread iThread;
        public void Start(string aToken)
        {
            iPipeChild = PipedSubprocess.ConnectPipedChild(aToken, iFifoDirectory, (int)ConnectionTimeout.TotalMilliseconds);
            // Mono's FileStream.BeginRead method is, inexplicably, synchronous.
            // Thus we need to spin up our own thread here.
            iThread = new Thread(()=>
                {
                    using (StreamReader reader = new StreamReader(iPipeChild.FromParent))
                    {
                        reader.ReadToEnd();
                    }
                    EventHandler callback;
                    lock (iLock)
                    {
                        callback = iWhenGuardianTerminates;
                        iGuardianTerminated = true;
                    }
                    if (callback != null)
                    {
                        callback(this, EventArgs.Empty);
                    }
                });
            iThread.IsBackground = true;
            iThread.Start();
            // Note that we never join the thread. We can't guarantee that it
            // will finish, because its synchronous read from the parent pipe
            // may never finish. Instead we set it to be a background thread
            // and rely on it getting cleaned up when the process exits.
            // TODO: Investigate whether we can safely close the FileStream
            // from the main thread to get the background thread to unblock.
            iParentWriter = new StreamWriter(iPipeChild.ToParent);
        }
        public void ReportFatalError(string aMessage)
        {
            iParentWriter.WriteLine(aMessage);
        }
    }
    public class Guardian
    {
        /// <summary>
        /// If more than this number of failures occur during
        /// FailureWindow, the Guardian will give up re-running
        /// the subprocess. Set to 0 to never give up.
        /// </summary>
        public int MaxFailures { get; set; }
        public TimeSpan FailureWindow { get; set; }
        public TimeSpan RetryPause { get; set; }
        public Func<int, ExitBehaviour> WhenChildExitsWithCode { get; set; }
        public TimeSpan ConnectionTimeout { get; set; }
        string iFifoDirectory;

        public Guardian(string aFifoDirectory)
        {
            iFifoDirectory = aFifoDirectory;
            MaxFailures = 0;
            FailureWindow = TimeSpan.FromSeconds(60);
            RetryPause = TimeSpan.FromSeconds(5);
            ConnectionTimeout = TimeSpan.FromSeconds(5);
            WhenChildExitsWithCode = aExitCode => aExitCode == 0 ? ExitBehaviour.Exit : ExitBehaviour.Retry;
        }

        ILog iLogger;
        public ILog Logger
        {
            get
            {
                if (iLogger == null)
                {
                    iLogger = LogManager.GetLogger(typeof(Guardian));
                }
                return iLogger;
            }
            set { iLogger = value; }
        }

        public int Run(Func<string, Process> aSpawnFunc)
        {
            Console.In.Close();

            TimeSpan failureWindow = TimeSpan.FromSeconds(60);
            Queue<DateTime> crashTimes = new Queue<DateTime>(10);
            for (; ; )
            {
                int exitCode = RunChildProcess(aSpawnFunc);
                ExitBehaviour behaviour = WhenChildExitsWithCode(exitCode);
                if (behaviour == ExitBehaviour.Repeat)
                {
                    continue;
                }
                if (behaviour == ExitBehaviour.Exit)
                {
                    return exitCode;
                }
                Logger.FatalFormat("Child terminated with exit code {0}.", exitCode);
                if (MaxFailures > 0)
                {
                    DateTime now = DateTime.UtcNow;
                    crashTimes.Enqueue(now);
                    if (crashTimes.Count == MaxFailures)
                    {
                        DateTime timeOfNthLastCrash = crashTimes.Dequeue();
                        if (now - timeOfNthLastCrash <= failureWindow)
                        {
                            Logger.Fatal("Node crashed too often. Abandoning.");
                            return exitCode;
                        }
                    }
                }
                Thread.Sleep(RetryPause);
            }
        }

        int RunChildProcess(Func<string, Process> aSpawnFunc)
        {
            IPipedProcessParent pipeParent = PipedSubprocess.SpawnPipedSubprocess(aSpawnFunc, iFifoDirectory, (int)ConnectionTimeout.TotalMilliseconds);
            int exitCode;
            using (var reader = new StreamReader(pipeParent.FromChild))
            {
                string errorMessage = reader.ReadToEnd();
                if (errorMessage != "")
                {
                    Logger.FatalFormat("Child reported error:\n" + errorMessage);
                }
                pipeParent.Child.WaitForExit();
                exitCode = pipeParent.Child.ExitCode;
            }
            pipeParent.Dispose();
            return exitCode;
        }
    }
}
