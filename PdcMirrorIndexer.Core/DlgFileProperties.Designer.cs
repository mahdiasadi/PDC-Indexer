namespace PdcMirrorIndexer
{
    partial class DlgFileProperties
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
        private void InitializeComponent() {
            this.llCrc = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.llFileSize = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.llFileDescription = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.llFileVersion = new System.Windows.Forms.Label();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.button1 = new System.Windows.Forms.Button();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tcDescription.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbIcon)).BeginInit();
            this.tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Size = new System.Drawing.Size(55, 13);
            this.label1.Text = "File name:";
            // 
            // tcDescription
            // 
            this.tcDescription.Controls.Add(this.tabPage4);
            this.tcDescription.Size = new System.Drawing.Size(650, 223);
            this.tcDescription.SelectedIndexChanged += new System.EventHandler(this.tcDescription_SelectedIndexChanged);
            this.tcDescription.Controls.SetChildIndex(this.tabPage4, 0);
            this.tcDescription.Controls.SetChildIndex(this.tabPage2, 0);
            this.tcDescription.Controls.SetChildIndex(this.tabPage1, 0);
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label9);
            this.tabPage1.Controls.Add(this.llFileVersion);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.llFileDescription);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Controls.Add(this.llCrc);
            this.tabPage1.Controls.Add(this.llFileSize);
            this.tabPage1.Size = new System.Drawing.Size(642, 197);
            this.tabPage1.Controls.SetChildIndex(this.tbItemName, 0);
            this.tabPage1.Controls.SetChildIndex(this.tbPath, 0);
            this.tabPage1.Controls.SetChildIndex(this.pbIcon, 0);
            this.tabPage1.Controls.SetChildIndex(this.llFileSize, 0);
            this.tabPage1.Controls.SetChildIndex(this.llCrc, 0);
            this.tabPage1.Controls.SetChildIndex(this.label4, 0);
            this.tabPage1.Controls.SetChildIndex(this.label2, 0);
            this.tabPage1.Controls.SetChildIndex(this.label1, 0);
            this.tabPage1.Controls.SetChildIndex(this.llFileDescription, 0);
            this.tabPage1.Controls.SetChildIndex(this.label6, 0);
            this.tabPage1.Controls.SetChildIndex(this.llFileVersion, 0);
            this.tabPage1.Controls.SetChildIndex(this.label9, 0);
            // 
            // tabPage2
            // 
            this.tabPage2.Size = new System.Drawing.Size(642, 197);
            // 
            // llCrc
            // 
            this.llCrc.AutoSize = true;
            this.llCrc.BackColor = System.Drawing.Color.Transparent;
            this.llCrc.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llCrc.Location = new System.Drawing.Point(117, 94);
            this.llCrc.Name = "llCrc";
            this.llCrc.Size = new System.Drawing.Size(32, 13);
            this.llCrc.TabIndex = 109;
            this.llCrc.Text = "llCrc";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.Transparent;
            this.label2.Location = new System.Drawing.Point(15, 94);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(44, 13);
            this.label2.TabIndex = 108;
            this.label2.Text = "CRC32:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.Color.Transparent;
            this.label4.Location = new System.Drawing.Point(15, 119);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(47, 13);
            this.label4.TabIndex = 110;
            this.label4.Text = "File size:";
            // 
            // llFileSize
            // 
            this.llFileSize.AutoSize = true;
            this.llFileSize.BackColor = System.Drawing.Color.Transparent;
            this.llFileSize.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llFileSize.Location = new System.Drawing.Point(117, 119);
            this.llFileSize.Name = "llFileSize";
            this.llFileSize.Size = new System.Drawing.Size(57, 13);
            this.llFileSize.TabIndex = 111;
            this.llFileSize.Text = "llFileSize";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.BackColor = System.Drawing.Color.Transparent;
            this.label6.Location = new System.Drawing.Point(15, 144);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(80, 13);
            this.label6.TabIndex = 112;
            this.label6.Text = "File description:";
            // 
            // llFileDescription
            // 
            this.llFileDescription.AutoSize = true;
            this.llFileDescription.BackColor = System.Drawing.Color.Transparent;
            this.llFileDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llFileDescription.Location = new System.Drawing.Point(117, 144);
            this.llFileDescription.Name = "llFileDescription";
            this.llFileDescription.Size = new System.Drawing.Size(97, 13);
            this.llFileDescription.TabIndex = 113;
            this.llFileDescription.Text = "llFileDescription";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.BackColor = System.Drawing.Color.Transparent;
            this.label9.Location = new System.Drawing.Point(15, 169);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(63, 13);
            this.label9.TabIndex = 114;
            this.label9.Text = "File version:";
            // 
            // llFileVersion
            // 
            this.llFileVersion.AutoSize = true;
            this.llFileVersion.BackColor = System.Drawing.Color.Transparent;
            this.llFileVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llFileVersion.Location = new System.Drawing.Point(117, 169);
            this.llFileVersion.Name = "llFileVersion";
            this.llFileVersion.Size = new System.Drawing.Size(75, 13);
            this.llFileVersion.TabIndex = 115;
            this.llFileVersion.Text = "llFileVersion";
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.button1);
            this.tabPage4.Controls.Add(this.pictureBox1);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(642, 197);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Show";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(556, 15);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "ShowFull";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // pictureBox1
            // 
            this.pictureBox1.Location = new System.Drawing.Point(32, 15);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(518, 166);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // DlgFileProperties
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.ClientSize = new System.Drawing.Size(650, 261);
            this.Name = "DlgFileProperties";
            this.Text = "File Properties";
            this.tcDescription.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pbIcon)).EndInit();
            this.tabPage4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        protected internal System.Windows.Forms.Label label2;
        protected internal System.Windows.Forms.Label llCrc;
        protected internal System.Windows.Forms.Label llFileSize;
        protected internal System.Windows.Forms.Label label4;
        protected internal System.Windows.Forms.Label label9;
        protected internal System.Windows.Forms.Label llFileVersion;
        protected internal System.Windows.Forms.Label label6;
        protected internal System.Windows.Forms.Label llFileDescription;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Button button1;

    }
}
