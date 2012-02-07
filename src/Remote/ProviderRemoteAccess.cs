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
using Renci.SshNet;

namespace OpenHome.Os.Remote
{
    public class ProviderRemoteAccess : DvProviderOpenhomeOrgRemoteAccess1, ILoginValidator, IDisposable
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
        private readonly string iNetworkAdapter;
        private string iPassword;
        private ProxyServer iProxyServer;
        private SshClient iSshClient;
        private ForwardedPortRemote iForwardedPortRemote;
        private string iSshServerHost;
        private int iSshServerPort;
        private string iPortForwardAddress;
        private uint iPortForwardPort;

        public ProviderRemoteAccess(DvDevice aDevice, string aStoreDir, ProxyServer aProxyServer, string aNetworkAdapter)
            : base(aDevice)
        {
            iDeviceUdn = aDevice.Udn();
            iStoreDir = aStoreDir;
            iProxyServer = aProxyServer;
            iNetworkAdapter = aNetworkAdapter;

            EnablePropertyUserName();
            EnablePropertyPublicUri();
            EnablePropertyEnabled();
            EnablePropertyPasswordSet();
            EnableActionSetUserName();
            EnableActionSetPassword();
            EnableActionReset();
            EnableActionEnable();
            EnableActionClearAuthenticatedClients();

            string userDataFileName = FileFullName(kFileUserData);
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
                string privateKeyFileName = FileFullName(kFilePrivateKey);
                if (!File.Exists(privateKeyFileName))
                {
                    Console.WriteLine("No ssh key pair found.  Currently have to create these ourselves.");
                    Console.WriteLine("You can generate a key from the Linux command line using");
                    Console.WriteLine("\tssh-keygen -t rsa -f key -C\"\"");
                    Console.WriteLine("...then copy to ohWidget's 'remote' dir, renaming key to key.priv");
                    throw new ActionError();
                    /*using (var key = new RsaKey())
                    {
                        key.WritePublicKey(FileFullName(kFilePublicKey));
                        key.WritePrivateKey(privateKeyFileName);
                    }*/
                }
                string publicUri;
                aSucceeded = TrySetUserName(aUserName, out publicUri, out aAlternativeNames);
                if (!aSucceeded)
                    return;
                PropertiesLock();
                SetPropertyPublicUri(publicUri);
                if (SetPropertyUserName(aUserName))
                    iProxyServer.ClearAuthenticatedClients();
                PropertiesUnlock();
                WriteUserData();
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
        protected override void Reset(IDvInvocation aInvocation, uint aHandle)
        {
            lock (this)
            {
                if (!TryRemoveUserName())
                    throw new ActionError("Failed to remove username from remote server");
                PropertiesLock();
                SetPropertyUserName("");
                SetPropertyPublicUri("");
                SetPropertyPasswordSet(false);
                PropertiesUnlock();
                iPassword = "";
                Enable(false);
                iProxyServer.ClearAuthenticatedClients();
            }
        }
        protected override void Enable(IDvInvocation aInvocation, bool aEnable)
        {
            lock (this)
            {
                Enable(aEnable);
            }
        }
        protected override void ClearAuthenticatedClients(IDvInvocation aInvocation)
        {
            lock (this)
            {
                iProxyServer.ClearAuthenticatedClients();
            }
        }
        private string FileFullName(string aName)
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

            XmlWriter writer = XmlWriter.Create(FileFullName(kFileUserData));
            defaultXml.WriteTo(writer);
            writer.Close();
        }
        private void Enable(bool aEnable)
        {
            if (PropertyEnabled() != aEnable)
            {
                WriteUserData();
                if (aEnable)
                    Start();
                else
                    Stop();
                SetPropertyEnabled(aEnable);
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
            XElement sshServerElement = successElement.Element("sshserver");
            iSshServerHost = sshServerElement.Element("address").Value;
            iSshServerPort = Convert.ToInt32(sshServerElement.Element("port").Value);
            XElement portForwardElement = successElement.Element("portforward");
            iPortForwardAddress = portForwardElement.Element("address").Value;
            iPortForwardPort = (uint)Convert.ToInt32(portForwardElement.Element("port").Value);
            PrivateKeyFile pkf = new PrivateKeyFile(FileFullName(kFilePrivateKey));
            iSshClient = new SshClient(iSshServerHost, iSshServerPort, "root", pkf);
            iSshClient.Connect();
            Logger.InfoFormat("Connected to ssh server at {0}:{1}", iSshServerHost, iSshServerPort);
            iForwardedPortRemote = new ForwardedPortRemote(iPortForwardAddress, iPortForwardPort, iNetworkAdapter, iProxyServer.Port);
            iSshClient.AddForwardedPort(iForwardedPortRemote);
            iForwardedPortRemote.Start();
            Logger.InfoFormat("Forwarded remote port {0}:{1} to {2}:{3}", iPortForwardAddress, iPortForwardPort, iNetworkAdapter, iProxyServer.Port);
        }
        private void Stop()
        {
            if (iProxyServer != null)
            {
                iProxyServer.Stop();
                iProxyServer = null;
            }
            if (iSshClient != null)
            {
                if (iForwardedPortRemote != null)
                {
                    iForwardedPortRemote.Stop();
                    iForwardedPortRemote.Dispose();
                    iForwardedPortRemote = null;
                }
                iSshClient.Dispose();
                iSshClient = null;
            }
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
        private bool TrySetUserName(string aUserName, out string aPublicUri, out string aSuggestedNames)
        {
            aPublicUri = "";
            aSuggestedNames = "";
            XElement body = new XElement("register");
            body.Add(new XElement("username", aUserName));
            body.Add(new XElement("uidnode", iDeviceUdn));
            string publicKey = Encoding.ASCII.GetString(File.ReadAllBytes(FileFullName(kFilePublicKey)));
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

            aPublicUri = tree.Element("success").Element("url").Value;
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
        public new void Dispose()
        {
            Stop();
            base.Dispose();
        }
    }

    class RsaKey : IDisposable
    {
        private const int kKeySizeBits = 1024;
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
            iKey.Clear(); // we'd like to call Dispose() here but mono doesn't implement it.
                          // Clear() is documented as calling Dispose() so is an acceptable alternative
        }
    }
}
