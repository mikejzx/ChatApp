using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public ChatRoomChannel(string roomName)
        {
            this.recipients = new List<ChatRecipient>();
            this.roomName = roomName;
            this.messages = new List<ChatMessage>();
            this.unreadMessages = 0;
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

        // Add message to the channel's message history
        public ChatMessage AddMessage(ChatMessageType type, string sender, string message="")
        {
            // Add the message
            ChatMessage msg = new ChatMessage(type, sender, message);
            messages.Add(msg);
            return msg;
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
