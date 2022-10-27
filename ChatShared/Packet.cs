using System.Text;

namespace Mikejzx.ChatShared
{
    public enum PacketType : uint
    {
        // Client is first connecting to the server.
        ClientHello,

        // Client leaves the server.
        ClientDisconnect,

        // Client sends message to a user.
        ClientDirectMessage,

        // Client sends message to a room.
        ClientRoomMessage,

        // Client joins a room
        ClientRoomJoin,

        // Client leaves a room
        ClientRoomLeave,

        // Client creates a room.
        ClientRoomCreate,

        // Client deletes their room.
        ClientRoomDelete,

        // Client informing server that the given user's private key is correct
        // and that they can join the room.
        ClientEncryptedRoomAuthorise,

        // Encrypted room owner informing server that given user's private key
        // is incorrect and that they cannot join the room.
        ClientEncryptedRoomAuthoriseFail,

        // Server sending us an error
        ServerError,

        // Server allows a client into the server.
        ServerWelcome,

        // Server sending a client the most recent client list.
        ServerClientList,

        // Server sending a client the list of rooms
        ServerRoomList,

        // Server informing that a client joined the server
        ServerClientJoin,

        // Server informing that a client left the server
        ServerClientLeave,

        // Server informing that a direct message was received.
        ServerDirectMessageReceived,

        // Server informing that a room message was received.
        ServerRoomMessageReceived,

        // Server informing that a client joined a room.
        ServerClientRoomJoin,

        // Server informing that a client left a room.
        ServerClientRoomLeave,

        // Server informing client of the members of a room
        ServerClientRoomMembers,

        // Server informing client of the message history of a room
        ServerClientRoomMessages,

        // Server informing encrypted room owner that a client is attempting to
        // join the room.
        ServerClientJoinEncryptedRoomRequest,

        // Server informing client that their private key is correct and that
        // they may join the encrypted room.
        ServerClientEncryptedRoomAuthorise,

        // Server informing client that their private key is incorrect and that
        // they may not join the encrypted room.
        ServerClientEncryptedRoomAuthoriseFail,

        // Server informing that a room was created
        ServerRoomCreated,

        // Server informing that a room was deleted
        ServerRoomDeleted,

        // Server informing client that room creation failed
        ServerRoomCreateError,

        // Server informing client that room deletion failed
        ServerRoomDeleteError,

        // Server informing client that the room's owner changed.
        ServerRoomOwnerChange,
    }

    public enum PacketErrorCode : uint
    {
        OK,

        // Unknown error occurred
        UnknownError,

        // The given nickname is invalid.
        InvalidNickname,

        // The room name is already taken.
        RoomNameTaken,

        // The password is incorrect
        PasswordMismatch,
    }

    public class Packet : IDisposable
    {
        private int m_Position;
        private List<byte>? m_Buffer;
        private byte[]? m_ReadableBuffer;

        private bool m_Disposed = false;

        // 'locked' packet is one which does not allow writing.
        private bool m_IsLocked = false;

        public void Lock() => m_IsLocked = true;
        public void Unlock() => m_IsLocked = false;

        public int Length 
        {
            get 
            {
                if (m_Buffer is null)
                    throw new NullReferenceException();

                return m_Buffer.Count; 
            }
        }
        public int UnreadLength { get => Length - m_Position; }

        // Get the type of packet this is.
        public PacketType PacketType 
        {
            get 
            {
                if (m_ReadableBuffer is null)
                    throw new NullReferenceException();

                return (PacketType)BitConverter.ToUInt32(m_ReadableBuffer, 0);
            }
        }

        // Initialise empty packet.
        public Packet()
        {
            m_Buffer = new List<byte>();
            m_Position = 0;
        }

        // Initialise a new packet.
        public Packet(PacketType packetType)
        {
            m_Buffer = new List<byte>();
            m_Position = 0;

            Write((uint)packetType);
        }

        // Initialise a packet from binary data.
        public Packet(byte[] data)
        {
            m_Buffer = new List<byte>();
            m_Position = 0;

            SetBytes(data);
        }

        // Initialise packet from the contents of a binary reader
        public Packet(BinaryReader br)
        {
            ReadFromStream(br);
        }

        // Set the bytes of a packet.
        public void SetBytes(byte[] data)
        {
            Write(data);
            m_ReadableBuffer = m_Buffer?.ToArray();

            // Start 4 bytes into the buffer, to skip over packet type.
            m_Position = sizeof(int);
        }

        // Write methods.
        public void Write(byte data)
        {
            if (m_IsLocked)
                throw new Exception("Packet is locked for writing.");

            m_Buffer?.Add(data);
        }

        public void Write(byte[] data)
        {
            if (m_IsLocked)
                throw new Exception("Packet is locked for writing.");

            m_Buffer?.AddRange(data);
        }

        public void Write(bool data)
        {
            if (m_IsLocked)
                throw new Exception("Packet is locked for writing.");

            m_Buffer?.AddRange(BitConverter.GetBytes(data));
        }

        public void Write(uint data)
        {
            if (m_IsLocked)
                throw new Exception("Packet is locked for writing.");

            m_Buffer?.AddRange(BitConverter.GetBytes(data));
        }

        public void Write(int data) 
        { 
            if (m_IsLocked)
                throw new Exception("Packet is locked for writing.");

            m_Buffer?.AddRange(BitConverter.GetBytes(data)); 
        }

        public void Write(float data) 
        { 
            if (m_IsLocked)
                throw new Exception("Packet is locked for writing.");

            m_Buffer?.AddRange(BitConverter.GetBytes(data)); 
        }

        public void Write(string data)
        {
            if (m_IsLocked)
                throw new Exception("Packet is locked for writing.");

            Write(data.Length);
            m_Buffer?.AddRange(Encoding.UTF8.GetBytes(data));
        }

        // Read byte from packet
        public byte ReadByte(bool updatePosition=true)
        {
            if (m_Buffer is null || m_ReadableBuffer is null)
                throw new NullReferenceException();

            // Check for end of stream
            if (m_Buffer.Count <= m_Position)
                throw new EndOfStreamException();

            // Get next byte
            byte value = m_ReadableBuffer[m_Position];

            // Update read position
            if (updatePosition)
            {
                m_Position += sizeof(byte);
            }

            return value;
        }

        // Read bytes from packet
        public byte[] ReadBytes(int count, bool updatePosition=true)
        {
            if (m_Buffer is null)
                throw new NullReferenceException();

            // Check for end of stream
            if (m_Buffer.Count <= m_Position)
                throw new EndOfStreamException();

            // Get the bytes
            byte[] value = m_Buffer.GetRange(m_Position, count).ToArray();

            // Update read position
            if (updatePosition)
            {
                m_Position += count * sizeof(byte);
            }

            return value;
        }

        // Read boolean from packet
        public bool ReadBool(bool updatePosition=true)
        {
            if (m_Buffer is null || m_ReadableBuffer is null)
                throw new NullReferenceException();

            // Check for end of stream
            if (m_Buffer.Count <= m_Position)
                throw new EndOfStreamException();

            // Bools are stored as single byte.
            bool value = BitConverter.ToBoolean(m_ReadableBuffer, m_Position);

            // Update read position
            if (updatePosition)
            {
                m_Position += sizeof(bool);
            }

            return value;
        }

        // Read signed 32-bit integer from packet
        public int ReadInt32(bool updatePosition=true)
        {
            if (m_ReadableBuffer is null)
                throw new NullReferenceException();

            // Check for end of stream
            if (m_Buffer?.Count <= m_Position)
                throw new EndOfStreamException();

            // Get the bytes
            int value = BitConverter.ToInt32(m_ReadableBuffer, m_Position);

            // Update read position
            if (updatePosition)
            {
                m_Position += sizeof(int);
            }

            return value;
        }

        // Read unsigned 32-bit integer from packet
        public uint ReadUInt32(bool updatePosition=true)
        {
            if (m_ReadableBuffer is null)
                throw new NullReferenceException();

            // Check for end of stream
            if (m_Buffer?.Count <= m_Position)
                throw new EndOfStreamException();

            // Get the bytes
            uint value = BitConverter.ToUInt32(m_ReadableBuffer, m_Position);

            // Update read position
            if (updatePosition)
            {
                m_Position += sizeof(uint);
            }

            return value;
        }

        // Read string from packet
        public string ReadString(bool updatePosition=true)
        {
            if (m_ReadableBuffer is null)
                throw new NullReferenceException();

            try
            {
                int length = ReadInt32();

                string value = Encoding.UTF8.GetString(m_ReadableBuffer, m_Position, length);

                if (updatePosition && value.Length > 0)
                {
                    m_Position += length;
                }

                return value;
            }
            catch 
            {
                throw new Exception("Failed to read string");
            }
        }

        // Write packet to a binary stream.
        public void WriteToStream(BinaryWriter bw)
        {
            if (m_Buffer is null)
                return;

            // Convert buffer to an array of bytes.
            m_ReadableBuffer = m_Buffer.ToArray();

            // All packets must begin with message length.
            bw.Write(Length);

            // Write packet bytes.
            bw.Write(m_ReadableBuffer);
        }

        // Read packet from a binary stream
        public void ReadFromStream(BinaryReader br)
        {
            m_Buffer = new List<byte>();
            m_Position = 0;

            // Read message length
            int length = br.ReadInt32();

            // Read the bytes.
            SetBytes(br.ReadBytes(length));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
                return;

            if (disposing)
            {
                m_Buffer = null;
                m_ReadableBuffer = null;
                m_Position = 0;
            }

            m_Disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
