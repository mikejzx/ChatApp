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
    public partial class ChatClientRoomCreateForm : Form
    {
        private ChatClient m_Client;

        public ChatClientRoomCreateForm(ChatClient client)
        {
            InitializeComponent();

            m_Client = client;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            // Close the form.
            Close();
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            btnCreate.Enabled = false;
            btnCreate.Text = "Creating ...";

            m_Client.OnRoomCreateSuccess = () => { Close(); };
            m_Client.OnRoomCreateFail = (string error) =>
            {
                MessageBox.Show("Failed to create room: " + error, "Error", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);

                btnCreate.Text = "Create";
                btnCreate.Enabled = true;
            };

            // Create the room.
            m_Client.CreateRoom(roomName: txtName.Text, 
                                roomTopic: txtTopic.Text, 
                                roomEncrypted: chkEncrypt.Checked,
                                roomPassword: txtPassword.Text);
        }

        private void UpdateCreateButtonEnabledState()
        { 
            // Only allow creating the room if the room name is set.
            btnCreate.Enabled = txtName.TextLength > 0;

            // If a password is enabled then we need it to be set.
            if (chkEncrypt.Checked && txtPassword.TextLength <= 0)
            {
                btnCreate.Enabled = false;
            }
        }

        private void chkEncrypt_CheckedChanged(object sender, EventArgs e)
        {
            // Enable/disable the password field.
            txtPassword.Enabled = 
            lblPassword.Enabled = chkEncrypt.Checked;
            UpdateCreateButtonEnabledState();
        }

        private void txtName_TextChanged(object sender, EventArgs e) => UpdateCreateButtonEnabledState();
        private void txtPassword_TextChanged(object sender, EventArgs e) => UpdateCreateButtonEnabledState();
    }
}
