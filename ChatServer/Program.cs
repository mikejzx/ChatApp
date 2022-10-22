using Mikejzx.ChatServer;

ChatServer server = new ChatServer();

// Register exit handler for server process.
EventHandler? e = new EventHandler(OnExit);
AppDomain.CurrentDomain.ProcessExit += e;

// Start the chat server.
server.Run();

// Program exit handler.
void OnExit(object? sender, EventArgs? e)
{
    server.Cleanup();
}
