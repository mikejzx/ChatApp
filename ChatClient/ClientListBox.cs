using System.ComponentModel;

namespace Mikejzx.ChatClient
{
    public partial class ClientListBox : ListBox
    {
        public bool NoEvents = false;

        public ClientListBox()
        {
            InitializeComponent();
        }

        public ClientListBox(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            if (!NoEvents)
                base.OnSelectedIndexChanged(e);
        }
    }
}