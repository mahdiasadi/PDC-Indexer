using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PdcMirrorIndexer
{
    public partial class DlgReadingThreadProgress : Form
    {

        DateTime started;
        string title;
        bool useSize;
        bool dontShowAgain = false;
        VolumeReadingThread volumeReadingThread;

        internal DlgReadingThreadProgress(string title, string currentStatus, bool useSize, VolumeReadingThread volumeReadingThread) {
            InitializeComponent();
            this.title = title;
            this.useSize = useSize;
            this.volumeReadingThread = volumeReadingThread;
            started = DateTime.Now;
            if (currentStatus != null)
                llWorkStatus.Text = currentStatus;
            updateTitle();
        }

        DateTime startShowing = DateTime.Now;
        public void StartShowing(TimeSpan wait) {
            startShowing = startShowing + wait;
        }

        private void btnCancel_Click(object sender, EventArgs e) {
            if (MessageBox.Show("Are you sure to cancel this operation?", ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                volumeReadingThread.Stop();
        }

        private void btnBackground_Click(object sender, EventArgs e) {
            dontShowAgain = true;
            Hide();
            FrmMain.Instance.SetToBackground(Text);
        }

        private void DlgReadingThreadProgress_FormClosed(object sender, FormClosedEventArgs e) {
            // cleanup
            if (!FrmMain.Instance.Enabled) {
                FrmMain.Instance.Enabled = true;
                volumeReadingThread.Stop();
            }
            if (!volumeReadingThread.Stopped)
                volumeReadingThread.Stop();
        }

        bool paused = false;

        private void updateTitle() {
            Text = title + (paused ? " [paused]" : string.Empty);
        }

        private void btnPause_Click(object sender, EventArgs e) {
            Paused = !Paused;
        }

        protected bool Paused {
            get { return paused; }
            set {
                paused = value;
                if (paused) {
                    btnPause.Text = "Resume";
                    volumeReadingThread.Suspend();
                }
                else {
                    btnPause.Text = "Pause";
                    volumeReadingThread.Resume();
                }
                updateTitle();
            }
        }

        private void updateStatus() {
            if (!dontShowAgain && !Visible && (startShowing <= DateTime.Now)) {
                // FrmMain.Instance.Enabled = false;
                Show(FrmMain.Instance);
            }
            if (Visible) {
                long runningFileCount = volumeReadingThread.RunningFileCount;
                long runningFileSize = volumeReadingThread.RunningFileSize;
                string currentItemName = volumeReadingThread.CurrentItemName;
                string operation = volumeReadingThread.Operation;
                int progress = 0; // 0..100
                if (volumeReadingThread.ProgressInfo != null) {
                    if (useSize) {
                        if (volumeReadingThread.ProgressInfo.FileSizeSum != 0)
                            progress = (int)(runningFileSize * 100 / volumeReadingThread.ProgressInfo.FileSizeSum);
                    }
                    else
                        if (volumeReadingThread.ProgressInfo.FileCount != 0)
                            progress = (int)(runningFileCount * 100 / volumeReadingThread.ProgressInfo.FileCount);
                    llFileCount.Text = runningFileCount + " / " + volumeReadingThread.ProgressInfo.FileCount;
                    llFileSize.Text = CustomConvert.ToKBAndB(runningFileSize) + " / " + CustomConvert.ToKBAndB(volumeReadingThread.ProgressInfo.FileSizeSum);
                }
                else {
                    llFileCount.Text = runningFileCount.ToString();
                    llFileSize.Text = CustomConvert.ToKBAndB(runningFileSize);
                }
                if (progress > 100)
                    progress = 100;

                progressBar.Value = progress;
                llProgress.Text = progress.ToString() + "%";
                if (currentItemName != null)
                    llWorkStatus.Text = currentItemName;

                TimeSpan elapsed = DateTime.Now - started;
                if (progress > 0) {
                    TimeSpan estimated = new TimeSpan(0, 0, (int)(elapsed.TotalSeconds / progress * 100));
                    llElapsedTime.Text = timeToString(elapsed) + " / " + timeToString(estimated);
                }
                else
                    llElapsedTime.Text = timeToString(elapsed);

                llOperation.Text = operation;
            }
        }

        private static string timeToString(TimeSpan time) {
            return time.Hours + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00");
        }

        private void timer_Tick(object sender, EventArgs e) {
            updateStatus();
        }

        private void DlgReadingThreadProgress_KeyDown(object sender, KeyEventArgs e) {
            if(e.Alt)
                if (e.KeyCode == Keys.F4) {
                    e.Handled = true;
                    btnCancel_Click(sender, e);
                }
        }

    }
}
