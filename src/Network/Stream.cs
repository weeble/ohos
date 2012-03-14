using System;

namespace OpenHome.Os.Network
{

    public class ReaderError : Exception
    {
    }

    public class WriterError : Exception
    {
    }

    public interface IReader
    {
        byte[] Read(int aBytes);
        byte[] ReadUntil(byte aSeparator);
        void ReadFlush();
    };

    public interface IReaderSource
    {
        int Read(byte[] aBuffer, int aOffset, int aMaxBytes); // returns bytes read
        void ReadFlush();
    };

    public interface IWriter
    {
        void Write(byte aValue);
        void Write(byte[] aBuffer);
        void WriteFlush();
    };

    public class Sxx
    {
        protected Sxx(int aMaxBytes)
        {
            iMaxBytes = aMaxBytes;
            iBuffer = new byte[iMaxBytes];
            iBytes = 0;
        }

        public byte[] Buffer()
        {
            byte[] result = new byte[iBytes];
            Array.Copy(iBuffer, 0, result, 0, iBytes);
            return result;
        }

        protected int iMaxBytes;
        protected byte[] iBuffer;
        protected int iBytes;
    };

    public class Srb : Sxx, IReader
    {
        private readonly IReaderSource iSource;
        private int iOffset;

        public Srb(int aMaxBytes, IReaderSource aSource)
            : base(aMaxBytes)
        {
            iSource = aSource;
            iOffset = 0;
        }

        public byte[] Read(int aBytes)
        {
            if (iBytes - iOffset < aBytes)
            {  // read not satisfied from data already in the buffer
                if (aBytes > iMaxBytes)
                { // unable to store the requested number of bytes in the buffer
                    throw (new ReaderError());
                }
                if (iMaxBytes - iOffset < aBytes)
                { // unable to fit requested bytes after current offset
                    Array.Copy(iBuffer, iOffset, iBuffer, 0, iBytes - iOffset); // so make some more room
                    iBytes -= iOffset;
                    iOffset = 0;
                }
                while (iBytes - iOffset < aBytes)
                { // collect more data from the source
                    iBytes += iSource.Read(iBuffer, iBytes, iMaxBytes - iBytes);
                }
            }
            byte[] result = new byte[aBytes];
            Array.Copy(iBuffer, iOffset, result, 0, aBytes);
            iOffset += aBytes;
            return result;
        }

        public byte[] ReadUntil(byte aSeparator)
        {
            int start = iOffset;
            int current = start;
            int count = 0;
            int remaining = iBytes - iOffset;
            while (true)
            {
                while (remaining > 0)
                {
                    if (iBuffer[current++] == aSeparator)
                    {
                        byte[] result = new byte[count];
                        Array.Copy(iBuffer, start, result, 0, count);
                        iOffset += count + 1; // skip over the separator
                        return result;
                    }
                    count++;
                    remaining--;
                }
            
                // separator not found in current buffer
                if (iOffset > 0)
                {   // so move everything down
                    start -= iOffset;
                    iBytes -= iOffset;
                    current -= iOffset;
                    if (iBytes > 0)
                    {
                        Array.Copy(iBuffer, iOffset, iBuffer, 0, iBytes); // so make some more room
                    }
                    iOffset = 0;
                }
                if (iBytes >= iMaxBytes)
                { // buffer full and no separator
                    throw (new ReaderError());
                }
                
                int additional = iSource.Read(iBuffer, iBytes, iMaxBytes - iBytes);
                if (additional == 0) // no more data to read
                    throw (new ReaderError());
                iBytes += additional;
                remaining += additional;
            }
        }

        public void ReadFlush()
        {
            iBytes = 0;
            iOffset = 0;
            iSource.ReadFlush();
        }
    };

    public class Swb : Sxx, IWriter
    {
        private readonly IWriter iWriter;

        public Swb(int aMaxBytes, IWriter aWriter)
            : base(aMaxBytes)
        {
            iWriter = aWriter;
        }

        public void Write(byte aValue)
        {
            if (iBytes >= iMaxBytes)
            { // would overflow, flush the buffer
                byte[] buffer = new byte[iBytes];
                Array.Copy(iBuffer, 0, buffer, 0, iBytes);
                iWriter.Write(buffer);
                iBytes = 0;
            }
            iBuffer[iBytes++] = aValue;
        }

        public void Write(byte[] aBuffer)
        {
            int bytes = aBuffer.Length;

            if (iBytes + bytes > iMaxBytes)
            { // would overflow, flush the buffer
                byte[] buffer = new byte[iBytes];
                Array.Copy(iBuffer, 0, buffer, 0, iBytes);
                iWriter.Write(buffer);
                iBytes = 0;

                if (bytes > iMaxBytes)
                { // would still overflow
                    iWriter.Write(aBuffer); // pass it on
                    return;
                }
            }

            Array.Copy(aBuffer, 0, iBuffer, iBytes, bytes);
            iBytes += bytes;
        }

        public void WriteFlush()
        {
            if (iBytes > 0)
            {
                byte[] buffer = new byte[iBytes];
                Array.Copy(iBuffer, 0, buffer, 0, iBytes);
                iWriter.Write(buffer);
                iBytes = 0;
                iWriter.WriteFlush();
            }
        }
    };
}