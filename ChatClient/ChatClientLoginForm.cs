namespace Mikejzx.ChatClient
{
    public partial class ChatClientLoginForm : Form
    {
        private ChatClient m_Client;

        public ChatClientLoginForm(ChatClient client, Form chatForm)
        {
            InitializeComponent();

            m_Client = client;
            m_Client.Nickname = txtNickname.Text;

            // On connection success we destroy the form and open the actual
            // chat program.
            m_Client.OnConnectionSuccess += () => 
            {
                // Hide the login form.
                Hide();

                // Show the chat form.
                chatForm.Show();

                // Re-enable controls.
                SetLoggingIn(false);
            };

            m_Client.OnError += (string msg) => 
            {
                // Show the error message.
                MessageBox.Show($"{msg}", "An error occurred.", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);

                // Re-enable controls
                SetLoggingIn(false);
            };

            SetLoggingIn(false);

            btnLogin.Focus();
        }

        private void SetLoggingIn(bool login)
        {
            //txtHostname.Enabled = !login;
            //txtNickname.Enabled = !login;
            btnLogin.Enabled = !login;
            btnLogin.Text = login ? "Logging in ..." : "Log in";
        }

        private void Form1_Load(object sender, EventArgs e) {}

        private void btnLogin_Click(object sender, EventArgs e)
        {
            SetLoggingIn(true);

            // Attempt to connect to given server.
            m_Client.Nickname = txtNickname.Text;
            m_Client.Hostname = txtHostname.Text;
            m_Client.Port = 19000;
            m_Client.Connect();
        }

        private void ChatClientLoginForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Hide();
            Program.CheckForExit();
        }
    }
}