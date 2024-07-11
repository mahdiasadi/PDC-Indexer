using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace PdcMirrorIndexer
{
    class VolumeReadingThread
    {

        List<string> excludedElements;
        string drive;
        DiscInDatabase discToScan;
        DiscInDatabase discToReplace;
        LogicalFolder[] logicalFolders;

        ProgressInfo progressInfo = null;

        public ProgressInfo ProgressInfo {
            get { return progressInfo; }
        }

        string currentItemName;

        public string CurrentItemName {
            get { return currentItemName; }
        }

        public static void SetCurrentItemName(string val)
        {

        }
        static  string operation;

        public string Operation {
            get { return operation; }
        }

        public static void SetOperation(string val)
        {

        }
        public VolumeReadingThread(string drive, List<string> excludedElements, DiscInDatabase discToScan, DiscInDatabase discToReplace, LogicalFolder[] logicalFolders) {
            this.excludedElements = excludedElements;
            this.drive = drive;
            this.discToScan = discToScan;
            this.discToReplace = discToReplace;
            this.logicalFolders = logicalFolders;
        }

        internal void Start() {
            startCalculatingProgressInfo();
            startReadingVolume();
        }

        #region Calculating ProgressInfo
        ManualResetEvent _event = new ManualResetEvent(true);
        Thread calculatingProgressInfoThread = null;
        private void startCalculatingProgressInfo() {
            calculatingProgressInfoThread = new Thread(new ThreadStart(calculateProgressInfo));
            calculatingProgressInfoThread.Name = "CalculatingProgressInfo";
            calculatingProgressInfoThread.Start();
        }

        private void calculateProgressInfo() {
            long fileCount = 0;
            long fileSizeSum = 0;
            try {
                calculateProgressInfo(drive, excludedElements, ref fileCount, ref fileSizeSum);
                lock (this) {
                    progressInfo = new ProgressInfo(fileCount, fileSizeSum);
                }
            }
            catch (ThreadAbortException) {
                progressInfo = null;
            }
        }

        private void calculateProgressInfo(string calculatingFolder, List<string> calculatingExcludedElements, ref long fileCount, ref long fileSizeSum) {
            try {
                System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(calculatingFolder);
                System.IO.DirectoryInfo[] subFolders = di.GetDirectories();
                foreach (System.IO.DirectoryInfo subFolder in subFolders) {
                    string subFolderName = subFolder.FullName.ToLower();
                    if (!calculatingExcludedElements.Contains(subFolderName)) {
                        calculateProgressInfo(subFolderName, calculatingExcludedElements, ref fileCount, ref fileSizeSum);
                    }
                }

                System.IO.FileInfo[] filesInFolder = di.GetFiles();
                foreach (System.IO.FileInfo fileInFolder in filesInFolder) {
                    if (!calculatingExcludedElements.Contains(fileInFolder.FullName.ToLower())) {
                        fileCount++;
                        fileSizeSum += fileInFolder.Length;
                    }
                }
            }
            catch (UnauthorizedAccessException) {
                // eat the exception
            }
        }

        #endregion

        #region Reading Volume

        Thread readingVolumeThread = null;
        private void startReadingVolume() {
            readingVolumeThread = new Thread(new ThreadStart(readVolume));
            readingVolumeThread.Name = "ReadingVolume";
            readingVolumeThread.Start();
        }

       static long runningFileCount = 0;

        public long RunningFileCount {
            get { return runningFileCount; }
        }

        public static void AddRunningFileCount()
        {
            runningFileCount = runningFileCount + 1;
        }
       static long runningFileSize = 0;

        public long RunningFileSize {
            get { return runningFileSize; }
        }
        public static void AddRunningFileSize(long val)
        {
            runningFileSize = val + runningFileSize;
        }
        private event EventHandler<EventArgs> processCompleted;

        public event EventHandler<EventArgs> ProcessCompleted {
            add {
                processCompleted += value;
            }
            remove {
                processCompleted -= value;
            }
        }

        private event EventHandler<EventArgs> processStopped;

        public event EventHandler<EventArgs> ProcessStopped {
            add {
                processStopped += value;
            }
            remove {
                processStopped -= value;
            }
        }

        private void readVolume() {
            try {
                bool useSize = Properties.Settings.Default.ComputeCrc;
                try {
                    if (!excludedElements.Contains(drive.ToLower())) {
                        try {
                            discToScan.ReadFromDrive(drive, excludedElements, ref runningFileCount, ref runningFileSize, ref currentItemName, ref operation, useSize, discToReplace);
                            FrmMain.Database.AddDisc(discToScan);
                        }
                        catch {
                            // jezeli wystapił blad podczas  a jednocześnie przekopiowaliśmy z discToReplace foldery logiczne, to teraz wywal ze wszystkich folderów
                            if (discToReplace != null)
                                discToScan.RemoveFromLogicalFolders();
                            throw;
                        }
                        discToScan.ApplyFolders(logicalFolders, false);
                        if (discToReplace != null)
                            discToReplace.RemoveFromDatabase(FrmMain.Database);
                        processCompleted(this, EventArgs.Empty);
                    }
                }
                finally {
                    try
                    {
                       // calculatingProgressInfoThread.Abort();  
                        calculatingProgressInfoThread.Interrupt();
                    }
                    catch (Exception ex)
                    {

                        throw ex;
                    }

                    processStopped(this, EventArgs.Empty);
                }
            }
            catch (ThreadAbortException) {
            }
        }

        #endregion

        public bool Stopped {
            get {
                return (readingVolumeThread == null) || (readingVolumeThread.ThreadState == ThreadState.Stopped);
            }
        }

        internal void Stop() {
            if (!Stopped) {
                if (readingVolumeThread.ThreadState == ThreadState.Suspended)
                    _event.Set();
                //   readingVolumeThread.Resume();
                readingVolumeThread.Interrupt();
            }
        }

        internal void Resume() {
            _event.Set();
          //  readingVolumeThread.Resume();
        }

        internal void Suspend() {
            _event.Reset();
          //  readingVolumeThread.Suspend();
        }
    }
}
