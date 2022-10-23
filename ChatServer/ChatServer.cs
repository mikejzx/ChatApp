﻿using System.Collections;
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

        private static readonly string CertificatePath = "cert.pfx";

        // Sync objects
        private readonly object clientSync = new object();

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

        public void Run()
        {
            Console.WriteLine("Starting server ...");

            // Read certificate file.
            try
            {
                m_Certificate = new X509Certificate2(CertificatePath);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                Console.WriteLine($"Failed to read server certificate file {CertificatePath}");
                Console.ReadKey();
                return;
            }

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
                
                m_Clients[client.Nickname] = client;
            }

            Console.WriteLine($"{client.Nickname} joined the server.");
        }

        internal bool RemoveClient(ChatServerClient client)
        {
            lock(clientSync)
            {
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
