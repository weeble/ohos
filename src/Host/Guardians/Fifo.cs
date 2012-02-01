using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace OpenHome.Os.Host.Guardians
{
    public enum FifoDirection
    {
        Read,
        Write
    };

    interface IPipedProcessParent : IDisposable
    {
        Stream ToChild { get; }
        Stream FromChild { get; }
        Process Child { get; }
    }

    interface IPipedProcessChild : IDisposable
    {
        Stream ToParent { get; }
        Stream FromParent { get; }
    }

    enum PipeStrategy
    {
        Auto,
        AnonymousPipe,
        PosixFifo
    }

    class PipedSubprocess
    {
        public static bool IsPosix
        {
            get
            {
                const int oldUnix = 128;
                const int newUnix = 4;
                const int macOsx = 6;
                int p = (int)Environment.OSVersion.Platform;
                return (p == newUnix) || (p == oldUnix) || (p == macOsx);
            }
        }
        public static IPipedProcessParent SpawnPipedSubprocess(Func<string, Process> aSpawnFunc, string aFifoDirectory)
        {
            return SpawnPipedSubprocess(aSpawnFunc, PipeStrategy.Auto, aFifoDirectory);
        }
        public static IPipedProcessParent SpawnPipedSubprocess(Func<string, Process> aSpawnFunc, PipeStrategy aStrategy, string aFifoDirectory)
        {
            if (aStrategy == PipeStrategy.Auto)
            {
                aStrategy = IsPosix ? PipeStrategy.PosixFifo : PipeStrategy.AnonymousPipe;
            }
            if (aStrategy == PipeStrategy.PosixFifo)
            {
                return new FifoParent(aToken => aSpawnFunc("fifo," + aToken), aFifoDirectory);
            }
            if (aStrategy == PipeStrategy.AnonymousPipe)
            {
                return new AnonymousPipeParent(aToken => aSpawnFunc("anon," + aToken));
            }
            throw new ArgumentException("Invalid pipe strategy.");
        }
        public static IPipedProcessChild ConnectPipedChild(string aToken, string aFifoDirectory)
        {
            if (aToken.StartsWith("fifo,"))
            {
                return new FifoChild(aToken.Substring(5), aFifoDirectory);
            }
            if (aToken.StartsWith("anon,"))
            {
                return new AnonymousPipeChild(aToken.Substring(5));
            }
            throw new ArgumentException("Unrecognized token.");
        }

        class AnonymousPipeParent : IPipedProcessParent
        {
            public Stream ToChild { get; private set; }
            public Stream FromChild { get; private set; }
            string Token { get; set; }
            public Process Child { get; private set; }
            public AnonymousPipeParent(Func<string, Process> aSpawnFunc)
            {
                var downPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
                var upPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
                Token = downPipe.GetClientHandleAsString() + "," + upPipe.GetClientHandleAsString();
                Child = aSpawnFunc(Token);
                downPipe.DisposeLocalCopyOfClientHandle();
                upPipe.DisposeLocalCopyOfClientHandle();
                ToChild = downPipe;
                FromChild = upPipe;
            }

            public void Dispose()
            {
                ToChild.Dispose();
                FromChild.Dispose();
                Child.Dispose();
            }
        }

        class AnonymousPipeChild : IPipedProcessChild
        {
            public Stream FromParent { get; private set; }
            public Stream ToParent { get; private set; }
            public AnonymousPipeChild(string aToken)
            {
                string[] handles = aToken.Split(',');
                if (handles.Length != 2)
                {
                    throw new ArgumentException("aToken must be of form '999,999'.");
                }
                FromParent = new AnonymousPipeClientStream(PipeDirection.In, handles[0]);
                ToParent = new AnonymousPipeClientStream(PipeDirection.Out, handles[1]);
            }

            public void Dispose()
            {
                FromParent.Dispose();
                ToParent.Dispose();
            }
        }

        class FifoParent : IPipedProcessParent
        {
            readonly Random iRng = new Random();
            public Stream ToChild { get; private set; }
            public Stream FromChild { get; private set; }
            string Token { get; set; }
            public Process Child { get; private set; }
            public FifoParent(Func<string, Process> aSpawnFunc, string aFifoDirectory)
            {
                Token = Process.GetCurrentProcess().Id.ToString() + "." + iRng.Next(1000000000).ToString().PadLeft(9,'0');
                string downFifoName = Path.Combine(aFifoDirectory, Token + ".down");
                string upFifoName = Path.Combine(aFifoDirectory, Token + ".up");
                if (!Directory.Exists(aFifoDirectory))
                {
                    Directory.CreateDirectory(aFifoDirectory);
                }
                MkFifo(downFifoName);
                MkFifo(upFifoName);
                Child = aSpawnFunc(Token);
                ToChild = WaitForFifo(downFifoName, FifoDirection.Write, 1000);
                FromChild = WaitForFifo(upFifoName, FifoDirection.Read, 1000);
            }

            public void Dispose()
            {
                ToChild.Dispose();
                FromChild.Dispose();
                Child.Dispose();
            }
        }

        class FifoChild : IPipedProcessChild
        {
            public Stream FromParent { get; private set; }
            public Stream ToParent { get; private set; }
            public FifoChild(string aToken, string aFifoDirectory)
            {
                string downFifoName = Path.Combine(aFifoDirectory, aToken + ".down");
                string upFifoName = Path.Combine(aFifoDirectory, aToken + ".up");
                FromParent = WaitForFifo(downFifoName, FifoDirection.Read, 1000);
                ToParent = WaitForFifo(upFifoName, FifoDirection.Write, 1000);
            }

            public void Dispose()
            {
                ToParent.Dispose();
                FromParent.Dispose();
            }
        }

        static void MkFifo(string aFileName)
        {
            Process p = Process.Start("mkfifo", "-m600 "+aFileName);
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                throw new FifoException();
            }
        }

        static Stream WaitForFifo(string aFifoName, FifoDirection aDirection, int aTimeoutMilliseconds)
        {
            FileAccess access = aDirection == FifoDirection.Read ? FileAccess.Read : FileAccess.Write;
            FileStream filestream = null;
            Thread t = new Thread(()=>
                {
                    // This will block until the other end "picks up".
                    try
                    {
                        filestream = new FileStream(aFifoName, FileMode.Open, access, FileShare.ReadWrite);
                    }
                    catch (FileNotFoundException)
                    {
                        filestream = null;
                    }
                });
            t.Start();
            bool joined = t.Join(aTimeoutMilliseconds);
            try
            {
                File.Delete(aFifoName);
            }
            catch (FileNotFoundException)
            {
                // Both sides try to delete the fifo, only one will succeed.
            }
            if (!joined)
            {
                t.Join();
            }
            return filestream;
        }
    }

    [Serializable]
    public class FifoException : Exception
    {
        public FifoException() { }
        public FifoException(string message) : base(message) { }
        public FifoException(string message, Exception inner) : base(message, inner) { }
        protected FifoException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
    }
}
