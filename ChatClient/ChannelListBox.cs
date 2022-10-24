using System.ComponentModel;

namespace Mikejzx.ChatClient
{
    public partial class ChannelListBox : ListBox
    {
        public bool NoEvents = false;

        public ChannelListBox()
        {
            InitializeComponent();
        }

        public ChannelListBox(IContainer container)
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