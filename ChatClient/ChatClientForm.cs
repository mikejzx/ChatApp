using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
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

            Text = "ChatApp";

            m_Client.OnRecipientChanged += (ChatClientRecipient? recipient) =>
            {
                if (recipient is null)
                {
                    lblHeading.Text = "";
                    Text = "ChatApp";
                    txtCompose.Enabled = false;
                    btnSend.Enabled = false;
                    txtMessages.Text = "";
                }
                else
                {
                    // Reset unread count.
                    recipient.UnreadMessages = 0;

                    // Refresh the client list to remove the unread messages.
                    RefreshClientList(m_Client.Clients);

                    // Update titles.
                    lblHeading.Text = $"{recipient.Nickname} Direct Messages:";

                    Text = $"ChatApp — {recipient.Nickname}";
                    txtCompose.Enabled = true;
                    btnSend.Enabled = true;

                    txtMessages.Text = "";

                    // Update the text in the messages view to display the current
                    // message history.
                    foreach (ChatMessage msg in recipient.Messages)
                    {
                        txtMessages.Text += $"<{msg.sender}>: {msg.message}\n";
                    }

                    txtCompose.Focus();
                }
            };

            m_Client.OnMessageReceived += (string channel, ChatMessage msg) => 
            {
                // Append message to the messages list (if we are on the user's channel).
                if (m_Client.Recipient == channel)
                {
                    txtMessages.Text += $"<{msg.sender}>: {msg.message}\n";
                    return true;
                }

                return false;
            };

            m_Client.OnClientJoin += (ChatClientRecipient recipient) =>
            {
                // Show message to indicate client joining the server.
                if (m_Client.Recipient == recipient.Nickname)
                {
                    txtMessages.Text += $"{recipient.Nickname} joined the server.\n";
                }

                // Refresh client list to update offline status.
                RefreshClientList(m_Client.Clients);
            };

            m_Client.OnClientLeave += (ChatClientRecipient recipient) =>
            {
                // Show message to indicate client leaving the server.
                if (m_Client.Recipient == recipient.Nickname)
                {
                    txtMessages.Text += $"{recipient.Nickname} left the server.\n";
                }

                // Refresh client list to update offline status.
                RefreshClientList(m_Client.Clients);
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

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ChatClientAboutBox about = new ChatClientAboutBox();
            about.ShowDialog();
        }
    }
}
