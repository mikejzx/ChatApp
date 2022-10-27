namespace Mikejzx.ChatShared
{
    public enum ChatMessageType
    { 
        UserMessage,
        UserJoin,
        UserLeave,
        UserJoinRoom,
        UserLeaveRoom,

        RoomCreated,
        RoomOwnerChanged,
        RoomTopicSet,
    }

    public class ChatMessage
    {
        // Type of message
        public ChatMessageType type;

        // Author/cause of message
        public string author;

        // Message itself (e.g. user message)
        public string? message;

        // Initialisation vector for encrypted messages.
        public string? ivString;

        public ChatMessage(ChatMessageType type, 
                           string author, 
                           string? message=null, 
                           string? ivString=null)
        {
            this.type = type;
            this.author = author;
            this.message = message;
            this.ivString = ivString;
        }

        // Convert the message to a string.
        public override string ToString()
        {
            switch (type)
            {
                case ChatMessageType.UserMessage:
                    return $"<{author}>: {message}";
                case ChatMessageType.UserLeave:
                    return $"{author} disconnected.";
                case ChatMessageType.UserJoin:
                    return $"{author} connected.";
                case ChatMessageType.UserLeaveRoom:
                    return $"{author} left the room.";
                case ChatMessageType.UserJoinRoom:
                    return $"{author} joined the room.";
                case ChatMessageType.RoomCreated:
                    return $"{author} created the room.";
                case ChatMessageType.RoomOwnerChanged:
                    return $"Room ownership transferred to {author}";
                case ChatMessageType.RoomTopicSet:
                    if (message is not null)
                        return $"{author} set the room topic to {message}";
                    return $"{author} removed the room topic.";
                default:
                    return string.Empty;
            }
        }
    }
}
