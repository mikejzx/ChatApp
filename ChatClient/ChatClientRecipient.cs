using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mikejzx.ChatClient
{
    public class ChatMessage
    {
        public string sender;
        public string message;

        public ChatMessage(string sender, string message)
        {
            this.sender = sender;
            this.message = message;
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

        // List of messages sent to the client.
        private List<ChatMessage> m_Messages = new List<ChatMessage>();
        public List<ChatMessage> Messages { get => m_Messages; }

        public ChatClientRecipient(string nickname, bool joined)
        {
            m_Nickname = nickname;
            m_Messages.Clear();
            isJoined = joined;
        }

        // Add message to the message history
        public ChatMessage AddMessage(string sender, string message)
        {
            // Add the message
            ChatMessage msg = new ChatMessage(sender, message);
            m_Messages.Add(msg);
            return msg;
        }
    }
}
