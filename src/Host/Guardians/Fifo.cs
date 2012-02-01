/*using System;
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
        IPipedProcessParent SpawnPipedSubprocess(Func<string, Process> aSpawnFunc)
        {
            if (IsPosix)
            {
                return new FifoParent(aToken => aSpawnFunc("fifo," + aToken));
            }
            else
            {
                return new AnonymousPipeParent(aToken => aSpawnFunc("anon," + aToken));
            }
        }
        IPipedProcessChild ConnectPipedChild(string aToken)
        {
            if (aToken.StartsWith("fifo,"))
            {
                return new FifoChild(aToken.Substring(5));
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
            public FifoParent(Func<string, Process> aSpawnFunc)
            {
                Token = Process.GetCurrentProcess().Id.ToString() + "." + iRng.Next(1000000000).ToString().PadLeft(9,'0');
                MkFifo(Token + ".down");
                MkFifo(Token + ".up");
                Child = aSpawnFunc(Token);
                ToChild = WaitForFifo(Token + ".down", FifoDirection.Write, 1000);
                FromChild = WaitForFifo(Token + ".up", FifoDirection.Read, 1000);
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
            public FifoChild(string aToken)
            {
                FromParent = WaitForFifo(aToken + ".down", FifoDirection.Read, 1000);
                ToParent = WaitForFifo(aToken + ".up", FifoDirection.Write, 1000);
            }

            public void Dispose()
            {
                ToParent.Dispose();
                FromParent.Dispose();
            }
        }

        static void MkFifo(string aFileName)
        {
            Process p = Process.Start("mkfifo", aFileName);
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
                        filestream = new FileStream(aFifoName, FileMode.Open, access);
                    }
                    catch (FileNotFoundException)
                    {
                        filestream = null;
                    }
                });
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
*/






using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace OpenHome.Os.Host.Guardians
{
/*
    public class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("*:"+string.Join(",",args));
            if (args.Length==0)
            {
                IPipedProcessParent parent = PipedSubprocess.SpawnPipedSubprocess(token=>
                {
                    var startInfo = new ProcessStartInfo(
                        System.Reflection.Assembly.GetExecutingAssembly().Location,
                        token)
                        {
                            UseShellExecute = false,
                        };
                    return Process.Start(startInfo);
                });
                using (var reader = new StreamReader(parent.FromChild))
                {
                    Console.WriteLine("P:"+reader.ReadLine());
                }
                using (var writer = new StreamWriter(parent.ToChild))
                {
                    writer.WriteLine("foobar");
                    writer.Flush();
                }
                parent.Child.WaitForExit();
                Console.WriteLine("P:"+parent.Child.ExitCode);
                return 2;
            }
            else
            {
                IPipedProcessChild child = PipedSubprocess.ConnectPipedChild(args[0]);
                using (var writer = new StreamWriter(child.ToParent))
                {
                    writer.WriteLine("xyzzy");
                    writer.Flush();    
                }
                using (var reader = new StreamReader(child.FromParent))
                {
                    Console.WriteLine("C:"+reader.ReadLine());
                }
                return 44;
            }
        }
    }
*/
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
        public static IPipedProcessParent SpawnPipedSubprocess(Func<string, Process> aSpawnFunc)
        {
            return SpawnPipedSubprocess(aSpawnFunc, PipeStrategy.Auto);
        }
        public static IPipedProcessParent SpawnPipedSubprocess(Func<string, Process> aSpawnFunc, PipeStrategy aStrategy)
        {
            if (aStrategy == PipeStrategy.Auto)
            {
                aStrategy = IsPosix ? PipeStrategy.PosixFifo : PipeStrategy.AnonymousPipe;
            }
            if (aStrategy == PipeStrategy.PosixFifo)
            {
                return new FifoParent(aToken => aSpawnFunc("fifo," + aToken));
            }
            if (aStrategy == PipeStrategy.AnonymousPipe)
            {
                return new AnonymousPipeParent(aToken => aSpawnFunc("anon," + aToken));
            }
            throw new ArgumentException("Invalid pipe strategy.");
        }
        public static IPipedProcessChild ConnectPipedChild(string aToken)
        {
            if (aToken.StartsWith("fifo,"))
            {
                return new FifoChild(aToken.Substring(5));
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
            public FifoParent(Func<string, Process> aSpawnFunc)
            {
                Token = Process.GetCurrentProcess().Id.ToString() + "." + iRng.Next(1000000000).ToString().PadLeft(9,'0');
                MkFifo(Token + ".down");
                MkFifo(Token + ".up");
                Child = aSpawnFunc(Token);
                ToChild = WaitForFifo(Token + ".down", FifoDirection.Write, 1000);
                FromChild = WaitForFifo(Token + ".up", FifoDirection.Read, 1000);
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
            public FifoChild(string aToken)
            {
                FromParent = WaitForFifo(aToken + ".down", FifoDirection.Read, 1000);
                ToParent = WaitForFifo(aToken + ".up", FifoDirection.Write, 1000);
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
                        filestream = new FileStream(aFifoName, FileMode.Open, access);
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