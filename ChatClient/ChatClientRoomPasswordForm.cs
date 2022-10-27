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

            m_Client.OnRoomPasswordError += (string message) =>
            {
                MessageBox.Show(message, "Error", 
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            //m_Client.OnRoomPasswordCorrect += () => { };
        }

        private void ChatClientRoomPasswordForm_Shown(object sender, EventArgs e)
        {
            // Reset the input field when shown.
            Reset();
        }

        // Get the password input.
        public string? GetPassword()
        {
            if (string.IsNullOrEmpty(txtPassword.Text))
                return null;

            return txtPassword.Text;
        }

        // Adjust buttons depending on current password length (so we don't send empty strings)>
        private void CheckLength()
        {
            btnOk.Enabled = txtPassword.TextLength > 0;
        }

        // Reset the input field
        public void Reset()
        {
            txtPassword.ResetText();
            CheckLength();
            txtPassword.Focus();
        }

        private void txtPassword_TextChanged(object sender, EventArgs e)
        {
            // Check the length of the new text.
            CheckLength();
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            // Set result and close the form.
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            // Set result and close the form.
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
