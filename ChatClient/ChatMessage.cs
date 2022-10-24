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
                    return $"{sender} disconnected.";
                case ChatMessageType.UserJoin:
                    return $"{sender} connected.";
            }
            return string.Empty;
        }
    }
}
