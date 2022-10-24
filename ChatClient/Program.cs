using Mikejzx.ChatShared;

namespace Mikejzx.ChatClient
{
    internal static class Program
    {
        // Global chat client.
        public static ChatClient? client;

        // Form used for login
        public static ChatClientLoginForm? loginForm;

        // Form used for chatting
        public static ChatClientForm? chatForm;

        // Name of the application
        public static readonly string AppName = "ChatApp";

        // Path of trusted certificates file.
        public static readonly string TOFUPath = "tofu.txt";

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Initialise application with the login form.
            ApplicationConfiguration.Initialize();

            client = new ChatClient("", ChatConstants.ServerPort);
            chatForm = new ChatClientForm(client);
            loginForm = new ChatClientLoginForm(client);

            // Set both forms invisible by default.
            loginForm.Visible = false;
            chatForm.Visible = false;

            Application.Run(loginForm);
        }

        private static void Cleanup()
        {
            if (client is not null)
                client.Disconnect();
        }

        // Closes the application if none of the forms are visible.
        public static void CheckForExit()
        {
            bool allHidden = loginForm != null && !loginForm.Visible &&
                             chatForm != null && !chatForm.Visible;

            if (allHidden)
            {
                Cleanup();

                // Both forms are closed--we exit the application.
                Application.Exit();
            }
        }
    }
}