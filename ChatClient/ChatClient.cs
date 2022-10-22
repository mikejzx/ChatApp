using System.Text;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatClient
{
    public class ChatClient
    {
        private string m_Nickname = "";

        public string Nickname { get => m_Nickname; set => m_Nickname = value; }

        private string m_Hostname = "";
        public string Hostname { get => m_Hostname; set => m_Hostname = value; }

        private int m_Port;
        public int Port { get => m_Port; set => m_Port = value; }

        private TcpClient? m_Tcp;
        private SslStream? m_Stream;
        private BinaryReader? m_Reader;
        private BinaryWriter? m_Writer;

        private List<string> m_Clients = new List<string>();

        public Action? OnConnectionSuccess;
        public Action<string>? OnConnectionFail;

        private bool m_InServer = false;

        private bool m_StopThread = false;
        private Thread? m_Thread;

        private Form? m_Form;
        public Form? Form { get => m_Form; set => m_Form = value; }

        public ChatClient(string nickname, int port)
        {
            Nickname = nickname;
            Port = port;
            m_InServer = false;
            m_StopThread = false;
        }

        private void Cleanup()
        {
            if (m_Writer is not null)
            {
                m_Writer.Close();
                m_Writer = null;
            }

            if (m_Reader is not null)
            {
                m_Reader.Close();
                m_Reader = null;
            }

            if (m_Stream is not null)
            {
                m_Stream.Close();
                m_Stream = null;
            }

            if (m_Tcp is not null)
            {
                m_Tcp.Close();
                m_Tcp = null;
            }

            if (m_Thread is not null)
            {
                m_StopThread = true;
                m_Thread.Join();
            }

            m_InServer = false;
        }

        public void Disconnect()
        {
            // Send disconnetion message.
            if (m_Writer is not null)
                m_Writer.Write((UInt32)ChatPacketType.ClientDisconnect);

            Cleanup();
        }

        private void ShowError(string msg)
        {
            if (Form is not null && OnConnectionFail is not null)
                Form.Invoke(OnConnectionFail, msg);
        }

        // Establish connection to host.
        public void Connect()
        {
            // Start the worker thread.
            m_Thread = new Thread(new ThreadStart(Run));
            m_Thread.Start();
        }

        private bool ValidateCertificate(object? sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors policyErrors)
        {
            // Trust all certificates.
            return true;
        }

        private void Run()
        {
            // Establish TCP connection
            try
            {
                m_Tcp = new TcpClient(Hostname, Port);
            }
            catch (Exception e)
            {
                ShowError(e.Message);
                return;
            }

            // Create SSL stream.
            m_Stream = new SslStream(m_Tcp.GetStream(),
                                     leaveInnerStreamOpen: false,
                                     userCertificateValidationCallback: ValidateCertificate);

            SslClientAuthenticationOptions options = new SslClientAuthenticationOptions();
            options.TargetHost = Hostname;
            m_Stream.AuthenticateAsClient(options);

            m_Stream.ReadTimeout = 5000;
            m_Stream.WriteTimeout = 5000;

            m_Writer = new BinaryWriter(m_Stream, Encoding.UTF8);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);

            // Write the hello packet.
            m_Writer.Write((uint)ChatPacketType.ClientHello);
            m_Writer.Write(Nickname);

            while (!m_StopThread)
            {
                try
                {
                    if (!m_Tcp.Connected)
                        break;

                    if (m_Tcp.Available <= 0)
                        continue;

                    ChatPacketType packet = (ChatPacketType)m_Reader.ReadUInt32();

                    // We are not "in" the server yet; so we only expect a ServerWelcome packet.
                    if (!m_InServer)
                    {
                        if (packet == ChatPacketType.ServerError)
                        {
                            ChatPacketErrorCode id = (ChatPacketErrorCode)m_Reader.ReadUInt32();
                            string msg = m_Reader.ReadString();
                            ShowError($"Server error {id}: {msg}");
                            return;
                        }

                        if (packet != ChatPacketType.ServerWelcome)
                        {
                            ShowError("Error: unexpected packet.");
                            return;
                        }
                    }

                    switch (packet)
                    {
                        // Server says that we are welcome in the server.
                        case (ChatPacketType.ServerWelcome):
                            m_InServer = true;

                            // Complete the connection.
                            if (Form is not null && OnConnectionSuccess is not null)
                                Form.Invoke(OnConnectionSuccess);

                            break;

                        // Server sends us the client list
                        case (ChatPacketType.ServerClientList):
                            // Clear the current client list.
                            m_Clients.Clear();

                            // Read number of clients
                            uint count = m_Reader.ReadUInt32();

                            // Read each client.
                            for (int i = 0; i < count; ++i)
                            {
                                string name = m_Reader.ReadString();

                                // Add to the new client list.
                                m_Clients.Add(name);
                            }

                            break;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception e)
                {
                    ShowError($"Exception: {e.Message}");
                    return;
                }
            }
        }
    }
}
