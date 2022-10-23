using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Mikejzx.ChatClient
{
    public partial class ReadOnlyRichTextBox : RichTextBox
    {
        [DllImport("user32.dll")]
        public static extern bool HideCaret(IntPtr hwnd);

        public ReadOnlyRichTextBox()
        {
            InitializeComponent();

            this.ReadOnly = true;
        }

        public ReadOnlyRichTextBox(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            HideCaret(this.Handle);
        }
    }
}
