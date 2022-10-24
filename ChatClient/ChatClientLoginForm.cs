namespace Mikejzx.ChatClient
{
    public partial class ChatClientLoginForm : Form
    {
        private ChatClient m_Client;

        public ChatClientLoginForm(ChatClient client)
        {
            InitializeComponent();

            m_Client = client;
            m_Client.Form = null;
            m_Client.Nickname = txtNickname.Text;

            // On connection success we destroy the form and open the actual
            // chat program.
            m_Client.OnConnectionSuccess += () => 
            {
                // Hide the login form.
                Hide();

                // Show the chat form.
                if (Program.chatForm is not null)
                    Program.chatForm.Show();

                // Null the recipient (to clear messages box on re-login).
                m_Client.Channel = null;

                // Re-enable controls.
                SetLoggingIn(false);
            };

            m_Client.OnError += (string msg) => 
            {
                TaskDialogButton okButton = new TaskDialogButton();
                okButton.Tag = DialogResult.OK;
                okButton.Text = "OK";

                TaskDialogPage page = new TaskDialogPage();
                page.Caption = "Error";
                page.DefaultButton = okButton;
                page.Heading = "Error";
                page.Icon = TaskDialogIcon.Error;
                page.Text = msg;
                page.Buttons = new TaskDialogButtonCollection() { okButton };

                TaskDialog.ShowDialog(this, page, TaskDialogStartupLocation.CenterScreen);

                // Re-enable controls
                SetLoggingIn(false);
            };

            m_Client.OnCertificateValidationFailed += () =>
            {
                // Re-enable controls
                SetLoggingIn(false);
            };

            SetLoggingIn(false);

            m_Client.Form = this;
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