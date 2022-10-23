using System.Text;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatClient
{
    public class ChatClient
    {
        // The nickname of the client.
        private string m_Nickname = string.Empty;
        public string Nickname
        {
            get => m_Nickname;
            set
            {
                m_Nickname = value;

                if (Form is not null && OnLoginNameChanged is not null)
                    Form.Invoke(OnLoginNameChanged, m_Nickname);
            }
        }

        // The hostname the client is connecting to.
        private string m_Hostname = string.Empty;
        public string Hostname { get => m_Hostname; set => m_Hostname = value; }

        // Port of server to connect to.
        private int m_Port;
        public int Port { get => m_Port; set => m_Port = value; }

        private TcpClient? m_Tcp;
        private SslStream? m_Stream;
        private BinaryReader? m_Reader;
        private BinaryWriter? m_Writer;

        // List of clients that are connected to the server.
        private Dictionary<string, ChatClientRecipient> m_Clients = new Dictionary<string, ChatClientRecipient>();
        public Dictionary<string, ChatClientRecipient> Clients { get => m_Clients; }

        // User we are chatting with.
        private string? m_RecipientName = null;
        public string? Recipient 
        { 
            get => m_RecipientName;
            set
            {
                // Can't chat with ourselves
                if (value == Nickname)
                {
                    m_RecipientName = null;
                }
                else
                {
                    if (value is not null)
                    {
                        if (!m_Clients.ContainsKey(value))
                        {
                            MessageBox.Show($"User {value} is not in the server.");
                            return;
                        }
                    }

                    m_RecipientName = value;
                }

                if (Form is not null && OnRecipientChanged is not null)
                {
                    if (m_RecipientName is null)
                        Form.Invoke(OnRecipientChanged, (object?)null);
                    else
                        Form.Invoke(OnRecipientChanged, m_Clients[m_RecipientName]);
                }
            }
        }

        // Callbacks
        public Action? OnConnectionSuccess;
        public Action<string>? OnLoginNameChanged;
        public Action<string>? OnError;
        public Action<Dictionary<string, ChatClientRecipient>>? OnClientListUpdate;
        public Action<ChatClientRecipient?>? OnRecipientChanged;
        public Func<string, ChatMessage, bool>? OnMessageReceived;
        public Action<ChatClientRecipient>? OnClientLeave;
        public Action<ChatClientRecipient>? OnClientJoin;

        private bool m_InServer = false;

        private bool m_StopThread = false;
        private Thread? m_Thread;

        private Form? m_Form = null;
        public Form? Form { get => m_Form; set => m_Form = value; }

        private readonly object m_ThreadStopSync = new object();

        public ChatClient(string nickname, int port)
        {
            Form = null;
            Nickname = nickname;
            Port = port;
            m_InServer = false;
            m_RecipientName = null;

            lock(m_ThreadStopSync)
                m_StopThread = false;
        }

        // Send a message to the current recipient
        public void SendMessage(string message)
        {
            if (m_Writer is null)
                return;

            // Need a recipient
            if (m_RecipientName is null)
                return;

            // Create the message packet
            using (Packet packet = new Packet(PacketType.ClientDirectMessage))
            {
                // Write recipient name
                packet.Write(m_RecipientName);

                // Write the message
                packet.Write(message);

                // Send to server.
                packet.WriteToStream(m_Writer);
                m_Writer.Flush();
            }
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
                lock(m_ThreadStopSync)
                    m_StopThread = true;

                m_Thread.Join();
            }

            m_InServer = false;
        }

        public void Disconnect()
        {
            // Send disconnetion message.
            if (m_Writer is not null)
            {
                using (Packet packet = new Packet(PacketType.ClientDisconnect))
                {
                    packet.WriteToStream(m_Writer);
                    m_Writer.Flush();
                }
            }

            Cleanup();
        }

        private void ShowError(string msg)
        {
            if (Form is not null && OnError is not null)
                Form.Invoke(OnError, msg);
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

        private void HandlePacket(Packet packet)
        {
            switch (packet.PacketType)
            {
                // Server welcomes us into the server.
                case PacketType.ServerWelcome:
                    m_InServer = true;

                    if (Form is not null && OnConnectionSuccess is not null)
                        Form.Invoke(OnConnectionSuccess);

                    break;

                // Server sends us an error message.
                case PacketType.ServerError:
                    PacketErrorCode code = (PacketErrorCode)packet.ReadUInt32();
                    string msg = packet.ReadString();
                    ShowError(msg);
                    break;

                // Server sends us current client list.
                case PacketType.ServerClientList:
                    m_Clients.Clear();

                    int count = packet.ReadInt32();

                    for (int i = 0; i < count; ++i)
                    {
                        string nickname = packet.ReadString();
                        m_Clients.Add(nickname, new ChatClientRecipient(nickname, true));
                    }    

                    if (Form is not null && OnClientListUpdate is not null)
                        Form.Invoke(OnClientListUpdate, m_Clients);

                    break;

                // Server tells us that a client joined
                case PacketType.ServerClientJoin:
                    // Read their nickname
                    string joinedNickname = packet.ReadString();

                    // Add the client (or set it as joined if they are unjoined).
                    if (m_Clients.ContainsKey(joinedNickname))
                    {
                        m_Clients[joinedNickname].isJoined = true;
                    }
                    else
                    {
                        m_Clients.Add(joinedNickname, new ChatClientRecipient(joinedNickname, true));
                    }

                    // Run client list change callback.
                    if (Form is not null && OnClientListUpdate is not null)
                        Form.Invoke(OnClientListUpdate, m_Clients);

                    // Run client join callback
                    if (Form is not null && OnClientJoin is not null)
                        Form.Invoke(OnClientJoin, m_Clients[joinedNickname]);

                    break;

                // Server tells us that a client left
                case PacketType.ServerClientLeave:
                    // Read their nickname
                    string leaveNickname = packet.ReadString();

                    // Unknown client left
                    if (!m_Clients.ContainsKey(leaveNickname))
                        break;

                    // Set client as unjoined.
                    m_Clients[leaveNickname].isJoined = false;

                    // Run client exit callback
                    if (Form is not null && OnClientLeave is not null)
                        Form.Invoke(OnClientLeave, m_Clients[leaveNickname]);

                    break;

                // Server tells us that we received a direct message
                case PacketType.ServerDirectMessageReceived:
                    // Read who the message was sent from.
                    string sender = packet.ReadString();

                    // Read recipient that message was sent to.
                    string recipient = packet.ReadString();

                    // Read the message
                    string message = packet.ReadString();

                    // Determine under whose name the message should be stored.
                    string channel;
                    if (sender == m_Nickname)
                        channel = recipient;
                    else
                        channel = sender;

                    // If we send the message 
                    if (!m_Clients.ContainsKey(channel))
                    {
                        ShowError($"Received message from unknown user {channel}.");
                        break;
                    }

                    // Append to message list for this recipient.
                    ChatMessage addedMessage = m_Clients[channel].AddMessage(sender, message);

                    if (Form is not null && OnMessageReceived is not null)
                    {
                        if (!(bool)Form.Invoke(OnMessageReceived, channel, addedMessage))
                        {
                            // Increment unread message count
                            if (m_Clients.ContainsKey(channel))
                            {
                                ++m_Clients[channel].UnreadMessages;

                                // Update the client list to show the unread messages.
                                if (OnClientListUpdate is not null)
                                    Form.Invoke(OnClientListUpdate, m_Clients);
                            }
                        }
                    }

                    break;
            }
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

            m_Stream.ReadTimeout = Timeout.Infinite;
            m_Stream.WriteTimeout = Timeout.Infinite;

            m_Writer = new BinaryWriter(m_Stream, Encoding.UTF8);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);

            // Write the hello packet.
            using (Packet packet = new Packet(PacketType.ClientHello))
            {
                packet.Write(Nickname);
                packet.WriteToStream(m_Writer);
            }

            m_InServer = false;

            while (true)
            {
                lock (m_ThreadStopSync)
                {
                    if (m_StopThread)
                        break;
                }

                try
                {
                    if (!m_Tcp.Connected)
                        break;

                    //if (!m_Tcp.GetStream().DataAvailable)
                        //continue;

                    // Read next packet.
                    Packet packet = new Packet(m_Reader);

                    // We are not "in" the server yet; so we only expect a ServerWelcome packet.
                    if (!m_InServer)
                    {
                        if (packet.PacketType == PacketType.ServerError)
                        {
                            PacketErrorCode id = (PacketErrorCode)packet.ReadUInt32();
                            string msg = packet.ReadString();
                            ShowError($"Server error {id}: {msg}");
                            return;
                        }

                        if (packet.PacketType != PacketType.ServerWelcome)
                        {
                            ShowError("Error: unexpected packet.");
                            return;
                        }
                    }

                    HandlePacket(packet);
                }
                catch (IOException)
                {
                    return;
                }
                catch (ObjectDisposedException)
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
