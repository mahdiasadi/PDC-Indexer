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
    public partial class DlgFileProperties : DlgItemProperties
    {

        public DlgFileProperties(FileInDatabase fileInDatabase)
            : base(fileInDatabase) {
            InitializeComponent();
            if (fileInDatabase.Crc != 0)
                llCrc.Text = fileInDatabase.Crc.ToString("X");
            else
                llCrc.Text = "(not computed)";
            llFileSize.Text = CustomConvert.ToKBAndB(fileInDatabase.Length);
            llFileDescription.Text = string.IsNullOrEmpty(fileInDatabase.FileDescription) ? "(empty)" : fileInDatabase.FileDescription;
            llFileVersion.Text = string.IsNullOrEmpty(fileInDatabase.FileVersion) ? "(empty)" : fileInDatabase.FileVersion;
            pbIcon.Image = Win32.GetFileIcon(fileInDatabase.Name,Win32.FileIconSize.Large).ToBitmap();
        }

        private void tcDescription_SelectedIndexChanged(object sender, EventArgs e)
        {
            
         string orginpath=   ClassGlobal.Path +ClassGlobal.Gudid+ @"";
         orginpath = orginpath + tbPath.Text  + tbItemName.Text ;

         switch (LeftRightMid.Right( orginpath,3))
            {
                case "jpg":
                    pictureBox1.Image = new Bitmap(orginpath);
                    break;
            
            } 
 

        }

        private void button1_Click(object sender, EventArgs e)
        {

            string orginpath = ClassGlobal.Path + ClassGlobal.Gudid + @"";
            orginpath = orginpath + tbPath.Text + tbItemName.Text;

            ShowFull frm = new ShowFull(orginpath);
            frm.ShowDialog();
        }
        
    }
}

