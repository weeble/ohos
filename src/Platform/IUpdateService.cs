using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenHome.Os.Platform
{
    public interface IUpdateService : IDisposable
    {
        bool UpdateAvailable { get; }
        void TriggerUpdate();
        void CheckForUpdate();
        event EventHandler UpdatesAvailableChanged;
        void Start();
        void Stop();
        
        string Server { get; set; }
        string Channel { get; set; }
    }
    public class NullUpdateService : IUpdateService, IDisposable
    {
        public bool UpdateAvailable { get { return false; } }
        public void CheckForUpdate() { }
        public event EventHandler UpdatesAvailableChanged { add { } remove { } }
        public void Start()
        {
        }

        public void Stop()
        {
        }

        public void TriggerUpdate() { }
        public void Refresh() { }
        public void Dispose() { }

        public string Server { get { return ""; } set { } }
        public string Channel { get { return ""; } set { } }
    }
}
