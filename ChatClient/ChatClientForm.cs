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

            m_Client.OnClientListUpdate += (HashSet<string> clients) =>
            {
                // Update the client list control.
                lstClients.Clear();
                lstClients.Columns.Add("");

                foreach (string client in clients)
                {
                    lstClients.Items.Add(client);
                }
            };

            btnSend.Focus();
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
            m_Client.SetRecipient(nickname);
        }
    }
}
