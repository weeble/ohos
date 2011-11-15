using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OpenHome.Widget.Nodes.IO;
using OpenHome.Widget.Nodes.Logging;

namespace OpenHome.Widget.Nodes
{
    
    public class PresetState
    {
        public Snapshot Snapshot { get; set; }
        public string Guid { get; set; }
        public override string ToString()
        {
            return String.Format(
                "Preset {{ Guid={0}, Snapshot={1} }}",
                Guid, Snapshot.ToXml());
        }
        public static PresetState FromXml(XElement aXml)
        {
            return
                new PresetState
                {
                    Guid = aXml.Element("guid").Value,
                    Snapshot = Snapshot.FromXml(aXml.Element("snapshot"))
                };
        }
        public XElement ToXml()
        {
            return new XElement("preset",
                new XElement("guid", Guid),
                Snapshot.ToXml());
        }
    }

    public interface IPresetDiskStore
    {
        IEnumerable<PresetState> LoadPresetsFromStore();
        void PutPreset(PresetState aPresetState);
        void RemovePreset(string aPresetGuid);
    }

    public class NullPresetDiskStore : IPresetDiskStore
    {
        public IEnumerable<PresetState> LoadPresetsFromStore()
        {
            yield break;
        }
        public void PutPreset(PresetState aPresetState) { }
        public void RemovePreset(string aPresetUdn) { }
    }

    public class PresetDiskStore : IPresetDiskStore
    {
        public const string PresetFileExtension = ".preset.xml";
        private readonly XmlDiskStore iStore;
        public PresetDiskStore(DirectoryInfo aStoreDirectory)
        {
            iStore = new XmlDiskStore(aStoreDirectory, new NullLogger(), PresetFileExtension);
        }
        public IEnumerable<PresetState> LoadPresetsFromStore()
        {
            return iStore.LoadXmlFiles().Select(aXElement=>PresetState.FromXml(aXElement));
        }

        public void PutPreset(PresetState aPresetState)
        {
            XElement xmlTree = aPresetState.ToXml();
            iStore.PutXmlFile(aPresetState.Guid, xmlTree);
        }

        public void RemovePreset(string aPresetGuid)
        {
            iStore.DeleteXmlFile(aPresetGuid);
        }
    }
}