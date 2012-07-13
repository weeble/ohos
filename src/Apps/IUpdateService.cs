using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Os.Platform
{
    public enum UpdateEventType
    {
        UpdateAvailable,
        DownloadFail,
        DownloadComplete,
        UpdateFail,
        UpdateComplete,
        Progress
    }

    public class UpdateEventArgs : EventArgs
    {
        public readonly UpdateEventType eventType;

        public readonly object eventData;

        public UpdateEventArgs(UpdateEventType type)
        {
            eventType = type;
        }

        public UpdateEventArgs(UpdateEventType type, object data)
        {
            eventType = type;
            eventData = data;
        }
    }

    public interface IUpdateService : IDisposable
    {
        bool UpdateAvailable { get; }
        bool DownloadUpdate();
        bool DoUpdate();
        bool CheckForUpdate();
        void CancelUpdate();
        void Reboot();
        event EventHandler<UpdateEventArgs> UpdateEventHandler;
        void Start();
        void Stop();
        
        string Server { get; set; }
        string Channel { get; set; }
    }
    public class NullUpdateService : IUpdateService, IDisposable
    {
        public bool UpdateAvailable { get { return false; } }
        public bool CheckForUpdate() { return false; }
        public event EventHandler<UpdateEventArgs> UpdateEventHandler { add { } remove { } }
        public void Start() { }
        public void Stop() { }
        public bool DownloadUpdate() { return false; }
        public bool DoUpdate() { return false; }
        public void CancelUpdate() { }
        public void Reboot() { }
        public void Refresh() { }
        public void Dispose() { }

        public string Server { get { return ""; } set { } }
        public string Channel { get { return ""; } set { } }
    }
}
