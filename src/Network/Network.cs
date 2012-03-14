using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using log4net;

namespace OpenHome.Os.Network
{
    public class NetworkError : Exception
    {
    }
    
    public class TcpServer
    {
        public IPEndPoint Endpoint { get; set; }
        private TcpListener iListener;
        private static readonly ILog Logger = LogManager.GetLogger(typeof(TcpServer));
        private const int kListenSlots = 100;
        private const int kMinPort = 49832;
        private const int kMaxPort = 49932;

        public TcpServer(IPAddress aInterface)
        {
            if (aInterface == IPAddress.Any)
            {
                IPHostEntry e = Dns.GetHostEntry(Dns.GetHostName());
                foreach (IPAddress i in e.AddressList)
                {   // find an IPV4 IPAddress
                    if (i.AddressFamily == AddressFamily.InterNetwork)
                    {
                        Initialise(i);
                        return;
                    }
                }
                throw new NetworkError();
            }
            Initialise(aInterface);
        }

        private void Initialise(IPAddress aInterface)
        {
            for (int port = kMinPort; port <= kMaxPort; port++)
            {
                // On vista, a new socket has to be created for each attempted bind - if a socket is
                // created and then bound, and the bind fails, attempting to bind the same socket
                // to another port always fails (on vista).
                // Also, the creation of the socket goes outside the try..catch because we want
                // SocketExceptions that occur on creation to be handled by the client code. The handling
                // code for the SocketException here is specifically for the bind.

                try
                {
                    Logger.InfoFormat("TcpServer+    {0}:{1}...", aInterface, port);
                    iListener = new TcpListener(aInterface, port);
                    iListener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
                    iListener.Start(kListenSlots);
                    Endpoint = new IPEndPoint(aInterface, port);
                    Logger.Info("TcpServer initialisation - Success");
                    return;
                }
                catch (SocketException e)
                {
                    Logger.ErrorFormat("TcpServer initialisation Failed ({0})", e.Message);
                }
            }

            throw new NetworkError();
        }

        public void Accept(TcpSessionStream aSession)
        {
            try
            {
                aSession.SetSocket(iListener.AcceptSocket());
            }
            catch (SocketException)
            {
                throw new NetworkError();
            }
            catch (ObjectDisposedException)
            {
                throw new NetworkError();
            }
        }

        public void Shutdown()
        {
            try
            {
                Logger.InfoFormat("TcpServer-    {0}:{1}...", Endpoint.Address, Endpoint.Port);
                iListener.Stop();
                Logger.Info("TcpServer shutdown - Success");
            }
            catch (SocketException e)
            {
                Logger.ErrorFormat("TcpServer shutdown Failed ({0})", e.Message);
                throw new NetworkError();
            }
            catch (ObjectDisposedException e)
            {
                Logger.ErrorFormat("TcpServer shutdown Failed ({0})", e.Message);
                throw new NetworkError();
            }
        }

        public void Close()
        {
            iListener.Server.Close();
        }
    }

    public class TcpStream : IWriter, IReaderSource
    {
        protected TcpStream()
        {
        }

        public void Write(byte aValue)
        {
        }

        public void Write(byte[] aBuffer)
        {
            try
            {
                iSocket.Send(aBuffer);
            }
            catch (SocketException)
            {
                throw new WriterError();
            }
            catch (ObjectDisposedException)
            {
                throw new WriterError();
            }
        }

        public void WriteFlush()
        {
        }

        public int Read(byte[] aBuffer, int aOffset, int aMaxBytes)
        {
            try
            {
                return iSocket.Receive(aBuffer, aOffset, aMaxBytes, SocketFlags.None);
            }
            catch (SocketException)
            {
                throw new ReaderError();
            }
            catch (ObjectDisposedException)
            {
                throw new ReaderError();
            }
        }

        public void ReadFlush()
        {
        }

        public void Connect(EndPoint aEndpoint)
        {

            try
            {
                iSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                iSocket.Connect(aEndpoint);
            }
            catch (SocketException)
            {
                throw new NetworkError();
            }
            catch (ObjectDisposedException)
            {
                throw new NetworkError();
            }
        }

        public void BeginConnect(EndPoint aEndpoint, AsyncCallback aCallback)
        {

            try
            {
                iSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                iSocket.BeginConnect(aEndpoint, aCallback, null);
            }
            catch (SocketException)
            {
                throw new NetworkError();
            }
            catch (ObjectDisposedException)
            {
                throw new NetworkError();
            }
        }

        public void EndConnect(IAsyncResult aAsync)
        {
            try
            {
                iSocket.EndConnect(aAsync);
            }
            catch (SocketException)
            {
                throw new NetworkError();
            }
            catch (ObjectDisposedException)
            {
                throw new NetworkError();
            }
        }


        public void BeginWaitForData(AsyncCallback aCallback)
        {
            byte[] buffer = new byte[0];

            try
            {
                iSocket.BeginReceive(buffer, 0, 0, SocketFlags.None, aCallback, null);
            }
            catch (SocketException)
            {
                throw new NetworkError();
            }
            catch (ObjectDisposedException)
            {
                throw new NetworkError();
            }
        }

        public void EndWaitForData(IAsyncResult aAsync)
        {
            try
            {
                iSocket.EndReceive(aAsync);
            }
            catch (SocketException)
            {
                throw new NetworkError();
            }
            catch (ObjectDisposedException)
            {
                throw new NetworkError();
            }
        }

        public void Close()
        {
            if (iSocket != null)
            {
                iSocket.Close();
            }
        }

        protected Socket iSocket;
    }

    public class TcpSessionStream : TcpStream
    {
        public void SetSocket(Socket aSocket)
        {
            iSocket = aSocket;
        }
    }

    public class TcpClientStream : TcpStream
    {
    }

    public class UdpMulticastStream : IWriter, IReaderSource
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(UdpMulticastStream));
        private readonly IPEndPoint iMulticast;
        private readonly Socket iSocket;
        private bool iReadOpen;
        private bool iWriteOpen;
        private EndPoint iSender;
        private const int kMinPort = 47832;
        private const int kMaxPort = 47932;

        public UdpMulticastStream(IPAddress aInterface, IPAddress aMulticast, int aPort, int aTtl)
        {
            iMulticast = new IPEndPoint(aMulticast, aPort);
            iReadOpen = true;
            iWriteOpen = true;
            iSender = new IPEndPoint(IPAddress.Any, 0);
            for (int port = kMinPort; port <= kMaxPort; port++)
            {
                // On vista, a new socket has to be created for each attempted bind - if a socket is
                // created and then bound, and the bind fails, attempting to bind the same socket
                // to another port always fails (on vista).
                // Also, the creation of the socket goes outside the try..catch because we want
                // SocketExceptions that occur on creation to be handled by the client code. The handling
                // code for the SocketException here is specifically for the bind.

                Logger.InfoFormat("{0}: UdpMulticastStream+    {1}:{2}...", DateTime.Now, aInterface, port);
                try
                {
                    iSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                    iSocket.Bind(new IPEndPoint(aInterface, port));
                    iSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
                    iSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, aTtl);
                    Logger.Info("UdpMulticastStream initialisation - Success");
                    return;
                }
                catch (SocketException e)
                {
                    Logger.ErrorFormat("UdpMulticastStream initialisation Failed ({0})", e.Message);
                }
            }

            throw new NetworkError();
        }

        public EndPoint Sender()
        {
            return iSender;
        }

        public void Write(byte aValue)
        {
            throw new NotImplementedException();
        }

        public void Write(byte[] aBuffer)
        {
            if (iWriteOpen)
            {
                try
                {
                    iSocket.SendTo(aBuffer, iMulticast);
                    iWriteOpen = false;
                }
                catch (SocketException)
                {
                    throw new WriterError();
                }
                catch (ObjectDisposedException)
                {
                    throw new WriterError();
                }
            }
            else
            {
                throw new WriterError();
            }
        }

        public void WriteFlush()
        {
            iWriteOpen = true;
        }

        public int Read(byte[] aBuffer, int aOffset, int aMaxBytes)
        {
            // The reasom why I am using an asynchronous methodology
            // and turning it back into a syncronous function is 
            // because it is more responsive to thread abort requests
            if (iReadOpen)
            {
                iReadOpen = false;
                int count = 0;
                ManualResetEvent semaphore = new ManualResetEvent(false);
                while (count == 0)
                {
                    try
                    {
                        iSocket.BeginReceiveFrom(aBuffer, aOffset, aMaxBytes, SocketFlags.None, ref iSender, delegate(IAsyncResult aResult)
                        {
                            try
                            {
                                count = iSocket.EndReceiveFrom(aResult, ref iSender);
                            }
                            catch (SocketException) { }
                            catch (ObjectDisposedException) { }
                            semaphore.Set();
                        }, null);
                        semaphore.WaitOne();
                    }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) { }
                }

                return count;
            }

            throw new ReaderError();
        }

        public void ReadFlush()
        {
            iReadOpen = true;
        }

        public void Shutdown()
        {
            try
            {
                iSocket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        public void Close()
        {
            try
            {
                iSocket.Close();
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }

    // UdpMulticastReader - Read multicast

    public class UdpMulticastReader : IReaderSource
    {
        private readonly IPEndPoint iMulticast;
        private readonly Socket iSocket;
        private bool iReadOpen;
        private EndPoint iSender;

        public UdpMulticastReader(IPAddress aInterface, IPAddress aMulticast, int aPort)
        {
            iMulticast = new IPEndPoint(aMulticast, aPort);
            try
            {
                iSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                iSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                switch (Environment.OSVersion.Platform)
                {
                    case PlatformID.Unix:
                        // Unix - bind to multicast address
                        iSocket.Bind(iMulticast);
                        break;
                    default:
                        // Windows - bind to local interface
                        iSocket.Bind(new IPEndPoint(aInterface, aPort));
                        break;
                }

                // Join the multicast group
                MulticastOption option = new MulticastOption(aMulticast, aInterface);
                iSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, option);
                iReadOpen = true;
                iSender = new IPEndPoint(IPAddress.Any, 0);
            }
            catch (SocketException)
            {
                throw new NetworkError();
            }
            catch (ObjectDisposedException)
            {
                throw new NetworkError();
            }
        }

        public EndPoint Sender()
        {
            return iSender;
        }

        public int Read(byte[] aBuffer, int aOffset, int aMaxBytes)
        {
            // The reasom why I am using an asynchronous methodology
            // and turning it back into a syncronous function is 
            // because it is more responsive to thread abort requests
            if (iReadOpen)
            {
                iReadOpen = false;
                int count = 0;
                ManualResetEvent semaphore = new ManualResetEvent(false);
                while (count == 0)
                {
                    try
                    {
                        iSocket.BeginReceiveFrom(aBuffer, aOffset, aMaxBytes, SocketFlags.None, ref iSender, delegate(IAsyncResult aResult)
                        {
                            try
                            {
                                count = iSocket.EndReceiveFrom(aResult, ref iSender);
                            }
                            catch (SocketException) { }
                            catch (ObjectDisposedException) { }
                            semaphore.Set();
                        }, null);
                        semaphore.WaitOne();
                    }
                    catch (SocketException) { }
                    catch (ObjectDisposedException) { }
                }

                return count;
            }

            throw new ReaderError();
        }

        public void ReadFlush()
        {
            iReadOpen = true;
        }

        public void Shutdown()
        {
            try
            {
                iSocket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }

        public void Close()
        {
            try
            {
                iSocket.Close();
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
        }
    }
}
