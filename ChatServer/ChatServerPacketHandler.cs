using Mikejzx.ChatShared;

namespace Mikejzx.ChatServer
{
    public class ChatServerPacketHandler
    {
        private readonly ChatServerClient m_Client;
        private readonly ChatServer m_Server;

        private delegate void PacketHandler(Packet packet);

        // Packet function table.
        private readonly Dictionary<PacketType, PacketHandler> m_Handlers;

        // Create packet handler for client.
        public ChatServerPacketHandler(ChatServerClient client, ChatServer server)
        {
            this.m_Client = client;
            this.m_Server = server;

            m_Handlers = new Dictionary<PacketType, PacketHandler>()
            {
                { PacketType.ClientDisconnect, ClientDisconnect },
                { PacketType.ClientDirectMessage, ClientDirectMessage },
                { PacketType.ClientRoomMessage, ClientRoomMessage },
                { PacketType.ClientRoomCreate, ClientRoomCreate },
                { PacketType.ClientRoomDelete, ClientRoomDelete },
                { PacketType.ClientRoomJoin, ClientRoomJoin },
                { PacketType.ClientEncryptedRoomAuthorise, ClientEncryptedRoomAuthorise },
                { PacketType.ClientEncryptedRoomAuthoriseFail, ClientEncryptedRoomAuthoriseFail },
            };
        }

        // Handle given packet
        public void Handle(Packet packet)
        {
            if (!m_Handlers.ContainsKey(packet.PacketType))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unhandled packet " + packet.PacketType.ToString());
                Console.ResetColor();
                return;
            }

            // Lock writing to the packet to avoid nasty bugs.
            packet.Lock();

            // Run the handler.
            m_Handlers[packet.PacketType](packet);

            packet.Unlock();
        }

        // Client is disconnecting
        private void ClientDisconnect(Packet packet)
        {
            // Indicate that worker thread should stop.
            lock (m_Client.disconnectSync)
                m_Client.disconnected = true;
        }

        // Client is sending a direct message to a user
        private void ClientDirectMessage(Packet packet)
        {
            string recipientName = packet.ReadString();
            string msg = packet.ReadString();

            Console.WriteLine($"'{m_Client}' --> '{recipientName}': '{msg}'");

            // Send message to both the clients.
            using (Packet packet2 = new Packet(PacketType.ServerDirectMessageReceived))
            {
                packet2.Write(m_Client.ToString());
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
                lock (m_Client.sendSync)
                {
                    packet2.WriteToStream(m_Client.Writer);
                    m_Client.Writer.Flush();
                }
            }
        }

        // Client is sending a message to a room.
        private void ClientRoomMessage(Packet packet)
        {
            string roomName = packet.ReadString();
            string msg = packet.ReadString();

            Console.WriteLine($"'{m_Client}' --> '{roomName}' (room): '{msg}'");

            // Get the room
            ChatRoom? room = m_Server.GetRoom(roomName);
            if (room is null)
                return;

            // Ensure that the client is actually a member of the room
            if (!m_Client.Rooms.Contains(room))
            {
                Console.WriteLine($"Error: User '{m_Client}' attempt to " +
                                  $"write to room '{roomName}' which they are not a member of.");
                return;
            }

            // If the room is encrypted; we include the IV.
            string? iv = null;
            if (room.isEncrypted)
                iv = packet.ReadString();

            // Write the message to the message history.
            room.AddMessage(new ChatMessage(ChatMessageType.UserMessage, m_Client.Nickname, msg, iv));
        }

        // Client is creating a room
        private void ClientRoomCreate(Packet packet)
        {
            string roomName = packet.ReadString();
            string roomTopic = packet.ReadString();
            bool roomEncrypted = packet.ReadBool();

            // Skip if room name is malformed
            if (string.IsNullOrEmpty(roomName))
                return;

            // Add the room
            ChatRoom? room = m_Server.CreateRoom(m_Client, roomName, roomTopic, roomEncrypted);

            if (room is null)
            {
                // Inform client that we failed to create the room.
                using (Packet error = new Packet(PacketType.ServerRoomCreateError))
                {
                    error.Write((uint)PacketErrorCode.RoomNameTaken);
                    error.Write("Room name is already taken.  Please enter a different name.");

                    lock (m_Client.sendSync)
                    {
                        error.WriteToStream(m_Client.Writer);
                        m_Client.Writer.Flush();
                    }
                }

                return;
            }

            if (roomEncrypted)
                Console.WriteLine($"'{m_Client}' created encrypted room '{roomName}' with topic '{roomTopic}'");
            else
                Console.WriteLine($"'{m_Client}' created room '{roomName}' with topic '{roomTopic}'");
        }

        private ChatRoom? GetRoomByName(string roomName)
        {
            ChatRoom? result = null;

            m_Server.EnumerateRoomsUntil((ChatRoom room) =>
            {
                if (room.name == roomName)
                {
                    result = room;
                    return true;
                }
                return false;
            });

            return result;
        }

        // Client is deleting a room
        private void ClientRoomDelete(Packet packet)
        {
            string roomName = packet.ReadString();

            // Skip if room name is malformed
            if (string.IsNullOrEmpty(roomName))
                return;

            // Get room
            ChatRoom? room = GetRoomByName(roomName);
            if (room is null)
            {
                Console.WriteLine($"'{m_Client}' attempted to delete unknown room '{roomName}'");
                return;
            }

            // Deleter must be owner.
            if (room.owner != m_Client)
            {
                Console.WriteLine($"'{m_Client}' attempted to delete room " +
                                  $"'{roomName}' that they do not own.");
                return;
            }

            // Remove the room.
            m_Server.DeleteRoom(room);
        }

        // Client wants to join room.
        private void ClientRoomJoin(Packet packet)
        {
            string roomName = packet.ReadString();

            Console.WriteLine($"'{m_Client}' joining room {roomName} ...");

            // Get room
            ChatRoom? room = GetRoomByName(roomName);
            if (room is null)
            {
                Console.WriteLine($"'{m_Client}' attempted to join unknown room {roomName}");
                return;
            }

            if (!room.isEncrypted)
            {
                // Simply add them to the room's participants list.
                m_Server.AddClientToRoom(m_Client, room);
            }
            else
            {
                // Encrypted room must be owned.
                if (room.owner is null)
                {
                    Console.WriteLine($"'{m_Client}' attempted to join encrypted room with no owner.");
                    return;
                }

                // Forward the attached encrypted message to the owner
                // of the room; who will respond to us if the request
                // message was decrypted correctly to the expected string.
                string saltString = packet.ReadString();
                string ivString = packet.ReadString();
                string messageString = packet.ReadString();

                Console.WriteLine($"'{m_Client}' attempting to join encrypted room '{roomName}'");
                Console.WriteLine($"'{m_Client}' salt:{saltString} iv:{ivString} message:{messageString}");

                // Send authorisation request to the room's owner.
                using (Packet packet2 = new Packet(PacketType.ServerClientJoinEncryptedRoomRequest))
                {
                    packet2.Write(roomName);
                    packet2.Write(m_Client.ToString());
                    packet2.Write(saltString);
                    packet2.Write(ivString);
                    packet2.Write(messageString);

                    lock (room.owner.sendSync)
                    {
                        packet2.WriteToStream(room.owner.Writer);
                        room.owner.Writer.Flush();
                    }
                }
            }
        }

        private void DontAuthoriseClient(ChatServerClient client, ChatRoom room, PacketErrorCode errorCode)
        {
            // Inform the client that they are not authorised.
            using (Packet packet = new Packet(PacketType.ServerClientEncryptedRoomAuthoriseFail))
            {
                packet.Write(room.name);
                packet.Write((int)errorCode);

                lock (client.sendSync)
                {
                    packet.WriteToStream(client.Writer);
                    client.Writer.Flush();
                }
            }
        }

        // Common checks for authorisation and non-authorisation.
        private bool EncryptedRoomAuthoriseChecks(string roomName, string nickname,
                                                  out ChatServerClient? client, out ChatRoom? room,
                                                  bool noAuth)
        {
            string authStr = noAuth ? "NOT authorise" : "authorise";

            Console.WriteLine($"'{m_Client}' attempting to {authStr} '{nickname}' to '{roomName}' ...");

            client = null;
            room = null;

            // Client must exist.
            client = m_Server.GetClient(nickname);
            if (client is null)
            {
                Console.WriteLine($"'{m_Client}' attempted to {authStr} " +
                                  $"unknown user '{nickname}' to room '{roomName}'");
                return false;
            }

            // Get room
            room = GetRoomByName(roomName);
            if (room is null)
            {
                Console.WriteLine($"'{m_Client}' attempted to {authStr} " +
                                  $"'{nickname}' to unknown room '{roomName}'");
                return false;
            }

            // User must own the room to authorise
            if (room.owner != m_Client)
            {
                Console.WriteLine($"'{m_Client}' attempted to {authStr} " +
                                  $"'{nickname}' to room '{roomName}' which they do not own.");
                DontAuthoriseClient(client, room, PacketErrorCode.UnknownError);
                return false;
            }

            // Room must be encrypted
            if (!room.isEncrypted)
            {
                Console.WriteLine($"'{m_Client}' attempted to {authStr} " +
                                  $"'{nickname}' to non-encrypted room '{roomName}'");
                DontAuthoriseClient(client, room, PacketErrorCode.UnknownError);
                return false;
            }

            Console.WriteLine($"'{m_Client}' did {authStr} '{nickname}' to '{roomName}' ...");

            return true;
        }

        // Encrypted room owner authorises the client to join.
        private void ClientEncryptedRoomAuthorise(Packet packet)
        {
            string roomName = packet.ReadString();
            string nickname = packet.ReadString();

            // Perform checks
            ChatServerClient? client;
            ChatRoom? room;
            if (!EncryptedRoomAuthoriseChecks(roomName, nickname, out client, out room, false) ||
                room is null || client is null)
                return;

            // Inform the client that they are authorised.
            using (Packet packet2 = new Packet(PacketType.ServerClientEncryptedRoomAuthorise))
            {
                packet2.Write(room.name);

                lock (client.sendSync)
                {
                    packet2.WriteToStream(client.Writer);
                    client.Writer.Flush();
                }
            }

            // Add the client to the encrypted room.
            m_Server.AddClientToRoom(client, room);

            return;
        }

        // Encrypted room owner does not authorise the client to join.
        private void ClientEncryptedRoomAuthoriseFail(Packet packet)
        {
            string roomName = packet.ReadString();
            string nickname = packet.ReadString();
            int errorCode = packet.ReadInt32();

            // Perform checks
            ChatServerClient? client;
            ChatRoom? room;
            if (!EncryptedRoomAuthoriseChecks(roomName, nickname, out client, out room, false) ||
                room is null || client is null)
            {
                return;
            }

            // Inform the client that they are not authorised.
            DontAuthoriseClient(client, room, (PacketErrorCode)errorCode);
        }
    }
}