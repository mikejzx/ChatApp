using System.Collections;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatServer
{
    public class ChatServer
    {
        // Server SSL certificate.
        private X509Certificate2? m_Certificate;

        // List of clients that are connected to the server.
        private Dictionary<string, ChatServerClient> m_Clients = new Dictionary<string, ChatServerClient>();

        // List of rooms in the server.
        private Dictionary<string, ChatRoom> m_Rooms = new Dictionary<string, ChatRoom>();

        // Sync objects
        private readonly object clientSync = new object();
        private readonly object roomSync = new object();

        public static readonly string MainRoomName = "Main Room";
        public static readonly string MainRoomTopic = "The main room.";

        // Get number of clients that are in the server.
        public int ClientCount
        {
            get
            {
                lock (clientSync)
                    return m_Clients.Count;
            }
        }

        // Enumerate client list
        public void EnumerateClients(Action<ChatServerClient> action)
        {
            lock(clientSync)
            {
                foreach (ChatServerClient client in m_Clients.Values)
                {
                    action.Invoke(client);
                }
            }
        }

        // Get client by name.
        public ChatServerClient? GetClient(string nickname)
        {
            lock (clientSync)
            {
                if (!m_Clients.ContainsKey(nickname))
                    return null;

                return m_Clients[nickname];
            }
        }

        // Get number of rooms that are in the server.
        public int RoomCount
        {
            get
            {
                lock (roomSync)
                    return m_Rooms.Count;
            }
        }

        // Enumerate room list
        public void EnumerateRooms(Action<ChatRoom> action)
        {
            lock(roomSync)
            {
                foreach (ChatRoom room in m_Rooms.Values)
                {
                    action.Invoke(room);
                }
            }
        }

        // Enumerate room list until given function returns true.
        public void EnumerateRoomsUntil(Func<ChatRoom, bool> func)
        {
            lock(roomSync)
            {
                foreach (ChatRoom room in m_Rooms.Values)
                {
                    if (func.Invoke(room))
                        return;
                }
            }
        }

        // Get room by name.
        public ChatRoom? GetRoom(string roomName)
        {
            lock (roomSync)
            {
                if (!m_Rooms.ContainsKey(roomName))
                    return null;

                return m_Rooms[roomName];
            }
        }

        public void Cleanup()
        {
            Console.WriteLine("Shutting down the server ...");

            // Cleanup client connections
            lock (clientSync)
            {
                foreach (ChatServerClient client in m_Clients.Values)
                {
                    client.Cleanup();
                }
            }
        }

        public void Run(string certificatePath)
        {
            Console.WriteLine("Starting server ...");

            Console.WriteLine($"Using certificate '{certificatePath}'");

            // Read certificate file.
            try
            {
                m_Certificate = new X509Certificate2(certificatePath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                Console.WriteLine($"Failed to read server certificate file {certificatePath}");
                Console.ReadKey();
                return;
            }

            // Create the main room that all users are in by default.
            m_Rooms.Clear();
            m_Rooms.Add(MainRoomName, new ChatRoom(null, MainRoomName, MainRoomTopic, false));

            // Create TCP listener socket.
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Any, ChatConstants.ServerPort);
                listener.Start();
            }
            catch (SocketException e)
            {
                Console.WriteLine($"Failed to start server: {e.Message}");
                return;
            }

            Console.WriteLine($"Server is listening on 127.0.0.1:{ChatConstants.ServerPort}");

            // Listen for incoming connections.
            while(true)
            {
                TcpClient client = listener.AcceptTcpClient();
                ProcessClient(client);
            }
        }

        private void ProcessClient(TcpClient tcpClient)
        {
            Console.WriteLine("Accepted incoming connection.");
            if (m_Certificate == null)
                return;

            // Client connected--create the SslStream.
            SslStream sslStream = new SslStream(tcpClient.GetStream(),
                                                leaveInnerStreamOpen: false);

            // Authenticate server (but don't require client to authenticate).
            try
            {
                sslStream.AuthenticateAsServer(m_Certificate,
                                               clientCertificateRequired: false,
                                               enabledSslProtocols: SslProtocols.Tls,
                                               checkCertificateRevocation: true);

                // Set timeouts
                sslStream.ReadTimeout = System.Threading.Timeout.Infinite;
                sslStream.WriteTimeout = System.Threading.Timeout.Infinite;

                // Start client thread.
                ChatServerClient client = new ChatServerClient(tcpClient, sslStream, this);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine($"Exception: {e.Message}");

                if (e.InnerException != null)
                    Console.WriteLine($"Inner Exception: {e.InnerException.Message}");

                Console.WriteLine("Authentication failed.  Closing the connection ...");

                sslStream.Close();
                tcpClient.Close();
            }
        }

        internal ChatRoom? CreateRoom(ChatServerClient owner, string name, string topic, bool encrypted)
        {
            lock(roomSync)
            {
                // Room name already taken.
                if (m_Rooms.ContainsKey(name))
                    return null;

                // Create and add the room
                ChatRoom room = new ChatRoom(owner, name, topic, encrypted);
                m_Rooms.Add(name, room);

                // Inform everyone in server that the room was created.
                using (Packet packet = new Packet(PacketType.ServerRoomCreated))
                {
                    packet.Write(room.name);
                    packet.Write(room.topic);
                    packet.Write(room.isEncrypted);

                    EnumerateClients((ChatServerClient client) =>
                    {
                        lock (client.sendSync)
                        {
                            packet.WriteToStream(client.Writer);
                            client.Writer.Flush();
                        }
                    });
                }

                // Add the owner to the room automatically.
                owner.Rooms.Add(room);

                // Inform owner that they joined the room (automatically).
                using (Packet packet = new Packet(PacketType.ServerClientRoomJoin))
                {
                    packet.Write(room.name);
                    packet.Write(owner.Nickname);

                    lock (owner.sendSync)
                    {
                        packet.WriteToStream(owner.Writer);
                        owner.Writer.Flush();
                    }
                }

                return room;
            }
        }

        internal bool DeleteRoom(ChatRoom room)
        {
            lock(roomSync)
            {
                // Remove the room from all clients.
                EnumerateClients((ChatServerClient client) =>
                {
                    client.Rooms.Remove(room);
                });

                // Remove the room
                if (!m_Rooms.Remove(room.name))
                    return false;

                // Inform everyone in server that the room was deleted.
                using (Packet packet = new Packet(PacketType.ServerRoomDeleted))
                {
                    packet.Write(room.name);

                    EnumerateClients((ChatServerClient client) =>
                    {
                        lock (client.sendSync)
                        {
                            packet.WriteToStream(client.Writer);
                            client.Writer.Flush();
                        }
                    });
                }

                return true;
            }
        }

        internal void AddClientToRoom(ChatServerClient client, ChatRoom room)
        {
            lock(roomSync)
            {
                // Send join message to all other clients in the room.
                using (Packet packet = new Packet(PacketType.ServerClientRoomJoin))
                {
                    packet.Write(room.name);
                    packet.Write(client.Nickname);

                    foreach (ChatServerClient client2 in room.clients)
                    {
                        lock(client2.sendSync)
                        {
                            packet.WriteToStream(client2.Writer);
                            client2.Writer.Flush();
                        }
                    }
                }

                // Add the client to the room list.
                room.clients.Add(client);

                Console.WriteLine($"'{client.Nickname}' joined room '{room.name}'");

                // And add room to client's internal list.
                lock(clientSync)
                    client.Rooms.Add(room);

                // Send the current list of clients to the newly-joining
                // client.  We include the newly joining client to indicate
                // that they are a part of the room too.
                using (Packet packet = new Packet(PacketType.ServerClientRoomMembers))
                {
                    packet.Write(room.name);
                    packet.Write(room.clients.Count);

                    foreach (ChatServerClient client2 in room.clients)
                    {
                        packet.Write(client2.Nickname);
                    }

                    lock (client.sendSync)
                    {
                        packet.WriteToStream(client.Writer);
                        client.Writer.Flush();
                    }
                }
            }
        }

        internal void AddClient(ChatServerClient client)
        {
            lock(clientSync)
            {
                // Send join message to all other clients.
                using (Packet packet = new Packet(PacketType.ServerClientJoin))
                {
                    packet.Write(client.Nickname);

                    foreach (ChatServerClient client2 in m_Clients.Values)
                    {
                        lock(client2.sendSync)
                        {
                            packet.WriteToStream(client2.Writer);
                            client2.Writer.Flush();
                        }
                    }
                }

                // Automatically add client to the main room
                AddClientToRoom(client, m_Rooms[MainRoomName]);
                
                m_Clients[client.Nickname] = client;
            }

            Console.WriteLine($"{client.Nickname} joined the server.");
        }

        internal void RemoveClientFromRoom(ChatServerClient client, ChatRoom room)
        {
            lock(roomSync)
            {
                // Remove the client from the room list.
                room.clients.Remove(client);

                // Remove room from client's list.
                lock(clientSync)
                    client.Rooms.Remove(room);

                // Send leave message to the other clients in the room.
                using (Packet packet = new Packet(PacketType.ServerClientRoomLeave))
                {
                    packet.Write(room.name);
                    packet.Write(client.Nickname);

                    foreach (ChatServerClient client2 in room.clients)
                    {
                        lock(client2.sendSync)
                        {
                            packet.WriteToStream(client2.Writer);
                            client2.Writer.Flush();
                        }
                    }
                }
            }
        }

        internal bool RemoveClient(ChatServerClient client)
        {
            lock(clientSync)
            {
                // Remove client from it's rooms
                List<ChatRoom> roomsTmp = new List<ChatRoom>(client.Rooms);
                foreach (ChatRoom room in roomsTmp)
                {
                    RemoveClientFromRoom(client, room);
                }

                // Remove the client from the client list.
                bool rc = m_Clients.Remove(client.Nickname);

                if (rc)
                    Console.WriteLine($"{client.Nickname} left the server.");

                // Send leave message to all other clients.
                using (Packet packet = new Packet(PacketType.ServerClientLeave))
                {
                    packet.Write(client.Nickname);

                    foreach (ChatServerClient client2 in m_Clients.Values)
                    {
                        lock(client2.sendSync)
                        {
                            packet.WriteToStream(client2.Writer);
                            client2.Writer.Flush();
                        }
                    }
                }

                return rc;
            }
        }

        internal bool NicknameIsValid(string nickname)
        {
            if (nickname.Length <= 0 || nickname.Length > 32)
                return false;

            // Ensure the nickname is not already taken.
            lock(clientSync)
            {
                foreach (ChatServerClient client in m_Clients.Values)
                {
                    if (client.Nickname == nickname)
                        return false;
                }
            }

            return true;
        }
    }
}
