using System;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using OpenHome.Net.Device;
using OpenHome.Net.Device.Providers;

namespace OpenHome.Os.Remote
{
    public class ProviderRemoteAccess : DvProviderOpenhomeOrgRemoteAccess1, ILoginValidator
    {
        private const string kFileUserData = "UserData.xml";
        private const string kTagEnabled = "enabled";
        private const string kTagUserName = "username";
        private const string kTagPassword = "password";
        private const string kTagPublicUrl = "url";
        private readonly string iStoreDir;
        private readonly ProxyServer iProxyServer;
        private string iPassword;

        public ProviderRemoteAccess(DvDevice aDevice, string aStoreDir, ProxyServer aProxyServer)
            : base(aDevice)
        {
            iStoreDir = aStoreDir;
            iProxyServer = aProxyServer;

            EnablePropertyUserName();
            EnablePropertyPublicUri();
            EnablePropertyEnabled();
            EnablePropertyPasswordSet();
            EnableActionSetUserName();
            EnableActionSetPassword();
            EnableActionEnable();
            EnableActionClearAuthenticatedClients();

            string userDataFileName = UserDataFileName();
            if (!File.Exists(UserDataFileName()))
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
            {
                iProxyServer.Start(this);
            }
        }
        public bool ValidateCredentials(string aUserName, string aPassword)
        {
            lock (this)
            {
                return (PropertyUserName() == aUserName && iPassword == aPassword);
            }
        }
        protected override void SetUserName(IDvInvocation aInvocation, string aUserName, out bool aSucceeded, out string aAlternativeNames)
        {
            lock (this)
            {
                // TODO: call web service to set new username (or clear account if aUserName.Length==0 ??)

                SetPropertyUserName(aUserName);
                if (aUserName.Length == 0)
                    Enable(false);
                aSucceeded = true;
                aAlternativeNames = "";
            }
            throw (new ActionDisabledError());
        }
        protected override void SetPassword(IDvInvocation aInvocation, string aPassword)
        {
            lock (this)
            {
                iPassword = aPassword;
                SetPropertyPasswordSet((iPassword.Length > 0));
                WriteUserData();
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
            throw new NotImplementedException();
        }
        private string UserDataFileName()
        {
            return iStoreDir + Path.DirectorySeparatorChar + kFileUserData;
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

            XmlWriter writer = XmlWriter.Create(UserDataFileName());
            defaultXml.WriteTo(writer);
            writer.Close();
        }
        private void Enable(bool aEnable)
        {
            SetPropertyEnabled(aEnable);
            // TODO: start/stop proxy depending on state of aEnable
            if (aEnable)
                iProxyServer.Start(this);
            else
                iProxyServer.Stop();
            WriteUserData();
        }
    }
}
