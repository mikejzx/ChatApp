﻿using System;
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

            lblHeading.Text = "Click on a user to chat with them.";
            txtCompose.Enabled = false;
            btnSend.Enabled = false;

            m_Client.OnClientListUpdate += (Dictionary<string, ChatClientRecipient> clients) =>
            {
                // Update the client list control.
                lstClients.Clear();
                lstClients.Columns.Add("");

                foreach (ChatClientRecipient client in clients.Values)
                {
                    lstClients.Items.Add(client.Nickname);
                }
            };

            m_Client.OnRecipientChanged += (ChatClientRecipient? recipient) =>
            {
                if (recipient is null)
                {
                    lblHeading.Text = "Click on a user to chat with them.";
                    Text = "Chat";
                    txtCompose.Enabled = false;
                    btnSend.Enabled = false;
                    txtMessages.Text = "";
                }
                else
                {
                    // Update titles.
                    lblHeading.Text = $"Chatting with {recipient.Nickname}";

                    Text = $"Chatting with {recipient.Nickname} as {m_Client.Nickname}";
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
                }
            };

            m_Client.OnClientJoin += (ChatClientRecipient recipient) =>
            {
                // Show message to indicate client joining the server.
                if (m_Client.Recipient == recipient.Nickname)
                {
                    txtMessages.Text += $"{recipient.Nickname} joined the server.\n";
                }
            };

            m_Client.OnClientLeave += (ChatClientRecipient recipient) =>
            {
                // Show message to indicate client leaving the server.
                if (m_Client.Recipient == recipient.Nickname)
                {
                    txtMessages.Text += $"{recipient.Nickname} left the server.\n";
                }
            };

            txtCompose.Focus();
        }

        private void ChatClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Hide();
            Program.CheckForExit();
        }

        private void lstClients_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            string nickname = e.Item.Text;

            // Set new recipient to chat with.
            m_Client.Recipient = nickname;
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
    }
}
