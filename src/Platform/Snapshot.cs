using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Xml.Linq;
using OpenHome.Widget.Nodes.XmlHelpers;

namespace OpenHome.Widget.Nodes
{
    public interface IHandleConverter
    {
        bool TryGetHandleFromRoomUuid(string aRoomUuid, out uint aRoomHandle);
        bool TryGetHandleFromWidgetUuid(string aWidgetUuid, out uint aWidgetHandle);
        bool TryGetUuidFromRoomHandle(uint aRoomHandle, out string aRoomUuid);
        bool TryGetUuidFromWidgetHandle(uint aWidgetHandle, out string aWidgetUuid);
    }

    namespace XmlHelpers
    {
        internal static class XmlHelper
        {
            public static XElement RequiredElement(this XElement aParent, string aChildName)
            {
                var elements = aParent.Elements(aChildName);
                try
                {
                    return elements.Single();
                }
                catch (InvalidOperationException)
                {
                    throw new BadSnapshotXmlException(String.Format("Expected single '<{0}>' element, found {1}.", aChildName, elements.Count()));
                }
            }
            public static void RequireName(this XElement aElement, string aName)
            {
                if (aElement.Name != aName)
                {
                    throw new BadSnapshotXmlException(String.Format("Expected <{0}> tag, got {1} instead.", aName, aElement.Name));
                }
            }
        }
    }
    public class ListProperty<T> : IEnumerable<T>
    {
        private List<T> iList;
        public ListProperty() { iList = new List<T>(); }
        public ListProperty(IEnumerable<T> aItems)
        {
            iList = new List<T>(aItems);
        }
        public IEnumerator<T> GetEnumerator()
        {
            return iList.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public void Add(T aItem)
        {
            iList.Add(aItem);
        }
    }
    public class SnapshotWidgetProperty
    {
        public string Name { get; set; }
        public byte[] Value { get; set; }

        public SnapshotWidgetProperty(string aName, byte[] aValue)
        {
            Name = aName;
            Value = aValue;
        }

        public XElement ToXml()
        {
            return new XElement(
                "property",
                new XElement("name", Name),
                new XElement("value", Convert.ToBase64String(Value)));
        }
        public static SnapshotWidgetProperty FromXml(XElement aElement)
        {
            aElement.RequireName("property");
            try
            {
                return
                    new SnapshotWidgetProperty(
                            aElement.RequiredElement("name").Value,
                            Convert.FromBase64String(aElement.RequiredElement("value").Value)
                        );
            }
            catch (FormatException)
            {
                throw new BadSnapshotXmlException("Value wasn't a properly encoded base64 string.");
            }
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Value: [{1}]", Name, BitConverter.ToString(Value));
        }
    }
    public class SnapshotWidget
    {
        public string Id { get; set; }
        public ListProperty<SnapshotWidgetProperty> Properties { get; private set; }
        public SnapshotWidget()
        {
            Properties = new ListProperty<SnapshotWidgetProperty>();
        }
        public SnapshotWidget(string aId, IEnumerable<SnapshotWidgetProperty> aProperties)
        {
            Id = aId;
            Properties = new ListProperty<SnapshotWidgetProperty>(aProperties);
        }
        public SnapshotWidget(string aId, params SnapshotWidgetProperty[] aProperties)
            : this(aId, (IEnumerable<SnapshotWidgetProperty>)aProperties)
        {
        }

        public XElement ToXml()
        {
            XElement widget = new XElement(
                "widget",
                new XElement("udn", Id));
            widget.Add(Properties.Select(aProperty=>aProperty.ToXml()).ToArray());
            return widget;
        }

        public XElement ToHandleBasedXml(IHandleConverter aHandleConverter)
        {
            uint handle;
            if (!aHandleConverter.TryGetHandleFromWidgetUuid(Id, out handle))
            {
                return null;
            }
            XElement widget = new XElement(
                "widget",
                new XElement("handle", handle));
            widget.Add(Properties.Select(aProperty=>aProperty.ToXml()).ToArray());
            return widget;
        }

        public static SnapshotWidget FromXml(XElement aElement)
        {
            aElement.RequireName("widget");
            return
                new SnapshotWidget()
                {
                    Id = aElement.RequiredElement("udn").Value,
                    Properties = new ListProperty<SnapshotWidgetProperty>(
                        from propertyElement in aElement.Elements("property")
                        select SnapshotWidgetProperty.FromXml(propertyElement))
                };
        }

        public static SnapshotWidget FromHandleBasedXml(XElement aElement, IHandleConverter aHandleConverter)
        {
            aElement.RequireName("widget");

            uint handle;
            if (!uint.TryParse(aElement.RequiredElement("handle").Value, out handle))
            {
                throw new BadSnapshotXmlException("handle must be integer in range 0 to 4294967295.");
            }
            string widgetId;
            if (!aHandleConverter.TryGetUuidFromWidgetHandle(handle, out widgetId))
            {
                throw new BadSnapshotXmlException(String.Format("handle ({0} not recognized.", handle));
            }

            return
                new SnapshotWidget()
                {
                    Id = widgetId,
                    Properties = new ListProperty<SnapshotWidgetProperty>(
                        from propertyElement in aElement.Elements("property")
                        select SnapshotWidgetProperty.FromXml(propertyElement))
                };
        }

        public override string ToString()
        {
            return string.Format("Id: {0}, Properties: [{1}]", Id, String.Join(", ", Properties.Select(aProp=>aProp.ToString()).ToArray()));
        }
    }
    public class Snapshot
    {
        public string RoomId { get; set; }
        public string Name { get; set; }
        public ListProperty<SnapshotWidget> Widgets { get; private set; }
        public Snapshot()
        {
            Widgets = new ListProperty<SnapshotWidget>();
        }

        public Snapshot(string aName, string aRoomId, IEnumerable<SnapshotWidget> aWidgets)
        {
            Name = aName;
            RoomId = aRoomId;
            Widgets = new ListProperty<SnapshotWidget>(aWidgets);
        }
        public Snapshot(string aName, string aRoomId, params SnapshotWidget[] aWidgets)
            :this(aName, aRoomId, (IEnumerable<SnapshotWidget>) aWidgets)
        {
        }

        public XElement ToXml()
        {
            XElement snapshot = new XElement(
                "snapshot",
                new XElement("name", Name),
                new XElement("roomId", RoomId));
            snapshot.Add(Widgets.Select(aWidget=>aWidget.ToXml()).ToArray());
            return snapshot;
        }
        public XElement ToHandleBasedXml(IHandleConverter aHandleConverter)
        {
            uint handle;
            if (!aHandleConverter.TryGetHandleFromRoomUuid(RoomId, out handle))
            {
                return null;
            }
            XElement snapshot = new XElement(
                "snapshot",
                new XElement("name", Name),
                new XElement("roomHandle", handle));
            snapshot.Add(
                Widgets
                    .Select(aWidget=>aWidget.ToHandleBasedXml(aHandleConverter))
                    .Where(aXElement=>aXElement!=null)
                    .ToArray());
            return snapshot;
        }
        public static Snapshot FromXml(XElement aElement)
        {
            aElement.RequireName("snapshot");
            return
                new Snapshot()
                {
                    RoomId = aElement.RequiredElement("roomId").Value,
                    Name = aElement.RequiredElement("name").Value,
                    Widgets = new ListProperty<SnapshotWidget>(
                        from widgetElement in aElement.Elements("widget")
                        select SnapshotWidget.FromXml(widgetElement))
                };
        }
        public static Snapshot FromHandleBasedXml(XElement aElement, IHandleConverter aHandleConverter)
        {
            aElement.RequireName("snapshot");
            uint handle;
            if (!uint.TryParse(aElement.RequiredElement("roomHandle").Value, out handle))
            {
                throw new BadSnapshotXmlException("roomHandle must be integer in range 0 to 4294967295.");
            }
            string roomId;
            if (!aHandleConverter.TryGetUuidFromRoomHandle(handle, out roomId))
            {
                throw new BadSnapshotXmlException(String.Format("roomHandle ({0} not recognized.", handle));
            }
            return
                new Snapshot()
                {
                    RoomId = roomId,
                    Name = aElement.RequiredElement("name").Value,
                    Widgets = new ListProperty<SnapshotWidget>(
                        from widgetElement in aElement.Elements("widget")
                        select SnapshotWidget.FromHandleBasedXml(widgetElement, aHandleConverter))
                };
        }

        public override string ToString()
        {
            return string.Format("RoomId: {0}, Name: {1}, Widgets: {2}", RoomId, Name, String.Join(", ", Widgets.Select(aW=>aW.ToString()).ToArray()));
        }

        public static Snapshot Merge(IEnumerable<Snapshot> aSnapshots, string aName, string aRoomId)
        {
            var widgets = aSnapshots.SelectMany(aSnap=>aSnap.Widgets);
            return
                new Snapshot
                {
                    Name = aName,
                    RoomId = aRoomId,
                    Widgets = new ListProperty<SnapshotWidget>(widgets)
                };
        }
    }

    [Serializable]
    public class BadSnapshotXmlException : Exception
    {
        //
        // For guidelines regarding the creation of new exception types, see
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/cpgenref/html/cpconerrorraisinghandlingguidelines.asp
        // and
        //    http://msdn.microsoft.com/library/default.asp?url=/library/en-us/dncscol/html/csharp07192001.asp
        //

        public BadSnapshotXmlException()
        {
        }

        public BadSnapshotXmlException(string aMessage) : base(aMessage)
        {
        }

        public BadSnapshotXmlException(string aMessage, Exception aInner) : base(aMessage, aInner)
        {
        }

        protected BadSnapshotXmlException(
            SerializationInfo aInfo,
            StreamingContext aContext) : base(aInfo, aContext)
        {
        }
    }
}