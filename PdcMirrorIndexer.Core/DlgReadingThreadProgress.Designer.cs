namespace PdcMirrorIndexer
{
    partial class DlgReadingThreadProgress
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
            this.components = new System.ComponentModel.Container();
            this.label2 = new System.Windows.Forms.Label();
            this.llElapsedTime = new System.Windows.Forms.Label();
            this.btnBackground = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.llWorkStatus = new System.Windows.Forms.Label();
            this.llProgress = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.label3 = new System.Windows.Forms.Label();
            this.llFileCount = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.llFileSize = new System.Windows.Forms.Label();
            this.llOperation = new System.Windows.Forms.Label();
            this.btnPause = new System.Windows.Forms.Button();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.Transparent;
            this.label2.Location = new System.Drawing.Point(12, 152);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(70, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Elapsed time:";
            // 
            // llElapsedTime
            // 
            this.llElapsedTime.AutoSize = true;
            this.llElapsedTime.BackColor = System.Drawing.Color.Transparent;
            this.llElapsedTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llElapsedTime.Location = new System.Drawing.Point(88, 152);
            this.llElapsedTime.Name = "llElapsedTime";
            this.llElapsedTime.Size = new System.Drawing.Size(14, 13);
            this.llElapsedTime.TabIndex = 10;
            this.llElapsedTime.Text = "0";
            // 
            // btnBackground
            // 
            this.btnBackground.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBackground.Location = new System.Drawing.Point(382, 177);
            this.btnBackground.Name = "btnBackground";
            this.btnBackground.Size = new System.Drawing.Size(83, 23);
            this.btnBackground.TabIndex = 12;
            this.btnBackground.Text = "Background";
            this.btnBackground.UseVisualStyleBackColor = true;
            this.btnBackground.Click += new System.EventHandler(this.btnBackground_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.Color.Transparent;
            this.label1.Location = new System.Drawing.Point(12, 86);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(51, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Progress:";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(471, 177);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 13;
            this.btnCancel.Text = "Cancel...";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // llWorkStatus
            // 
            this.llWorkStatus.AutoEllipsis = true;
            this.llWorkStatus.BackColor = System.Drawing.Color.Transparent;
            this.llWorkStatus.Location = new System.Drawing.Point(12, 33);
            this.llWorkStatus.Name = "llWorkStatus";
            this.llWorkStatus.Size = new System.Drawing.Size(534, 13);
            this.llWorkStatus.TabIndex = 1;
            this.llWorkStatus.Text = "llWorkStatus";
            // 
            // llProgress
            // 
            this.llProgress.AutoSize = true;
            this.llProgress.BackColor = System.Drawing.Color.Transparent;
            this.llProgress.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llProgress.Location = new System.Drawing.Point(88, 86);
            this.llProgress.Name = "llProgress";
            this.llProgress.Size = new System.Drawing.Size(23, 13);
            this.llProgress.TabIndex = 4;
            this.llProgress.Text = "0%";
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(12, 51);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(534, 18);
            this.progressBar.TabIndex = 2;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.BackColor = System.Drawing.Color.Transparent;
            this.label3.Location = new System.Drawing.Point(12, 108);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(62, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "Added files:";
            // 
            // llFileCount
            // 
            this.llFileCount.AutoSize = true;
            this.llFileCount.BackColor = System.Drawing.Color.Transparent;
            this.llFileCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llFileCount.Location = new System.Drawing.Point(88, 108);
            this.llFileCount.Name = "llFileCount";
            this.llFileCount.Size = new System.Drawing.Size(35, 13);
            this.llFileCount.TabIndex = 6;
            this.llFileCount.Text = "0 / 0";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.BackColor = System.Drawing.Color.Transparent;
            this.label4.Location = new System.Drawing.Point(12, 130);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(53, 13);
            this.label4.TabIndex = 7;
            this.label4.Text = "Scanned:";
            // 
            // llFileSize
            // 
            this.llFileSize.AutoSize = true;
            this.llFileSize.BackColor = System.Drawing.Color.Transparent;
            this.llFileSize.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llFileSize.Location = new System.Drawing.Point(88, 130);
            this.llFileSize.Name = "llFileSize";
            this.llFileSize.Size = new System.Drawing.Size(35, 13);
            this.llFileSize.TabIndex = 8;
            this.llFileSize.Text = "0 / 0";
            // 
            // llOperation
            // 
            this.llOperation.AutoSize = true;
            this.llOperation.BackColor = System.Drawing.Color.Transparent;
            this.llOperation.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
            this.llOperation.Location = new System.Drawing.Point(12, 9);
            this.llOperation.Name = "llOperation";
            this.llOperation.Size = new System.Drawing.Size(68, 13);
            this.llOperation.TabIndex = 0;
            this.llOperation.Text = "llOperation";
            // 
            // btnPause
            // 
            this.btnPause.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPause.Location = new System.Drawing.Point(301, 177);
            this.btnPause.Name = "btnPause";
            this.btnPause.Size = new System.Drawing.Size(75, 23);
            this.btnPause.TabIndex = 11;
            this.btnPause.Text = "Pause";
            this.btnPause.UseVisualStyleBackColor = true;
            this.btnPause.Click += new System.EventHandler(this.btnPause_Click);
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Interval = 500;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // DlgReadingThreadProgress
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(558, 212);
            this.ControlBox = false;
            this.Controls.Add(this.btnPause);
            this.Controls.Add(this.llOperation);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.llFileCount);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.llFileSize);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.llElapsedTime);
            this.Controls.Add(this.btnBackground);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.llWorkStatus);
            this.Controls.Add(this.llProgress);
            this.Controls.Add(this.progressBar);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DlgReadingThreadProgress";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "DlgReadingThreadProgress";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.DlgReadingThreadProgress_FormClosed);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.DlgReadingThreadProgress_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        protected internal System.Windows.Forms.Label label2;
        protected internal System.Windows.Forms.Label llElapsedTime;
        protected System.Windows.Forms.Button btnBackground;
        protected internal System.Windows.Forms.Label label1;
        protected System.Windows.Forms.Button btnCancel;
        protected System.Windows.Forms.Label llWorkStatus;
        protected System.Windows.Forms.Label llProgress;
        protected System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label llFileCount;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label llFileSize;
        private System.Windows.Forms.Label llOperation;
        private System.Windows.Forms.Button btnPause;
        private System.Windows.Forms.Timer timer;
    }
}