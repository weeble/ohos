using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OpenHome.Widget.Nodes.Logging;

namespace OpenHome.Widget.Nodes
{
    public interface IScheduleRegistryListener
    {
        void NotifyScheduleAdded(string aGuid);
        void NotifyScheduleUpdated(string aGuid);
        void NotifyScheduleRemoved(string aGuid);
        void NotifyActiveChanged();
        void NotifyPauseChanged();
        void NotifyVacationChanged();
    }

    public interface IScheduleRegistryReader
    {
        string[] ActiveScheduleGuids { get; }
        string ScheduleXml(string aGuid);
    }

    public class NullScheduleRegistryReader : IScheduleRegistryReader
    {
        public string[] ActiveScheduleGuids
        {
            get { return new[] { ScheduleRegistry.kScheduleNone, ScheduleRegistry.kScheduleNone, ScheduleRegistry.kScheduleNone 
                , ScheduleRegistry.kScheduleNone , ScheduleRegistry.kScheduleNone , ScheduleRegistry.kScheduleNone , ScheduleRegistry.kScheduleNone };
            }
        }
        public string ScheduleXml(string aGuid)
        {
            throw new BadScheduleHandleException();
        }
    }

    public class BadScheduleHandleException : Exception
    {
    }
    
    public class InvalidXmlException : Exception
    {
    }

    public class VacationScheduleAlreadyExistsException : Exception
    {
    }

    public class ScheduleRegistry : IScheduleRegistryReader
    {
        internal class Schedule
        {
            public uint Handle { get; private set; }
            public uint Seq { get; set; }
            public string Name { get; set; }
            public string Guid { get; private set; }
            public uint Color { get; set; }

            public Schedule(uint aHandle, string aName, string aGuid, uint aColor)
            {
                Handle = aHandle;
                Seq = 0;
                Name = aName;
                Guid = aGuid;
                Color = aColor;
            }
        }

        public List<uint> HandleArray
        {
            get
            {
                lock (this)
                {
                    List<uint> handleArray = new List<uint>(iSchedules.Count);
                    handleArray.AddRange(iSchedules.Select(schedule => schedule.Handle));
                    return handleArray;
                }
            }
        }
        public List<uint> SequenceNumberArray
        {
            get
            {
                lock (this)
                {
                    List<uint> seqArray = new List<uint>(iSchedules.Count);
                    seqArray.AddRange(iSchedules.Select(schedule => schedule.Seq));
                    return seqArray;
                }
            }
        }
        public uint[] ActiveScheduleHandles
        {
            get
            {
                lock (this)
                {
                    if (!iVacationModeActive)
                        return iActiveScheduleHandles;
                    Schedule schedule = FindSchedule(iVacationScheduleGuid);
                    if (schedule == null)
                        return new[] { kHandleNull, kHandleNull, kHandleNull, kHandleNull, kHandleNull, kHandleNull, kHandleNull };
                    return new[] { schedule.Handle, schedule.Handle, schedule.Handle, schedule.Handle, schedule.Handle, schedule.Handle, schedule.Handle };
                }
            }
        }
        public string[] ActiveScheduleGuids
        {
            get
            {
                lock (this)
                {
                    if (iVacationModeActive)
                    {
                        Schedule schedule = FindSchedule(iVacationScheduleGuid);
                        if (schedule == null)
                            return new[] { kScheduleNone, kScheduleNone, kScheduleNone, kScheduleNone, kScheduleNone, kScheduleNone, kScheduleNone };
                        return new[] { schedule.Guid, schedule.Guid, schedule.Guid, schedule.Guid, schedule.Guid, schedule.Guid, schedule.Guid };
                    }
                    string[] guids = new string[7];
                    for (int i = 0; i < guids.Length; i++)
                    {
                        Schedule schedule = FindSchedule(iActiveScheduleHandles[i]);
                        guids[i] = (schedule == null ? kScheduleNone : schedule.Guid);
                    }
                    return guids;
                }
            }
        }
        public bool Paused
        {
            get
            {
                lock (this)
                {
                    return iPaused;
                }
            }
        }
        public bool VacationModeActive
        {
            get
            {
                lock (this)
                {
                    return iVacationModeActive;
                }
            }
        }
        public const uint kHandleNull = 0;
        public const string kScheduleNone = "";
        public const int kActiveSchedulesLength = 7;

        private const string kFileActiveSchedules = "ActiveSchedules.xml";
        private const string kFileSchedulerData = "SchedulerData.xml";
        private const string kTagName = "name";
        private const string kTagGuid = "guid";
        private const string kTagColor = "color";
        private const string kTagSchedule = "schedule";
        private const string kTagWidget = "widget";
        private const string kTagWidgetId = "id";
        private const string kTagWidgetHandle = "handle";
        private const string kTagVacationMode = "VacationMode";
        private const string kTagVacationGuid = "VacationGuid";
        private const string kTagPaused = "paused";
        private const string kTagDay = "day";
        private readonly string[] iDayNames = new[] { "sunday", "monday", "tuesday", "wednesday", "thursday", "friday", "saturday" };
        private const string kNameDefault = "";
        private const uint kColorDefault = 0xffffffff;
        private const string kValueBooleanEnabled = "on";
        private const string kValueBooleanDisabled = "off";
        private readonly string iSchedulesDir;
        private readonly IWidgetHandleMapper iWidgetHandleMapper;
        private IScheduleRegistryListener iListener;
        private readonly ILogger iLogger;
        private uint iNextHandle;
        private readonly List<Schedule> iSchedules;
        public uint[] iActiveScheduleHandles;
        private bool iVacationModeActive;
        private string iVacationScheduleGuid;
        private bool iPaused;

        public ScheduleRegistry(string aSchedulesDir, IWidgetHandleMapper aWidgetIdMapper, ILogger aLogger)
        {
            iSchedulesDir = aSchedulesDir;
            iWidgetHandleMapper = aWidgetIdMapper;
            iLogger = aLogger;
            iSchedules = new List<Schedule>();
            iNextHandle = 1;
            iActiveScheduleHandles = new uint[7];
            BuildSchedulesList();
            ReadActiveSchedules();
            ReadSchedulerData();
        }
        private void BuildSchedulesList()
        {
            DirectoryInfo dirInfo = new DirectoryInfo(iSchedulesDir);
            FileInfo[] files = dirInfo.GetFiles();
            foreach (FileInfo file in files)
            {
                if (file.Name == kFileActiveSchedules || file.Name == kFileSchedulerData)
                    continue;
                string xml = File.ReadAllText(file.FullName, Encoding.UTF8);
                XElement tree = XElement.Parse(xml);
                XElement nameElement = tree.Element(kTagName);
                XElement guidElement = tree.Element(kTagGuid);
                XElement colorElement = tree.Element(kTagColor);
                if (nameElement == null || guidElement == null || colorElement == null)
                    continue;
                string name = nameElement.Value;
                string guid = guidElement.Value;
                uint color = (uint)Convert.ToInt32(colorElement.Value);
                iSchedules.Add(new Schedule(iNextHandle, name, guid, color));
                iNextHandle++;
            }
        }
        public void SetListener(IScheduleRegistryListener aListener)
        {
            iListener = aListener;
        }
        public uint CreateSchedule(string aXml)
        {
            XElement tree = XElement.Parse(aXml);
            XElement nameElement = tree.Element(kTagName);
            XElement colorElement = tree.Element(kTagColor);
            if (nameElement == null || colorElement == null || tree.Element(kTagGuid) != null)
            {
                iLogger.Log("ScheduleRegistry.CreateSchedule: schedule details are invalid");
                throw new InvalidXmlException();
            }
            string name = nameElement.Value;
            uint color = (uint)Convert.ToInt32(colorElement.Value);
            string guid = Guid.NewGuid().ToString();
            tree.Add(new XElement(kTagGuid, guid));
            ConvertHandlesToUdns(tree);
            WriteFile(tree, guid + ".xml");
            uint handle = iNextHandle;
            iNextHandle++;
            lock (this)
            {
                iSchedules.Add(new Schedule(handle, name, guid, color));
            }
            iListener.NotifyScheduleAdded(guid);
            return handle;
        }
        public void CreateVacationSchedule(string aXml)
        {
            uint handle = CreateSchedule(aXml);
            lock (this)
            {
                Schedule schedule = FindSchedule(handle);
                if (schedule == null)
                    return;
                iVacationScheduleGuid = schedule.Guid;
            }
            WriteSchedulerData();
        }
        public void GetDetails(uint aHandle, out string aName, out uint aColor)
        {
            lock (this)
            {
                Schedule schedule = FindSchedule(aHandle);
                if (schedule == null)
                {
                    throw new BadScheduleHandleException();
                }
                aName = schedule.Name;
                aColor = schedule.Color;
            }
        }
        public void SetScheduleXml(uint aHandle, string aXml)
        {
            string guid;
            lock (this)
            {
                Schedule schedule = FindSchedule(aHandle);
                if (schedule == null)
                    throw new BadScheduleHandleException();
                guid = schedule.Guid;
                XElement tree = XElement.Parse(aXml);
                XElement nameElement = tree.Element(kTagName);
                XElement colorElement = tree.Element(kTagColor);
                if (nameElement == null || colorElement == null || tree.Element(kTagGuid) == null)
                {
                    iLogger.Log("ScheduleRegistry.CreateSchedule: schedule details are invalid");
                    throw new InvalidXmlException();
                }
                schedule.Name = nameElement.Value;
                schedule.Color = (uint)Convert.ToInt32(colorElement.Value);
                schedule.Seq++; // TODO: consider checking for changes in name/color/tasks before incrementing sequence number
                ConvertHandlesToUdns(tree);

                File.WriteAllText(ScheduleFullName(schedule.Guid), tree.ToString(), Encoding.UTF8);
            }
            iListener.NotifyScheduleUpdated(guid);
        }
        public string ScheduleXml(uint aHandle)
        {
            lock (this)
            {
                Schedule schedule = FindSchedule(aHandle);
                if (schedule == null)
                    throw new BadScheduleHandleException();
                string xml = File.ReadAllText(ScheduleFullName(schedule.Guid), Encoding.UTF8);
                XElement tree = XElement.Parse(xml);
                // Copy elements into a list before iteration, because it's not safe to
                // mutate the XML tree while iterating over it.
                List<XElement> widgetElements = tree.Elements(kTagWidget).ToList();
                foreach (var widget in widgetElements)
                {
                    XElement idElement = widget.Element(kTagWidgetId);
                    string udn = idElement.Value;
                    uint handle;
                    if (!iWidgetHandleMapper.TryGetWidgetHandle(udn, out handle))
                    {
                        widget.Remove();
                        continue;
                    }
                    idElement.Remove();
                    widget.Add(new XElement(kTagWidgetHandle, handle));
                }
                return tree.ToString();
            }
        }
        public string ScheduleXml(string aGuid)
        {
            lock (this)
            {
                Schedule schedule = FindSchedule(aGuid);
                if (schedule == null)
                    throw new BadScheduleHandleException();
                return File.ReadAllText(ScheduleFullName(schedule.Guid), Encoding.UTF8);
            }
        }
        public void DeleteSchedule(uint aHandle)
        {
            bool activeChanged = false;
            string guid;
            lock (this)
            {
                int index = -1;
                for (int i = 0; i < iSchedules.Count; i++)
                {
                    if (iSchedules[i].Handle == aHandle)
                    {
                        index = i;
                        break;
                    }
                }
                if (index == -1)
                    throw new BadScheduleHandleException();
                guid = iSchedules[index].Guid;
                File.Delete(ScheduleFullName(iSchedules[index].Guid));
                iSchedules.RemoveAt(index);
                for (int i = 0; i < iActiveScheduleHandles.Length; i++)
                {
                    if (iActiveScheduleHandles[i] == aHandle)
                    {
                        iActiveScheduleHandles[i] = kHandleNull;
                        activeChanged = true;
                    }
                }
            }
            iListener.NotifyScheduleRemoved(guid);
            if (activeChanged)
            {
                iListener.NotifyActiveChanged();
            }
        }
        public void SetActiveSchedules(uint[] aHandles)
        {
            bool changed = false;
            lock (this)
            {
                for (int i = 0; i < aHandles.Length; i++)
                {
                    Schedule schedule = FindSchedule(aHandles[i]);
                    if (schedule == null)
                    {
                        if (iActiveScheduleHandles[i] != kHandleNull)
                            changed = true;
                        iActiveScheduleHandles[i] = kHandleNull;
                    }
                    else
                    {
                        if (iActiveScheduleHandles[i] != aHandles[i])
                            changed = true;
                        iActiveScheduleHandles[i] = aHandles[i];
                    }
                }
            }
            if (changed)
            {
                iListener.NotifyActiveChanged();
                WriteActiveSchedules();
            }
        }
        public void EnableVacationMode(bool aEnable)
        {
            lock (this)
            {
                if (aEnable == iVacationModeActive)
                    return;
                iVacationModeActive = aEnable;
            }
            iListener.NotifyVacationChanged();
            iListener.NotifyActiveChanged();
        }
        public void PauseScheduler(bool aPause)
        {
            lock (this)
            {
                if (iPaused == aPause)
                    return;
                iPaused = aPause;
            }
            iListener.NotifyPauseChanged();
        }
        private void ReadActiveSchedules()
        {
            string activeSchedulesFile = iSchedulesDir + Path.DirectorySeparatorChar + kFileActiveSchedules;
            if (!File.Exists(activeSchedulesFile))
                return;
            string xml = File.ReadAllText(activeSchedulesFile, Encoding.UTF8);
            XElement tree = XElement.Parse(xml);
            IEnumerable<XElement> scheduleIds = tree.Elements(kTagSchedule);
            foreach (var scheduleId in scheduleIds)
            {
                string day = scheduleId.Element(kTagDay).Value;
                int index = -1;
                for (int i = 0; i < iDayNames.Length; i++)
                {
                    if (String.Compare(day, iDayNames[i], StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        index = i;
                        break;
                    }
                }
                if (index == -1)
                    continue;
                string guid = scheduleId.Element("guid").Value;
                Schedule schedule = FindSchedule(guid);
                iActiveScheduleHandles[index] = (schedule == null ? kHandleNull : schedule.Handle);
            }
        }
        private void WriteActiveSchedules()
        {
            lock (this)
            {
                XElement tree = new XElement("activeSchedules");
                for (int i=0; i<iActiveScheduleHandles.Length; i++)
                {
                    Schedule schedule = FindSchedule(iActiveScheduleHandles[i]);
                    string guid = (schedule == null? kScheduleNone : schedule.Guid);
                    tree.Add(new XElement(kTagSchedule, new XElement(kTagDay, iDayNames[i]), new XElement(kTagGuid, guid)));
                }
                WriteFile(tree, kFileActiveSchedules);
            }
        }
        private void ReadSchedulerData()
        {
            string dataFile = iSchedulesDir + Path.DirectorySeparatorChar + kFileSchedulerData;
            if (!File.Exists(dataFile))
            {
                iVacationScheduleGuid = kScheduleNone;
                return;
            }
            string xml = File.ReadAllText(dataFile, Encoding.UTF8);
            XElement tree = XElement.Parse(xml);
            iVacationModeActive = (String.Compare(tree.Element(kTagVacationMode).Value, kValueBooleanEnabled, StringComparison.OrdinalIgnoreCase) == 0);
            iVacationScheduleGuid = tree.Element(kTagVacationGuid).Value;
            iPaused = (String.Compare(tree.Element(kTagPaused).Value, kValueBooleanEnabled, StringComparison.OrdinalIgnoreCase) == 0);
        }
        private void WriteSchedulerData()
        {
            lock (this)
            {
                XElement tree = new XElement("schedulerData");
                tree.Add(new XElement(kTagVacationMode, iVacationModeActive ? kValueBooleanEnabled : kValueBooleanDisabled));
                tree.Add(new XElement(kTagVacationGuid, iVacationScheduleGuid));
                tree.Add(new XElement(kTagPaused, iPaused ? kValueBooleanEnabled : kValueBooleanDisabled));
                WriteFile(tree, kFileSchedulerData);
            }
        }
        private void ConvertHandlesToUdns(XElement aTree)
        {
            // Copy elements into a list to avoid mutating the XML tree during
            // iteration.
            List<XElement> widgetElements = aTree.Elements(kTagWidget).ToList();
            foreach (var widget in widgetElements)
            {
                XElement handleElement = widget.Element(kTagWidgetHandle);
                uint handle = Convert.ToUInt32(handleElement.Value);
                string udn;
                if (!iWidgetHandleMapper.TryGetWidgetUdn(handle, out udn))
                {
                    widget.Remove();
                    continue;
                }
                handleElement.Remove();
                widget.Add(new XElement(kTagWidgetId, udn));
            }
        }
        private void WriteFile(XElement aTree, string aFileName)
        {
            string filename = iSchedulesDir + Path.DirectorySeparatorChar + aFileName;
            XmlWriter writer = XmlWriter.Create(filename);
            aTree.WriteTo(writer);
            writer.Close();
        }
        private string ScheduleFullName(string aGuid)
        {
            return iSchedulesDir + Path.DirectorySeparatorChar + aGuid + ".xml";
        }
        private Schedule FindSchedule(uint aHandle)
        {
            foreach (var schedule in iSchedules)
            {
                if (schedule.Handle == aHandle)
                {
                    return schedule;
                }
            }
            return null;
        }
        private Schedule FindSchedule(string aGuid)
        {
            foreach (var schedule in iSchedules)
            {
                if (schedule.Guid == aGuid)
                {
                    return schedule;
                }
            }
            return null;
        }
    }
}
