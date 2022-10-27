using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatServer
{
    public class ChatServerClient
    {
        private ChatServer m_Server;
        private TcpClient m_Tcp;
        private SslStream m_Stream;

        // Streams
        private BinaryWriter m_Writer;
        private BinaryReader m_Reader;
        public BinaryWriter Writer { get => m_Writer; }
        public BinaryReader Reader { get => m_Reader; }

        // Rooms the client is in.
        private List<ChatRoom> m_Rooms = new List<ChatRoom>();
        public List<ChatRoom> Rooms { get => m_Rooms; }

        // Client worker thread and stop flag.
        private Thread m_Thread;
        public bool stopThread = false;

        // Whether the client has actually joined the server yet
        private bool m_IsInServer = false;
        public bool IsInServer { get => m_IsInServer; set => m_IsInServer = value; }

        // True if the client is disconnected.
        public bool disconnected = true;

        // Client nickname
        private string m_Nickname = "";
        public string Nickname { get => m_Nickname; set => m_Nickname = value; }

        // Sync objects
        public readonly object receiveSync = new object();
        public readonly object sendSync = new object();
        public readonly object threadStopSync = new object();
        public readonly object disconnectSync = new object();

        private readonly ChatServerPacketHandler m_PacketHandler;

        public ChatServerClient(TcpClient tcpClient, SslStream stream, ChatServer server)
        {
            m_Tcp = tcpClient;
            m_Stream = stream;
            m_Server = server;
            m_PacketHandler = new ChatServerPacketHandler(this, m_Server);

            m_Writer = new BinaryWriter(m_Stream, Encoding.UTF8);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);

            m_Rooms = new List<ChatRoom>();

            IsInServer = false;

            lock(disconnectSync)
                disconnected = false;

            lock(threadStopSync)
                stopThread = false;

            // Start the worker thread.
            m_Thread = new Thread(new ThreadStart(Run));
            m_Thread.Start();
        }

        // Convert client to a string.
        public override string ToString() => Nickname;

        // Send the welcome message to the client
        private void SendWelcome()
        {
            using (Packet packet = new Packet(PacketType.ServerWelcome))
            {
                lock (sendSync)
                {
                    packet.WriteToStream(Writer);
                    Writer.Flush();
                }
            }
        }

        // Send the current server client list to the client
        private void SendServerClientList()
        {
            using (Packet packet = new Packet(PacketType.ServerClientList))
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
        }

        // Send the current server room list to the client
        private void SendServerRoomList()
        {
            using (Packet packet = new Packet(PacketType.ServerRoomList))
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
        }

        private bool PerformChecks()
        {
            Packet packet;

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
                    return false;
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
                return false;
            }

            return true;
        }

        private void HandleException(Exception e, string exceptionName)
        {
            Console.WriteLine($"{exceptionName}: {e.Message}");

            // Print the inner exception
            if (e.InnerException is not null)
                Console.WriteLine($"  InnerException: {e.InnerException.Message}");

            // Print stacktrace
            Console.WriteLine("StackTrace: " + e.StackTrace);

            // Disconnect the client.
            lock (disconnectSync)
                disconnected = true;
        }

        public void Run()
        {
            try
            {
                // Ensure that client sent necessary things, has a valid nickname, etc.
                if (!PerformChecks())
                    return;

                // Welcome the client to the server..
                SendWelcome();

                // Send the client list to the client.
                SendServerClientList();

                // Send the room list to the client.
                SendServerRoomList();

                // Add the client to the server client list.
                m_Server.AddClient(this);

                IsInServer = true;

                // Initialise the packet which we use for reading.
                Packet packet = new Packet();

                // Main client message loop.
                while (true)
                {
                    // Stop if the thread is flagged to stop.
                    lock (threadStopSync)
                    {
                        if (stopThread)
                            break;
                    }

                    // Stop if we are marked as disconnected.
                    lock (disconnectSync)
                    {
                        if (disconnected)
                            break;
                    }

                    // Receive data into packet structure.
                    lock (receiveSync)
                    {
                        if (!m_Tcp.Connected)
                            break;

                        packet.ReadFromStream(Reader);
                    }

                    // Handle the packet.
                    m_PacketHandler.Handle(packet);
                }

                packet.Dispose();
            }
            catch (IOException e)
            {
                HandleException(e, "IOException");
            }
            catch (Exception e)
            {
                HandleException(e, "Exception");
            }

            lock (disconnectSync)
            {
                if (disconnected)
                {
                    Disconnect();
                }
            }
        }

        public void Cleanup()
        {
            lock (threadStopSync)
                stopThread = false;

            lock (disconnectSync)
                disconnected = true;

            IsInServer = false;

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
