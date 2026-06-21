using EVent.Connections;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EVent.CoreFunctionality
{
    internal class EVentConnection : IServer
    {
        Action<Package, IServer?, Action<Package>>? DataReceived;
        Action<Package, IServer?, Action<Package>>? InterconnectDataReceivedEvent;
        Action<string, IServer>? AddedEvent;
        Action<string, IServer>? RemovedEvent;
        ICommsProtocol protocolHandler;

        Dictionary<string, HashSet<QueuedClient>> localEvents = new Dictionary<string, HashSet<QueuedClient>>();
        Dictionary<string, HashSet<QueuedClient>> interconnectEvents = new Dictionary<string, HashSet<QueuedClient>>();

        Dictionary<QueuedClient, HashSet<string>> localClients = new Dictionary<QueuedClient, HashSet<string>>();
        Dictionary<QueuedClient, HashSet<string>> interconnectClients = new Dictionary<QueuedClient, HashSet<string>>();

        object interconnectLock = new object();
        object localLock = new object();

        public EVentConnection(ICommsProtocol protocolHandler)
        {
            this.protocolHandler = protocolHandler;
            this.protocolHandler.OnClientAccepted(AcceptClient);
        }

        public void OnDataReceived(Action<Package, IServer?, Action<Package>> handler)
        {
            DataReceived += handler;
        }
        public void OnInterconnectDataReceived(Action<Package, IServer?, Action<Package>> handler)
        {
            InterconnectDataReceivedEvent += handler;
        }
        public void OnEventAdded(Action<string, IServer> handler)
        {
            AddedEvent += handler;
        }
        public void OnEventRemoved(Action<string, IServer> handler)
        {
            RemovedEvent += handler;
        }

        public void Run()
        {
            protocolHandler.Run();
        }
        private async void RunClient(QueuedClient client, bool isInterconnect)
        {
            try
            {
                Package? package = null;
                while ((package = await client.ReadPackage()) != null)
                {
                    if (isInterconnect)
                    {
                        isInterconnect = await ProcessInterconnectData(package, client);
                    }
                    else
                    {
                        isInterconnect = await ProcessLocalData(package, client);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception on server: {ex.Message}");
            }
            finally
            {
                if (isInterconnect)
                {
                    RemoveFromInterconnect(client);
                }
                else
                {
                    RemoveFromLocal(client);
                }
            }

        }
        private async Task<bool> ProcessLocalData(Package package, QueuedClient? client)
        {
            switch (package.type)
            {
                case PackageType.ServerAdminEvent:
                    {
                        await SendData(package);
                        switch (package.EventID)
                        {
                            case "DisconnectClient":
                                {
                                    client!.Shutdown();
                                    break;
                                }
                            case "CreateInterconnect":
                                {
                                    var interconnect = await protocolHandler.EstablishInterconnect(package);
                                    if (interconnect != null)
                                    {
                                        AcceptClient(interconnect, true);
                                        await interconnect.Send(new Package("InterconnectRunning", PackageType.ServerAdminEvent));
                                    }
                                    break;
                                }
                            case "UpgradeToInterconnect":
                                {
                                    lock (interconnectLock)
                                    {
                                        if (!interconnectClients.ContainsKey(client!))
                                        {
                                            interconnectClients.Add(client!, new HashSet<string>());
                                        }
                                    }
                                    if (localClients.TryGetValue(client!, out var events))
                                    {
                                        foreach (var item in events)
                                        {
                                            HookInterconnect(client!, item);
                                        }
                                    }
                                    RemoveFromLocal(client!);

                                    await client!.Send(new Package("InterconnectRunning", PackageType.ServerAdminEvent));
                                    return true;
                                }
                            case "EventRemoved":
                                {
                                    BinaryConvertableString? payload;
                                    if ((payload = Deserialize<BinaryConvertableString>(package)) != null)
                                    {
                                        UnhookLocal(client!, payload);
                                        RemovedEvent?.Invoke(payload, this);
                                    }
                                    break;
                                }
                            case "EventAdded":
                                {
                                    BinaryConvertableString? payload;
                                    if ((payload = Deserialize<BinaryConvertableString>(package)) != null)
                                    {
                                        HookLocal(client!, payload);
                                        AddedEvent?.Invoke(payload, null);
                                    }

                                    break;
                                }
                        }
                        break;
                    }
                default:
                    {
                        if (package.type != PackageType.ServerAdminEvent)
                        {
                            await SendDataOnInterconnect(package, client);

                            await SendData(package);
                        }

                        InterconnectDataReceivedEvent?.Invoke(package, this, x => { });
                        break;
                    }
            }
            return false;
        }
        private static HashSet<QueuedClient> GetClients(QueuedClient? receivedFrom, HashSet<QueuedClient> clients)
        {
            if (receivedFrom != null)
            {
                return clients.Where(x => x != receivedFrom).ToHashSet();
            }
            else
            {
                return clients;
            }
        }
        public async Task SendData(Package package, QueuedClient? receivedFrom = null)
        {
            HashSet<QueuedClient> clientSnapshot;
            lock (localLock)
            {
                if (localEvents.TryGetValue(package.EventID, out var clients))
                {
                    clientSnapshot = GetClients(receivedFrom, clients);
                }
                else
                {
                    return;
                }
            }

            foreach (var client in clientSnapshot)
            {
                await client.Send(package);
            }
        }
        private async Task<bool> ProcessInterconnectData(Package package, QueuedClient client)
        {
            switch (package.type)
            {
                case PackageType.ServerAdminEvent:
                    {
                        await SendData(package);
                        switch (package.EventID)
                        {
                            case "DisconnectInterconnect":
                                {
                                    RemoveFromInterconnect(client);
                                    client.Shutdown();
                                    break;
                                }
                            case "EventRemoved":
                                {
                                    BinaryConvertableString? payload;
                                    if ((payload = Deserialize<BinaryConvertableString>(package)) != null)
                                    {
                                        UnhookInterconnect(client, payload);
                                    }
                                    break;
                                }
                            case "EventAdded":
                                {
                                    BinaryConvertableString? payload;
                                    if ((payload = Deserialize<BinaryConvertableString>(package)) != null)
                                    {
                                        HookInterconnect(client, payload);
                                    }

                                    break;
                                }
                            case "QuerryEvents":
                                {
                                    InterconnectDataReceivedEvent?.Invoke(package, this, x => { _ = client.Send(x); });
                                    break;
                                }
                            case "ListEvents":
                                {
                                    var collection = Deserialize<BinaryConvertableCollection<BinaryConvertableString>>(package);
                                    if (collection == null)
                                    {
                                        break;
                                    }

                                    var remoteEvents = collection.binaryConvertables.Where(x => x != null).Select(x => x!).ToList();

                                    foreach (var item in remoteEvents)
                                    {
                                        HookInterconnect(client, item);
                                    }
                                    break;
                                }
                            case "InterconnectRunning":
                                {
                                    await client.Send(new Package("QuerryEvents", PackageType.ServerAdminEvent));
                                    break;
                                }
                        }
                        break;
                    }
                default:
                    {
                        if (package.type != PackageType.ServerAdminEvent)
                        {
                            await SendDataOnInterconnect(package, client);

                            await SendData(package);
                        }

                        InterconnectDataReceivedEvent?.Invoke(package, this, x => { });
                        break;
                    }
            }

            return true;
        }
        public async Task SendDataOnInterconnect(Package package, QueuedClient? receivedFrom = null)
        {
            HashSet<QueuedClient> sendTo;
            lock (interconnectLock)
            {
                if (interconnectEvents.TryGetValue(package.EventID, out var partners))
                {
                    sendTo = GetClients(receivedFrom, partners);
                }
                else
                {
                    return;
                }
            }

            foreach (var partner in sendTo)
            {
                await partner.Send(package);
            }
        }
        public async Task SendCommandOnInterconnect(Package package, QueuedClient? receivedFrom = null)
        {
            HashSet<QueuedClient> sendTo;
            lock (interconnectLock)
            {
                sendTo = GetClients(receivedFrom, interconnectClients.Keys.ToHashSet());
            }

            foreach (var partner in sendTo)
            {
                await partner.Send(package);
            }
        }
        public IEnumerable<string> GetEvents()
        {
            return interconnectEvents.Keys.Union(localEvents.Keys).ToList();
        }
        public void Stop()
        {
            foreach (var partner in interconnectEvents)
            {

            }
        }
        private void AcceptClient(QueuedClient client)
        {
            AcceptClient(client, false);
        }
        private void AcceptClient(QueuedClient client, bool interconnect)
        {
            localClients.Add(client, new HashSet<string>());
            RunClient(client, interconnect);
        }
        private T? Deserialize<T>(Package package) where T : IBinaryConvertable, new()
        {
            T item = new T();
            if (item.FromBytes(package.Data))
            {
                return item;
            }
            else
            {
                return default;
            }
        }
        private void RemoveFromLocal(QueuedClient client)
        {
            lock (localEvents)
            {
                if (localClients.TryGetValue(client, out var clientEvents))
                {
                    foreach (var item in clientEvents)
                    {
                        localEvents[item].Remove(client);

                        if (localEvents[item].Count == 0)
                        {
                            localEvents.Remove(item);
                            SendEventRemoved(item, client);
                        }
                    }

                    localClients.Remove(client);
                }
            }
        }
        private void RemoveFromInterconnect(QueuedClient client)
        {
            lock (interconnectLock)
            {
                if (interconnectClients.TryGetValue(client, out var clientEvents))
                {
                    foreach (var item in clientEvents)
                    {
                        interconnectEvents[item].Remove(client);

                        if (interconnectEvents[item].Count == 0)
                        {
                            interconnectEvents.Remove(item);
                            SendEventRemoved(item, client);
                        }
                    }

                    interconnectClients.Remove(client);
                }
            }
        }
        private void HookLocal(QueuedClient client, string eventID)
        {
            lock (localLock)
            {
                if (!localEvents.ContainsKey(eventID))
                {
                    localEvents[eventID] = new HashSet<QueuedClient> { client };
                }
                else
                {
                    localEvents[eventID].Add(client);
                }

                try
                {
                    localClients[client].Add(eventID);
                }
                catch
                {
                    Debug.WriteLine("Local wasn't initialized properly");
                }
            }
        }
        private void UnhookLocal(QueuedClient client, string eventID)
        {
            lock (localLock)
            {
                if (localEvents.ContainsKey(eventID))
                {
                    if (localEvents[eventID].Contains(client))
                    {
                        localEvents[eventID].Remove(client);

                        if (localEvents[eventID].Count == 0)
                        {
                            localEvents.Remove(eventID);
                        }
                    }
                }

                if (localClients[client].Contains(eventID))
                {
                    localClients[client].Remove(eventID);
                }
            }
        }
        private async void SendEventAdded(string eventID, QueuedClient? client)
        {
            var addEventPackage = new Package("EventAdded", PackageType.ServerAdminEvent, (BinaryConvertableString)eventID);
            IEnumerable<QueuedClient> clients;
            lock (interconnectLock)
            {
                clients = GetClients(client, interconnectClients.Keys.ToHashSet());
            }
            foreach (var partner in clients)
            {
                await partner.Send(addEventPackage);
            }
            AddedEvent?.Invoke(eventID, this);
            await SendData(addEventPackage, client);
        }
        private async void SendEventRemoved(string eventID, QueuedClient? client)
        {
            var removeEventPackage = new Package("EventRemoved", PackageType.ServerAdminEvent, (BinaryConvertableString)eventID);
            IEnumerable<QueuedClient> clients;
            lock (interconnectLock)
            {
                clients = GetClients(client, interconnectClients.Keys.ToHashSet());
            }
            foreach (var partner in clients)
            {
                await partner.Send(removeEventPackage);
            }
            RemovedEvent?.Invoke(eventID, this);
        }
        private void HookInterconnect(QueuedClient client, string eventID)
        {
            lock (interconnectLock)
            {
                if (!interconnectEvents.ContainsKey(eventID))
                {
                    interconnectEvents[eventID] = new HashSet<QueuedClient> { client };
                    SendEventAdded(eventID, client);
                }
                else
                {
                    interconnectEvents[eventID].Add(client);
                    SendEventAdded(eventID, client);
                }

                lock (interconnectLock)
                {
                    if (!interconnectClients.ContainsKey(client!))
                    {
                        interconnectClients.Add(client!, new HashSet<string>());
                    }
                }
            }
        }
        private void UnhookInterconnect(QueuedClient client, string eventID)
        {
            lock (interconnectLock)
            {
                if (interconnectEvents.ContainsKey(eventID))
                {
                    if (interconnectEvents[eventID].Contains(client))
                    {
                        interconnectEvents[eventID].Remove(client);

                        if (interconnectEvents[eventID].Count == 0)
                        {
                            interconnectEvents.Remove(eventID);
                            SendEventRemoved(eventID, client);
                        }
                    }
                }

                try
                {
                    if (interconnectClients[client].Contains(eventID))
                    {
                        interconnectClients[client].Remove(eventID);
                    }
                }
                catch
                {
                    Debug.WriteLine("Interconnect not initialized properly");
                }
            }
        }
        public async void CommandServer(Package command)
        {
            switch (command.EventID)
            {
                case "CreateInterconnect":
                    {
                        var interconnect = await protocolHandler.EstablishInterconnect(command);
                        if (interconnect != null)
                        {
                            AcceptClient(interconnect, true);
                            await interconnect.Send(new Package("InterconnectRunning", PackageType.ServerAdminEvent));
                        }
                        break;
                    }
            }
        }
            
    }
}
