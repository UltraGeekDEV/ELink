using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.CoreFunctionality;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Connections
{
    public interface ICommsProtocol
    {
        public void OnClientAccepted(Action<QueuedClient> action);
        public void Run();
        public Task<QueuedClient?> EstablishInterconnect(Package package);
        public void Stop();
    }
}
