using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using log4net;
using OpenHome.Os.Platform.IO;
using OpenHome.Os.Platform.Logging;

namespace OpenHome.Os.Apps
{
    public class AppMetadata
    {
        public string AppName { get; set; }
        public bool InstallPending { get; set; }
        public bool DeletePending { get; set; }
        public string LocalInstallLocation { get; set; }
        public string UpdateUrl { get; set; }
        public bool AutoUpdate { get; set; }
        public List<string> GrantedPermissions { get; set; }
        public string Udn { get; set; }
        public string FriendlyName { get; set; }
        public DateTime? LastModified { get; set; }

        public AppMetadata Clone()
        {
            return new AppMetadata
            {
                AppName = AppName,
                DeletePending = DeletePending,
                GrantedPermissions = new List<string>(GrantedPermissions),
                InstallPending = InstallPending,
                LocalInstallLocation = LocalInstallLocation,
                Udn = Udn,
                AutoUpdate = AutoUpdate,
                UpdateUrl = UpdateUrl,
                FriendlyName = FriendlyName,
                LastModified = LastModified
            };
        }
    }

    public interface IAppMetadataStore
    {
        IEnumerable<AppMetadata> LoadAppsFromStore();
        void PutApp(AppMetadata aApp);
        void DeleteApp(string aAppName);
        AppMetadata GetApp(string aAppName);
    }

    class AppMetadataStore : IAppMetadataStore
    {
        const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";
        // Schema
        const string AppMetadataSchemaXml =
              @"<?xml version=""1.0"" encoding=""utf-8"" ?>
                <xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema""
                           elementFormDefault=""qualified"">
                  <xs:element name=""app"" type=""app""/>
                  <xs:complexType name=""app"">
                    <xs:sequence>
                      <xs:element name=""name"" type=""xs:string""/>
                      <xs:element name=""installPending"" type=""xs:boolean""/>
                      <xs:element name=""deletePending"" type=""xs:boolean""/>
                      <xs:element name=""localInstallationLocation"" type=""xs:string""/>
                      <xs:element name=""updateUrl"" type=""xs:string"" minOccurs=""0""/>
                      <xs:element name=""autoUpdate"" type=""xs:boolean"" minOccurs=""0""/>
                      <xs:element name=""grantedPermissions"">
                        <xs:complexType>
                          <xs:sequence>
                            <xs:element name=""permission"" type=""xs:string"" minOccurs=""0"" maxOccurs=""unbounded""/>
                          </xs:sequence>
                        </xs:complexType>
                      </xs:element>
                      <xs:element name=""udn"" type=""xs:string"" minOccurs=""0""/>
                      <xs:element name=""friendlyName"" type=""xs:string"" minOccurs=""0""/>
                      <xs:element name=""lastModified"" type=""xs:string"" minOccurs=""0""/>
                    </xs:sequence>
                  </xs:complexType>
                </xs:schema>";
        static readonly XmlSchemaSet AppMetadataSchemaSet = MakeSchemaSet(AppMetadataSchemaXml);

        // XML -> object
        static AppMetadata ParseAppElement(XElement aAppElement)
        {
            if (aAppElement == null) return null;
            var udnElement = aAppElement.Element("udn");
            string udn = (udnElement==null) ? null : udnElement.Value;
            var autoUpdateElement = aAppElement.Element("autoUpdate");
            bool autoUpdate = (autoUpdateElement == null) ? false : (bool)autoUpdateElement;

            var updateUrlElement = aAppElement.Element("updateUrl");
            string updateUrl = (updateUrlElement == null) ? "" : updateUrlElement.Value;

            var friendlyNameElement = aAppElement.Element("friendlyName");
            string friendlyName = (friendlyNameElement == null) ? "" : friendlyNameElement.Value;

            DateTime? lastModified = ParseLastModifiedElement(aAppElement);
            return
                new AppMetadata
                {
                    AppName = (string)aAppElement.Element("name"),
                    InstallPending = (bool)aAppElement.Element("installPending"),
                    DeletePending = (bool)aAppElement.Element("deletePending"),
                    LocalInstallLocation = (string)aAppElement.Element("localInstallationLocation"),
                    UpdateUrl = updateUrl,
                    AutoUpdate = autoUpdate,
                    GrantedPermissions = aAppElement.Element("grantedPermissions").Elements().Select(aE=>(string)aE).ToList(),
                    Udn = udn,
                    FriendlyName = friendlyName,
                    LastModified = lastModified
                };
        }

        static DateTime? ParseLastModifiedElement(XElement aAppElement)
        {
            DateTime? lastModified;
            var lastModifiedElement = aAppElement.Element("lastModified");
            DateTime parsedTime;
            if (lastModifiedElement!=null &&
                DateTime.TryParseExact(
                    lastModifiedElement.Value,
                    TimeFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,
                    out parsedTime))
            {
                lastModified = parsedTime;
            }
            else
            {
                lastModified = null;
            }
            return lastModified;
        }

        static XElement FormatLastModifiedElement(DateTime? aLastModified)
        {
            if (aLastModified == null)
            {
                // Note: XElement constructors deliberately accept and ignore
                // null elements.
                return null;
            }
            return new XElement("lastModified", aLastModified.Value.ToString(TimeFormat));
        }

        // object -> XML
        static XElement AppToXElement(AppMetadata aApp)
        {
            return
                new XElement("app",
                    new XElement("name", aApp.AppName),
                    new XElement("installPending", aApp.InstallPending),
                    new XElement("deletePending", aApp.DeletePending),
                    new XElement("localInstallationLocation", aApp.LocalInstallLocation),
                    new XElement("updateUrl", aApp.UpdateUrl),
                    new XElement("autoUpdate", aApp.AutoUpdate),
                    new XElement("grantedPermissions",
                        aApp.GrantedPermissions.Select(aPermission=>new XElement("permission", aPermission))),
                    new XElement("udn", aApp.Udn),
                    new XElement("friendlyName", aApp.FriendlyName),
                    FormatLastModifiedElement(aApp.LastModified));
        }

        static XmlSchemaSet MakeSchemaSet(string aSchemaXml)
        {
            TextReader reader = new StringReader(aSchemaXml);
            XmlSchemaSet schemaSet = new XmlSchemaSet();
            schemaSet.Add(XmlSchema.Read(reader, (aO,aE)=>{throw aE.Exception;}));
            return schemaSet;
        }

        /*static XElement ReadAppXml(TextReader aInput)
        {
            XmlReaderSettings settings =
                new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema,
                    Schemas = AppMetadataSchemaSet
                };
            XmlReader reader = XmlReader.Create(aInput, settings);

            // Parse the file.
            return XElement.Load(reader);
        }*/


        //static AppMetadata ReadApp(TextReader aInput)
        //{
        //    return ParseAppElement(ReadAppXml(aInput));
        //}

        readonly DiskStore<AppMetadata> iDiskStore;
        static readonly ILog Logger = LogManager.GetLogger(typeof(AppMetadataStore));
        const string AppFileExtension = ".app.xml";

        public AppMetadataStore(DirectoryInfo aStoreDirectory)
        {
            var xmlReaderWriter = new XmlReaderWriter(AppMetadataSchemaSet, Logger);
            iDiskStore = new DiskStore<AppMetadata>(
                aStoreDirectory,
                AppFileExtension,
                aReader => ParseAppElement(xmlReaderWriter.ReadFile(aReader)),
                (aWriter, aApp) => xmlReaderWriter.WriteFile(aWriter, AppToXElement(aApp)));
        }

        public IEnumerable<AppMetadata> LoadAppsFromStore()
        {
            return iDiskStore.LoadFiles();
        }
        public void PutApp(AppMetadata aApp)
        {
            iDiskStore.PutFile(aApp.AppName, aApp);
        }
        public void DeleteApp(string aAppName)
        {
            iDiskStore.DeleteFile(aAppName);
        }
        public AppMetadata GetApp(string aAppName)
        {
            return iDiskStore.GetFile(aAppName);
        }

    }
}
