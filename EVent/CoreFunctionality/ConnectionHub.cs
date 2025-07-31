using EVent.Comms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.CoreFunctionality
{
    public class ConnectionHub
    {
        private Action<string, IComms>? EventHooked;

        public void AttachConnectionHandler(Action<string, IComms> handler)
        {
            EventHooked += handler;
        }
    }
}
