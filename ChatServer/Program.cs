using Mikejzx.ChatServer;

ChatServer server = new ChatServer();

// Register exit handler for server process.
EventHandler? e = new EventHandler(OnExit);
AppDomain.CurrentDomain.ProcessExit += e;

string certificatePath;

// Read certificate file from arguments.
if (args.Length > 1 && !string.IsNullOrEmpty(args[1]))
{
    certificatePath = args[1];
}
else
{
    // Try use default certificate names
    certificatePath = "server.pfx";
    if (!FileExistsAndIsReadable(certificatePath))
        certificatePath = "cert.pfx";
}

// Prompt user for certificate file
for (bool first = true; !FileExistsAndIsReadable(certificatePath);)
{
    if (!first)
        Console.WriteLine($"error: {certificatePath} does not exist.");

    Console.WriteLine("Please enter the path of server certificate file: ");
    Console.Write("> ");
    string? line = Console.ReadLine();

    if (line is null)
        return;

    certificatePath = line;

    first = false;
}

// Start the chat server.
server.Run(certificatePath);

// Program exit handler.
void OnExit(object? sender, EventArgs? e)
{
    server.Cleanup();
}

bool FileExistsAndIsReadable(string path)
{
    if (!File.Exists(path))
        return false;

    // Check if file can be read
    // https://stackoverflow.com/a/17318735
    try
    {
        File.Open(path, FileMode.Open, FileAccess.Read).Dispose();
        return true;
    }
    catch(IOException) 
    {
        return false;
    }
}
