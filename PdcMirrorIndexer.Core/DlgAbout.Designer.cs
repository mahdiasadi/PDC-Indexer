namespace PdcMirrorIndexer
{
    partial class DlgAbout
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DlgAbout));
            tbHistory = new TextBox();
            panel1 = new Panel();
            llVersion = new Label();
            label3 = new Label();
            llTitle = new Label();
            label2 = new Label();
            linkLabel1 = new LinkLabel();
            llCopyright = new Label();
            llCodePlex = new LinkLabel();
            tcAbout = new TabControl();
            tabPage1 = new TabPage();
            textBox2 = new TextBox();
            tpChangeLog = new TabPage();
            tabPage2 = new TabPage();
            tbLicense = new TextBox();
            tcAbout.SuspendLayout();
            tabPage1.SuspendLayout();
            tpChangeLog.SuspendLayout();
            tabPage2.SuspendLayout();
            SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(btnCancel, "btnCancel");
            // 
            // tbHistory
            // 
            resources.ApplyResources(tbHistory, "tbHistory");
            tbHistory.Name = "tbHistory";
            tbHistory.ReadOnly = true;
            // 
            // panel1
            // 
            panel1.BackColor = SystemColors.ActiveCaption;
            resources.ApplyResources(panel1, "panel1");
            panel1.Name = "panel1";
            // 
            // llVersion
            // 
            resources.ApplyResources(llVersion, "llVersion");
            llVersion.Name = "llVersion";
            // 
            // label3
            // 
            resources.ApplyResources(label3, "label3");
            label3.Name = "label3";
            // 
            // llTitle
            // 
            resources.ApplyResources(llTitle, "llTitle");
            llTitle.Name = "llTitle";
            // 
            // label2
            // 
            resources.ApplyResources(label2, "label2");
            label2.Name = "label2";
            // 
            // linkLabel1
            // 
            resources.ApplyResources(linkLabel1, "linkLabel1");
            linkLabel1.Name = "linkLabel1";
            linkLabel1.TabStop = true;
            linkLabel1.Click += linkLabel1_Click;
            // 
            // llCopyright
            // 
            resources.ApplyResources(llCopyright, "llCopyright");
            llCopyright.Name = "llCopyright";
            // 
            // llCodePlex
            // 
            resources.ApplyResources(llCodePlex, "llCodePlex");
            llCodePlex.Name = "llCodePlex";
            llCodePlex.TabStop = true;
            llCodePlex.LinkClicked += llCodePlex_LinkClicked;
            // 
            // tcAbout
            // 
            tcAbout.Controls.Add(tabPage1);
            tcAbout.Controls.Add(tpChangeLog);
            tcAbout.Controls.Add(tabPage2);
            resources.ApplyResources(tcAbout, "tcAbout");
            tcAbout.Name = "tcAbout";
            tcAbout.SelectedIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(textBox2);
            resources.ApplyResources(tabPage1, "tabPage1");
            tabPage1.Name = "tabPage1";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // textBox2
            // 
            resources.ApplyResources(textBox2, "textBox2");
            textBox2.Name = "textBox2";
            textBox2.ReadOnly = true;
            // 
            // tpChangeLog
            // 
            tpChangeLog.BackColor = Color.Transparent;
            tpChangeLog.Controls.Add(tbHistory);
            resources.ApplyResources(tpChangeLog, "tpChangeLog");
            tpChangeLog.Name = "tpChangeLog";
            tpChangeLog.UseVisualStyleBackColor = true;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(tbLicense);
            resources.ApplyResources(tabPage2, "tabPage2");
            tabPage2.Name = "tabPage2";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // tbLicense
            // 
            resources.ApplyResources(tbLicense, "tbLicense");
            tbLicense.Name = "tbLicense";
            tbLicense.ReadOnly = true;
            // 
            // DlgAbout
            // 
            resources.ApplyResources(this, "$this");
            Controls.Add(tcAbout);
            Controls.Add(llCodePlex);
            Controls.Add(panel1);
            Controls.Add(llVersion);
            Controls.Add(label3);
            Controls.Add(llTitle);
            Controls.Add(label2);
            Controls.Add(linkLabel1);
            Controls.Add(llCopyright);
            Name = "DlgAbout";
            Controls.SetChildIndex(btnCancel, 0);
            Controls.SetChildIndex(llCopyright, 0);
            Controls.SetChildIndex(linkLabel1, 0);
            Controls.SetChildIndex(label2, 0);
            Controls.SetChildIndex(llTitle, 0);
            Controls.SetChildIndex(label3, 0);
            Controls.SetChildIndex(llVersion, 0);
            Controls.SetChildIndex(panel1, 0);
            Controls.SetChildIndex(llCodePlex, 0);
            Controls.SetChildIndex(tcAbout, 0);
            tcAbout.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            tpChangeLog.ResumeLayout(false);
            tpChangeLog.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox tbHistory;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label llVersion;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label llTitle;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label llCopyright;
        private System.Windows.Forms.LinkLabel llCodePlex;
        private System.Windows.Forms.TabControl tcAbout;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tpChangeLog;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TextBox tbLicense;
    }
}
