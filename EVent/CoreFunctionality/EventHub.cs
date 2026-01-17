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
        private HashSet<IServer> servers = new HashSet<IServer>();
        private Dictionary<IServer,object> serverLocks;

        public EventHub(List<IServer> connections,string HubID)
        {
            this.HubID = HubID;
            servers = connections.ToHashSet();
            serverLocks = connections.Select(x => new { connection = x, lockObject = new object() }).ToDictionary(x => x.connection, x => x.lockObject);
        }
        public void Setup()
        {
            foreach (var connection in servers)
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
            InterconnectDataReceived(addEventPackage, server, x => { });
        }
        private void RemoveEvent(string eventID, IServer server)
        {
            Debug.WriteLine($"Removed Event: {eventID}");
            var removeEventPackage = new Package("EventRemoved", PackageType.ServerAdminEvent, ((BinaryConvertableString)eventID));
            InterconnectDataReceived(removeEventPackage, server, x => { });
        }
        private void DataRecieved(Package package,IServer? server,Action<Package> callback)
        {
            var eventList = package.EventID.Split('|').ToHashSet();
            HashSet<IServer> serversCopy = servers.Where(x=>x!=server).ToHashSet();

            if (package.type != PackageType.ServerAdminEvent)
            {
                InterconnectDataReceived(package, null, x => { });
            }
            else
            {
                if (package.EventID == "QuerryEvents")
                {
                    var eventQuerryResponse = new Package(EventID: "ListEvents", PackageType.ServerAdminEvent, (BinaryCovnertableCollection<BinaryConvertableString>)servers.SelectMany(x => x.GetEvents().Select(x => (BinaryConvertableString)x)).ToList());
                    server.SendData(eventQuerryResponse);
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

        private void InterconnectDataReceived(Package package,IServer? server, Action<Package> callback)
        {
            if (package.type == PackageType.ServerAdminEvent)
            {
                if (package.EventID == "QuerryEvents")
                {
                    var eventQuerryResponse = new Package(EventID: "ListEvents", PackageType.ServerAdminEvent, (BinaryCovnertableCollection<BinaryConvertableString>)servers.SelectMany(x => x.GetEvents().Select(x => (BinaryConvertableString)x)).ToList());
                    callback(eventQuerryResponse);
                    return;
                }
                if (package.EventID == "EventAdded" || package.EventID == "EventRemoved")
                {

                }
                else
                {
                    return;
                }
            }

            HashSet<IServer> serversCopy = servers.Where(x=>x != server).ToHashSet();

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
