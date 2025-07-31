using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.Comms
{
    public interface IConnection
    {
        public void SendData(byte[] data);
        public void ExpectData(string eventID, IComms handler);
    }
}
