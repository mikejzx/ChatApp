using Mikejzx.ChatShared;

namespace Mikejzx.ChatClient
{
    public class ChatDirectChannel : ChatChannel
    {
        public ChatRecipient Recipient { get => recipients[0]; }

        // String displayed in listbox
        public override string DisplayString
        {
            get
            {
                if (Recipient.isJoined)
                {
                    // Show unread count next to name
                    if (unreadMessages > 0)
                        return $"({unreadMessages}) {Recipient.nickname}";

                    return Recipient.nickname;
                }
                else
                {
                    // Show unread count next to name
                    if (unreadMessages > 0)
                        return $"({unreadMessages}) {Recipient.nickname} (offline)";

                    return Recipient.nickname + " (offline)";
                }
            }
        }

        public override bool IsDirect { get => true; }

        public override bool IsRoomChannel(string roomName) => false;

        public override bool IsDirectChannel(string recipient) => this.Recipient.nickname == recipient;

        public ChatDirectChannel(ChatRecipient recipient)
        {
            this.recipients = new List<ChatRecipient>() { recipient };
            this.messages = new List<ChatMessage>();
            this.unreadMessages = 0;
        }
    }

    public class ChatRoomChannel : ChatChannel
    {
        // Name of room
        public string roomName;

        // Topic of room
        public string roomTopic;

        // Whether room is encrypted
        public bool isEncrypted;

        // The room's secret key, derived from the room password.
        public byte[]? roomKey;

        // Whether we are a member of this room.
        public bool isJoined;

        // String displayed in listbox
        public override string DisplayString
        {
            get
            {
                if (unreadMessages > 0)
                    return $"({unreadMessages}) {roomName}";

                return $"{roomName}";
            }
        }

        public override bool IsDirect { get => false; }

        public override bool IsRoomChannel(string roomName) => this.roomName == roomName;

        public override bool IsDirectChannel(string roomName) => false;

        public ChatRoomChannel(string roomName, string roomTopic, bool roomEncrypted)
        {
            this.recipients = new List<ChatRecipient>();
            this.roomName = roomName;
            this.roomTopic = roomTopic;
            this.isEncrypted = roomEncrypted;
            this.roomKey = null;
            this.messages = new List<ChatMessage>();
            this.unreadMessages = 0;
            this.isJoined = false;
        }
    }

    public abstract class ChatChannel
    {
        // 'Unread' message count.
        public int unreadMessages;

        // List of recipients in the channel (except ourselves)
        public List<ChatRecipient> recipients = new List<ChatRecipient>();

        // List of messages sent in the channel.
        public List<ChatMessage> messages = new List<ChatMessage>();

        // String to be displayed in list box.
        public abstract string DisplayString { get; }

        // Whether this is a direct message channel
        public abstract bool IsDirect { get; }

        // Returns true if this is a room channel with the given name.
        public abstract bool IsRoomChannel(string roomName);

        // Returns true if this is a direct message channel with the given name.
        public abstract bool IsDirectChannel(string directName);

        // Construct and add message to the channel's message history
        public ChatMessage AddMessage(ChatMessageType type, string author, string message="")
        {
            // Add the message
            ChatMessage msg = new ChatMessage(type, author, message);
            return AddMessage(msg);
        }

        // Add message to the channel's message history
        public ChatMessage AddMessage(ChatMessage message)
        {
            messages.Add(message);
            return message;
        }

        public bool ContainsRecipient(string recipient)
        {
            foreach (ChatRecipient recipient2 in recipients)
            {
                if (recipient2.nickname == recipient)
                    return true;
            }
            return false;
        }

        public bool ContainsRecipient(ChatRecipient recipient)
        {
            return ContainsRecipient(recipient.nickname);
        }
    }
}
