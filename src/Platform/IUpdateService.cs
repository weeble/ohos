using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Widget.Nodes
{
    public enum UpdateEventType
    {
        UpdateAvailable,
        DownloadFail,
        DownloadComplete,
        UpdateFail,
        UpdateComplete
    }

    public class UpdateEventArgs : EventArgs
    {
        public readonly UpdateEventType eventType;

        public UpdateEventArgs(UpdateEventType type)
        {
            eventType = type;
        }
    }

    public interface IUpdateService : IDisposable
    {
        bool UpdateAvailable { get; }
        void DownloadUpdate();
        void DoUpdate();
        void CheckForUpdate();
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
        public void CheckForUpdate() { }
        public event EventHandler<UpdateEventArgs> UpdateEventHandler { add { } remove { } }
        public void Start() { }
        public void Stop() { }
        public void DownloadUpdate() { }
        public void DoUpdate() { }
        public void Reboot() { }
        public void Refresh() { }
        public void Dispose() { }

        public string Server { get { return ""; } set { } }
        public string Channel { get { return ""; } set { } }
    }
}
