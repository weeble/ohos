using System;
using System.Text;

//     0   1   2   3   4   5   6   7   8   9   A   B   C   D   E   F
// 0  NUL SOH STX ETX EOT ENQ ACK BEL BS  HT  LF  VT  FF  CR  SO  SI
// 1  DLE DC1 DC2 DC3 DC4 NAK SYN ETB CAN EM  SUB ESC FS  GS  RS  US
// 2   SP  !   "   #   $   %   &   '   (   )   *   +   ,   -   .   /
// 3   0   1   2   3   4   5   6   7   8   9   :   ;   <   =   >   ?
// 4   @   A   B   C   D   E   F   G   H   I   J   K   L   M   N   O
// 5   P   Q   R   S   T   U   V   W   X   Y   Z   [   \   ]   ^   _
// 6   `   a   b   c   d   e   f   g   h   i   j   k   l   m   n   o
// 7   p   q   r   s   t   u   v   w   x   y   z   {   |   }   ~ DEL

namespace OpenHome.Os.Network
{
    internal class AsciiError : Exception
    {
    }

    public interface IWriterAscii : IWriter
    {
        void WriteSpace();
        void WriteNewline();
        void WriteInt(int aValue);
        void WriteUint(uint aValue);
        void WriteInt64(long aValue);
        void WriteUint64(ulong aValue);
        void WriteHex(uint aValue);
        void WriteHex(byte aValue);
        void WriteHexPrefix();
    }

    internal class Ascii
    {
        internal const byte kAsciiNul = 0x00;
        internal const byte kAsciiSoh = 0x01;
        internal const byte kAsciiStx = 0x02;
        internal const byte kAsciiEtx = 0x03;
        internal const byte kAsciiEot = 0x04;
        internal const byte kAsciiEnq = 0x05;
        internal const byte kAsciiAck = 0x06;
        internal const byte kAsciiBel = 0x07;
        internal const byte kAsciiBs = 0x08;
        internal const byte kAsciiHt = 0x09;
        internal const byte kAsciiLf = 0x0a;
        internal const byte kAsciiVt = 0x0b;
        internal const byte kAsciiFf = 0x0c;
        internal const byte kAsciiCr = 0x0d;
        internal const byte kAsciiSo = 0x0e;
        internal const byte kAsciiSi = 0x0f;

        internal const byte kAsciiDle = 0x10;
        internal const byte kAsciiDc1 = 0x11;
        internal const byte kAsciiDc2 = 0x12;
        internal const byte kAsciiDc3 = 0x13;
        internal const byte kAsciiDc4 = 0x14;
        internal const byte kAsciiNak = 0x15;
        internal const byte kAsciiSyn = 0x16;
        internal const byte kAsciiEtb = 0x17;
        internal const byte kAsciiCan = 0x18;
        internal const byte kAsciiEm = 0x19;
        internal const byte kAsciiSub = 0x1a;
        internal const byte kAsciiEsc = 0x1b;
        internal const byte kAsciiFs = 0x1c;
        internal const byte kAsciiGs = 0x1d;
        internal const byte kAsciiRs = 0x1e;
        internal const byte kAsciiUs = 0x1f;

        internal const byte kAsciiSp = 0x20;

        internal const byte kAsciiMinus = 0x2d;
        internal const byte kAsciiDot = 0x2e;
        internal const byte kAsciiColon = 0x3a;
        internal const byte kAsciiEquals = 0x3d;
        internal const byte kAsciiAngleOpen = 0x3c;
        internal const byte kAsciiAngleClose = 0x3e;
        internal const byte kAsciiHyphen = 0x2d;

        internal const byte kAsciiDel = 0x7f;

        internal const string kAsciiNewline = "\r\n";
        internal const string kAsciiHexPrefix = "0x";

        internal static byte[] Trim(byte[] aBuffer)
        {
            int start = 0;
            int bytes = aBuffer.Length;

            for (int i = 0; i < bytes; i++) {
                if (!IsWhitespace(aBuffer[start])) {
                    break;
                }
                start++;
            }

            if (start == bytes) {
                return new byte[0];
            }

            int end = bytes;

            while (IsWhitespace(aBuffer[end - 1])) {
                end--;
            }

            if (start == 0 && end == bytes) {
                return aBuffer;
            }

            byte[] result = new byte[end - start];

            Array.Copy(aBuffer, start, result, 0, end - start);

            return result;
        }

        internal static bool IsWhitespace(byte aValue)
        {
            return aValue <= kAsciiSp;
        }

        internal static uint Uint(byte[] aBuffer)
        {
            uint value;

            try
            {
                value = UInt32.Parse(Encoding.UTF8.GetString(aBuffer, 0, aBuffer.Length));
            }
            catch (FormatException)
            {
                throw (new AsciiError());
            }
            catch (OverflowException)
            {
                throw (new AsciiError());
            }

            return value;
        }

        internal static int Int(byte[] aBuffer)
        {
            int value;

            try
            {
                value = Int32.Parse(Encoding.UTF8.GetString(aBuffer, 0, aBuffer.Length));
            }
            catch (FormatException)
            {
                throw (new AsciiError());
            }
            catch (OverflowException)
            {
                throw (new AsciiError());
            }

            return value;
        }
    }

    internal class WriterAscii : IWriterAscii
    {
        private readonly IWriter iWriter;

        internal WriterAscii(IWriter aWriter)
        {
            iWriter = aWriter;
        }
        public void Write(byte aValue)
        {
            iWriter.Write(aValue);
        }
        public void Write(byte[] aBuffer)
        {
            iWriter.Write(aBuffer);
        }
        public void WriteFlush()
        {
            iWriter.WriteFlush();
        }
        public void WriteSpace()
        {
            iWriter.Write(Ascii.kAsciiSp);
        }
        public void WriteNewline()
        {
            iWriter.Write(Encoding.UTF8.GetBytes(Ascii.kAsciiNewline));
        }
        public void WriteInt(int aValue)
        {
            iWriter.Write(Encoding.UTF8.GetBytes(aValue.ToString()));
        }
        public void WriteUint(uint aValue)
        {
            iWriter.Write(Encoding.UTF8.GetBytes(aValue.ToString()));
        }
        public void WriteInt64(long aValue)
        {
            iWriter.Write(Encoding.UTF8.GetBytes(aValue.ToString()));
        }
        public void WriteUint64(ulong aValue)
        {
            iWriter.Write(Encoding.UTF8.GetBytes(aValue.ToString()));
        }
        public void WriteHex(uint aValue)
        {
            throw new NotImplementedException();
        }
        public void WriteHex(byte aValue)
        {
            throw new NotImplementedException();
        }
        public void WriteHexPrefix()
        {
            iWriter.Write(Encoding.UTF8.GetBytes(Ascii.kAsciiHexPrefix));
        }
    }

}
