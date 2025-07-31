using EVent.Comms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.CoreFunctionality
{
    public class EventHub
    {
        private Dictionary<string,List<IComms>> outgoingEvents = new Dictionary<string, List<IComms>>();
        private Dictionary<string,List<IComms>> incommingEvents = new Dictionary<string, List<IComms>>();
        private ConnectionHub connectionHub = new ConnectionHub();
        public void Setup()
        {
            connectionHub.AttachIncommingHandler(HookIncommingEvent);
            connectionHub.AttachOutgoingHandler(HookOutgoingEvent);
        }
        private void HookOutgoingEvent(string eventID,IComms eventHandler)
        {
            if (!outgoingEvents.ContainsKey(eventID))
            {
                outgoingEvents[eventID] = new List<IComms>();
            }

            outgoingEvents[eventID].Add(eventHandler);
        }
        private void HookIncommingEvent(string eventID, IComms eventHandler)
        {
            if (!incommingEvents.ContainsKey(eventID))
            {
                incommingEvents[eventID] = new List<IComms>();
            }

            incommingEvents[eventID].Add(eventHandler);
            eventHandler.HookDataRecieved<byte[]>((data) => HandleIncomming(eventID, data));
        }
        private void HandleIncomming(string eventID,byte[] data)
        {
            if(!outgoingEvents.ContainsKey(eventID))
            {
                return;
            }

            foreach (var outgoingEvent in outgoingEvents[eventID])
            {
                outgoingEvent.Send(data);
            }
        }
    }
}
