namespace Mikejzx.ChatShared
{
    public enum ChatPacketType : uint
    {
        // Client is first connecting to the server.
        ClientHello,

        // Client leaves the server.
        ClientDisconnect,

        // Client sends message to a user.
        ClientDirectMessage,

        // Server is sending us an error
        ServerError,

        // Server allows client into the server.
        ServerWelcome,

        // Server is sending a client the most recent client list.
        ServerClientList,
    }

    public enum ChatPacketErrorCode : uint
    {
        OK,

        // The given nickname is invalid.
        InvalidNickname,
    }
}
