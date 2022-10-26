using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatServer
{
    public class ChatServerClient
    {
        private ChatServer m_Server;
        private TcpClient m_Tcp;
        private SslStream m_Stream;

        private BinaryWriter m_Writer;
        private BinaryReader m_Reader;
        public BinaryWriter Writer { get => m_Writer; }
        public BinaryReader Reader { get => m_Reader; }

        private List<ChatRoom> m_Rooms = new List<ChatRoom>();
        public List<ChatRoom> Rooms { get => m_Rooms; }

        private Thread m_Thread;
        private bool m_StopThread = false;

        private bool m_IsInServer = false;
        public bool IsInServer { get => m_IsInServer;  }

        private bool m_Disconnected = true;

        private string m_Nickname = "";
        public string Nickname { get => m_Nickname; set => m_Nickname = value; }

        // Sync objects
        public readonly object receiveSync = new object();
        public readonly object sendSync = new object();
        private readonly object m_ThreadStopSync = new object();
        private readonly object m_DisconnectSync = new object();

        public ChatServerClient(TcpClient tcpClient, SslStream stream, ChatServer server)
        {
            m_Tcp = tcpClient;
            m_Stream = stream;
            m_Server = server;

            m_Writer = new BinaryWriter(m_Stream, Encoding.UTF8);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);

            m_Rooms = new List<ChatRoom>();

            lock(m_DisconnectSync)
                m_Disconnected = false;

            lock(m_ThreadStopSync)
                m_StopThread = false;

            // Start the worker thread.
            m_Thread = new Thread(new ThreadStart(Run));
            m_Thread.Start();
        }

        private void HandlePacket(Packet packet)
        {
            switch (packet.PacketType)
            {
                // Client is disconnecting
                case PacketType.ClientDisconnect:
                    // Indicate that worker thread should stop.
                    lock (m_DisconnectSync)
                        m_Disconnected = true;

                    break;

                // Client is sending a direct message to a user.
                case PacketType.ClientDirectMessage:
                {
                    string recipientName = packet.ReadString();
                    string msg = packet.ReadString();

                    Console.WriteLine($"{Nickname} --> {recipientName}: {msg}");

                    // Send message to both the clients.
                    using (Packet packet2 = new Packet(PacketType.ServerDirectMessageReceived))
                    {
                        packet2.Write(Nickname);
                        packet2.Write(recipientName);
                        packet2.Write(msg);

                        // Send to recipient
                        ChatServerClient? recipient = m_Server.GetClient(recipientName);
                        if (recipient is not null)
                        {
                            lock (recipient.sendSync)
                            {
                                packet2.WriteToStream(recipient.Writer);
                                recipient.Writer.Flush();
                            }
                        }

                        // Send to the sender to indicate that their message was sent.
                        lock (sendSync)
                        {
                            packet2.WriteToStream(Writer);
                            Writer.Flush();
                        }
                    }
                    break;
                }

                // Client is sending a message to a room.
                case PacketType.ClientRoomMessage:
                {
                    string roomName = packet.ReadString();
                    string msg = packet.ReadString();

                    Console.WriteLine($"{Nickname} --> {roomName} (room): {msg}");

                    ChatRoom? room = m_Server.GetRoom(roomName);
                    if (room is null)
                        break;

                    // Ensure that the client is actually a member of the room
                    if (!m_Rooms.Contains(room))
                    {
                        Console.WriteLine($"Error: User '{Nickname}' attempt to " +
                            $"write to room {roomName} which they are not a member of.");
                        break;
                    }

                    // If the room is encrypted; we include the IV.
                    string? iv = null;
                    if (room.isEncrypted)
                        iv = packet.ReadString();

                    // Send the message back to everyone in the room
                    using (Packet packet2 = new Packet(PacketType.ServerRoomMessageReceived))
                    {
                        packet2.Write(Nickname);
                        packet2.Write(roomName);
                        packet2.Write(msg);

                        if (iv is not null)
                            packet2.Write(iv);

                        // Send the message to all clients who are in the room.
                        foreach (ChatServerClient client in room.clients)
                        {
                            lock (client.sendSync)
                            {
                                packet2.WriteToStream(client.Writer);
                                client.Writer.Flush();
                            }
                        }
                    }

                    break;
                }

                // Client is creating a room
                case PacketType.ClientRoomCreate:
                { 
                    string roomName = packet.ReadString();
                    string roomTopic = packet.ReadString();
                    bool roomEncrypted = packet.ReadBool();

                    // Skip if room name is malformed
                    if (string.IsNullOrEmpty(roomName))
                        break;

                    // Add the room
                    ChatRoom? room = m_Server.CreateRoom(this, roomName, roomTopic, roomEncrypted);

                    if (room is null)
                    {
                        // Inform client that we failed to create the room.
                        using (Packet error = new Packet(PacketType.ServerRoomCreateError))
                        {
                            error.Write((uint)PacketErrorCode.RoomNameTaken);
                            error.Write("Room name is already taken.  Please enter a different name.");

                            lock (sendSync)
                            {
                                error.WriteToStream(Writer);
                                Writer.Flush();
                            }
                        }

                        break;
                    }

                    if (roomEncrypted)
                        Console.WriteLine($"{Nickname} created encrypted room '{roomName}' with topic '{roomTopic}'");
                    else
                        Console.WriteLine($"{Nickname} created room '{roomName}' with topic '{roomTopic}'");

                    break;
                }

                // Client is deleting a room
                case PacketType.ClientRoomDelete:
                {
                    string roomName = packet.ReadString();

                    // Skip if room name is malformed
                    if (string.IsNullOrEmpty(roomName))
                        break;

                    // Get room
                    ChatRoom? room = null;
                    m_Server.EnumerateRoomsUntil((ChatRoom room2) =>
                    {
                        if (room2.name == roomName)
                        {
                            room = room2;
                            return true;
                        }
                        return false;
                    });

                    // No such room to delete
                    if (room is null)
                    {
                        Console.WriteLine($"{Nickname} attempted to delete unknown room {roomName}");
                        break;
                    }

                    // Deleter must be owner.
                    if (room.owner != this)
                    {
                        Console.WriteLine($"{Nickname} attempted to delete room " +
                                           "{roomName} that they do not own.");
                        break;
                    }

                    // Remove the room.
                    m_Server.DeleteRoom(room);

                    break;
                }

                // Client wants to join room.
                case PacketType.ClientRoomJoin:
                {
                    string roomName = packet.ReadString();

                    Console.WriteLine($"'{Nickname}' joining room {roomName} ...");

                    // Get room
                    ChatRoom? room = null;
                    m_Server.EnumerateRoomsUntil((ChatRoom room2) =>
                    {
                        if (room2.name == roomName)
                        {
                            room = room2;
                            return true;
                        }
                        return false;
                    });

                    // No such room to join
                    if (room is null)
                    {
                        Console.WriteLine($"{Nickname} attempted to join unknown room {roomName}");
                        break;
                    }

                    if (!room.isEncrypted)
                    {
                        // Simply add them to the room's participants list.
                        m_Server.AddClientToRoom(this, room);
                    }
                    else
                    {
                        // We need a room owner
                        if (room.owner is null)
                        {
                            Console.WriteLine($"{Nickname} attempted to join encrypted room with no owner.");
                            break;
                        }

                        // Forward the attached encrypted message to the owner
                        // of the room; who will respond to us if the request
                        // message was decrypted correctly to the expected string.
                        string saltString = packet.ReadString();
                        string ivString = packet.ReadString();
                        string messageString = packet.ReadString();

                        Console.WriteLine($"'{Nickname}' attempting to join encrypted room '{roomName}'");
                        Console.WriteLine($"'{Nickname}' salt:{saltString} iv:{ivString} message:{messageString}");

                        // Send authorisation request to the room's owner.
                        using (Packet packet2 = new Packet(PacketType.ServerClientJoinEncryptedRoomRequest))
                        {
                            packet2.Write(roomName);
                            packet2.Write(Nickname);
                            packet2.Write(saltString);
                            packet2.Write(ivString);
                            packet2.Write(messageString);

                            lock (room.owner.sendSync)
                            {
                                packet2.WriteToStream(room.owner.Writer);
                                room.owner.Writer.Flush();
                            }
                        }
                    }

                    break;
                }

                // Encrypted room owner does not authorise the client to join.
                case PacketType.ClientEncryptedRoomAuthoriseFail:
                {
                    string roomName = packet.ReadString();
                    string nickname = packet.ReadString();

                    Console.WriteLine($"'{Nickname}' attempting to NOT authorise '{nickname}' to '{roomName}' ...");

                    // Client must exist.
                    ChatServerClient? client = m_Server.GetClient(nickname);
                    if (client is null)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to NOT authorise " + 
                                          $"unknown user '{nickname}' to room '{roomName}'");
                        break;
                    }

                    // Get room
                    ChatRoom? room = null;
                    m_Server.EnumerateRoomsUntil((ChatRoom room2) =>
                    {
                        if (room2.name == roomName)
                        {
                            room = room2;
                            return true;
                        }
                        return false;
                    });

                    // No such room to join
                    if (room is null)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to NOT authorise " +
                                          $"'{nickname}' to unknown room '{roomName}'");
                        break;
                    }

                    // User must own the room to authorise
                    if (room.owner != this)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to NOT authorise "+
                                          $"'{nickname}' to room '{roomName}' which they do not own.");
                        break;
                    }

                    // Room must be encrypted
                    if (!room.isEncrypted)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to NOT authorise " +
                                          $"'{nickname}' to non-encrypted room '{roomName}'");
                        break;
                    }

                    Console.WriteLine($"'{Nickname}' NOT authorising '{nickname}' to '{roomName}' ...");

                    // Inform the client that they are not authorised.
                    using (Packet packet2 = new Packet(PacketType.ServerClientEncryptedRoomAuthoriseFail))
                    {
                        packet2.Write(room.name);

                        lock (client.sendSync)
                        {
                            packet2.WriteToStream(client.Writer);
                            client.Writer.Flush();
                        }
                    }

                    break;
                }

                // Encrypted room owner authorises the client to join.
                case PacketType.ClientEncryptedRoomAuthorise:
                {
                    string roomName = packet.ReadString();
                    string nickname = packet.ReadString();

                    Console.WriteLine($"'{Nickname}' attempting to authorise '{nickname}' to '{roomName}' ...");

                    // Client must exist.
                    ChatServerClient? client = m_Server.GetClient(nickname);
                    if (client is null)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to authorise " + 
                                          $"unknown user '{nickname}' to room '{roomName}'");
                        break;
                    }

                    // Get room
                    ChatRoom? room = null;
                    m_Server.EnumerateRoomsUntil((ChatRoom room2) =>
                    {
                        if (room2.name == roomName)
                        {
                            room = room2;
                            return true;
                        }
                        return false;
                    });

                    // No such room to join
                    if (room is null)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to authorise " +
                                          $"'{nickname}' to unknown room '{roomName}'");
                        break;
                    }

                    // User must own the room to authorise
                    if (room.owner != this)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to authorise "+
                                          $"'{nickname}' to room '{roomName}' which they do not own.");
                        goto fail;
                    }

                    // Room must be encrypted
                    if (!room.isEncrypted)
                    {
                        Console.WriteLine($"'{Nickname}' attempted to authorise " +
                                          $"'{nickname}' to non-encrypted room '{roomName}'");
                        goto fail;
                    }

                    Console.WriteLine($"'{Nickname}' authorising '{nickname}' to '{roomName}' ...");

                    // Inform the client that they are authorised.
                    using (Packet packet2 = new Packet(PacketType.ServerClientEncryptedRoomAuthorise))
                    {
                        packet2.Write(room.name);

                        lock (client.sendSync)
                        {
                            packet2.WriteToStream(client.Writer);
                            client.Writer.Flush();
                        }
                    }

                    // Add the client to the encrypted room.
                    m_Server.AddClientToRoom(client, room);

                    break;

                fail:
                    // Inform the client that they are not authorised.
                    using (Packet packet2 = new Packet(PacketType.ServerClientEncryptedRoomAuthoriseFail))
                    {
                        packet2.Write(room.name);

                        lock (client.sendSync)
                        {
                            packet2.WriteToStream(client.Writer);
                            client.Writer.Flush();
                        }
                    }
                    break;
                }
            }
        }

        public void Run()
        {
            Packet? packet;

            try
            {
                using (packet = new Packet())
                {
                    // Read the packet
                    lock (receiveSync)
                        packet.ReadFromStream(Reader);

                    if (packet.PacketType != PacketType.ClientHello)
                    {
                        // Client sent us an invalid packet, as they are not joined
                        // yet we only expect a ClientHello.
                        Console.WriteLine("Client did not send ClientHello packet");
                        Cleanup();
                        return;
                    }

                    // Read nickname that client sends us when they join.
                    Nickname = packet.ReadString();
                }

                // Check if nickname is valid
                if (!m_Server.NicknameIsValid(Nickname))
                {
                    using (packet = new Packet(PacketType.ServerError))
                    {
                        packet.Write((uint)PacketErrorCode.InvalidNickname);
                        packet.Write("Please enter a different nickname.");

                        lock (sendSync)
                        {
                            packet.WriteToStream(Writer);
                            Writer.Flush();
                        }
                    }
                    Cleanup();
                    return;
                }

                // Client is welcome.
                using (packet = new Packet(PacketType.ServerWelcome))
                {
                    lock (sendSync)
                    {
                        packet.WriteToStream(Writer);
                        Writer.Flush();
                    }
                }

                // Send the client list to the client.
                using (packet = new Packet(PacketType.ServerClientList))
                {
                    packet.Write(m_Server.ClientCount);

                    // Write client nick names
                    m_Server.EnumerateClients((ChatServerClient client) =>
                    {
                        packet.Write(client.Nickname);
                    });

                    lock (sendSync)
                    {
                        packet.WriteToStream(Writer);
                        Writer.Flush();
                    }
                }

                // Send the room list to the client.
                using (packet = new Packet(PacketType.ServerRoomList))
                {
                    // Write room count.
                    packet.Write(m_Server.RoomCount);

                    // Write rooms
                    m_Server.EnumerateRooms((ChatRoom room) =>
                    {
                        // Write room name
                        packet.Write(room.name);

                        // Write room topic
                        packet.Write(room.topic);

                        // Write whether room is encrypted
                        packet.Write(room.isEncrypted);
                    });

                    lock (sendSync)
                    {
                        packet.WriteToStream(Writer);
                        Writer.Flush();
                    }
                }

                // Add the client to the server client list.
                m_Server.AddClient(this);

                m_IsInServer = true;

                // Initialise the packet which we use for reading.
                packet = new Packet();

                // Enter main message loop.
                while (true)
                {
                    lock (m_ThreadStopSync)
                    {
                        if (m_StopThread)
                            break;
                    }

                    lock (m_DisconnectSync)
                    {
                        if (m_Disconnected)
                            break;
                    }

                    lock (receiveSync)
                    {
                        if (!m_Tcp.Connected)
                            break;

                        //if (!m_Tcp.GetStream().DataAvailable)
                            //continue;

                        packet.ReadFromStream(Reader);
                    }

                    packet.Lock();
                    HandlePacket(packet);
                    packet.Unlock();
                }

                packet.Dispose();
            }
            catch (IOException e)
            {
                Console.WriteLine($"IOException: {e.Message}");
                if (e.InnerException is not null)
                    Console.WriteLine($"InnerException: {e.InnerException.Message}");

                // Disconnect the client.
                lock (m_DisconnectSync)
                    m_Disconnected = true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                if (e.InnerException is not null)
                    Console.WriteLine($"InnerException: {e.InnerException.Message}");

                // Disconnect the client.
                lock (m_DisconnectSync)
                    m_Disconnected = true;
            }

            lock (m_DisconnectSync)
            {
                if (m_Disconnected)
                {
                    Disconnect();
                }
            }
        }

        public void Cleanup()
        {
            lock (m_ThreadStopSync)
                m_StopThread = false;

            lock (m_DisconnectSync)
                m_Disconnected = true;

            m_IsInServer = false;

            lock (receiveSync)
                m_Reader.Close();
            lock (sendSync)
                m_Writer.Close();

            m_Stream.Close();
            m_Tcp.Close();
            m_Thread.Join();
        }

        public void Disconnect()
        {
            if (!string.IsNullOrEmpty(Nickname))
            { 
                Console.WriteLine(Nickname + " disconnected.");
                m_Server.RemoveClient(this);
            }

            Cleanup();
        }
    }
}
