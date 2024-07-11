using Igorary.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PdcMirrorIndexer
{
    public partial class DlgFolderProperties : DlgItemProperties
    {
        public DlgFolderProperties(FolderInDatabase folderInDatabase)
            : base(folderInDatabase) {
            InitializeComponent();
            pbIcon.Image = Win32.GetFolderIcon(folderInDatabase.Name, Win32.FileIconSize.Large).ToBitmap();
        }
        
    }
}

