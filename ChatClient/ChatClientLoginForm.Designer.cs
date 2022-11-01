namespace Mikejzx.ChatClient
{
    partial class ChatClientLoginForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.txtHostname = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.txtNickname = new System.Windows.Forms.TextBox();
            this.btnLogin = new System.Windows.Forms.Button();
            this.cboxLan = new System.Windows.Forms.ComboBox();
            this.radUseHostname = new System.Windows.Forms.RadioButton();
            this.radUseLan = new System.Windows.Forms.RadioButton();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(244, 15);
            this.label1.TabIndex = 0;
            this.label1.Text = "Enter hostname or IP address of chat server:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // txtHostname
            // 
            this.txtHostname.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtHostname.Location = new System.Drawing.Point(34, 27);
            this.txtHostname.Name = "txtHostname";
            this.txtHostname.Size = new System.Drawing.Size(222, 23);
            this.txtHostname.TabIndex = 1;
            this.txtHostname.Text = "localhost";
            this.txtHostname.MouseClick += new System.Windows.Forms.MouseEventHandler(this.txtHostname_MouseClick);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.Location = new System.Drawing.Point(12, 105);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(244, 23);
            this.label2.TabIndex = 2;
            this.label2.Text = "Enter your nickname:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            // 
            // txtNickname
            // 
            this.txtNickname.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtNickname.Location = new System.Drawing.Point(12, 131);
            this.txtNickname.Name = "txtNickname";
            this.txtNickname.Size = new System.Drawing.Size(244, 23);
            this.txtNickname.TabIndex = 3;
            this.txtNickname.Text = "User";
            // 
            // btnLogin
            // 
            this.btnLogin.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLogin.Location = new System.Drawing.Point(12, 160);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(244, 23);
            this.btnLogin.TabIndex = 4;
            this.btnLogin.Text = "&Log In";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // cboxLan
            // 
            this.cboxLan.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cboxLan.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboxLan.Enabled = false;
            this.cboxLan.FormattingEnabled = true;
            this.cboxLan.Location = new System.Drawing.Point(34, 79);
            this.cboxLan.Name = "cboxLan";
            this.cboxLan.Size = new System.Drawing.Size(222, 23);
            this.cboxLan.TabIndex = 5;
            this.cboxLan.MouseClick += new System.Windows.Forms.MouseEventHandler(this.cboxLan_MouseClick);
            // 
            // radUseHostname
            // 
            this.radUseHostname.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.radUseHostname.Checked = true;
            this.radUseHostname.Location = new System.Drawing.Point(12, 27);
            this.radUseHostname.Name = "radUseHostname";
            this.radUseHostname.Size = new System.Drawing.Size(16, 23);
            this.radUseHostname.TabIndex = 6;
            this.radUseHostname.TabStop = true;
            this.radUseHostname.UseVisualStyleBackColor = true;
            // 
            // radUseLan
            // 
            this.radUseLan.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.radUseLan.Enabled = false;
            this.radUseLan.Location = new System.Drawing.Point(12, 79);
            this.radUseLan.Name = "radUseLan";
            this.radUseLan.Size = new System.Drawing.Size(16, 23);
            this.radUseLan.TabIndex = 7;
            this.radUseLan.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label3.Location = new System.Drawing.Point(12, 53);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(244, 23);
            this.label3.TabIndex = 8;
            this.label3.Text = "Or select a server on your LAN:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            // 
            // ChatClientLoginForm
            // 
            this.AcceptButton = this.btnLogin;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(268, 194);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.radUseLan);
            this.Controls.Add(this.radUseHostname);
            this.Controls.Add(this.cboxLan);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.txtNickname);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.txtHostname);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "ChatClientLoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Chat Client Login";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ChatClientLoginForm_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.VisibleChanged += new System.EventHandler(this.ChatClientLoginForm_VisibleChanged);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Label label1;
        private TextBox txtHostname;
        private Label label2;
        private TextBox txtNickname;
        private Button btnLogin;
        private ComboBox cboxLan;
        private RadioButton radUseHostname;
        private RadioButton radUseLan;
        private Label label3;
    }
}