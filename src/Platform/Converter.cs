using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHome.Os.Platform
{
    public class Converter
    {
        public static bool BinaryToBoolean(byte[] aBin)
        {
            return BitConverter.ToBoolean(aBin, 0);
        }
        public static int BinaryToInteger(byte[] aBin)
        {
            return System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(aBin, 0));
        }
        public static string BinaryToString(byte[] aBin)
        {
            return System.Text.Encoding.UTF8.GetString(aBin);
        }
        public static byte[] BooleanToBinary(bool aVal)
        {
            return new[] { aVal ? (byte)1 : (byte)0 };
        }
        public static byte[] IntegerToBinary(int aVal)
        {
            return BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(aVal));
        }
        public static byte[] StringToBinary(string aVal)
        {
            return System.Text.Encoding.UTF8.GetBytes(aVal);
        }
        public static List<uint> BinaryToUintArray(byte[] aBin)
        {
            int count = aBin.Length / 4;
            return Enumerable.Range(0,count).Select(i=>NetworkToHostOrderUint(BitConverter.ToUInt32(aBin, i * 4))).ToList();
        }
        public static uint NetworkToHostOrderUint(uint networkOrderUint)
        {
            unchecked
            {
                return (uint)System.Net.IPAddress.NetworkToHostOrder((int)networkOrderUint);
            }
        }
        public static uint HostToNetworkOrderUint(uint hostOrderUint)
        {
            unchecked
            {
                return (uint)System.Net.IPAddress.HostToNetworkOrder((int)hostOrderUint);
            }
        }
        public static byte[] ConvertUintListToNetworkOrderByteArray(List<uint> aIntegers)
        {
            byte[] bytes = new byte[4 * aIntegers.Count];
            for (int i = 0; i != aIntegers.Count; ++i)
            {
                byte[] newBytes = BitConverter.GetBytes(HostToNetworkOrderUint(aIntegers[i]));
                newBytes.CopyTo(bytes, i * 4);
            }
            return bytes;
        }
    }
}