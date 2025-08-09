using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EVent.Comms;
using EVent.Connections.Models.BaseBinaryConvertables;

namespace EVent.Connections.Models
{
    public interface IServer
    {
        public bool HasEvent(string eventID);
        public bool HasEvent(IEnumerable<string> events);
        public void OnEventAdded(Action<string, IServer> handler);
        public void OnEventRemoved(Action<string, IServer> handler);
        public Task SendData(PackageInfo data);
        public void OnDataRecieved(Action<PackageInfo> handler);
        public void Run();
        public void Stop();
    }
}
