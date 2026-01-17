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
        public void OnEventAdded(Action<string, IServer> handler);
        public void OnEventRemoved(Action<string, IServer> handler);
        public Task SendData(Package data);
        public Task SendDataOnInterconnect(Package data);
        public void OnDataRecieved(Action<Package, IServer?, Action<Package>> handler);
        public void OnInterconnectDataRecieved(Action<Package, IServer?, Action<Package>> handler);
        public IEnumerable<string> GetEvents();
        public void Run();
        public void Stop();
    }
}
