using EVent.Comms;
using EVent.Connections.Models;
using EVent.Connections.Models.BaseBinaryConvertables;
using EVent.Connections.UDP;
using EVent.Utils;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace EVent.Connections.TCP
{
    public class TCPServer : IServer
    {
        Action<Package, IServer?, Action<Package>>? DataRecieved;
        Action<Package, IServer?, Action<Package>>? InterconnectDataRecievedEvent;
        Action<string,IServer>? AddedEvent;
        Action<string,IServer>? RemovedEvent;
        TcpListener tcpListener;
        List<ServerTCPBroadcast> discoveryBroadcastChannels;
        Task mainThread;
        bool IsAlive = true;

        IPAddress listeningAdress;
        int port;

        Dictionary<string, HashSet<TcpClient>> events = new Dictionary<string, HashSet<TcpClient>>();
        Dictionary<TcpClient, HashSet<string>> interconnectEvents = new Dictionary<TcpClient, HashSet<string>>();
        object interconnectLock = new object();
        object eventLock = new object();
        public TCPServer(IPAddress listeningAdress, int port)
        {
            this.listeningAdress = listeningAdress;
            this.port = port;
        }
        public TCPServer(IPAddress listeningAdress, int port,params string[] serverBroadcastedIDs) : this(listeningAdress, port)
        {
            IPAddress localIP = IPUtils.GetLocalIPv4();

            discoveryBroadcastChannels = serverBroadcastedIDs.Select(x =>
            {
                var broadcast = new ServerTCPBroadcast(x, localIP.ToString(), port.ToString());
                broadcast.Start();
                return broadcast;
            }).ToList();
        }
        public void Run()
        {
            Debug.WriteLine("Server started");
            
            mainThread = Task.Run(async () =>
            {
                try
                {
                    tcpListener = new TcpListener(listeningAdress, port);
                    tcpListener.Start();
                    while (IsAlive)
                    {
                        var client = tcpListener.AcceptTcpClient();
                        await AcceptClient(client);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Exception on the server{ex.Message}");
                }
            });   
        }
        private async Task AcceptClient(TcpClient client)
        {
            var stream = client.GetStream();

            Package? handshake;
            try
            {
                handshake = await Package.ReadPackage(stream);
            }
            catch(Exception ex)
            {
                Debug.WriteLine("Connection threw");
                return;
            }

            if (handshake is null || handshake.type == PackageType.Invalid) 
            {
                Debug.WriteLine("Package was malformed");
                return;
            }

            switch (handshake.type)
            {
                case PackageType.ConnectEvent:
                    {
                        HookEvents(client, handshake);
                        Task.Run(() => RunClient(client, handshake.EventID));
                        break;
                    }
                case PackageType.ServerAdminEvent:
                    {
                        switch (handshake.EventID)
                        {
                            case "EstablishInterconnect":
                                {
                                    try
                                    {
                                        {
                                            var interconnectStream = client.GetStream();
                                            Package interconnectHandshake = new Package() { Data = new byte[0], EventID = "EstablishInterconnect", type = PackageType.ServerAdminEvent };
                                            stream.Write(interconnectHandshake.ToBytes());

                                            lock (interconnectLock)
                                            {
                                                if (!interconnectEvents.ContainsKey(client))
                                                {
                                                    interconnectEvents[client] = new HashSet<string>();
                                                }
                                            }
                                        }
                                        Task.Run(() => RunInterconnect(client));
                                    }
                                    catch
                                    {
                                        Console.WriteLine("Error establishing interconnect.");
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                default:
                    {
                        Debug.WriteLine($"\tTransmitter recieved");
                        DataRecieved?.Invoke(handshake, this, x => { });
                        Task.Run(() => RunClient(client, null));
                        break;
                    }
            }
        }

        private void HookEvents(TcpClient client, Package handhsake)
        {
            Debug.WriteLine($"\tReciever recieved on event: {handhsake.EventID}");
            lock (eventLock)
            {
                if (!events.ContainsKey(handhsake.EventID))
                {
                    events[handhsake.EventID] = new HashSet<TcpClient>();
                    AddedEvent?.Invoke(handhsake.EventID, null);

                    var InterconenctDropped = new Package() { EventID = "EventAdded", type = PackageType.ServerAdminEvent, Data = ((BinaryConvertableString)handhsake.EventID).ToBytes() };
                    DataRecieved?.Invoke(InterconenctDropped, null, x => { });
                }

                events[handhsake.EventID].Add(client);
            }
        }
        private void UnhookEvents(TcpClient client, IEnumerable<string> eventIDs)
        {
            lock (eventLock)
            {
                foreach (var eventID in eventIDs)
                {
                    if (events.ContainsKey(eventID) && events[eventID].Contains(client))
                    {
                        events[eventID].Remove(client);

                        if (events[eventID].Count == 0)
                        {
                            events.Remove(eventID);
                            RemovedEvent?.Invoke(eventID, this);

                            var InterconenctDropped = new Package() { EventID = "EventRemoved", type = PackageType.ServerAdminEvent, Data = ((BinaryConvertableString)eventID).ToBytes() };
                            DataRecieved?.Invoke(InterconenctDropped, null, x => { });
                        }
                    }
                }
            }
        }

        private async void RunClient(TcpClient client,string eventID)
        {
            HashSet<string> events = new HashSet<string>();
            if (eventID != null)
            {
                events.Add(eventID);
            }
  
            try
            {
                var stream = client.GetStream();

                Package? package = null;
                while ((package = await Package.ReadPackage(stream)) != null)
                {
                    switch (package.type)
                    {
                        case PackageType.ConnectEvent:
                            {   
                                foreach (var newEvent in package.EventID.Split('|').Distinct())
                                {
                                    if (!events.Contains(newEvent))
                                    {
                                        events.Add(newEvent);
                                    }
                                }

                                HookEvents(client, package);
                                break;
                            }   


                        case PackageType.DisconnectEvent:
                            {
                                var eventsToUnhook = new List<string>() { package.EventID };
                                UnhookEvents(client, eventsToUnhook);
                                break;
                            }
                        case PackageType.ServerAdminEvent:
                            {
                                switch (package.EventID)
                                {
                                    case "InitiateInterconnect":
                                        {
                                            var connectionData = new TCPConnectionData();

                                            if (connectionData.FromBytes(package.Data))
                                            {
                                                Task.Run(() => RunInterconnect(connectionData.IP, connectionData.Port));
                                                break;
                                            }
                                            else
                                            {
                                                throw new Exception("Package was malformed");
                                            }
                                        }
                                    case "EventRemoved":
                                        {
                                            var payload = new BinaryConvertableString();
                                            payload.FromBytes(package.Data);

                                            lock (eventLock)
                                            {
                                                if (this.events.ContainsKey(payload))
                                                {
                                                    this.events[payload].Remove(client);
                                                }

                                                if (this.events[payload].Count == 0)
                                                {
                                                    this.events.Remove(payload);
                                                }
                                            }

                                            RemovedEvent?.Invoke(payload, this);
                                            break;
                                        }
                                    case "EventAdded":
                                        {
                                            var payload = new BinaryConvertableString();
                                            payload.FromBytes(package.Data);

                                            lock (eventLock)
                                            {
                                                if (!this.events.ContainsKey(payload))
                                                {
                                                    this.events[payload] = new HashSet<TcpClient>() { client };
                                                }
                                                else if (this.events[payload].Contains(client))
                                                {
                                                    this.events[payload].Remove(client);
                                                }
                                            }
                                            
                                            AddedEvent?.Invoke(payload, this);
                                            break;
                                        }
                                    case "QuerryEvents":
                                        {
                                            DataRecieved?.Invoke(package, this, x => { client.GetStream().Write(x.ToBytes()); });
                                            break;
                                        }
                                    }
                                break;
                            }
                        default:
                            {
                                SendData(package, client);
                                DataRecieved?.Invoke(package,this, x => { });
                                break;
                            }
                    }
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Exception on server: {ex.Message}");
            }
            finally
            {
                if (eventID != null)
                {
                    DropClient(client, events);
                }
            }
            
        }
        private void DropClient(TcpClient client,IEnumerable<string> eventIDs)
        {
            UnhookEvents(client,eventIDs);

            client.Close();
        }

        public async Task SendData(Package package)
        {
            List<TcpClient>? clientSnapshot = null;
            lock (eventLock)
            {
                if (events.TryGetValue(package.EventID, out var clients))
                {
                    clientSnapshot = clients.ToList();
                }
            }
           
            
            if (clientSnapshot == null || clientSnapshot.Count == 0)
            {
                return;
            }
            var clientPackage = new Package() { Data = package.Data, type = package.type, EventID = package.EventID };
            var tasks = clientSnapshot.Select(async listeningClient => {
                try
                {
                    var sendStream = listeningClient.GetStream();
                    var sendData = clientPackage.ToBytes();
                    await sendStream.WriteAsync(sendData);
                }
                catch 
                {
                }
            });
            await Task.WhenAll(tasks);
        }
        private async Task SendData(Package package,TcpClient receivedFrom)
        {
            List<TcpClient>? clientSnapshot = null;
            lock (eventLock)
            {
                if (events.TryGetValue(package.EventID, out var clients))
                {
                    clientSnapshot = clients.Where(x=>x!=receivedFrom).ToList();
                }
            }


            if (clientSnapshot == null || clientSnapshot.Count == 0)
            {
                return;
            }
            var clientPackage = new Package() { Data = package.Data, type = package.type, EventID = package.EventID };
            var tasks = clientSnapshot.Select(async listeningClient => {
                try
                {
                    var sendStream = listeningClient.GetStream();
                    var sendData = clientPackage.ToBytes();
                    await sendStream.WriteAsync(sendData);
                }
                catch
                {
                }
            });
            await Task.WhenAll(tasks);
        }
        public void OnDataRecieved(Action<Package,IServer?, Action<Package>> handler)
        {
            DataRecieved += handler;
        }
        public void OnInterconnectDataRecieved(Action<Package, IServer?, Action<Package>> handler)
        {
            InterconnectDataRecievedEvent += handler;
        }
        public void OnEventAdded(Action<string,IServer> handler)
        {
            AddedEvent += handler;
        }
        public void OnEventRemoved(Action<string, IServer> handler)
        {
            RemovedEvent += handler;
        }
        public void Stop()
        {
            IsAlive = false;
        }
        private async void RunInterconnect(TcpClient tcpClient)
        {
            try
            {
                var stream = tcpClient.GetStream();
                while (tcpClient.Connected)
                {
                    var package = await Package.ReadPackage(stream);
                    if (package == null)
                    {
                        Debug.WriteLine("Interconnect recieved package was invalid");
                        continue;
                    }

                    InterconnectDataRecieved(package, tcpClient);
                }
            }
            catch (IOException ioEx)
            {
                lock (interconnectLock)
                {
                    if (interconnectEvents.ContainsKey(tcpClient))
                    {
                        interconnectEvents.Remove(tcpClient);
                    }
                }

                var InterconnectDropped = new Package() { EventID = "DisconnectInterconnect", type = PackageType.ServerAdminEvent };
                InterconnectDataRecieved(InterconnectDropped, tcpClient);
                DataRecieved?.Invoke(InterconnectDropped, null, x => { });
                Debug.WriteLine("Server connection forcibly closed");
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while running client: {ex}");
                await Task.Delay(1000);
            }
        }
        private async void RunInterconnect(string serverAdress, int serverPort)
        {
            try
            {
                TcpClient tcpClient = new TcpClient();
                
                await tcpClient.ConnectAsync(serverAdress, serverPort);
                var stream = tcpClient.GetStream();
                Package handshake = new Package() { Data = new byte[0], EventID = "EstablishInterconnect", type = PackageType.ServerAdminEvent };
                stream.Write(handshake.ToBytes());
                InterconnectDataRecieved(new Package("QuerryEvents", PackageType.ServerAdminEvent, new BinaryConvertableString()), tcpClient);

                lock (interconnectLock)
                {
                    if (!interconnectEvents.ContainsKey(tcpClient))
                    {
                        interconnectEvents[tcpClient] = new HashSet<string>();
                    }
                }

                RunInterconnect(tcpClient);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error while establishing TCP interconnect: {ex}");
            }
        }
        private async void InterconnectDataRecieved(Package package,TcpClient client)
        {
            switch (package.type)
            {
                case PackageType.ServerAdminEvent:
                    {
                        switch (package.EventID)
                            {
                            case "DisconnectInterconnect":
                                {
                                    interconnectEvents.Remove(client);
                                    client.Close();
                                    break;
                                }
                            case "EstablishInterconnect":
                                {
                                    if (!interconnectEvents.ContainsKey(client))
                                    {
                                        interconnectEvents[client] = new HashSet<string>();
                                    }
                                    InterconnectDataRecieved(new Package("QuerryEvents",PackageType.ServerAdminEvent,new BinaryConvertableString()), client);
                                    break;
                                }
                            case "EventRemoved":
                                {
                                    var payload = new BinaryConvertableString();
                                    payload.FromBytes(package.Data);

                                    lock (interconnectLock)
                                    {
                                        if (interconnectEvents[client].Contains(payload))
                                        {
                                            interconnectEvents[client].Remove(payload);
                                        }
                                    }

                                    RemovedEvent?.Invoke(payload, this);
                                    break;
                                }
                            case "EventAdded":
                                {
                                    var payload = new BinaryConvertableString();
                                    payload.FromBytes(package.Data);

                                    lock (interconnectLock)
                                    {
                                        if (!interconnectEvents.ContainsKey(client))
                                        {
                                            interconnectEvents[client] = new HashSet<string>() { payload };
                                        }
                                        else if (!interconnectEvents[client].Contains(payload))
                                        {
                                            interconnectEvents[client].Add(payload);
                                        }
                                    }
                                    AddedEvent?.Invoke(payload, this);
                                    break;
                                }
                            case "QuerryEvents":
                                {
                                    InterconnectDataRecievedEvent?.Invoke(package, this, x => { client.GetStream().Write(x.ToBytes()); });
                                    break;
                                }
                            case "ListEvents":
                                {
                                    var collection = new BinaryCovnertableCollection<BinaryConvertableString>();
                                    collection.FromBytes(package.Data);
                                    var remoteEvents = collection.binaryConvertables.Where(x => x != null).Select(x=>x!).ToList();

                                    foreach (var item in remoteEvents)
                                    {
                                        if (!interconnectEvents[client].Contains(item))
                                        {
                                            interconnectEvents[client].Add(item);
                                            AddedEvent?.Invoke(item,this);
                                        }
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

                        InterconnectDataRecievedEvent?.Invoke(package, this, x => { });
                        break;
                    }
            }
        }
        private async Task SendDataOnInterconnect(Package package,TcpClient recievedFrom)
        {
            List<TcpClient> sendTo;
            lock (interconnectLock)
            {
                sendTo =  
                interconnectEvents.Where(x => x.Key != recievedFrom 
                                                    && (x.Value.Contains(package.EventID) 
                                                    || package.type != PackageType.Data))
                       .Select(x => x.Key).ToList();
            }

            var data = package.ToBytes();

            foreach (var partner in sendTo)
            {
                var stream = partner.GetStream();
                await stream.WriteAsync(data);
            }
        }
        public async Task SendDataOnInterconnect(Package package)
        {
            List<TcpClient> sendTo;
            lock (interconnectLock)
            {
                sendTo =
                interconnectEvents  .Where(x => x.Value.Contains(package.EventID)|| package.type != PackageType.Data)
                                    .Select(x => x.Key).ToList();
            }

            var data = package.ToBytes();

            foreach(var partner in sendTo)
            {
                var stream = partner.GetStream();
                await stream.WriteAsync(data);
            }
        }

        public IEnumerable<string> GetEvents()
        {
            return interconnectEvents.SelectMany(x => x.Value).Union(events.Keys).ToList();
        }
    }
}
