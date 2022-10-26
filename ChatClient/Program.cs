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

        // Form used for room password input
        public static ChatClientRoomPasswordForm? roomPasswordForm;

        // Room creation forms
        public static List<ChatClientRoomCreateForm> roomCreateForms = new List<ChatClientRoomCreateForm>();

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
            roomPasswordForm = new ChatClientRoomPasswordForm(client);

            // Hide forms by default.
            loginForm.Hide();
            chatForm.Hide();
            roomPasswordForm.Hide();

            // Show login form first.
            Application.Run(loginForm);
        }

        private static void Cleanup()
        {
            // Close all room creator forms
            foreach (ChatClientRoomCreateForm form in roomCreateForms)
                form.Close();

            if (roomPasswordForm is not null)
                roomPasswordForm.Close();

            // Disconnect the client
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