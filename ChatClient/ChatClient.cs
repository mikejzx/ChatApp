using System.Text;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatClient
{
    public enum ChatClientPasswordAwaitState
    {
        // Not waiting.
        None,

        // Waiting for response
        Waiting,

        // Password was incorrect.
        Failed,

        // Password was correct.
        Successful,
    };

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

                Invoke(OnLoginNameChanged, m_Nickname);
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

        public BinaryReader Reader 
        {
            get
            {
                if (m_Reader is null)
                    throw new NullReferenceException();

                return m_Reader;
            }
        }

        public BinaryWriter Writer 
        {
            get
            {
                if (m_Writer is null)
                    throw new NullReferenceException();

                return m_Writer;
            }
        }

        private ChatClientPacketHandler m_PacketHandler;

        // List of message channels.
        private List<ChatChannel> m_Channels = new List<ChatChannel>();
        public List<ChatChannel> Channels { get => m_Channels; }

        // List of rooms that we created.
        private List<ChatRoomChannel> m_OwnedRooms = new List<ChatRoomChannel>();
        public List<ChatRoomChannel> OwnedRooms { get => m_OwnedRooms; }

        // List of keys used for encrypted rooms.
        private Dictionary<string, byte[]?> m_RoomKeychain = new Dictionary<string, byte[]?>();
        public Dictionary<string, byte[]?> RoomKeychain { get => m_RoomKeychain; }

        // Timeout in seconds to wait for password response.
        private static readonly int RoomPasswordResponseTimeout = 5;

        // List of clients that are connected to the server.
        private Dictionary<string, ChatRecipient> m_Clients = new Dictionary<string, ChatRecipient>();
        public Dictionary<string, ChatRecipient> Clients { get => m_Clients; }

        // Channel we are chatting in.
        private ChatChannel? m_Channel = null;
        public ChatChannel? Channel
        { 
            get => m_Channel;
            set
            {
                if (value is not null)
                {
                    if (!m_Channels.Contains(value))
                    {
                        Invoke(() => MessageBox.Show($"Unknown channel"));
                        return;
                    }
                }

                // If we have not joined the room, attempt to join it.
                if (value is not null &&
                    !value.IsDirect && 
                    !((ChatRoomChannel)value).isJoined &&
                    !JoinRoom((ChatRoomChannel)value))
                {
                    // If we didn't join it; we don't change the channel, we
                    // leave it as what it was.
                }
                else
                {
                    m_Channel = value;
                }

                Invoke(OnChannelChanged);
            }
        }

        // Callbacks
        public Action? OnConnectionSuccess;
        public Action? OnConnectionLost;
        public Action<string>? OnLoginNameChanged;
        public Action<string>? OnError;
        public Action? OnChannelListUpdate;
        public Action? OnChannelChanged;
        public Func<ChatChannel, ChatMessage, bool>? OnMessageReceived;
        public Action<ChatRecipient>? OnClientLeave;
        public Action<ChatRecipient>? OnClientJoin;
        public Action<ChatRecipient, ChatMessage>? OnClientLeaveCurrentChannel;
        public Action<ChatRecipient, ChatMessage>? OnClientJoinCurrentChannel;
        public Action<ChatRecipient>? OnClientLeaveRoom;
        public Action<ChatRecipient>? OnClientJoinRoom;
        public Action<ChatRecipient, ChatMessage>? OnClientLeaveCurrentRoom;
        public Action<ChatRecipient, ChatMessage>? OnClientJoinCurrentRoom;
        public Func<X509Certificate, string, bool>? OnCertificateChanged;
        public Func<X509Certificate, bool>? OnCertificateFirstTime;
        public Action? OnCertificateValidationFailed;
        public Action? OnRoomCreateSuccess;
        public Action<string>? OnRoomCreateFail;
        public Action<string>? OnRoomDeleteFail;
        public Func<string?>? OnRoomPasswordRequested;
        public Action? OnRoomPasswordPending;
        public Action? OnRoomPasswordResponse;
        public Action? OnRoomPasswordMismatch;
        public Action? OnRoomPasswordCorrect;

        private bool m_InServer = false;
        public bool InServer { get => m_InServer; set => m_InServer = value; }

        private bool m_StopThread = false;
        private Thread? m_Thread;

        private Form? m_Form = null;
        public Form Form 
        {
            get
            {
                if (m_Form is null)
                    throw new NullReferenceException();

                return m_Form;
            }
            set => m_Form = value; 
        }

        // Indicates current password awaiting state (e.g. waiting for validation).
        public ChatClientPasswordAwaitState passwordAwait = ChatClientPasswordAwaitState.None;

        // Sync objects
        public readonly object threadStopSync = new object();
        public readonly object passwordAwaitSync = new object();

        // Convert client to a string (i.e. get our nickname)
        public override string ToString() => Nickname;

        // Function invocation shorthand.
        private object Invoke(Delegate? method, params object[] args)
        {
            if (m_Form is null || method is null)
                return false;

            return m_Form.Invoke(method, args);
        }

        public ChatClient(string nickname, int port)
        {
            m_Form = null;
            Nickname = nickname;
            Port = port;
            InServer = false;
            m_Channel = null;

            m_PacketHandler = new ChatClientPacketHandler(this);

            lock(threadStopSync)
                m_StopThread = false;

            lock (passwordAwaitSync)
                passwordAwait = ChatClientPasswordAwaitState.None;
        }

        // Send a message to the current channel.
        public void SendMessage(string message)
        {
            if (m_Writer is null)
                return;

            // Need a channel
            if (Channel is null)
                return;

            bool isEncrypted = false;
            byte[]? iv = null;

            // Create the message packet
            if (Channel.IsDirect)
            {
                ChatDirectChannel dc = (ChatDirectChannel)Channel;
                
                // Send direct message
                using (Packet packet = new Packet(PacketType.ClientDirectMessage))
                {
                    // Write recipient name
                    packet.Write(dc.Recipient.nickname);

                    // Write the message
                    packet.Write(message);

                    // Send to server.
                    packet.WriteToStream(Writer);
                    Writer.Flush();
                }
            }
            else
            {
                ChatRoomChannel rc = (ChatRoomChannel)Channel;

                if (rc.isEncrypted)
                {
                    // Can't send if keychain is missing the key.
                    if (!RoomKeychain.ContainsKey(rc.roomName))
                    {
                        ShowError($"No key for room '{rc.roomName}'");
                        return;
                    }

                    byte[]? roomKey = RoomKeychain[rc.roomName];
                    if (roomKey is null)
                    {
                        ShowError($"No key for room '{rc.roomName}'");
                        return;
                    }

                    isEncrypted = true;

                    // Encrypt the message.
                    using (Aes aes = Aes.Create())
                    {
                        //aes.KeySize = 128; // bits
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.BlockSize = 128; // bits
                        aes.Key = roomKey;
                        iv = aes.IV;

                        ICryptoTransform encryptor = aes.CreateEncryptor();

                        byte[] cipherMessage;
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                            {
                                using (StreamWriter sw = new StreamWriter(cs))
                                {
                                    sw.Write(message);
                                }
                                cipherMessage = ms.ToArray();
                            }
                        }

                        // Update the message with the cipher text.
                        message = Convert.ToBase64String(cipherMessage);
                    }
                }

                // Send room message
                using (Packet packet = new Packet(PacketType.ClientRoomMessage))
                {
                    // Write room name
                    packet.Write(rc.roomName);

                    // Write the message
                    packet.Write(message);

                    // Append the IV
                    if (isEncrypted && iv is not null)
                        packet.Write(Convert.ToBase64String(iv));

                    // Send to the server.
                    packet.WriteToStream(Writer);
                    Writer.Flush();
                }
            }
        }

        // Create a chat room.
        public void CreateRoom(string roomName, string roomTopic, bool roomEncrypted, string roomPassword="")
        {
            // If room name is already in use.
            foreach (ChatChannel channel in Channels)
            {
                if (channel.IsRoomChannel(roomName))
                {
                    Invoke(OnRoomCreateFail, "Please specify a different room name.");
                    return;
                }
            }

            // Encrypted room.
            if (roomEncrypted)
            {
                // Must have room password if encrypted.
                if (string.IsNullOrEmpty(roomPassword))
                {
                    Invoke(OnRoomCreateFail, "Please specify a room password.");
                    return;
                }

                // Derive secret key from the room password.
                byte[] key = ChatClientCrypto.DeriveKey(roomPassword, ChatClientCrypto.Salt);

                // Save the key to our keychain.
                if (RoomKeychain.ContainsKey(roomName))
                    RoomKeychain[roomName] = key;
                else
                    RoomKeychain.Add(roomName, key);
            }

            // Create the packet
            using (Packet packet = new Packet(PacketType.ClientRoomCreate))
            {
                // Write room name
                packet.Write(roomName);

                // Write room topic
                packet.Write(roomTopic);

                // Write whether room is password-protected.
                packet.Write(roomEncrypted);

                // Send it to server.
                packet.WriteToStream(Writer);
                Writer.Flush();
            }
        }

        // Delete a room (must be owned by the client).
        public void DeleteRoom(string roomName)
        {
            // Create the packet
            using (Packet packet = new Packet(PacketType.ClientRoomDelete))
            {
                // Write room name
                packet.Write(roomName);

                // Send it to server.
                packet.WriteToStream(Writer);
                Writer.Flush();
            }
        }

        // Join an encrypted room (returns true if successful).
        private bool JoinRoomEncrypted(ChatRoomChannel room)
        {
            if (OnRoomPasswordRequested is null ||
                OnRoomPasswordPending is null)
            {
                return false;
            }

            while (true)
            {
                lock (passwordAwaitSync)
                    passwordAwait = ChatClientPasswordAwaitState.None;

                // Request the password from user.
                string? password = (string?)Invoke(OnRoomPasswordRequested);
                if (password is null)
                {
                    // User cancelled the operation.
                    return false;
                }

                // Generate private key
                byte[] key = ChatClientCrypto.DeriveKey(password, ChatClientCrypto.Salt);

                // Set the key in the keychain.
                if (RoomKeychain.ContainsKey(room.roomName))
                    RoomKeychain[room.roomName] = key;
                else
                    RoomKeychain.Add(room.roomName, key);

                // Create the initial message.
                string plainText = Nickname + room.roomName + ChatClientCrypto.SaltString;

                // Encrypt the message
                CryptoCipher cipher = ChatClientCrypto.EncryptMessage(plainText, key);

                // Send a join request
                using (Packet packet = new Packet(PacketType.ClientRoomJoin))
                {
                    // Write room name
                    packet.Write(room.roomName);

                    // Write the salt.
                    packet.Write(ChatClientCrypto.SaltString);

                    // Write IV.
                    packet.Write(cipher.IVString);

                    // Write the join message to the server to be checked
                    // for validity.
                    packet.Write(cipher.CipherString);

                    // Send it to server.
                    packet.WriteToStream(Writer);
                    Writer.Flush();
                }

                // Set form as pending.
                Invoke(OnRoomPasswordPending);

                // Set the awaiting flag.
                lock (passwordAwaitSync)
                    passwordAwait = ChatClientPasswordAwaitState.Waiting;

                // Block until we get a response from the server.
                DateTime startTime = DateTime.Now;
                for (;;)
                {
                    lock (passwordAwaitSync)
                    {
                        if (passwordAwait != ChatClientPasswordAwaitState.Waiting)
                            break;
                    }

                    Thread.Sleep(100);

                    // Check for timeout, so we don't freeze the application
                    // for too long.
                    DateTime now = DateTime.Now;
                    if ((now - startTime).TotalSeconds > RoomPasswordResponseTimeout)
                    {
                        ShowError("Server took too long to respond to password input.");
                        break;
                    }
                }

                Invoke(OnRoomPasswordResponse);

                lock(passwordAwaitSync)
                {
                    switch (passwordAwait)
                    {
                    // Password was incorrect
                    case ChatClientPasswordAwaitState.Failed:
                        // Set the key in the keychain.
                        if (RoomKeychain.ContainsKey(room.roomName))
                            RoomKeychain[room.roomName] = null;

                        Invoke(OnRoomPasswordMismatch);

                        break;

                    // Password was correct
                    case ChatClientPasswordAwaitState.Successful:
                        Invoke(OnRoomPasswordCorrect);
                        return true;

                    // Time out
                    default: break;
                    }
                }
            }
        }

        // Join a room (returns true if successful).
        public bool JoinRoom(ChatRoomChannel room)
        {
            // Handle encrypted room.
            if (room.isEncrypted)
                return JoinRoomEncrypted(room);

            // Create the packet
            using (Packet packet = new Packet(PacketType.ClientRoomJoin))
            {
                // Write room name
                packet.Write(room.roomName);

                // Send it to server.
                packet.WriteToStream(Writer);
                Writer.Flush();
            }

            return true;
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
                lock(threadStopSync)
                    m_StopThread = true;

                //m_Thread.Join();
            }

            InServer = false;
        }

        public void Disconnect()
        {
            // Send disconnetion message.
            if (m_Writer is not null)
            {
                using (Packet packet = new Packet(PacketType.ClientDisconnect))
                {
                    packet.WriteToStream(Writer);
                    Writer.Flush();
                }
            }

            // Clear the clients and channels (to clear message history on logout).
            m_Clients.Clear();
            m_Channels.Clear();

            Cleanup();
        }

        private void ShowError(string msg) => Invoke(OnError, msg);

        // Establish connection to host.
        public void Connect()
        {
            lock(threadStopSync)
                m_StopThread = false;

            // Start the worker thread.
            m_Thread = new Thread(new ThreadStart(Run));
            m_Thread.IsBackground = true;
            m_Thread.Start();
        }

        private bool ValidateCertificate(object? sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors policyErrors)
        {
            if (cert is null)
            {
                ShowError("Server did not present a certificate.");
                return false;
            }

            // Base64 hashes of all certificates that we trust.
            Dictionary<string, string> trustedCertificates = new Dictionary<string, string>();

            using (FileStream file = new FileStream(Program.TOFUPath, FileMode.OpenOrCreate, FileAccess.Read))
            {
                if (!file.CanRead)
                {
                    ShowError("Failed to open TOFU file for reading.");
                    return false;
                }

                // Read list of trusted certificates from TOFU file.
                using (StreamReader reader = new StreamReader(file))
                {
                    for (string? line = reader.ReadLine();
                         line is not null;
                         line = reader.ReadLine())
                    {
                        string[] columns = line.Split(" ");

                        // Invalid line.
                        if (columns.Length != 2)
                            continue;

                        // Store hostname and fingerprint.
                        trustedCertificates.Add(columns[0], columns[1]);
                    }
                }

                // Get certificate details.
                string certName = $"{Hostname}:{Port}";
                string certFingerprint = cert.GetCertHashString();

                // Perform trust-on-first-use (TOFU) validation check.
                if (!trustedCertificates.ContainsKey(certName))
                {
                    // First time seeing this certificate, so we add it.
                    trustedCertificates.Add(certName, certFingerprint);

                    if (!(bool)Invoke(OnCertificateFirstTime, cert))
                    {
                        // User rejected the first certificate.
                        return false;
                    }
                }
                else
                {
                    // Check if hash matches what we have stored already.
                    if (trustedCertificates[certName] != certFingerprint)
                    {
                        // Warn user if hashes do not match
                        if (!(bool)Invoke(OnCertificateChanged,
                                          cert,
                                          trustedCertificates[certName]))
                        {
                            // User rejected the certificate
                            return false;
                        }

                        // User trusts new certificate; we replace it.
                        trustedCertificates[certName] = certFingerprint;
                    }
                }
            }

            using (FileStream file = new FileStream(Program.TOFUPath, FileMode.OpenOrCreate, FileAccess.Write))
            {
                if (!file.CanWrite)
                {
                    ShowError("Failed to open TOFU file for writing.");
                    return false;
                }

                // Write the new file.
                using(StreamWriter writer = new StreamWriter(file))
                {
                    foreach (KeyValuePair<string, string> certificate in trustedCertificates)
                    {
                        // Write as two columns; the hostname and the certificate itself.
                        writer.WriteLine($"{certificate.Key} {certificate.Value}");
                    }
                }
            }

            // Certificate is trusted.
            return true;
        }

        // Get direct message channel for a client.
        public ChatDirectChannel? GetDirectChannelForClient(ChatRecipient recipient)
        {
            foreach (ChatChannel channel2 in Channels)
            {
                if (channel2.IsDirectChannel(recipient.nickname))
                    return (ChatDirectChannel)channel2;
            }

            return null;
        }

        public ChatDirectChannel? GetDirectChannelForClient(string nickname)
        {
            if (!m_Clients.ContainsKey(nickname))
                return null;

            return GetDirectChannelForClient(m_Clients[nickname]);
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

            SslClientAuthenticationOptions options = new();
            options.TargetHost = Hostname;

            try
            {
                m_Stream.AuthenticateAsClient(options);
            }
            catch (System.Security.Authentication.AuthenticationException)
            {
                // Validation failed; certificate is untrusted.
                Invoke(OnCertificateValidationFailed);

                return;
            }
            catch(ObjectDisposedException)
            {
                // Due to user closing out of application.
                return;
            }

            m_Stream.ReadTimeout = Timeout.Infinite;
            m_Stream.WriteTimeout = Timeout.Infinite;

            m_Writer = new BinaryWriter(m_Stream, Encoding.UTF8);
            m_Reader = new BinaryReader(m_Stream, Encoding.UTF8);

            // Write the hello packet.
            using (Packet packet = new Packet(PacketType.ClientHello))
            {
                packet.Write(Nickname);
                packet.WriteToStream(m_Writer);
                m_Writer.Flush();
            }

            InServer = false;

            while (true)
            {
                lock (threadStopSync)
                {
                    if (m_StopThread)
                        break;
                }

                try
                {
                    if (!m_Tcp.Connected)
                        break;

                    //if (m_Tcp.Available <= 0)
                    //    continue;

                    // Read next packet.
                    using (Packet packet = new Packet(Reader))
                    {
                        // We are not "in" the server yet; so we only expect a ServerWelcome packet.
                        if (!InServer)
                        {
                            if (packet.PacketType == PacketType.ServerError)
                            {
                                PacketErrorCode id = (PacketErrorCode)packet.ReadUInt32();
                                string msg = packet.ReadString();
                                ShowError($"Server error {id}: {msg}");
                                Cleanup();
                                return;
                            }

                            if (packet.PacketType != PacketType.ServerWelcome)
                            {
                                ShowError("Error: unexpected packet.");
                                Cleanup();
                                return;
                            }
                        }

                        m_PacketHandler.Handle(packet);
                    }
                }
                catch (IOException)
                {
                    if (InServer)
                    {
                        Invoke(OnConnectionLost);
                    }
                    InServer = false;
                    Cleanup();
                    return;
                }
                catch (ObjectDisposedException)
                {
                    if (InServer)
                    {
                        Invoke(OnConnectionLost);
                    }
                    InServer = false;
                    Cleanup();
                    return;
                }
                catch (Exception e)
                {
                    if (InServer)
                    {
                        Invoke(OnConnectionLost);
                    }
                    InServer = false;
                    Cleanup();

                    ShowError($"Exception: {e.Message}");
                    return;
                }
            }
        }
    }
}
