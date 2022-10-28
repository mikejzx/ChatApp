namespace Mikejzx.ChatClient
{
    partial class ChatClientForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblHeading = new System.Windows.Forms.Label();
            this.btnSend = new System.Windows.Forms.Button();
            this.txtCompose = new System.Windows.Forms.TextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.btnScrollToBottom = new System.Windows.Forms.Button();
            this.txtMessages = new Mikejzx.ChatClient.ReadOnlyRichTextBox(this.components);
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.lstRooms = new Mikejzx.ChatClient.ChannelListBox(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.lstChannels = new Mikejzx.ChatClient.ChannelListBox(this.components);
            this.lblServer = new System.Windows.Forms.Label();
            this.btnConnect = new System.Windows.Forms.Button();
            this.lblMyName = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logOutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.roomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.createRoomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteRoomToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblHeading
            // 
            this.lblHeading.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHeading.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblHeading.Location = new System.Drawing.Point(3, 3);
            this.lblHeading.Name = "lblHeading";
            this.lblHeading.Size = new System.Drawing.Size(417, 27);
            this.lblHeading.TabIndex = 0;
            this.lblHeading.Text = "Heading";
            this.lblHeading.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // btnSend
            // 
            this.btnSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSend.Location = new System.Drawing.Point(457, 383);
            this.btnSend.Name = "btnSend";
            this.btnSend.Size = new System.Drawing.Size(75, 23);
            this.btnSend.TabIndex = 4;
            this.btnSend.Text = "Send";
            this.btnSend.UseVisualStyleBackColor = true;
            this.btnSend.Click += new System.EventHandler(this.btnSend_Click);
            // 
            // txtCompose
            // 
            this.txtCompose.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtCompose.Location = new System.Drawing.Point(3, 383);
            this.txtCompose.Name = "txtCompose";
            this.txtCompose.Size = new System.Drawing.Size(448, 23);
            this.txtCompose.TabIndex = 5;
            this.txtCompose.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtCompose_KeyDown);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer1.Location = new System.Drawing.Point(12, 27);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.btnScrollToBottom);
            this.splitContainer1.Panel1.Controls.Add(this.txtMessages);
            this.splitContainer1.Panel1.Controls.Add(this.txtCompose);
            this.splitContainer1.Panel1.Controls.Add(this.lblHeading);
            this.splitContainer1.Panel1.Controls.Add(this.btnSend);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
            this.splitContainer1.Size = new System.Drawing.Size(776, 411);
            this.splitContainer1.SplitterDistance = 537;
            this.splitContainer1.TabIndex = 6;
            // 
            // btnScrollToBottom
            // 
            this.btnScrollToBottom.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnScrollToBottom.Enabled = false;
            this.btnScrollToBottom.Location = new System.Drawing.Point(426, 3);
            this.btnScrollToBottom.Name = "btnScrollToBottom";
            this.btnScrollToBottom.Size = new System.Drawing.Size(106, 24);
            this.btnScrollToBottom.TabIndex = 7;
            this.btnScrollToBottom.Text = "Scroll to bottom";
            this.btnScrollToBottom.UseVisualStyleBackColor = true;
            this.btnScrollToBottom.Click += new System.EventHandler(this.btnScrollToBottom_Click);
            // 
            // txtMessages
            // 
            this.txtMessages.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMessages.BackColor = System.Drawing.SystemColors.Window;
            this.txtMessages.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.txtMessages.Font = new System.Drawing.Font("Lucida Console", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtMessages.Location = new System.Drawing.Point(3, 33);
            this.txtMessages.Name = "txtMessages";
            this.txtMessages.ReadOnly = true;
            this.txtMessages.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedVertical;
            this.txtMessages.ShortcutsEnabled = false;
            this.txtMessages.Size = new System.Drawing.Size(529, 337);
            this.txtMessages.TabIndex = 6;
            this.txtMessages.TabStop = false;
            this.txtMessages.Text = "";
            // 
            // splitContainer2
            // 
            this.splitContainer2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(0, 0);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.lstRooms);
            this.splitContainer2.Panel1.Controls.Add(this.label1);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.lstChannels);
            this.splitContainer2.Panel2.Controls.Add(this.lblServer);
            this.splitContainer2.Panel2.Controls.Add(this.btnConnect);
            this.splitContainer2.Panel2.Controls.Add(this.lblMyName);
            this.splitContainer2.Panel2.Controls.Add(this.label2);
            this.splitContainer2.Size = new System.Drawing.Size(235, 411);
            this.splitContainer2.SplitterDistance = 139;
            this.splitContainer2.TabIndex = 0;
            // 
            // lstRooms
            // 
            this.lstRooms.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstRooms.FormattingEnabled = true;
            this.lstRooms.IntegralHeight = false;
            this.lstRooms.ItemHeight = 15;
            this.lstRooms.Location = new System.Drawing.Point(3, 33);
            this.lstRooms.Name = "lstRooms";
            this.lstRooms.Size = new System.Drawing.Size(227, 101);
            this.lstRooms.TabIndex = 15;
            this.lstRooms.SelectedIndexChanged += new System.EventHandler(this.lstRooms_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(3, 3);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(227, 27);
            this.label1.TabIndex = 13;
            this.label1.Text = "Rooms:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // lstChannels
            // 
            this.lstChannels.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lstChannels.FormattingEnabled = true;
            this.lstChannels.IntegralHeight = false;
            this.lstChannels.ItemHeight = 15;
            this.lstChannels.Location = new System.Drawing.Point(3, 23);
            this.lstChannels.Name = "lstChannels";
            this.lstChannels.Size = new System.Drawing.Size(227, 184);
            this.lstChannels.TabIndex = 14;
            this.lstChannels.SelectedIndexChanged += new System.EventHandler(this.lstChannels_SelectedIndexChanged);
            // 
            // lblServer
            // 
            this.lblServer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblServer.Location = new System.Drawing.Point(3, 238);
            this.lblServer.Name = "lblServer";
            this.lblServer.Size = new System.Drawing.Size(135, 25);
            this.lblServer.TabIndex = 12;
            this.lblServer.Text = "Server: ";
            this.lblServer.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnConnect
            // 
            this.btnConnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnConnect.AutoSize = true;
            this.btnConnect.Enabled = false;
            this.btnConnect.Location = new System.Drawing.Point(142, 238);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(88, 25);
            this.btnConnect.TabIndex = 11;
            this.btnConnect.Text = "Connecting...";
            this.btnConnect.UseVisualStyleBackColor = true;
            // 
            // lblMyName
            // 
            this.lblMyName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblMyName.Location = new System.Drawing.Point(3, 210);
            this.lblMyName.Name = "lblMyName";
            this.lblMyName.Size = new System.Drawing.Size(227, 25);
            this.lblMyName.TabIndex = 10;
            this.lblMyName.Text = "Logged in as ...";
            this.lblMyName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(227, 20);
            this.label2.TabIndex = 9;
            this.label2.Text = "Direct Messages:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.roomToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(800, 24);
            this.menuStrip.TabIndex = 7;
            this.menuStrip.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logOutToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // logOutToolStripMenuItem
            // 
            this.logOutToolStripMenuItem.Name = "logOutToolStripMenuItem";
            this.logOutToolStripMenuItem.Size = new System.Drawing.Size(115, 22);
            this.logOutToolStripMenuItem.Text = "Log out";
            this.logOutToolStripMenuItem.Click += new System.EventHandler(this.logOutToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(115, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // roomToolStripMenuItem
            // 
            this.roomToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.createRoomToolStripMenuItem,
            this.deleteRoomToolStripMenuItem});
            this.roomToolStripMenuItem.Name = "roomToolStripMenuItem";
            this.roomToolStripMenuItem.Size = new System.Drawing.Size(51, 20);
            this.roomToolStripMenuItem.Text = "Room";
            this.roomToolStripMenuItem.DropDownOpening += new System.EventHandler(this.roomToolStripMenuItem_DropDownOpening);
            // 
            // createRoomToolStripMenuItem
            // 
            this.createRoomToolStripMenuItem.Name = "createRoomToolStripMenuItem";
            this.createRoomToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.createRoomToolStripMenuItem.Text = "Create room...";
            this.createRoomToolStripMenuItem.Click += new System.EventHandler(this.createRoomToolStripMenuItem_Click);
            // 
            // deleteRoomToolStripMenuItem
            // 
            this.deleteRoomToolStripMenuItem.Enabled = false;
            this.deleteRoomToolStripMenuItem.Name = "deleteRoomToolStripMenuItem";
            this.deleteRoomToolStripMenuItem.Size = new System.Drawing.Size(149, 22);
            this.deleteRoomToolStripMenuItem.Text = "Delete room...";
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(107, 22);
            this.aboutToolStripMenuItem.Text = "About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.aboutToolStripMenuItem_Click);
            // 
            // ChatClientForm
            // 
            this.AcceptButton = this.btnSend;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.splitContainer1);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "ChatClientForm";
            this.Text = "Chat";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ChatClientForm_FormClosing);
            this.Resize += new System.EventHandler(this.ChatClientForm_Resize);
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Label lblHeading;
        private Button btnSend;
        private TextBox txtCompose;
        private SplitContainer splitContainer1;
        private ReadOnlyRichTextBox txtMessages;
        private MenuStrip menuStrip;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem logOutToolStripMenuItem;
        private Button btnScrollToBottom;
        private ToolStripMenuItem roomToolStripMenuItem;
        private ToolStripMenuItem createRoomToolStripMenuItem;
        private ToolStripMenuItem deleteRoomToolStripMenuItem;
        private SplitContainer splitContainer2;
        private ChannelListBox lstRooms;
        private Label label1;
        private ChannelListBox lstChannels;
        private Label lblServer;
        private Button btnConnect;
        private Label lblMyName;
        private Label label2;
    }
}