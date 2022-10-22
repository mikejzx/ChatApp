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
        public ChatClientForm()
        {
            InitializeComponent();
        }

        private void ChatClientForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Hide();
            Program.CheckForExit();
        }
    }
}
