using EVent.Comms;
using EVent.Connections;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace EVent.CoreFunctionality
{
    public class EventHub
    {
        public string HubID;
        private Dictionary<Delegate, Dictionary<string, (Action<Package> network, Action<IBinaryConvertable> local)>> handlers = new Dictionary<Delegate, Dictionary<string, (Action<Package> network, Action<IBinaryConvertable> local)>>();
        private Dictionary<string, (Action<Package>? network, Action<IBinaryConvertable>? local)> nativeCallbacks = new Dictionary<string, (Action<Package>? network, Action<IBinaryConvertable>? local)>();
        private object nativeCallbacksLock = new object();
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
                connection.OnDataReceived(DataReceived);
                connection.OnDataReceived((package,server,handler) => FireNativeEvents(package));
                connection.OnInterconnectDataReceived(InterconnectDataReceived);
                connection.OnInterconnectDataReceived((package, server, handler) => FireNativeEvents(package));
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
        private void DataReceived(Package package,IServer? server,Action<Package> callback)
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
                    var eventQuerryResponse = new Package(EventID: "ListEvents", PackageType.ServerAdminEvent, (BinaryConvertableCollection<BinaryConvertableString>)serverLocks.Keys.SelectMany(x => x.GetEvents().Select(x => (BinaryConvertableString)x)).ToList());
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
                switch (package.EventID) 
                {
                    case "QuerryEvents":
                        {
                            var eventQuerryResponse = new Package(EventID: "ListEvents", PackageType.ServerAdminEvent, (BinaryConvertableCollection<BinaryConvertableString>)serverLocks.Keys.SelectMany(x => x.GetEvents().Select(x => (BinaryConvertableString)x)).ToList());
                            callback(eventQuerryResponse);
                            return;
                        }
                    case "EventAdded":
                    case "EventRemoved":
                        {
                            SendCommand(package, server, callback);
                            return;
                        }
                    case "CreateInterconnect":
                        {
                            var connections = serverLocks.Keys.ToList();
                            foreach (var connection in connections)
                            {
                                connection.CommandServer(package);
                            }
                            return;
                        }
                default:
                {
                    return;
                }
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

        private void FireNativeEvents(Package package)
        {
            lock (nativeCallbacksLock)
            {
                (Action<Package>? network, Action<IBinaryConvertable>? local) handler;
                if (nativeCallbacks.TryGetValue(package.EventID, out handler))
                {
                    _ = Task.Run(() => { handler.network?.Invoke(package); });
                }
            }
        }

        public void HookEvent<T>(string eventID,Action<T> action) where T : IBinaryConvertable, new()
        {
            try
            {
                lock (nativeCallbacksLock)
                {
                    (Action<Package> network, Action<IBinaryConvertable> local) callback = (package =>
                    {
                        T data = new();
                        if (data.FromBytes(package.Data))
                        {
                            action?.Invoke(data);
                        }
                    },
                    binaryConvertable =>
                    {
                        if (binaryConvertable is T)
                        {
                            action?.Invoke((T)binaryConvertable);
                        }
                    }
                    );

                    handlers.TryAdd(action, new Dictionary<string, (Action<Package> network, Action<IBinaryConvertable> local)>());
                    if (!handlers[action].TryAdd(eventID, callback))
                    {
                        return;
                    }

                    if (!nativeCallbacks.ContainsKey(eventID))
                    {
                        nativeCallbacks.Add(eventID, callback);
                        AddEvent(eventID, null);
                    }
                    else
                    {
                        var modify = nativeCallbacks[eventID];
                        modify.network += callback.network;
                        modify.local += callback.local;
                        nativeCallbacks[eventID] = modify;
                    }
                }
            }
            catch
            {
                Debug.WriteLine("Lcoal hook failed");
            }
        }
        public void UnHook<T>(string eventID, Action<T> action) where T : IBinaryConvertable, new()
        {
            lock (nativeCallbacksLock)
            {
                Dictionary<string,(Action<Package> network, Action<IBinaryConvertable> local)>? eventList;
                if (handlers.TryGetValue(action,out eventList))
                {
                    (Action<Package> network, Action<IBinaryConvertable> local) callback;
                    if (eventList.TryGetValue(eventID,out callback))
                    {
                        if (nativeCallbacks.ContainsKey(eventID))
                        {
                            var modify = nativeCallbacks[eventID];
                            modify.network -= callback.network;
                            modify.local -= callback.local;

                            if (modify.network == null)
                            {
                                nativeCallbacks.Remove(eventID);
                            }
                            else
                            {
                                nativeCallbacks[eventID] = modify;
                            }
                        }
                        eventList.Remove(eventID);
                        if (eventList.Count == 0)
                        {
                            RemoveEvent(eventID, null);
                            handlers.Remove(action);
                        }
                    }
                }
            }
        }
        public void Fire(string eventID,IBinaryConvertable data)
        {
            lock (nativeCallbacksLock)
            {
                (Action<Package>? network, Action<IBinaryConvertable>? local) handler;
                if (nativeCallbacks.TryGetValue(eventID, out handler))
                {
                    _ = Task.Run(() => { handler.local?.Invoke(data); });
                }
            }

            DataReceived(new Package(eventID, PackageType.Data, data), null, x => { });
        }
    }
}
