using EVent.Comms;
using EVent.Connections;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EVent.CoreFunctionality
{
    public class EventHub
    {
        public string HubID;
        private Dictionary<IServer, HashSet<string>> servers = new Dictionary<IServer, HashSet<string>>();
        private object eventsLock = new object();
        private Dictionary<IServer,object> serverLocks;
        public EventHub(List<IServer> connections,string HubID)
        {
            this.HubID = HubID;
            servers = connections.ToDictionary(x=>x,y=>new HashSet<string>());
            serverLocks = connections.Select(x => new { connection = x, lockObject = new object() }).ToDictionary(x => x.connection, x => x.lockObject);
        }
        public void Setup()
        {
            foreach (var connection in servers.Keys)
            {
                connection.OnEventAdded(AddEvent);
                connection.OnEventRemoved(RemoveEvent);
                connection.OnDataRecieved(DataRecieved);
                connection.OnInterconnectDataRecieved(InterconnectDataReceived);
                connection.Run();
            }
        }
        private void AddEvent(string eventID,IServer server)
        {
            Debug.WriteLine($"Added Event: {eventID}");
            lock (eventsLock)
            {
                servers[server].Add(eventID);
            }

            var removeEventPackage = new PackageInfo() { EventID = eventID, type = PackageType.ConnectEvent, Data = new byte[0], Sender = HubID };

            InterconnectDataReceived(removeEventPackage, null);
        }
        private void RemoveEvent(string eventID, IServer server)
        {
            Debug.WriteLine($"Removed Event: {eventID}");
            lock (eventsLock)
            {
                servers[server].Remove(eventID);
            }

            var removeEventPackage = new PackageInfo() { EventID = eventID , type = PackageType.DisconnectEvent, Data = new byte[0],Sender = HubID };

            InterconnectDataReceived(removeEventPackage, null);
        }
        private void DataRecieved(PackageInfo package,IServer server)
        {
            var eventList = package.EventID.Split('|').ToHashSet();
            Dictionary<IServer, HashSet<string>> serversCopy;

            lock (eventsLock)
            {
                serversCopy = servers.ToDictionary();

            }
            var serverList = serversCopy.Where(x => x.Value.Any(y => eventList.Contains(y)) && x.Key != server).Select(x=>x.Key).ToList();

            if (servers.Count == 0)
            {
                return;
            }

            foreach (var partner in serverList)
            {
                lock (serverLocks[partner])
                {
                    partner.SendData(package);
                }
            }
            InterconnectDataReceived(package, null);
        }
        private void InterconnectDataReceived(PackageInfo package,IServer? server)
        {
            var eventList = package.EventID.Split('|').ToHashSet();
            Dictionary<IServer, HashSet<string>> serversCopy;

            lock (eventsLock)
            {
                serversCopy = servers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new HashSet<string>(kvp.Value)
                                );


            }
            var serverList = serversCopy.Where(x => (x.Value.Any(y => eventList.Contains(y)) || package.type != PackageType.Data ||true) && x.Key != server).Select(x => x.Key).ToList();

            if (serverList.Count == 0)
            {
                return;
            }

            foreach (var partner in serverList)
            {
                lock (serverLocks[partner])
                {
                    partner.SendDataOnInterconnect(package);
                }
            }
        }
    }
}
