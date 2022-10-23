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

        private Thread m_Thread;
        private bool m_StopThread = false;

        private bool m_IsInServer = false;
        public bool IsInServer { get => m_IsInServer;  }

        private string m_Nickname = "";
        public string Nickname { get => m_Nickname; set => m_Nickname = value; }

        // Sync objects
        public readonly object receiveSync = new object();
        public readonly object sendSync = new object();
        private readonly object m_ThreadStopSync = new object();

        public ChatServerClient(TcpClient tcpClient, SslStream stream, ChatServer server)
        {
            m_Tcp = tcpClient;
            m_Stream = stream;
            m_Server = server;

            m_Writer = new BinaryWriter(m_Stream, Encoding.UTF8);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);

            // Start worker thread.
            m_StopThread = false;
            m_Thread = new Thread(new ThreadStart(Run));
            m_Thread.Start();
        }

        private void HandlePacket(Packet packet)
        {
            switch(packet.PacketType)
            {
                // Client is disconnecting
                case PacketType.ClientDisconnect:
                    // Indicate that worker thread should stop.
                    lock (m_ThreadStopSync)
                        m_StopThread = true;

                    break;

                // Client is sending a direct message to a user.
                case PacketType.ClientDirectMessage:
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
                        lock(sendSync)
                        {
                            packet2.WriteToStream(Writer);
                            Writer.Flush();
                        }
                    }

                    break;
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
                        packet.Dispose();
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

                m_IsInServer = true;

                // Add the client to the server client list.
                m_Server.AddClient(this);

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

                    lock (receiveSync)
                    {
                        if (!m_Tcp.Connected)
                            break;

                        //if (!m_Tcp.GetStream().DataAvailable)
                            //continue;

                        packet.ReadFromStream(Reader);
                    }

                    HandlePacket(packet);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                if (e.InnerException is not null)
                    Console.WriteLine($"InnerException: {e.InnerException.Message}");
            }

            Console.WriteLine("Disconnecting " + Nickname);
            Disconnect();
        }

        private void Cleanup()
        {
            lock (m_ThreadStopSync)
                m_StopThread = false;

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
            m_Server.RemoveClient(this);
            Cleanup();
        }
    }
}
