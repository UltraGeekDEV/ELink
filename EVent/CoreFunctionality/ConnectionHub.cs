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
        private Action<string, IComms>? IncommingEventHooked;
        private Action<string, IComms>? OutgoingEventHooked;

        public void AttachIncommingHandler(Action<string, IComms> handler)
        {
            IncommingEventHooked += handler;
        }
        public void AttachOutgoingHandler(Action<string, IComms> handler)
        {
            OutgoingEventHooked += handler;
        }
    }
}
