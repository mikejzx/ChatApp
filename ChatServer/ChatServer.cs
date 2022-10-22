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

        public void Cleanup()
        {
            Console.WriteLine("Shutting down the server ...");

            // Cleanup client connections
            foreach (ChatServerClient client in m_Clients.Values)
            {
                client.Disconnect();
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

            // Listen for incoming connections.
            while(true)
            {
                Console.WriteLine("Listening ...");

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
            Console.WriteLine("Performing TLS handshake ...");
            SslStream sslStream = new SslStream(tcpClient.GetStream(),
                                                leaveInnerStreamOpen: false);

            // Authenticate server (but don't require client to authenticate).
            try
            {
                Console.WriteLine("Authenticating ...");
                sslStream.AuthenticateAsServer(m_Certificate,
                                               clientCertificateRequired: false,
                                               enabledSslProtocols: SslProtocols.Tls,
                                               checkCertificateRevocation: true);

                // Set timeouts
                sslStream.ReadTimeout = 5000;
                sslStream.WriteTimeout = 5000;

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

        // Send current client list to a client.
        internal void SendClientList(ChatServerClient client)
        {
            client.Writer.Write((uint)ChatPacketType.ServerClientList);
            client.Writer.Write((uint)m_Clients.Count);
            foreach (ChatServerClient client2 in m_Clients.Values)
            {
                client.Writer.Write(client2.Nickname);
            }
        }

        internal void AddClient(ChatServerClient client)
        {
            lock(m_Clients)
            {
                m_Clients.Add(client.Nickname, client);

                // For each client currently in the server, we send the current client list.
                foreach (ChatServerClient client2 in m_Clients.Values)
                {
                    SendClientList(client2);
                }
            }

            Console.WriteLine($"{client.Nickname} joined the server.");
        }

        internal bool RemoveClient(ChatServerClient client)
        {
            lock(m_Clients)
            {
                return m_Clients.Remove(client.Nickname);
            }
        }

        internal bool NicknameIsValid(string nickname)
        {
            if (nickname.Length <= 0 || nickname.Length > 32)
                return false;

            // Ensure the nickname is not already taken.
            lock(m_Clients)
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
