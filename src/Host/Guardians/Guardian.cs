﻿using System;
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
        public void Start(string aToken)
        {
            iPipeChild = PipedSubprocess.ConnectPipedChild(aToken);
            byte[] buffer = new byte[1];
            iPipeChild.FromParent.BeginRead(buffer, 0, 1,
                aAsyncResult =>
                {
                    int bytesRead = iPipeChild.FromParent.EndRead(aAsyncResult);
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
                }, null);
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

        public Guardian()
        {
            MaxFailures = 0;
            FailureWindow = TimeSpan.FromSeconds(60);
            RetryPause = TimeSpan.FromSeconds(5);
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
            IPipedProcessParent pipeParent = PipedSubprocess.SpawnPipedSubprocess(aSpawnFunc);
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