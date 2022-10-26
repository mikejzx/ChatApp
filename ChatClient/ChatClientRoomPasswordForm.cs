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
    public partial class ChatClientRoomPasswordForm : Form
    {
        private ChatClient m_Client;

        public ChatClientRoomPasswordForm(ChatClient client)
        {
            InitializeComponent();

            m_Client = client;

            m_Client.OnRoomPasswordPending += () =>
            {
                btnOk.Enabled = false;
            };

            m_Client.OnRoomPasswordResponse += () => { btnOk.Enabled = true; };

            m_Client.OnRoomPasswordMismatch += () =>
            {
                MessageBox.Show("The password is incorrect.", "Error", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            //m_Client.OnRoomPasswordCorrect += () => { };
        }

        private void ChatClientRoomPasswordForm_Shown(object sender, EventArgs e)
        {
            Reset();
        }

        // Get the password input.
        public string? GetPassword()
        {
            if (string.IsNullOrEmpty(txtPassword.Text))
                return null;

            return txtPassword.Text;
        }

        // Reset the input field
        public void Reset()
        {
            txtPassword.ResetText();
            btnOk.Enabled = true;
            txtPassword.Focus();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
