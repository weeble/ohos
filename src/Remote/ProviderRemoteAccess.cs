using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.Collections.Generic;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;
using log4net;

namespace OpenHome.Os.Remote
{
    public class ProviderRemoteAccess : DvProviderOpenhomeOrgRemoteAccess1, ILoginValidator
    {
        private const string kFileUserData = "UserData.xml";
        private const string kFilePublicKey = "key.pub";
        private const string kFilePrivateKey = "key.priv";
        private const string kTagEnabled = "enabled";
        private const string kTagUserName = "username";
        private const string kTagPassword = "password";
        private const string kTagPublicUrl = "url";
        private const string kWebServiceAddress = "http://remoteaccess-dev.linn.co.uk:2001/";
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ProviderRemoteAccess));
        private readonly string iDeviceUdn;
        private readonly string iStoreDir;
        private readonly ProxyServer iProxyServer;
        private string iPassword;

        public ProviderRemoteAccess(DvDevice aDevice, string aStoreDir, ProxyServer aProxyServer)
            : base(aDevice)
        {
            iDeviceUdn = aDevice.Udn();
            iStoreDir = aStoreDir;
            iProxyServer = aProxyServer;

            EnablePropertyUserName();
            EnablePropertyPublicUri();
            EnablePropertyEnabled();
            EnablePropertyPasswordSet();
            EnableActionSetUserName();
            EnableActionSetPassword();
            EnableActionEnable();
            EnableActionGetUserName();
            EnableActionClearAuthenticatedClients();

            string userDataFileName = RemoteAccessFileName(kFileUserData);
            if (!File.Exists(userDataFileName))
                WriteUserData(false, "", "", "");
            string xml = File.ReadAllText(userDataFileName, Encoding.UTF8);
            XElement tree = XElement.Parse(xml);
            bool enabled = Convert.ToBoolean(tree.Element(kTagEnabled).Value);
            string userName = tree.Element(kTagUserName).Value;
            iPassword = tree.Element(kTagPassword).Value;
            string publicUrl = tree.Element(kTagPublicUrl).Value;

            SetPropertyUserName(userName);
            SetPropertyPublicUri(publicUrl);
            SetPropertyEnabled(enabled);
            SetPropertyPasswordSet((iPassword.Length > 0));

            if (enabled)
                Start();
        }
        public bool ValidateCredentials(string aUserName, string aPassword)
        {
            lock (this)
            {
                return (PropertyUserName() == aUserName && iPassword == aPassword);
            }
        }
        protected override void SetUserName(IDvInvocation aInvocation, uint aHandle, string aUserName, out bool aSucceeded, out string aAlternativeNames)
        {
            lock (this)
            {
                string privateKeyFileName = RemoteAccessFileName(kFilePrivateKey);
                if (!File.Exists(privateKeyFileName))
                {
                    using (var key = new RsaKey())
                    {
                        key.WritePublicKey(RemoteAccessFileName(kFilePublicKey));
                        key.WritePrivateKey(privateKeyFileName);
                    }
                }
                if (aUserName.Length == 0)
                {
                    aAlternativeNames = "";
                    aSucceeded = TryRemoveUserName();
                }
                else
                {
                    aSucceeded = TrySetUserName(aUserName, out aAlternativeNames);
                }
                if (!aSucceeded)
                    return;
                if (SetPropertyUserName(aUserName))
                {
                    iProxyServer.ClearAuthenticatedClients();
                    if (aUserName.Length == 0)
                        Enable(false);
                }
            }
        }
        protected override void SetPassword(IDvInvocation aInvocation, uint aHandle, string aPassword)
        {
            lock (this)
            {
                if (iPassword != aPassword)
                {
                    iPassword = aPassword;
                    SetPropertyPasswordSet((iPassword.Length > 0));
                    WriteUserData();
                    iProxyServer.ClearAuthenticatedClients();
                }
            }
        }
        protected override void Enable(IDvInvocation aInvocation, bool aEnable)
        {
            lock (this)
            {
                Enable(aEnable);
            }
        }
        protected override void GetUserName(IDvInvocation aInvocation, uint aHandle, out string aUserName)
        {
            lock (this)
            {
                aUserName = PropertyUserName();
            }
        }
        protected override void ClearAuthenticatedClients(IDvInvocation aInvocation)
        {
            lock (this)
            {
                iProxyServer.ClearAuthenticatedClients();
            }
        }
        private string RemoteAccessFileName(string aName)
        {
            return iStoreDir + Path.DirectorySeparatorChar + aName;
        }
        private void WriteUserData()
        {
            WriteUserData(PropertyEnabled(), PropertyUserName(), iPassword, PropertyPublicUri());
        }
        private void WriteUserData(bool aEnabled, string aUserName, string aPassword, string aPublicUri)
        {
            XElement defaultXml = new XElement("remoteAccessUserData");
            defaultXml.Add(new XElement(kTagEnabled, aEnabled.ToString()));
            defaultXml.Add(new XElement(kTagUserName, aUserName));
            defaultXml.Add(new XElement(kTagPassword, aPassword));
            defaultXml.Add(new XElement(kTagPublicUrl, aPublicUri));

            XmlWriter writer = XmlWriter.Create(RemoteAccessFileName(kFileUserData));
            defaultXml.WriteTo(writer);
            writer.Close();
        }
        private void Enable(bool aEnable)
        {
            if (SetPropertyEnabled(aEnable))
            {
                WriteUserData();
                if (aEnable)
                    Start();
                else
                    iProxyServer.Stop();
            }
        }
        private void Start()
        {
            iProxyServer.Start(this);
            XElement body = new XElement("getaddress");
            body.Add(new XElement("uidnode", iDeviceUdn));
            XElement tree = CallWebService("getaddress", body.ToString());
            if (tree == null)
                return;
            XElement error = tree.Element("error");
            if (error != null)
            {
                Logger.ErrorFormat("Remote access method {0} failed with error {1}.", "getaddress", error.Value);
                return;
            }
            XElement successElement = tree.Element("success");
            XElement address = successElement.Element("address");
            XElement port = successElement.Element("port");
            Console.WriteLine("Remote access method {0} returned address {1}:{2}.", "getaddress", address.Value, port.Value);
        }
        private bool TryRemoveUserName()
        {
            XElement body = new XElement("remove");
            body.Add(new XElement("uidnode", iDeviceUdn));
            XElement tree = CallWebService("remove", body.ToString());
            if (tree == null)
                return false;
            XElement error = tree.Element("error");
            if (error != null)
            {
                Logger.ErrorFormat("Remote access method {0} failed with error {1}.", "remove", error.Value);
                return false;
            }
            return true;
        }
        private bool TrySetUserName(string aUserName, out string aSuggestedNames)
        {
            aSuggestedNames = "";
            XElement body = new XElement("register");
            body.Add(new XElement("username", aUserName));
            body.Add(new XElement("uidnode", iDeviceUdn));
            string publicKey = Encoding.ASCII.GetString(File.ReadAllBytes(RemoteAccessFileName(kFilePublicKey)));
            body.Add(new XElement("sshkey", publicKey));
            XElement tree = CallWebService("register", body.ToString());
            if (tree == null)
                return false;
            XElement error = tree.Element("error");
            if (error != null)
            {
                Logger.ErrorFormat("Remote access method {0} failed with error {1}.", "register", error.Value);
                IEnumerable<XElement> suggestedNames = tree.Elements("suggestionlist");
                XElement namesXml = new XElement("suggestionlist");
                foreach (var name in suggestedNames)
                    namesXml.Add(new XElement("suggestion", name.Value));
                aSuggestedNames = namesXml.ToString();
                return false;
            }

            return true;
        }
        private static XElement CallWebService(string aRequestMethod, string aRequestBody)
        {
            string url = kWebServiceAddress + aRequestMethod;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            if (aRequestBody != null)
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(aRequestBody);
                request.GetRequestStream().Write(bodyBytes, 0, bodyBytes.Length);
            }
            HttpWebResponse resp;
            try
            {
                resp = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                Logger.ErrorFormat("Remote access web service {0} failed with error {1}.", aRequestMethod, e.Message);
                return null;
            }
            Stream respStream = resp.GetResponseStream();
            MemoryStream memStream = new MemoryStream();
            respStream.CopyTo(memStream);
            byte[] bytes = memStream.ToArray();
            return XElement.Parse(Encoding.UTF8.GetString(bytes));
        }
    }

    class RsaKey : IDisposable
    {
        private const int kKeySizeBits = 2048;
        private readonly System.Security.Cryptography.RSACryptoServiceProvider iKey;
        private readonly System.Security.Cryptography.RSAParameters iKeyInfo;

        public RsaKey()
        {
            iKey = new System.Security.Cryptography.RSACryptoServiceProvider(kKeySizeBits);
            iKeyInfo = iKey.ExportParameters(true);
        }
        public void WritePublicKey(string aFileName)
        {
            FileStream stream = new FileStream(aFileName, FileMode.OpenOrCreate);

            byte[] sshrsa = Encoding.UTF8.GetBytes("ssh-rsa");
            byte[] buf = new byte[sshrsa.Length + 4 + iKeyInfo.Exponent.Length + 4 + iKeyInfo.Modulus.Length + 4];
            int index = 0;
            AppendBytesPublic(buf, ref index, sshrsa);
            AppendBytesPublic(buf, ref index, iKeyInfo.Exponent);
            AppendBytesPublic(buf, ref index, iKeyInfo.Modulus);

            byte[] b64Buf = Encoding.ASCII.GetBytes(Convert.ToBase64String(buf));
            stream.Write(sshrsa, 0, sshrsa.Length);
            stream.Write(new [] { (byte)' ' }, 0, 1);
            stream.Write(b64Buf, 0, b64Buf.Length);
            stream.Write(new[] { (byte)' ' }, 0, 1);
            stream.Write(new[] { (byte)'\n' }, 0, 1);

            stream.Close();
        }
        public void WritePrivateKey(string aFileName)
        {
            FileStream stream = new FileStream(aFileName, FileMode.OpenOrCreate);

            // convert key to plain text byte stream (OpenSSH format)
            int content =
                1 + BytesForLength(1) + 1 +
                1 + BytesForLength(iKeyInfo.Modulus.Length) + iKeyInfo.Modulus.Length +
                1 + BytesForLength(iKeyInfo.Exponent.Length) + iKeyInfo.Exponent.Length +
                1 + BytesForLength(iKeyInfo.D.Length) + iKeyInfo.D.Length +
                1 + BytesForLength(iKeyInfo.P.Length) + iKeyInfo.P.Length +
                1 + BytesForLength(iKeyInfo.Q.Length) + iKeyInfo.Q.Length +
                1 + BytesForLength(iKeyInfo.DP.Length) + iKeyInfo.DP.Length +
                1 + BytesForLength(iKeyInfo.DQ.Length) + iKeyInfo.DQ.Length +
                1 + BytesForLength(iKeyInfo.InverseQ.Length) + iKeyInfo.InverseQ.Length;
            int total = 1 + BytesForLength(content) + content;
            byte[] plain = new byte[total];
            int index = 0;
            plain[index++] = 0x30;
            AppendLength(plain, ref index, content);
            AppendBytesPrivate(plain, ref index, new byte[1]);
            AppendBytesPrivate(plain, ref index, iKeyInfo.Modulus);
            AppendBytesPrivate(plain, ref index, iKeyInfo.Exponent);
            AppendBytesPrivate(plain, ref index, iKeyInfo.D);
            AppendBytesPrivate(plain, ref index, iKeyInfo.P);
            AppendBytesPrivate(plain, ref index, iKeyInfo.Q);
            AppendBytesPrivate(plain, ref index, iKeyInfo.DP);
            AppendBytesPrivate(plain, ref index, iKeyInfo.DQ);
            AppendBytesPrivate(plain, ref index, iKeyInfo.InverseQ);


            // base-64 encode data and write private key (OpenSSH format)
            byte[] prv = Encoding.ASCII.GetBytes(Convert.ToBase64String(plain)); // TODO: validate this gives same as Util.toBase64(plain, 0, plain.Length);
            byte[] cr = new [] { (byte)'\n' };
            byte[] start = Encoding.ASCII.GetBytes("-----BEGIN RSA PRIVATE KEY-----");
            stream.Write(start, 0, start.Length);
            stream.Write(cr, 0, cr.Length);
            index = 0;
            while (index < prv.Length)
            {
                if (index + 64 < prv.Length)
                {
                    stream.Write(prv, index, 64);
                    stream.Write(cr, 0, cr.Length);
                    index += 64;
                    continue;
                }
                stream.Write(prv, index, prv.Length - index);
                stream.Write(cr, 0, cr.Length);
                break;
            }
            byte[] end = Encoding.ASCII.GetBytes("-----END RSA PRIVATE KEY-----");
            stream.Write(end, 0, end.Length);
            stream.Write(cr, 0, cr.Length);

            stream.Close();
        }
        private static void AppendBytesPublic(byte[] aDest, ref int aIndex, byte[] aData)
        {
            uint val = (uint)aData.Length;
            byte[] tmp = new byte[4];
            tmp[0] = (byte)(val >> 24);
            tmp[1] = (byte)(val >> 16);
            tmp[2] = (byte)(val >> 8);
            tmp[3] = (byte)(val);
            Array.Copy(tmp, 0, aDest, aIndex, tmp.Length);
            aIndex += tmp.Length;
            Array.Copy(aData, 0, aDest, aIndex, aData.Length);
            aIndex += aData.Length;
        }
        private static int BytesForLength(int aLen)
        {
            int numBytes = 1;
            if (aLen <= 0x7f)
                return numBytes;
            while (aLen > 0)
            {
                aLen >>= 8;
                numBytes++;
            }
            return numBytes;
        }
        private static void AppendBytesPrivate(byte[] aDest, ref int aIndex, byte[] aBuf)
        {
            aDest[aIndex++] = 0x02;
            AppendLength(aDest, ref aIndex, aBuf.Length);
            Array.Copy(aBuf, 0, aDest, aIndex, aBuf.Length);
            aIndex += aBuf.Length;
        }
        private static void AppendLength(byte[] aData, ref int aIndex, int aLength)
        {
            int i = BytesForLength(aLength) - 1;
            if (i == 0)
            {
                aData[aIndex++] = (byte)aLength;
                return;
            }
            aData[aIndex++] = (byte)(0x80 | i);
            while (i > 0)
            {
                aData[aIndex + i - 1] = (byte)(aLength & 0xff);
                aLength >>= 8;
                i--;
            }
            aIndex += i;
        }
        public void Dispose()
        {
            iKey.Dispose();
        }
    }
}
