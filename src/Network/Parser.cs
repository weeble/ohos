using System;

namespace OpenHome.Os.Network
{
    internal class Parser
    {
        private byte[] iBuffer;
        private int iIndex;

        internal Parser(byte[] aBuffer)
        {
            Set(aBuffer);
        }

        internal void Set(byte[] aBuffer)
        {
            iBuffer = aBuffer;
            iIndex = 0;
        }

        internal bool Finished()
        {
            return iIndex == iBuffer.Length;
        }

        internal byte[] Next()
        {
            return Next(Ascii.kAsciiSp);
        }

        internal byte[] Next(byte aDelimiter)
        {
            int start = iIndex;
            int bytes = iBuffer.Length;
            while (start < bytes) {
                if (!Ascii.IsWhitespace(iBuffer[start]))
                    break;
                start++;
            }
            if (start == bytes)
                return (new byte[0]);

            int extra = 1;
            int delimiter = start;
            while (delimiter < bytes)
            {
                if (iBuffer[delimiter] == aDelimiter)
                    break;
                delimiter++;
            }

            if (delimiter == bytes) {
                extra = 0;
            }

            int length = delimiter - start;
            int end = delimiter;
            while (length > 0) {
                if (!Ascii.IsWhitespace(iBuffer[--end]))
                {
                    end++;
                    break;
                }
                length--;
            }

            iIndex = delimiter + extra; // go one past delimiter if not end of buffer
            int count = end - start;
            byte[] result = new byte[count];
            Array.Copy(iBuffer, start, result, 0, count);
            return(result);
        }

        internal void Restart()
        {
            iIndex = 0;
        }

        internal byte At(int aOffset)  // relative to current position
        {
            return (iBuffer[iIndex + aOffset]);
        }

        internal void Back(int aOffset) // relative to current position
        {
            iIndex -= aOffset;
        }

        internal void Forward(int aOffset) // relative to current position
        {
            iIndex += aOffset;
        }

        internal byte[] Remaining()
        {
            int count = iBuffer.Length - iIndex;
            byte[] result = new byte[count];
            Array.Copy(iBuffer, iIndex, result, 0, count);
            return (result);
        }
    }
}
