using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatServer
{
    internal class ChatServerMulticaster
    {
        private Thread m_Thread;

        private Socket m_Socket;
        private IPEndPoint m_Endpoint;

        // Sync object
        private readonly object m_Sync = new object();

        // True if the multicaster is running.
        private bool m_Running = false;

        private bool Running
        {
            get
            {
                lock (m_Sync)
                    return m_Running;
            }
            set
            {
                lock (m_Sync)
                    m_Running = value;
            }
        }

        private ChatServer m_Server;

        // How long to wait between sending multicast packets (ms)
        private static readonly int MulticastDelay = 4000;

        public ChatServerMulticaster(ChatServer server)
        {
            m_Server = server;

            // Create UDP socket.
            m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // Join multicast group.
            m_Socket.SetSocketOption(SocketOptionLevel.IP,
                                     SocketOptionName.AddMembership,
                                     new MulticastOption(ChatConstants.MulticastIP));

            // Set time-to-live to 1, so it only travels through the local network.
            m_Socket.SetSocketOption(SocketOptionLevel.IP,
                                     SocketOptionName.MulticastTimeToLive,
                                     2);

            // Create the endpoint.
            m_Endpoint = new IPEndPoint(ChatConstants.MulticastIP, ChatConstants.MulticastPort);

            // Connect socket to the endpoint.
            m_Socket.Connect(m_Endpoint);

            // We are now running.
            Running = true;

            // Start the multicaster thread.
            m_Thread = new Thread(new ThreadStart(Run));
            m_Thread.IsBackground = true;
            m_Thread.Start();

            Console.WriteLine("Server multicaster initialised.");
        }

        public void Stop()
        {
            m_Socket.Close();

            Running = false;
        }

        // Produce message to send to multicast group that advertises the
        // server's presence.
        private byte[] GetMessage()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(ChatConstants.MulticastMagic);
                    bw.Write(m_Server.hostAddressString);
                }
                return ms.GetBuffer();
            }
        }

        private void Run()
        {
            // The message we will send to the multicast group.
            byte[] message = GetMessage();

            while (Running)
            {
                // Send the data.
                try
                {
                    m_Socket.Send(message);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception in multicaster: {e.Message}");
                    Console.WriteLine($"Stacktrace: {e.StackTrace}");
                    Console.WriteLine("Multicaster thread will now exit.");
                    break;
                }

                Thread.Sleep(MulticastDelay);
            }
        }
    }
}