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

            m_Client.Channel = null;

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
            lstChannels.DisplayMember = "DisplayString";
            lstRooms.DisplayMember = "DisplayString";

            m_Client.OnConnectionSuccess += () => { NoConnection(false); };

            m_Client.OnError += (string msg) => { NoConnection(true); };

            m_Client.OnConnectionLost += () => { NoConnection(true, true); };

            m_Client.OnLoginNameChanged += (string name) => { lblMyName.Text = "Logged in as " + name; };

            m_Client.OnChannelListUpdate += () =>
            {
                RefreshChannelsList();
            };

            Text = Program.AppName;

            m_Client.OnChannelChanged += () =>
            {
                bool shouldScroll;

                if (m_Client.Channel is null)
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
                    m_Client.Channel.unreadMessages = 0;

                    // Refresh the client list to remove the unread messages.
                    RefreshChannelsList();

                    // Update titles.
                    if (m_Client.Channel.IsDirect)
                    {
                        ChatDirectChannel dc = (ChatDirectChannel)m_Client.Channel;
                        lblHeading.Text = "Direct Messages with " + dc.Recipient.nickname + ":";
                        Text = $"{Program.AppName} — {dc.Recipient.nickname}";
                    }
                    else
                    {
                        ChatRoomChannel rc = (ChatRoomChannel)m_Client.Channel;
                        lblHeading.Text = rc.roomName + ":";
                        Text = $"{Program.AppName} — {rc.roomName}";
                    }

                    txtCompose.Enabled = true;
                    btnSend.Enabled = true;

                    txtMessages.Text = "";

                    // Update the text in the messages view to display the current
                    // message history.
                    foreach (ChatMessage msg in m_Client.Channel.messages)
                    {
                        txtMessages.Text += msg.ToString() + "\n";
                    }

                    txtCompose.Focus();
                }

                if (shouldScroll)
                    txtMessages.ScrollToBottom();
            };

            m_Client.OnMessageReceived += (ChatChannel channel, ChatMessage msg) => 
            {
                // Append message to the messages list (if we are in the user's
                // direct message channel).
                if (m_Client.Channel == channel)
                {
                    bool shouldScroll = txtMessages.IsAtMaxScroll();

                    txtMessages.Text += msg.ToString() + "\n";

                    if (shouldScroll)
                        txtMessages.ScrollToBottom();

                    return true;
                }

                return false;
            };

            m_Client.OnClientJoinCurrentChannel += (ChatRecipient recipient, ChatMessage msg) =>
            {
                // Show message to indicate client joining the server.
                bool shouldScroll = txtMessages.IsAtMaxScroll();

                txtMessages.Text += msg.ToString() + "\n";

                if (shouldScroll)
                    txtMessages.ScrollToBottom();
            };

            m_Client.OnClientJoin += (ChatRecipient recipient) =>
            {
                // Refresh the channel list to update offline statuses.
                RefreshChannelsList();
            };

            m_Client.OnClientLeaveCurrentChannel += (ChatRecipient recipient, ChatMessage msg) =>
            {
                // Show message to indicate client leaving the server.
                bool shouldScroll = txtMessages.IsAtMaxScroll();

                txtMessages.Text += msg.ToString() + "\n";

                if (shouldScroll)
                    txtMessages.ScrollToBottom();
            };

            m_Client.OnClientLeave += (ChatRecipient recipient) =>
            {
                // Refresh the channel list to update offline statuses.
                RefreshChannelsList();
            };

            m_Client.OnClientJoinCurrentRoom += (ChatRecipient recipient, ChatMessage msg) =>
            {
                // Show message to indicate client joining the room.
                bool shouldScroll = txtMessages.IsAtMaxScroll();

                txtMessages.Text += msg.ToString() + "\n";

                if (shouldScroll)
                    txtMessages.ScrollToBottom();
            };

            m_Client.OnClientLeaveCurrentRoom += (ChatRecipient recipient, ChatMessage msg) =>
            {
                // Show message to indicate client leaving the room.
                bool shouldScroll = txtMessages.IsAtMaxScroll();

                txtMessages.Text += msg.ToString() + "\n";

                if (shouldScroll)
                    txtMessages.ScrollToBottom();
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

            m_Client.OnRoomPasswordRequested += () =>
            {
                if (Program.roomPasswordForm is null)
                    return null;

                // Show the password input form.
                Program.roomPasswordForm.Reset();
                if (Program.roomPasswordForm.ShowDialog(this) == DialogResult.OK)
                {
                    return Program.roomPasswordForm.GetPassword();
                }

                return null;
            };

            txtCompose.Focus();
        }

        private void NoConnection(bool notConnected, bool inform=false)
        {
            lblServer.Text = $"Server: {m_Client.Hostname}:{m_Client.Port}";

            btnConnect.Enabled = notConnected;
            btnConnect.Text = notConnected ? "Reconnect" : "Connected";

            if (notConnected && inform)
            {
                MessageBox.Show("Lost connection to the server.  " +
                                "Click 'Reconnect' to attempt to reconnect.", 
                                "Connection lost.",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private void RefreshChannelsList()
        {
            // Update channel list controls.
            lstChannels.Items.Clear();
            lstRooms.Items.Clear();
            foreach (ChatChannel channel in m_Client.Channels)
            {
                if (channel.IsDirect)
                    lstChannels.Items.Add(channel);
                else
                    lstRooms.Items.Add(channel);
            }

            lstChannels.NoEvents = true;
            lstRooms.NoEvents = true;
            if (m_Client.Channel is null)
            {
                // No selected recipient.
                lstChannels.SelectedItem = null;
                lstRooms.SelectedItem = null;
            }
            else
            {
                // Select the channel we are on.
                if (m_Client.Channel.IsDirect)
                    lstChannels.SelectedItem = m_Client.Channel;
                else
                    lstRooms.SelectedItem = m_Client.Channel;
            }
            lstChannels.NoEvents = false;
            lstRooms.NoEvents = false;
        }

        private void ChatClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Hide();
            Program.CheckForExit();
        }

        private void lstChannels_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstChannels.SelectedItem is null)
            {
                m_Client.Channel = null;
            }
            else
            {
                // Set the channel
                ChatChannel channel = (ChatChannel)lstChannels.SelectedItem;
                m_Client.Channel = channel;
            }
        }

        private void lstRooms_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstRooms.SelectedItem is null)
            {
                m_Client.Channel = null;
            }
            else
            {
                // Set the channel
                ChatChannel channel = (ChatChannel)lstRooms.SelectedItem;
                m_Client.Channel = channel;
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

        private void createRoomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show room creator form.
            ChatClientRoomCreateForm createForm = new ChatClientRoomCreateForm(m_Client);
            createForm.Show();
        }

        private void roomToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            deleteRoomToolStripMenuItem.Enabled = m_Client.OwnedRooms.Count > 0;

            deleteRoomToolStripMenuItem.DropDownItems.Clear();
            foreach (ChatRoomChannel room in m_Client.OwnedRooms)
            {
                ToolStripButton button = new ToolStripButton(room.roomName);

                button.Text = room.roomName;

                button.Click += (object? sender, EventArgs e) =>
                {
                    if (sender is null)
                        return;

                    string roomName = ((ToolStripButton)sender).Text;

                    // Delete the room with this name
                    m_Client.DeleteRoom(roomName);
                };

                deleteRoomToolStripMenuItem.DropDownItems.Add(button);
            }
        }

        private void deleteRoomToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
        }
    }
}
