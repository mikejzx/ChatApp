using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mikejzx.ChatClient
{
    public enum ChatMessageType
    { 
        UserMessage,
        UserJoin,
        UserLeave,
    }

    public class ChatMessage
    {
        public ChatMessageType type;
        public string sender;
        public string message;

        public ChatMessage(ChatMessageType type, string sender, string message)
        {
            this.type = type;
            this.sender = sender;
            this.message = message;
        }

        // Convert the message to a string.
        public override string ToString()
        {
            switch (type)
            {
                case ChatMessageType.UserMessage:
                    return $"<{sender}>: {message}";
                case ChatMessageType.UserLeave:
                    return $"{sender} left the server.";
                case ChatMessageType.UserJoin:
                    return $"{sender} joined the server.";
            }
            return string.Empty;
        }
    }

    // A client we can chat with.
    public class ChatClientRecipient
    {
        // Nickname of the client.
        private string m_Nickname = string.Empty;
        public string Nickname { get => m_Nickname; }

        // Whether the client is in the server currently.
        public bool isJoined;

        // String displayed in listbox
        public string DisplayString
        {
            get
            {
                if (isJoined)
                {
                    // Show unread count next to name
                    if (UnreadMessages > 0)
                        return $"({UnreadMessages}) {Nickname}";

                    return Nickname;
                }
                else
                {
                    // Show unread count next to name
                    if (UnreadMessages > 0)
                        return $"({UnreadMessages}) {Nickname} (offline)";

                    return Nickname + " (offline)";
                }
            }
        }

        // Number of messages that have been sent that we have not seen yet.
        private int m_UnreadMessages = 0;
        public int UnreadMessages { get => m_UnreadMessages; set => m_UnreadMessages = value; }

        // List of messages sent to the client.
        private List<ChatMessage> m_Messages = new List<ChatMessage>();
        public List<ChatMessage> Messages { get => m_Messages; }

        public ChatClientRecipient(string nickname, bool joined)
        {
            m_Nickname = nickname;
            m_Messages.Clear();
            isJoined = joined;
            m_UnreadMessages = 0;
        }

        // Add message to the message history
        public ChatMessage AddMessage(ChatMessageType type, string sender, string message="")
        {
            // Add the message
            ChatMessage msg = new ChatMessage(type, sender, message);
            m_Messages.Add(msg);
            return msg;
        }
    }
}
