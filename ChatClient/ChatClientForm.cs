using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Mikejzx.ChatClient
{
    public partial class ChatClientForm : Form
    {
        private ChatClient m_Client;

        public ChatClientForm(ChatClient client)
        {
            InitializeComponent();

            m_Client = client;
            m_Client.Form = this;

            lblHeading.Text = "";
            txtCompose.Enabled = false;
            btnSend.Enabled = false;

            m_Client.Recipient = null;

            // When the messages textbox is scrolled to bottom we hide the
            // "Scroll to bottom" button.
            txtMessages.ScrolledToBottom += (object? sender, EventArgs e) =>
            {
                btnScrollToBottom.Hide();
                btnScrollToBottom.Enabled = false;
            };
            txtMessages.UnscrolledFromBottom += (object? sender, EventArgs e) =>
            {
                btnScrollToBottom.Show();
                btnScrollToBottom.Enabled = true;
            };

            // We display the DisplayString member of ChatClientRecipient.
            lstClients.DisplayMember = "DisplayString";

            m_Client.OnConnectionSuccess += () => { NoConnection(false); };

            m_Client.OnError += (string msg) => { NoConnection(true); };

            m_Client.OnConnectionLost += () => { NoConnection(true); };

            m_Client.OnLoginNameChanged += (string name) => { lblMyName.Text = "Logged in as " + name; };

            m_Client.OnClientListUpdate += (Dictionary<string, ChatClientRecipient> clients) =>
            {
                RefreshClientList(clients);
            };

            Text = Program.AppName;

            m_Client.OnRecipientChanged += (ChatClientRecipient? recipient) =>
            {
                bool shouldScroll;

                if (recipient is null)
                {
                    lblHeading.Text = "";
                    Text = Program.AppName;
                    txtCompose.Enabled = false;
                    btnSend.Enabled = false;
                    txtMessages.Text = "";
                    shouldScroll = true;
                }
                else
                {
                    shouldScroll = txtMessages.IsAtMaxScroll();

                    // Reset unread count.
                    recipient.UnreadMessages = 0;

                    // Refresh the client list to remove the unread messages.
                    RefreshClientList(m_Client.Clients);

                    // Update titles.
                    lblHeading.Text = $"Direct Messages with {recipient.Nickname}:";

                    Text = $"{Program.AppName} — {recipient.Nickname}";
                    txtCompose.Enabled = true;
                    btnSend.Enabled = true;

                    txtMessages.Text = "";

                    // Update the text in the messages view to display the current
                    // message history.
                    foreach (ChatMessage msg in recipient.Messages)
                    {
                        txtMessages.Text += msg.ToString() + "\n";
                    }

                    txtCompose.Focus();
                }

                if (shouldScroll)
                    txtMessages.ScrollToBottom();
            };

            m_Client.OnMessageReceived += (string channel, ChatMessage msg) => 
            {
                // Append message to the messages list (if we are on the user's channel).
                if (m_Client.Recipient == channel)
                {
                    bool shouldScroll = txtMessages.IsAtMaxScroll();

                    txtMessages.Text += msg.ToString() + "\n";

                    if (shouldScroll)
                        txtMessages.ScrollToBottom();

                    return true;
                }

                return false;
            };

            m_Client.OnClientJoin += (ChatClientRecipient recipient, ChatMessage msg) =>
            {
                // Show message to indicate client joining the server.
                if (m_Client.Recipient == recipient.Nickname)
                {
                    bool shouldScroll = txtMessages.IsAtMaxScroll();

                    txtMessages.Text += msg.ToString() + "\n";

                    if (shouldScroll)
                        txtMessages.ScrollToBottom();
                }

                // Refresh the client list to update offline status.
                RefreshClientList(m_Client.Clients);
            };

            m_Client.OnClientLeave += (ChatClientRecipient recipient, ChatMessage msg) =>
            {
                bool shouldScroll = txtMessages.IsAtMaxScroll();

                // Show message to indicate client leaving the server.
                if (m_Client.Recipient == recipient.Nickname)
                {
                    txtMessages.Text += msg.ToString() + "\n";

                    if (shouldScroll)
                        txtMessages.ScrollToBottom();
                }

                // Refresh the client list to update offline status.
                RefreshClientList(m_Client.Clients);
            };

            m_Client.OnCertificateValidationFailed += () =>
            {
                NoConnection(true);
            };

            m_Client.OnCertificateFirstTime += (X509Certificate cert) =>
            {
                TaskDialogButton okButton = new TaskDialogButton();
                okButton.Tag = DialogResult.OK;
                okButton.Text = "OK";

                TaskDialogButton cancelButton = new TaskDialogButton();
                cancelButton.Tag = DialogResult.No;
                cancelButton.Text = "I don't trust this certificate";

                TaskDialogExpander expander = new TaskDialogExpander();
                expander.Expanded = false;
                expander.CollapsedButtonText = "Show Certificate Details";
                expander.ExpandedButtonText = "Hide Certificate Details";
                expander.Text = $"Certificate details:\n" +
                                $"    Subject: {cert.Subject}\n" +
                                $"    Fingerprint: {cert.GetCertHashString()}\n" +
                                $"    Issued by: {cert.Issuer}\n" +
                                $"    Issued: {cert.GetEffectiveDateString()}\n" +
                                $"    Expires: {cert.GetExpirationDateString()}\n";

                TaskDialogPage page = new TaskDialogPage();
                page.Caption = "Security Information";
                page.DefaultButton = okButton;
                page.Expander = expander;
                page.Heading = "This is your first time connecting to this server";
                page.Icon = TaskDialogIcon.Information;
                page.Text = "Click OK if you trust the certificate the server has presented.\n\n" +
                            "You may wish to review the certificate details and " +
                            "determine whether you trust it or not.";
                page.Buttons = new TaskDialogButtonCollection() { okButton, cancelButton };

                if (Program.loginForm is null)
                    return false;

                TaskDialogButton result = TaskDialog.ShowDialog(Program.loginForm, page);

                if (result.Tag is null)
                    return false;

                return (DialogResult)result.Tag == DialogResult.OK;
            };

            m_Client.OnCertificateChanged += (X509Certificate newCert, string oldFingerprint) =>
            {
                TaskDialogButton yesButton = new TaskDialogButton();
                yesButton.Tag = DialogResult.Yes;
                yesButton.Text = "Trust the new certificate";

                TaskDialogButton noButton = new TaskDialogButton();
                noButton.Tag = DialogResult.No;
                noButton.Text = "Reject the new certificate";

                TaskDialogExpander expander = new TaskDialogExpander();
                expander.Expanded = false;
                expander.CollapsedButtonText = "Review Certificate Details";
                expander.ExpandedButtonText = "Hide Certificate Details";
                expander.Text = $"Trusted certificate details:\n" +
                                $"    Fingerprint: {oldFingerprint}\n" +
                                $"\n" +
                                $"New Certificate details:\n" +
                                $"    Fingerprint: {newCert.GetCertHashString()}\n" +
                                $"    Subject: {newCert.Subject}\n" +
                                $"    Issued by: {newCert.Issuer}\n" +
                                $"    Issued: {newCert.GetEffectiveDateString()}\n" +
                                $"    Expires: {newCert.GetExpirationDateString()}\n";

                TaskDialogPage page = new TaskDialogPage();
                page.Caption = "Security Warning";
                page.DefaultButton = noButton;
                page.Expander = expander;
                page.Heading = "Server sent an unknown certificate";
                page.Icon = TaskDialogIcon.ShieldErrorRedBar;
                page.Text = "Do you want trust the new certificate the server has presented?\n\n" +
                            "This could be due to a man-in-the-middle attack, or more likely, " + 
                            "the server may have updated their certificate.\n\n" +
                            "Please review the certificate details below and " +
                            "determine whether you wish to trust the new certificate or not.\n\n";
                page.Buttons = new TaskDialogButtonCollection() { yesButton, noButton };
                page.AllowCancel = false;

                if (Program.loginForm is null)
                    return false;

                TaskDialogButton result = TaskDialog.ShowDialog(Program.loginForm, 
                                                                page, 
                                                                TaskDialogStartupLocation.CenterScreen);

                if (result.Tag is null)
                    return false;

                return (DialogResult)result.Tag == DialogResult.Yes;
            };

            txtCompose.Focus();
        }

        private void NoConnection(bool notConnected)
        {
            lblServer.Text = $"Server: {m_Client.Hostname}:{m_Client.Port}";

            btnConnect.Enabled = notConnected;
            btnConnect.Text = notConnected ? "Reconnect" : "Connected";
        }

        private void RefreshClientList(Dictionary<string, ChatClientRecipient> clients)
        {
            // Update the client list control.
            lstClients.Items.Clear();
            foreach (ChatClientRecipient client in clients.Values)
            {
                // Skip ourself
                if (client.Nickname == m_Client.Nickname)
                    continue;

                // Add all other users.
                lstClients.Items.Add(client);
            }

            if (m_Client.Recipient is null)
            {
                // No selected recipient.
                lstClients.NoEvents = true;
                lstClients.SelectedItem = null;
                lstClients.NoEvents = false;
            }
            else
            {
                // Select the client we are chatting with.
                foreach (ChatClientRecipient client in lstClients.Items)
                {
                    if (client.Nickname == m_Client.Recipient)
                    {
                        lstClients.NoEvents = true;
                        lstClients.SelectedItem = client;
                        lstClients.NoEvents = false;
                        break;
                    }
                }
            }
        }

        private void ChatClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Hide();
            Program.CheckForExit();
        }

        private void lstClients_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstClients.SelectedItem is null)
            {
                m_Client.Recipient = null;
            }
            else
            {
                // Set the new recipient.
                ChatClientRecipient recipient = (ChatClientRecipient)lstClients.SelectedItem;
                m_Client.Recipient = recipient.Nickname;
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            txtCompose.Focus();

            // No text to send
            if (txtCompose.TextLength <= 0)
                return;

            // Send message to the current client.
            m_Client.SendMessage(txtCompose.Text);

            txtCompose.Clear();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            btnConnect.Text = "Connecting...";
            btnConnect.Enabled = false;
            m_Client.Connect();
        }

        private void logOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Log out of the server.
            m_Client.Disconnect();

            // Return to the login form.
            Hide();
            if (Program.loginForm is not null)
                Program.loginForm.Show();

            Program.CheckForExit();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChatClientAboutBox about = new ChatClientAboutBox();
            about.ShowDialog();
        }

        private void btnScrollToBottom_Click(object sender, EventArgs e)
        {
            txtMessages.ScrollToBottom();
        }
    }
}
