﻿using System.Text;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
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
                        if (Form is not null)
                            Form.Invoke(() => MessageBox.Show($"Unknown channel"));
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

                if (Form is not null && OnChannelChanged is not null)
                    Form.Invoke(OnChannelChanged);
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

        private bool m_StopThread = false;
        private Thread? m_Thread;

        private Form? m_Form = null;
        public Form? Form { get => m_Form; set => m_Form = value; }

        private enum PasswordAwaitState
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

        // True if we are waiting for the server to validate our password when
        // joining encrypted room.
        private PasswordAwaitState m_PasswordAwait = PasswordAwaitState.None;

        // Sync objects
        private readonly object m_ThreadStopSync = new object();
        private readonly object m_PasswordAwaitSync = new object();

        public ChatClient(string nickname, int port)
        {
            Form = null;
            Nickname = nickname;
            Port = port;
            m_InServer = false;
            m_Channel = null;

            lock(m_ThreadStopSync)
                m_StopThread = false;

            lock (m_PasswordAwaitSync)
                m_PasswordAwait = PasswordAwaitState.None;
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
                    packet.WriteToStream(m_Writer);
                    m_Writer.Flush();
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
                    packet.WriteToStream(m_Writer);
                    m_Writer.Flush();
                }
            }
        }

        // Create a chat room.
        public void CreateRoom(string roomName, string roomTopic, bool roomEncrypted, string roomPassword="")
        {
            if (m_Writer is null)
                return;

            // If room name is already in use.
            foreach (ChatChannel channel in Channels)
            {
                if (!channel.IsDirect &&
                    ((ChatRoomChannel)channel).roomName == roomName)
                {
                    ShowError("Please specify a different room name.");
                    return;
                }
            }

            // Encrypted room.
            if (roomEncrypted)
            {
                // Must have room password if encrypted.
                if (string.IsNullOrEmpty(roomPassword))
                {
                    ShowError("Please specify a room password.");
                    return;
                }

                // Derive secret key from the room password via PBKDF2.
                byte[] passwordBytes = Encoding.UTF8.GetBytes(roomPassword);
                //byte[] salt = RandomNumberGenerator.GetBytes(128);
                byte[] salt = new byte[] { 0x01, 0x02, 0x03, 0x04, 
                                           0x05, 0x06, 0x07, 0x08,
                                           0x09, 0x0a, 0x0b, 0x0c,
                                           0x0d, 0x0e, 0x0f, 0x10 };
                byte[] key = Rfc2898DeriveBytes.Pbkdf2(password: passwordBytes,
                                                       salt: salt,
                                                       iterations: 8,
                                                       hashAlgorithm: HashAlgorithmName.SHA512,
                                                       outputLength: 128 / 8);

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
                packet.WriteToStream(m_Writer);
                m_Writer.Flush();
            }
        }

        // Delete a room (must be owned by the client).
        public void DeleteRoom(string roomName)
        {
            if (m_Writer is null)
                return;

            // Create the packet
            using (Packet packet = new Packet(PacketType.ClientRoomDelete))
            {
                // Write room name
                packet.Write(roomName);

                // Send it to server.
                packet.WriteToStream(m_Writer);
                m_Writer.Flush();
            }
        }

        // Join an encrypted room (returns true if successful).
        private bool JoinRoomEncrypted(ChatRoomChannel room)
        {
            if (m_Writer is null)
                return false;

            if (Form is null)
                return false;

            if (OnRoomPasswordRequested is null ||
                OnRoomPasswordPending is null)
            {
                return false;
            }

            using (Aes aes = Aes.Create())
            {
                //aes.KeySize = 128; // bits
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.BlockSize = 128; // bits

                string ivString = Convert.ToBase64String(aes.IV);

                while (true)
                {
                    lock (m_PasswordAwaitSync)
                        m_PasswordAwait = PasswordAwaitState.None;

                    // Request the password from user.
                    string? password = (string?)Form.Invoke(OnRoomPasswordRequested);
                    if (password is null)
                    {
                        // User cancelled the operation.
                        return false;
                    }

                    // Generate a secret key from the given password via PBKDF2.
                    byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                    //byte[] salt = RandomNumberGenerator.GetBytes(128);
                    byte[] salt = new byte[] { 0x01, 0x02, 0x03, 0x04, 
                                               0x05, 0x06, 0x07, 0x08,
                                               0x09, 0x0a, 0x0b, 0x0c,
                                               0x0d, 0x0e, 0x0f, 0x10 };
                    byte[] key = Rfc2898DeriveBytes.Pbkdf2(password: passwordBytes,
                                                           salt: salt,
                                                           iterations: 8,
                                                           hashAlgorithm: HashAlgorithmName.SHA512,
                                                           outputLength: 128 / 8);
                    aes.Key = key;

                    // Set the key in the keychain.
                    if (RoomKeychain.ContainsKey(room.roomName))
                        RoomKeychain[room.roomName] = key;
                    else
                        RoomKeychain.Add(room.roomName, key);

                    string saltString = Convert.ToBase64String(salt);

                    ICryptoTransform encryptor = aes.CreateEncryptor();

                    // Encrypt <user name> + <room name> + <salt> with the derived key.
                    string plaintextMessage = Nickname + room.roomName + saltString;
                    byte[] cipherMessage;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            using (StreamWriter sw = new StreamWriter(cs))
                            {
                                sw.Write(plaintextMessage);
                            }
                            cipherMessage = ms.ToArray();
                        }
                    }

                    // Send a join request
                    using (Packet packet = new Packet(PacketType.ClientRoomJoin))
                    {
                        // Write room name
                        packet.Write(room.roomName);

                        // Write the salt.
                        packet.Write(saltString);

                        // Write IV.
                        packet.Write(ivString);

                        // Write the join message to the server to be checked
                        // for validity.
                        packet.Write(Convert.ToBase64String(cipherMessage));

                        // Send it to server.
                        packet.WriteToStream(m_Writer);
                        m_Writer.Flush();
                    }

                    // Set form as pending.
                    Form.Invoke(OnRoomPasswordPending);

                    // Set the awaiting flag.
                    lock (m_PasswordAwaitSync)
                        m_PasswordAwait = PasswordAwaitState.Waiting;

                    // Block until we get a response from the server.
                    DateTime startTime = DateTime.Now;
                    for (;;)
                    {
                        lock (m_PasswordAwaitSync)
                        {
                            if (m_PasswordAwait != PasswordAwaitState.Waiting)
                                break;
                        }

                        Thread.Sleep(100);

                        // Timeout
                        DateTime now = DateTime.Now;
                        if ((now - startTime).TotalSeconds > RoomPasswordResponseTimeout)
                        {
                            ShowError("Server took too long to respond to password input.");
                            break;
                        }
                    }

                    if (OnRoomPasswordResponse is not null)
                        Form.Invoke(OnRoomPasswordResponse);

                    lock(m_PasswordAwaitSync)
                    {
                        switch (m_PasswordAwait)
                        {
                        // Password was incorrect
                        case PasswordAwaitState.Failed:
                            // Set the key in the keychain.
                            if (RoomKeychain.ContainsKey(room.roomName))
                                RoomKeychain[room.roomName] = null;

                            if (OnRoomPasswordMismatch is not null)
                                Form.Invoke(OnRoomPasswordMismatch);
                            break;

                        // Password was correct
                        case PasswordAwaitState.Successful:
                            if (OnRoomPasswordCorrect is not null)
                                Form.Invoke(OnRoomPasswordCorrect);
                            return true;

                        // Time out
                        default: break;
                        }
                    }
                }
            }
        }

        // Join a room (returns true if successful).
        public bool JoinRoom(ChatRoomChannel room)
        {
            if (m_Writer is null)
                return false;

            // Handle encrypted room.
            if (room.isEncrypted)
                return JoinRoomEncrypted(room);

            // Create the packet
            using (Packet packet = new Packet(PacketType.ClientRoomJoin))
            {
                // Write room name
                packet.Write(room.roomName);

                // Send it to server.
                packet.WriteToStream(m_Writer);
                m_Writer.Flush();
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
                lock(m_ThreadStopSync)
                    m_StopThread = true;

                //m_Thread.Join();
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

            // Clear the clients and channels (to clear message history on logout).
            m_Clients.Clear();
            m_Channels.Clear();

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
            lock(m_ThreadStopSync)
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
                        if (columns.Count() != 2)
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
                    if (Form != null && OnCertificateFirstTime != null)
                    {
                        if (!(bool)Form.Invoke(OnCertificateFirstTime, cert))
                        {
                            // User rejected the first certificate.
                            return false;
                        }
                    }
                }
                else
                {
                    // Check if hash matches what we have stored already.
                    if (trustedCertificates[certName] != certFingerprint)
                    {
                        // Warn user if hashes do not match
                        if (Form != null && OnCertificateChanged != null)
                        {
                            if (!(bool)Form.Invoke(OnCertificateChanged,
                                                   cert,
                                                   trustedCertificates[certName]))
                            {
                                // User rejected the certificate
                                return false;
                            }

                            // User trusts new certificate; we replace it.
                            trustedCertificates[certName] = certFingerprint;
                        }
                        else
                        {
                            return false;
                        }
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
                if (channel2.IsDirect && channel2.ContainsRecipient(recipient))
                {
                    return (ChatDirectChannel)channel2;
                }
            }

            return null;
        }

        public ChatDirectChannel? GetDirectChannelForClient(string nickname)
        {
            if (!m_Clients.ContainsKey(nickname))
                return null;

            return GetDirectChannelForClient(m_Clients[nickname]);
        }

        private void HandlePacket(Packet packet)
        {
            switch (packet.PacketType)
            {
                // Server welcomes us into the server.
                case PacketType.ServerWelcome:
                { 
                    m_InServer = true;

                    if (Form is not null && OnConnectionSuccess is not null)
                        Form.Invoke(OnConnectionSuccess);

                    break;
                }

                // Server sends us an error message.
                case PacketType.ServerError:
                { 
                    PacketErrorCode code = (PacketErrorCode)packet.ReadUInt32();
                    string msg = packet.ReadString();

                    ShowError(msg);

                    break;
                }

                // Server sends us current client list.
                case PacketType.ServerClientList:
                {
                    int count = packet.ReadInt32();

                    foreach (ChatRecipient client in m_Clients.Values)
                    {
                        // Set each client as unjoined, and then whoever is in
                        // the server client list is set to joined.
                        client.isJoined = false;
                    }

                    for (int i = 0; i < count; ++i)
                    {
                        string nickname = packet.ReadString();

                        // Skip ourself
                        if (nickname == Nickname)
                            continue;

                        // Check if we already have the client.
                        if (!m_Clients.ContainsKey(nickname))
                        {
                            // Add the client.
                            ChatRecipient addedRecipient = new ChatRecipient(nickname, true);
                            m_Clients.Add(nickname, addedRecipient);

                            // Create the direct channel for the new recipient.
                            Channels.Add(new ChatDirectChannel(addedRecipient));
                        }
                        else
                        {
                            // Set them as joined.
                            m_Clients[nickname].isJoined = true;
                        }
                    }

                    // Update channel list.
                    if (Form is not null && OnChannelListUpdate is not null)
                        Form.Invoke(OnChannelListUpdate);

                    break;
                }

                // Server sends us current room list.
                case PacketType.ServerRoomList:
                {
                    int count = packet.ReadInt32();

                    // Clear old rooms.
                    foreach (ChatChannel channel in Channels)
                    {
                        if (!channel.IsDirect)
                            Channels.Remove(channel);
                    }

                    for (int i = 0; i < count; ++i)
                    {
                        string roomName = packet.ReadString();
                        string roomTopic = packet.ReadString();
                        bool roomEncrypted = packet.ReadBool();

                        // Create channel for the room.
                        ChatRoomChannel channel = new ChatRoomChannel(roomName, roomTopic, roomEncrypted);
                        Channels.Add(channel);
                    }

                    // Update channel list.
                    if (Form is not null && OnChannelListUpdate is not null)
                        Form.Invoke(OnChannelListUpdate);

                    break;
                }

                // Server sends us the members of a room (e.g. on room join)
                case PacketType.ServerClientRoomMembers:
                {
                    string roomName = packet.ReadString();
                    int memberCount = packet.ReadInt32();

                    // Find the channel
                    ChatChannel? channel = null;
                    foreach (ChatChannel channel2 in Channels)
                    {
                        if (!channel2.IsDirect && 
                            ((ChatRoomChannel)channel2).roomName == roomName)
                        {
                            channel = channel2;
                            break;
                        }
                    }

                    if (channel is null)
                    {
                        ShowError($"Got members for unknown room '{roomName}'");
                        break;
                    }

                    channel.recipients.Clear();

                    for (int i = 0; i < memberCount; ++i)
                    {
                        string clientName = packet.ReadString();

                        if (clientName == Nickname)
                        {
                            // We are a member of this room.
                            ((ChatRoomChannel)channel).isJoined = true;

                            continue;
                        }

                        // Ensure we have the client.
                        if (!m_Clients.ContainsKey(clientName))
                        {
                            ShowError($"Room contains unknown user {clientName}");
                            continue;
                        }

                        // Add the client to the channel
                        channel.recipients.Add(m_Clients[clientName]);
                    }

                    break;
                }

                // Server tells us that a client joined.
                case PacketType.ServerClientJoin:
                { 
                    // Read their nickname
                    string nickname = packet.ReadString();

                    // Skip ourself just to be safe.
                    if (nickname == Nickname)
                        break;

                    // Add the client (or set it as joined if they are unjoined).
                    if (!m_Clients.ContainsKey(nickname))
                    {
                        ChatRecipient joinedRecipient = new ChatRecipient(nickname, true);
                        m_Clients.Add(nickname, joinedRecipient);

                        // Create a direct channel for the user
                        Channels.Add(new ChatDirectChannel(joinedRecipient));
                    }
                    else
                    {
                        m_Clients[nickname].isJoined = true;
                    }

                    // Iterate over the channels that the joining client is a
                    // part of (e.g. it's direct-message channel).
                    foreach (ChatChannel c in Channels)
                    {
                        if (!c.ContainsRecipient(m_Clients[nickname]))
                            continue;

                        // Append join message
                        ChatMessage msg = c.AddMessage(ChatMessageType.UserJoin, nickname);

                        // Update active channel message list.
                        if (c == Channel && Form is not null && OnClientJoinCurrentChannel is not null)
                            Form.Invoke(OnClientJoinCurrentChannel, m_Clients[nickname], msg);
                    }

                    // Run channel list change callback.
                    if (Form is not null && OnChannelListUpdate is not null)
                        Form.Invoke(OnChannelListUpdate);

                    // Run client join callback
                    if (Form is not null && OnClientJoin is not null)
                        Form.Invoke(OnClientJoin, m_Clients[nickname]);

                    break;
                }

                // Server tells us that a client left
                case PacketType.ServerClientLeave:
                { 
                    // Read their nickname
                    string nickname = packet.ReadString();

                    // Skip ourself just to be safe.
                    if (nickname == Nickname)
                        break;

                    // Unknown client left
                    if (!m_Clients.ContainsKey(nickname))
                        break;

                    // Set client as unjoined.
                    m_Clients[nickname].isJoined = false;

                    // Append unjoin message to all channels that are relevant
                    // to the client.
                    foreach (ChatChannel c in Channels)
                    {
                        if (!c.ContainsRecipient(m_Clients[nickname]))
                            continue;

                        // Append join message
                        ChatMessage msg = c.AddMessage(ChatMessageType.UserLeave, nickname);

                        // Update active channel message list.
                        if (c == Channel && Form is not null && OnClientLeaveCurrentChannel is not null)
                            Form.Invoke(OnClientLeaveCurrentChannel, m_Clients[nickname], msg);
                    }

                    // Run client exit callback
                    if (Form is not null && OnClientLeave is not null)
                        Form.Invoke(OnClientLeave, m_Clients[nickname]);

                    break;
                }

                // Server tells us that a client joined the room.
                case PacketType.ServerClientRoomJoin:
                {
                    // Read their nickname
                    string roomName = packet.ReadString();
                    string nickname = packet.ReadString();

                    // Get room
                    ChatRoomChannel? room = null;
                    foreach (ChatChannel channel in m_Channels)
                    {
                        if (!channel.IsDirect && 
                            ((ChatRoomChannel)channel).roomName == roomName)
                        {
                            room = (ChatRoomChannel)channel;
                            break;
                        }
                    }

                    // Joined unknown room.
                    if (room is null)
                    {
                        ShowError($"Client '{nickname}' joined unknown room '{roomName}'.");
                        break;
                    }

                    // Skip if the room already contains recipient
                    if (room.ContainsRecipient(nickname))
                        break;

                    // Unknown client joined
                    if (nickname != Nickname &&
                        !m_Clients.ContainsKey(nickname))
                    {
                        ShowError($"Unknown client '{nickname}' joined room '{roomName}'.");
                        break;
                    }

                    // Add client to the room recipients
                    if (nickname != Nickname)
                    {
                        room.recipients.Add(m_Clients[nickname]);
                    }
                    else
                    {
                        // If this packet gets sent to us with our own nickname
                        // it indicates us joining a room that we created.
                        room.isJoined = true;

                        m_OwnedRooms.Add(room);

                        if (Form is not null && OnRoomCreateSuccess is not null)
                            Form.Invoke(OnRoomCreateSuccess);

                        break;
                    }

                    // Append join message
                    ChatMessage msg = room.AddMessage(ChatMessageType.UserJoinRoom, nickname);

                    if (Form is not null)
                    {
                        // Update active channel message list.
                        if (room == Channel && OnClientJoinCurrentRoom is not null)
                            Form.Invoke(OnClientJoinCurrentRoom, m_Clients[nickname], msg);

                        // Run client join callback
                        if (OnClientJoinRoom is not null)
                            Form.Invoke(OnClientJoinRoom, m_Clients[nickname]);
                    }

                    break;
                }

                // Server tells us that a client left the room.
                case PacketType.ServerClientRoomLeave:
                {
                    // Read their nickname
                    string roomName = packet.ReadString();
                    string nickname = packet.ReadString();

                    // Skip ourself just to be safe.
                    if (nickname == Nickname)
                        break;

                    // Get room
                    ChatRoomChannel? room = null;
                    foreach (ChatChannel channel in m_Channels)
                    {
                        if (!channel.IsDirect && 
                            ((ChatRoomChannel)channel).roomName == roomName)
                        {
                            room = (ChatRoomChannel)channel;
                        }
                    }

                    // Left unknown room.
                    if (room is null)
                        break;

                    // Unknown client left
                    if (!m_Clients.ContainsKey(nickname) || 
                        !room.ContainsRecipient(nickname))
                        break;

                    // Remove client from the room recipients
                    room.recipients.Remove(m_Clients[nickname]);

                    // Append leave message
                    ChatMessage msg = room.AddMessage(ChatMessageType.UserLeaveRoom, nickname);

                    if (Form is not null)
                    {
                        // Update active channel message list.
                        if (room == Channel && OnClientLeaveCurrentRoom is not null)
                            Form.Invoke(OnClientLeaveCurrentRoom, m_Clients[nickname], msg);

                        // Run client leave callback
                        if (OnClientLeaveRoom is not null)
                            Form.Invoke(OnClientLeaveRoom, m_Clients[nickname]);
                    }

                    break;
                }

                // Server tells us that we received a direct message
                case PacketType.ServerDirectMessageReceived:
                { 
                    // Read who the message was sent from.
                    string sender = packet.ReadString();

                    // Read recipient that message was sent to.
                    string recipient = packet.ReadString();

                    // Read the message
                    string message = packet.ReadString();

                    // Determine under whose name the message should be stored.
                    string channelName;
                    if (sender == m_Nickname)
                        channelName = recipient;
                    else
                        channelName = sender;

                    // Append to the channel's message list.
                    ChatChannel? channel = null;
                    foreach (ChatChannel channel2 in Channels)
                    {
                        if (channel2.IsDirect && channel2.ContainsRecipient(channelName))
                        {
                            channel = channel2;
                            break;
                        }
                    }

                    if (channel is null)
                    { 
                        ShowError($"Received message from unknown user {channelName}.");
                        break;
                    }

                    ChatMessage addedMessage = channel.AddMessage(ChatMessageType.UserMessage, 
                                                                  sender, 
                                                                  message);

                    if (Form is not null && OnMessageReceived is not null)
                    {
                        if (!(bool)Form.Invoke(OnMessageReceived, channel, addedMessage))
                        {
                            ++channel.unreadMessages;

                            // Update the client list to show the unread messages.
                            if (OnChannelListUpdate is not null)
                                Form.Invoke(OnChannelListUpdate);
                        }
                    }

                    break;
                }

                case PacketType.ServerRoomMessageReceived:
                {
                    // Read who the message was sent from.
                    string sender = packet.ReadString();

                    // Read room that message was sent to.
                    string roomName = packet.ReadString();

                    // Read the message itself.
                    string message = packet.ReadString();

                    // Append to the room's message list.
                    ChatRoomChannel? channel = null;
                    foreach (ChatChannel channel2 in Channels)
                    {
                        if (!channel2.IsDirect && 
                            ((ChatRoomChannel)channel2).roomName == roomName)
                        {
                            channel = (ChatRoomChannel)channel2;
                            break;
                        }
                    }

                    if (channel is null)
                    { 
                        ShowError($"Received message from unknown room {roomName}.");
                        break;
                    }

                    // Decrypt the message
                    if (channel.isEncrypted)
                    {
                        string ivString = packet.ReadString();
                        byte[] iv = Convert.FromBase64String(ivString);

                        // Can't send if keychain is missing the key.
                        if (!RoomKeychain.ContainsKey(channel.roomName))
                        {
                            ShowError($"No key for room '{channel.roomName}'");
                            break;
                        }

                        byte[]? roomKey = RoomKeychain[channel.roomName];
                        if (roomKey is null)
                        {
                            ShowError($"No key for room '{channel.roomName}'");
                            break;
                        }

                        byte[] cipherMessage = Convert.FromBase64String(message);
                        using (Aes aes = Aes.Create())
                        {
                            //aes.KeySize = 128; // bits
                            aes.Mode = CipherMode.CBC;
                            aes.Padding = PaddingMode.PKCS7;
                            aes.BlockSize = 128; // bits
                            aes.IV = iv;
                            aes.Key = roomKey;

                            ICryptoTransform decryptor = aes.CreateDecryptor();

                            // Decrypt the message
                            try
                            {
                                using (MemoryStream ms = new MemoryStream(cipherMessage))
                                {
                                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                                    {
                                        using (StreamReader sr = new StreamReader(cs))
                                        {
                                            // Use the deciphered message
                                            message = sr.ReadToEnd();
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Failure; presumably the key is wrong.
                                message = "!! failed to decrypt !!";
                            }
                        }
                    }

                    ChatMessage addedMessage = channel.AddMessage(ChatMessageType.UserMessage, 
                                                                  sender, 
                                                                  message);

                    if (Form is not null && OnMessageReceived is not null)
                    {
                        if (!(bool)Form.Invoke(OnMessageReceived, channel, addedMessage))
                        {
                            ++channel.unreadMessages;

                            // Update the channel list to show the unread messages.
                            if (OnChannelListUpdate is not null)
                                Form.Invoke(OnChannelListUpdate);
                        }
                    }

                    break;
                }

                // Server tells us that a room was created
                case PacketType.ServerRoomCreated:
                {
                    string roomName = packet.ReadString();
                    string roomTopic = packet.ReadString();
                    bool roomEncrypted = packet.ReadBool();

                    // Add the room channel.
                    ChatRoomChannel channel = new ChatRoomChannel(roomName, roomTopic, roomEncrypted);
                    Channels.Add(channel);

                    // Run channel list changed callback.
                    if (Form is not null && OnChannelListUpdate is not null)
                        Form.Invoke(OnChannelListUpdate);

                    break;
                }

                // Server tells us that a room was deleted
                case PacketType.ServerRoomDeleted:
                {
                    string roomName = packet.ReadString();

                    // Delete the room channel.
                    foreach (ChatChannel channel in Channels)
                    {
                        if (!channel.IsDirect && 
                            ((ChatRoomChannel)channel).roomName == roomName)
                        {
                            // Get out of the room if we are in it.
                            if (Channel == channel)
                                Channel = null;

                            // Remove from owned rooms
                            OwnedRooms.Remove((ChatRoomChannel)channel);

                            Channels.Remove(channel);

                            // Run channel list changed callback.
                            if (Form is not null && OnChannelListUpdate is not null)
                                Form.Invoke(OnChannelListUpdate);

                            break;
                        }
                    }

                    break;
                }

                // Server tells us that our room creation failed.
                case PacketType.ServerRoomCreateError:
                {
                    PacketErrorCode code = (PacketErrorCode)packet.ReadUInt32();
                    string msg = packet.ReadString();

                    if (Form is not null && OnRoomCreateFail is not null)
                        Form.Invoke(OnRoomCreateFail, msg);

                    break;
                }

                // Server tells us that our room deletion failed.
                case PacketType.ServerRoomDeleteError:
                {
                    PacketErrorCode code = (PacketErrorCode)packet.ReadUInt32();
                    string msg = packet.ReadString();

                    if (Form is not null && OnRoomDeleteFail is not null)
                        Form.Invoke(OnRoomDeleteFail, msg);

                    break;
                }

                // Server tells us that a client is attempting to join
                // encrypted room that we own.
                case PacketType.ServerClientJoinEncryptedRoomRequest:
                {
                    if (m_Writer is null)
                        break;

                    string roomName = packet.ReadString();
                    string nickname = packet.ReadString();
                    string saltString = packet.ReadString();
                    string ivString = packet.ReadString();
                    string cipherMessageString = packet.ReadString();

                    // Get the room from our list.
                    ChatRoomChannel? room = null;
                    foreach (ChatRoomChannel channel in m_OwnedRooms)
                    {
                        if (channel.roomName == roomName)
                        {
                            room = channel;
                            break;
                        }
                    }

                    // Ensure that the room exists
                    if (room is null)
                    {
                        // Ignore the request; we are not the owner of the room.
                        break;
                    }

                    // True if the decryption failed.
                    bool failed = false;

                    // Ensure we have the key.
                    byte[]? roomKey = null; 

                    if (RoomKeychain.ContainsKey(room.roomName))
                        roomKey = RoomKeychain[room.roomName];

                    if (roomKey is null)
                        break;

                    // Message that the encrypted message should decrypt to.
                    string expectedMessage = nickname + roomName + saltString;

                    // Convert from base64 to bytes.
                    byte[] salt = Convert.FromBase64String(saltString);
                    byte[] iv = Convert.FromBase64String(ivString);
                    byte[] cipherMessage = Convert.FromBase64String(cipherMessageString);

                    // Decrypt the string
                    string? decryptedMessage = null;
                    using (Aes aes = Aes.Create())
                    {
                        //aes.KeySize = 128; // bits
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.BlockSize = 128; // bits
                        aes.IV = iv;
                        aes.Key = roomKey;

                        ICryptoTransform decryptor = aes.CreateDecryptor();

                        // Decrypt the message
                        try
                        {
                            using (MemoryStream ms = new MemoryStream(cipherMessage))
                            {
                                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                                {
                                    using (StreamReader sr = new StreamReader(cs))
                                    {
                                        decryptedMessage = sr.ReadToEnd();
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // Failure; presumably the key is wrong.
                            failed = true;
                        }
                    }

                    // Check that the messages match.
                    if (failed || 
                        (decryptedMessage is not null && decryptedMessage != expectedMessage))
                    {
                        // Message does not match; do not authorise the user.
                        using (Packet packet2 = new Packet(PacketType.ClientEncryptedRoomAuthoriseFail))
                        {
                            packet2.Write(roomName);
                            packet2.Write(nickname);
                            packet2.WriteToStream(m_Writer);
                            m_Writer.Flush();
                        }

                        break;
                    }

                    // Message matches; we authorise the user.
                    using (Packet packet2 = new Packet(PacketType.ClientEncryptedRoomAuthorise))
                    {
                        packet2.Write(roomName);
                        packet2.Write(nickname);
                        packet2.WriteToStream(m_Writer);
                        m_Writer.Flush();
                    }

                    break;
                }

                // Server tells us that we are authorised into the encrypted
                // room.
                case PacketType.ServerClientEncryptedRoomAuthorise:
                {
                    lock (m_PasswordAwaitSync)
                        m_PasswordAwait = PasswordAwaitState.Successful;

                    break;
                }

                // Server tells us that we are not authorised into the
                // encrypted room.
                case PacketType.ServerClientEncryptedRoomAuthoriseFail:
                {
                    lock (m_PasswordAwaitSync)
                        m_PasswordAwait = PasswordAwaitState.Failed;

                    break;
                }
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

            try
            {
                m_Stream.AuthenticateAsClient(options);
            }
            catch (System.Security.Authentication.AuthenticationException)
            {
                // Validation failed; certificate is untrusted.
                if (Form is not null && OnCertificateValidationFailed is not null)
                    Form.Invoke(OnCertificateValidationFailed);

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

                    //if (m_Tcp.Available <= 0)
                    //    continue;

                    // Read next packet.
                    using (Packet packet = new Packet(m_Reader))
                    {
                        // We are not "in" the server yet; so we only expect a ServerWelcome packet.
                        if (!m_InServer)
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

                        packet.Lock();
                        HandlePacket(packet);
                        packet.Unlock();
                    }
                }
                catch (IOException)
                {
                    if (m_InServer)
                    {
                        if (Form is not null && OnConnectionLost is not null)
                            Form.Invoke(OnConnectionLost);
                    }
                    m_InServer = false;
                    Cleanup();
                    return;
                }
                catch (ObjectDisposedException)
                {
                    if (m_InServer)
                    {
                        if (Form is not null && OnConnectionLost is not null)
                            Form.Invoke(OnConnectionLost);
                    }
                    m_InServer = false;
                    Cleanup();
                    return;
                }
                catch (Exception e)
                {
                    if (m_InServer)
                    {
                        if (Form is not null && OnConnectionLost is not null)
                            Form.Invoke(OnConnectionLost);
                    }
                    m_InServer = false;
                    Cleanup();

                    ShowError($"Exception: {e.Message}");
                    return;
                }
            }
        }
    }
}
