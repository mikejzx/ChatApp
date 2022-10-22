using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatServer
{
    internal class ChatServerClient
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

        public void Run()
        {
            // Read packet that client sent us.
            try
            {
                ChatPacketType packetId = (ChatPacketType)Reader.ReadUInt32();
                if (packetId != ChatPacketType.ClientHello)
                {
                    // Client sent wrong packet.
                    return;
                }

                // Read nickname that client sends us when they join.
                string nickname = Reader.ReadString();

                // Check if nickname is valid
                if (!m_Server.NicknameIsValid(nickname))
                {
                    Writer.Write((UInt32)ChatPacketType.ServerError);
                    Writer.Write((UInt32)ChatPacketErrorCode.InvalidNickname);
                    Writer.Write("Please enter a different nickname.");
                    Cleanup();
                    return;
                }

                Nickname = nickname;

                // Client is welcome.
                Writer.Write((UInt32)ChatPacketType.ServerWelcome);

                m_IsInServer = true;

                // Add the client to the server client list.
                m_Server.AddClient(this);

                // Enter main message loop.
                while (!m_StopThread)
                {
                    if (!m_Tcp.Connected)
                        break;

                    if (m_Tcp.Available <= 0)
                        continue;

                    ChatPacketType packet = (ChatPacketType)Reader.ReadUInt32();

                    switch(packet)
                    {
                        // Client is disconnecting
                        case (ChatPacketType.ClientDisconnect):
                            m_StopThread = true;
                            break;

                        // Client is sending a public message.
                        case (ChatPacketType.ClientDirectMessage):
                            string recipient = Reader.ReadString();
                            string msg = Reader.ReadString();

                            Console.WriteLine($"{Nickname} sends '{msg}' to '{recipient}'");
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
            }

            Console.WriteLine("Disconnecting " + Nickname);
            Disconnect();
        }

        private void Cleanup()
        {
            m_StopThread = true;
            m_IsInServer = false;
            m_Writer.Close();
            m_Reader.Close();
            m_Stream.Close();
            m_Tcp.Close();
            m_Thread.Join();
        }

        public void Disconnect()
        {
            if (m_Server.RemoveClient(this))
            {
                Console.WriteLine($"{Nickname} left the server.");
            }

            Cleanup();
        }
    }
}
