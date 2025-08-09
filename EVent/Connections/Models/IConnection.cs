using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EVent.Comms;

namespace EVent.Connections.Models
{
    public interface IConnection
    {
        public bool HasEvent(string eventID);
        public bool HasEvent(IEnumerable<string> events);
        public Task SendData(PackageInfo data);
        public void OnDataRecieved(Action<PackageInfo> handler);
        public void Stop();
    }
}
