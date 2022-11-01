using System.Security.Cryptography;
using Mikejzx.ChatShared;

namespace Mikejzx.ChatClient
{
    public class ChatClientPacketHandler
    {
        private readonly ChatClient m_Client;

        private delegate void PacketHandler(Packet packet);

        // Packet function table.
        private readonly Dictionary<PacketType, PacketHandler> m_Handlers;

        // Create packet handler for client.
        public ChatClientPacketHandler(ChatClient client)
        {
            this.m_Client = client;

            m_Handlers = new()
            {
                { PacketType.ServerWelcome, ServerWelcome },
                { PacketType.ServerError, ServerError },
                { PacketType.ServerClientList, ServerClientList },
                { PacketType.ServerRoomList, ServerRoomList },
                { PacketType.ServerClientRoomMembers, ServerClientRoomMembers },
                { PacketType.ServerClientRoomMessages, ServerClientRoomMessages },
                { PacketType.ServerClientJoin, ServerClientJoin },
                { PacketType.ServerClientLeave, ServerClientLeave },
                { PacketType.ServerClientRoomJoin, ServerClientRoomJoin },
                { PacketType.ServerClientRoomLeave, ServerClientRoomLeave },
                { PacketType.ServerDirectMessageReceived, ServerDirectMessageReceived },
                { PacketType.ServerRoomMessageReceived, ServerRoomMessageReceived },
                { PacketType.ServerRoomCreated, ServerRoomCreated },
                { PacketType.ServerRoomDeleted, ServerRoomDeleted },
                { PacketType.ServerRoomCreateError, ServerRoomCreateError },
                { PacketType.ServerRoomDeleteError, ServerRoomDeleteError },
                { PacketType.ServerRoomOwnerChange, ServerRoomOwnerChange },
                { PacketType.ServerClientJoinEncryptedRoomRequest, ServerClientJoinEncryptedRoomRequest },
                { PacketType.ServerClientEncryptedRoomAuthorise, ServerClientEncryptedRoomAuthorise },
                { PacketType.ServerClientEncryptedRoomAuthoriseFail, ServerClientEncryptedRoomAuthoriseFail },
            };
        }

        // Function invocation shorthand.
        private object Invoke(Delegate? method, params object[] args)
        {
            if (method is null)
                return false;

            // Invoke on the client form.
            return m_Client.Form.Invoke(method, args);
        }

        // Handle given packet
        public void Handle(Packet packet)
        {
            if (!m_Handlers.ContainsKey(packet.PacketType))
            {
                // Warn about this
                ShowError("Unhandled packet type " + packet.PacketType.ToString());
                return;
            }

            // Lock writing to the packet to avoid nasty bugs.
            packet.Lock();

            // Run the handler.
            m_Handlers[packet.PacketType](packet);

            packet.Unlock();
        }

        private void ShowError(string message)
        {
            Invoke(m_Client.OnError, message);
        }

        // Server welcomes us into the server.
        private void ServerWelcome(Packet packet)
        {
            m_Client.InServer = true;

            Invoke(m_Client.OnConnectionSuccess);
        }

        // Server sends us a general error message.
        private void ServerError(Packet packet)
        {
            PacketErrorCode code = (PacketErrorCode)packet.ReadUInt32();
            string msg = packet.ReadString();

            ShowError(msg);
        }

        // Server sends us current client list.
        private void ServerClientList(Packet packet)
        {
            int count = packet.ReadInt32();

            foreach (ChatRecipient client in m_Client.Clients.Values)
            {
                // Set each client as unjoined, and then whoever is in
                // the server client list is set to joined.
                client.isJoined = false;
            }

            for (int i = 0; i < count; ++i)
            {
                string nickname = packet.ReadString();

                // Skip ourself
                if (nickname == m_Client.ToString())
                    continue;

                // Check if we already have the client.
                if (!m_Client.Clients.ContainsKey(nickname))
                {
                    // Add the client.
                    ChatRecipient addedRecipient = new ChatRecipient(nickname, true);
                    m_Client.Clients.Add(nickname, addedRecipient);

                    // Create the direct channel for the new recipient.
                    ChatDirectChannel channel = new ChatDirectChannel(addedRecipient);
                    m_Client.Channels.Add(channel);
                }
                else
                {
                    // Set them as joined.
                    m_Client.Clients[nickname].isJoined = true;
                }
            }

            // Update channel list.
            Invoke(m_Client.OnChannelListUpdate);
        }

        // Server sends us current room list.
        private void ServerRoomList(Packet packet)
        {
            int count = packet.ReadInt32();

            // Clear old rooms.
            foreach (ChatChannel channel in m_Client.Channels)
            {
                if (!channel.IsDirect)
                    m_Client.Channels.Remove(channel);
            }

            for (int i = 0; i < count; ++i)
            {
                string roomName = packet.ReadString();
                string roomTopic = packet.ReadString();
                bool roomEncrypted = packet.ReadBool();

                // Create channel for the room.
                ChatRoomChannel channel = new ChatRoomChannel(roomName, roomTopic, roomEncrypted);
                m_Client.Channels.Add(channel);
            }

            // Update channel list.
            Invoke(m_Client.OnChannelListUpdate);
        }

        // Find room channel by name.
        private ChatRoomChannel? FindRoom(string roomName)
        {
            foreach (ChatChannel channel in m_Client.Channels)
            {
                if (channel.IsRoomChannel(roomName))
                    return (ChatRoomChannel)channel;
            }

            return null;
        }

        // Find room channel by name.
        private ChatRoomChannel? FindOwnedRoom(string roomName)
        {
            foreach (ChatChannel channel in m_Client.OwnedRooms)
            {
                if (channel.IsRoomChannel(roomName))
                    return (ChatRoomChannel)channel;
            }

            return null;
        }

        // Find direct message channel by user name
        private ChatDirectChannel? FindDirectMessageChannel(string userName)
        {
            foreach (ChatChannel channel in m_Client.Channels)
            {
                if (channel.IsDirectChannel(userName))
                    return (ChatDirectChannel)channel;
            }

            return null;
        }

        // Server sends us the members of a room (e.g. on room join)
        private void ServerClientRoomMembers(Packet packet)
        {
            string roomName = packet.ReadString();
            int memberCount = packet.ReadInt32();

            // Find the channel
            ChatChannel? channel = FindRoom(roomName);
            if (channel is null)
            {
                ShowError($"Got members for unknown room '{roomName}'");
                return;
            }

            channel.recipients.Clear();

            for (int i = 0; i < memberCount; ++i)
            {
                string clientName = packet.ReadString();

                if (clientName == m_Client.ToString())
                {
                    // We are a member of this room.
                    ((ChatRoomChannel)channel).isJoined = true;
                    continue;
                }

                // Ensure we have the client.
                if (!m_Client.Clients.ContainsKey(clientName))
                {
                    ShowError($"Room contains unknown user {clientName}");
                    continue;
                }

                // Add the client to the channel
                channel.recipients.Add(m_Client.Clients[clientName]);
            }
        }

        // Attempt to decrypt a message.  Shows error to user on fail, and sets
        // message to indicate that it was not decrypted correctly.
        private string TryDecryptRoomMessage(ChatRoomChannel room, string iv, string message)
        {
            string finalMessage;

            if (string.IsNullOrEmpty(iv))
            {
                finalMessage = "!! failed to decrypt (missing IV) !!";
            }
            else
            {
                // Can't decrypt if keychain is missing the key.
                if (!m_Client.RoomKeychain.ContainsKey(room.roomName))
                {
                    ShowError($"No key for room '{room.roomName}'");
                    finalMessage = "!! failed to decrypt (missing key) !!";
                }
                else
                {
                    byte[]? roomKey = m_Client.RoomKeychain[room.roomName];
                    if (roomKey is null)
                    {
                        ShowError($"No key for room '{room.roomName}'");
                        finalMessage = "!! failed to decrypt (missing key) !!";
                    }
                    else
                    {
                        // Decrypt the cipher text and update the message.
                        CryptoCipher cipher = new CryptoCipher(iv, message);
                        finalMessage = ChatClientCrypto.DecryptMessage(cipher, roomKey, out _);
                    }
                }
            }

            return finalMessage;
        }

        // Read a room message from packet
        private ChatMessage ReadRoomMessage(Packet packet, ChatRoomChannel room)
        {
            int messageType = packet.ReadInt32();
            string messageAuthor = packet.ReadString();
            string messageString = packet.ReadString();

            string? finalMessage;

            if (string.IsNullOrEmpty(messageString))
            {
                finalMessage = null;
            }
            else
            {
                if (!room.isEncrypted ||
                    (ChatMessageType)messageType != ChatMessageType.UserMessage)
                {
                    finalMessage = messageString;
                }
                else
                {
                    // Decrypt the message
                    string ivString = packet.ReadString();
                    finalMessage = TryDecryptRoomMessage(room, messageString, ivString);
                }
            }

            // Create the message
            return new ChatMessage((ChatMessageType)messageType, messageAuthor, finalMessage);
        }

        // Server sends us the messages of a room (e.g. on room join)
        private void ServerClientRoomMessages(Packet packet)
        {
            string roomName = packet.ReadString();
            int messageCount = packet.ReadInt32();

            // Find the channel
            ChatRoomChannel? channel = FindRoom(roomName);
            if (channel is null)
            {
                ShowError($"Got messages for unknown room '{roomName}'");
                return;
            }

            channel.messages.Clear();

            for (int i = 0; i < messageCount; ++i)
            {
                ChatMessage message = ReadRoomMessage(packet, channel);

                // Add this message to the local room history.
                channel.AddMessage(message);
            }

            // Display the new room message list.
            Invoke(m_Client.OnRoomMessageListReceived, channel);
        }

        // Server tells us that a client joined.
        private void ServerClientJoin(Packet packet)
        {
            // Read their nickname
            string nickname = packet.ReadString();

            // Skip ourself just to be safe.
            if (nickname == m_Client.ToString())
                return;

            // Add the client (or set it as joined if they are unjoined).
            if (!m_Client.Clients.ContainsKey(nickname))
            {
                ChatRecipient joinedRecipient = new ChatRecipient(nickname, true);
                m_Client.Clients.Add(nickname, joinedRecipient);

                // Create a direct channel for the user
                m_Client.Channels.Add(new ChatDirectChannel(joinedRecipient));
            }
            else
            {
                m_Client.Clients[nickname].isJoined = true;
            }

            // Append join message to user's direct message channel
            ChatDirectChannel? channel = FindDirectMessageChannel(nickname);
            if (channel is not null)
            {
                ChatMessage msg = channel.AddMessage(ChatMessageType.UserJoin, nickname);
                Invoke(m_Client.OnMessageReceived, channel, msg);
            }

            // Run channel list change callback.
            Invoke(m_Client.OnChannelListUpdate);

            // Run client join callback
            Invoke(m_Client.OnClientJoin, m_Client.Clients[nickname]);

            return;
        }

        // Server tells us that a client left
        private void ServerClientLeave(Packet packet)
        {
            // Read their nickname
            string nickname = packet.ReadString();

            // Skip ourself just to be safe.
            if (nickname == m_Client.ToString())
                return;

            // Unknown client left
            if (!m_Client.Clients.ContainsKey(nickname))
                return;

            // Set client as unjoined.
            m_Client.Clients[nickname].isJoined = false;

            // Append unjoin message to user's direct message channel
            ChatDirectChannel? channel = FindDirectMessageChannel(nickname);
            if (channel is not null)
            {
                ChatMessage msg = channel.AddMessage(ChatMessageType.UserLeave, nickname);
                Invoke(m_Client.OnMessageReceived, channel, msg);
            }

            // Run client exit callback
            Invoke(m_Client.OnClientLeave, m_Client.Clients[nickname]);
        }

        // Server tells us that a client joined the room.
        private void ServerClientRoomJoin(Packet packet)
        {
            // Read their nickname
            string roomName = packet.ReadString();
            string nickname = packet.ReadString();

            // Get room
            ChatRoomChannel? room = FindRoom(roomName);
            if (room is null)
            {
                ShowError($"Client '{nickname}' joined unknown room '{roomName}'.");
                return;
            }

            // Skip if the room already contains recipient
            if (room.ContainsRecipient(nickname))
                return;

            if (nickname == m_Client.ToString())
            {
                // If this packet gets sent to us with our own nickname
                // it indicates us joining a room that we just created.
                room.isJoined = true;

                m_Client.OwnedRooms.Add(room);

                Invoke(m_Client.OnRoomCreateSuccess);

                return;
            }

            // Unknown client joined
            if (!m_Client.Clients.ContainsKey(nickname))
            {
                ShowError($"Unknown client '{nickname}' joined room '{roomName}'.");
                return;
            }

            // Add client to the room recipients
            room.recipients.Add(m_Client.Clients[nickname]);
        }

        // Server tells us that a client left the room.
        private void ServerClientRoomLeave(Packet packet)
        {
            // Read their nickname
            string roomName = packet.ReadString();
            string nickname = packet.ReadString();

            // Skip ourself just to be safe.
            if (nickname == m_Client.ToString())
                return;

            // Get room
            ChatRoomChannel? room = FindRoom(roomName);
            if (room is null)
                return;

            // Unknown client left
            if (!m_Client.Clients.ContainsKey(nickname) ||
                !room.ContainsRecipient(nickname))
            {
                return;
            }

            // Remove client from the room recipients
            room.recipients.Remove(m_Client.Clients[nickname]);
        }

        // Server tells us that we received a direct message
        private void ServerDirectMessageReceived(Packet packet)
        {
            // Read who the message was sent from.
            string sender = packet.ReadString();

            // Read recipient that message was sent to.
            string recipient = packet.ReadString();

            // Read the message
            string messageString = packet.ReadString();

            // Determine under whose name the message should be stored.
            string channelName;
            if (sender == m_Client.ToString())
                channelName = recipient;
            else
                channelName = sender;

            // Get the direct message channel.
            ChatChannel? channel = FindDirectMessageChannel(channelName);
            if (channel is null)
            {
                ShowError($"Received message on unknown DM {channelName}.");
                return;
            }

            // Construct and append to the channel's message list.
            ChatMessage message = new ChatMessage(ChatMessageType.UserMessage, sender, messageString);
            channel.AddMessage(message);

            if (!(bool)Invoke(m_Client.OnMessageReceived, channel, message))
            {
                if (message.type == ChatMessageType.UserMessage)
                    ++channel.unreadMessages;

                // Update the client list to show the unread messages.
                Invoke(m_Client.OnChannelListUpdate);
            }
        }

        // Server informing us of a message sent to a room.
        private void ServerRoomMessageReceived(Packet packet)
        {
            // Read room name
            string roomName = packet.ReadString();
            ChatRoomChannel? channel = FindRoom(roomName);
            if (channel is null)
            {
                ShowError($"Received message on unknown room {roomName}.");
                return;
            }

            // Read the message data
            ChatMessage message = ReadRoomMessage(packet, channel);

            // Add this message to the local room history.
            channel.AddMessage(message);

            if (!(bool)Invoke(m_Client.OnMessageReceived, channel, message))
            {
                if (message.type == ChatMessageType.UserMessage)
                    ++channel.unreadMessages;

                // Update the channel list to show the unread messages.
                Invoke(m_Client.OnChannelListUpdate);
            }
        }

        // Server tells us that a new room was created
        private void ServerRoomCreated(Packet packet)
        {
            string roomName = packet.ReadString();
            string roomTopic = packet.ReadString();
            bool roomEncrypted = packet.ReadBool();

            // Add the room channel.
            ChatRoomChannel channel = new ChatRoomChannel(roomName, roomTopic, roomEncrypted);
            m_Client.Channels.Add(channel);

            // Run channel list changed callback.
            Invoke(m_Client.OnChannelListUpdate);
        }

        // Server tells us that a room was deleted
        private void ServerRoomDeleted(Packet packet)
        {
            string roomName = packet.ReadString();

            // Delete the room channel.
            ChatRoomChannel? channel = FindRoom(roomName);

            // Unknown room deleted
            if (channel is null)
                return;

            // Get out of the room if we are in it.
            if (m_Client.Channel == channel)
                m_Client.Channel = null;

            // Remove from owned rooms
            m_Client.OwnedRooms.Remove(channel);

            m_Client.Channels.Remove(channel);

            // Run channel list changed callback.
            Invoke(m_Client.OnChannelListUpdate);
        }

        // Server tells us that our room creation failed.
        private void ServerRoomCreateError(Packet packet)
        {
            PacketErrorCode code = (PacketErrorCode)packet.ReadUInt32();
            string msg = packet.ReadString();

            Invoke(m_Client.OnRoomCreateFail, msg);
        }

        // Server tells us that our room deletion failed.
        private void ServerRoomDeleteError(Packet packet)
        {
            PacketErrorCode code = (PacketErrorCode)packet.ReadUInt32();
            string msg = packet.ReadString();

            Invoke(m_Client.OnRoomDeleteFail, msg);
        }

        // Server tells us that the room owner changed
        private void ServerRoomOwnerChange(Packet packet)
        {
            string roomName = packet.ReadString();
            string ownerName = packet.ReadString();

            ChatRoomChannel? room = FindRoom(roomName);
            if (room is null)
            {
                // Unknown room.
                return;
            }

            // If we are the owner, we add the room to our owned room list.
            if (ownerName == m_Client.ToString())
            {
                m_Client.OwnedRooms.Add(room);
            }
        }

        // Server tells us that a client is attempting to join
        // encrypted room that we own.
        private void ServerClientJoinEncryptedRoomRequest(Packet packet)
        {
            string roomName = packet.ReadString();
            string nickname = packet.ReadString();
            string saltString = packet.ReadString();
            string ivString = packet.ReadString();
            string cipherMessageString = packet.ReadString();

            // Get the room from our list.
            ChatRoomChannel? room = FindOwnedRoom(roomName);
            if (room is null)
            {
                // Ignore the request; we are not the owner of the room.
                using (Packet packet2 = new Packet(PacketType.ClientEncryptedRoomAuthoriseFail))
                {
                    packet2.Write(roomName);
                    packet2.Write(nickname);
                    packet2.Write((int)PacketErrorCode.UnknownError);
                    packet2.WriteToStream(m_Client.Writer);
                    m_Client.Writer.Flush();
                }
                return;
            }

            // Ensure we have the key.
            byte[]? roomKey = null;
            if (m_Client.RoomKeychain.ContainsKey(room.roomName))
                roomKey = m_Client.RoomKeychain[room.roomName];

            if (roomKey is null)
                return; // No key

            // Message that the encrypted message should decrypt to.
            string expectedMessage = nickname + roomName + saltString;

            // Decrypt the message
            CryptoCipher cipher = new CryptoCipher(ivString, cipherMessageString);
            string plainText = ChatClientCrypto.DecryptMessage(cipher, roomKey, out bool failed);

            // Check that the messages match.
            if (failed || plainText != expectedMessage)
            {
                // Message does not match; do not authorise the user.
                using (Packet packet2 = new Packet(PacketType.ClientEncryptedRoomAuthoriseFail))
                {
                    packet2.Write(roomName);
                    packet2.Write(nickname);
                    packet2.Write((int)PacketErrorCode.PasswordMismatch);
                    packet2.WriteToStream(m_Client.Writer);
                    m_Client.Writer.Flush();
                }

                return;
            }

            // Message matches; we authorise the user.
            using (Packet packet2 = new Packet(PacketType.ClientEncryptedRoomAuthorise))
            {
                packet2.Write(roomName);
                packet2.Write(nickname);
                packet2.WriteToStream(m_Client.Writer);
                m_Client.Writer.Flush();
            }
        }

        // Server tells us that we are authorised into the encrypted
        // room.
        private void ServerClientEncryptedRoomAuthorise(Packet packet)
        {
            // Indicate that our password was correct.
            lock (m_Client.passwordAwaitSync)
                m_Client.passwordAwait = ChatClientPasswordAwaitState.Successful;
        }

        // Server tells us that we are not authorised into the
        // encrypted room.
        private void ServerClientEncryptedRoomAuthoriseFail(Packet packet)
        {
            // Don't need the room name.
            packet.ReadString();

            // Read the error code.
            PacketErrorCode code = (PacketErrorCode)packet.ReadInt32();

            // Indicate that our password was rejected.
            lock (m_Client.passwordAwaitSync)
            {
                if (code == PacketErrorCode.PasswordMismatch)
                    m_Client.passwordAwait = ChatClientPasswordAwaitState.Incorrect;
                else
                    m_Client.passwordAwait = ChatClientPasswordAwaitState.UnknownError;
            }
        }
    }
}