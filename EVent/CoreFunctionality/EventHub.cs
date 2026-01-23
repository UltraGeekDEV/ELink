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
        private Dictionary<IServer,object> serverLocks;

        public EventHub(string HubID,params ICommsProtocol[] connections)
        {
            this.HubID = HubID;
            serverLocks = connections.Select(x => new { connection = (IServer)new EVentConnection(x), lockObject = new object() }).ToDictionary(x => x.connection, x => x.lockObject);
        }
        public void Setup()
        {
            foreach (var connection in serverLocks.Keys)
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
            var addEventPackage = new Package("EventAdded", PackageType.ServerAdminEvent,((BinaryConvertableString)eventID));
            SendCommand(addEventPackage, server, x => { });
        }
        private void RemoveEvent(string eventID, IServer server)
        {
            Debug.WriteLine($"Removed Event: {eventID}");
            var addEventPackage = new Package("EventRemoved", PackageType.ServerAdminEvent, ((BinaryConvertableString)eventID));
            SendCommand(addEventPackage, server, x => { });
        }
        private void DataRecieved(Package package,IServer? server,Action<Package> callback)
        {
            var eventList = package.EventID.Split('|').ToHashSet();
            HashSet<IServer> serversCopy = serverLocks.Keys.Where(x=>x!=server).ToHashSet();

            if (package.type != PackageType.ServerAdminEvent)
            {
                InterconnectDataReceived(package, null, x => { });
            }
            else
            {
                if (package.EventID == "QuerryEvents")
                {
                    var eventQuerryResponse = new Package(EventID: "ListEvents", PackageType.ServerAdminEvent, (BinaryCovnertableCollection<BinaryConvertableString>)serverLocks.Keys.SelectMany(x => x.GetEvents().Select(x => (BinaryConvertableString)x)).ToList());
                    callback(eventQuerryResponse);
                }
            }

            if (serversCopy.Count == 0)
            {
                return;
            }

            foreach (var client in serversCopy)
            {
                lock (serverLocks[client])
                {
                    client.SendData(package);
                }
            }
        }
        private void SendCommand(Package package, IServer server, Action<Package> callback)
        {
            HashSet<IServer> serversCopy = serverLocks.Keys.Where(x => x != server).ToHashSet();

            if (serversCopy.Count == 0)
            {
                return;
            }

            foreach (var partner in serversCopy)
            {
                lock (serverLocks[partner])
                {
                    partner.SendCommandOnInterconnect(package);
                }
            }
        }
        private void InterconnectDataReceived(Package package,IServer? server, Action<Package> callback)
        {
            if (package.type == PackageType.ServerAdminEvent)
            {
                if (package.EventID == "QuerryEvents")
                {
                    var eventQuerryResponse = new Package(EventID: "ListEvents", PackageType.ServerAdminEvent, (BinaryCovnertableCollection<BinaryConvertableString>)serverLocks.Keys.SelectMany(x => x.GetEvents().Select(x => (BinaryConvertableString)x)).ToList());
                    callback(eventQuerryResponse);
                    return;
                }
                if (package.EventID == "EventAdded" || package.EventID == "EventRemoved")
                {
                    SendCommand(package, server, callback);
                    return;
                }
                else
                {
                    return;
                }
            }

            HashSet<IServer> serversCopy = serverLocks.Keys.Where(x=>x != server).ToHashSet();

            if (serversCopy.Count == 0)
            {
                return;
            }

            foreach (var partner in serversCopy)
            {
                lock (serverLocks[partner])
                {
                    partner.SendDataOnInterconnect(package);
                }
            }
        }
    }
}
