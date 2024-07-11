using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace PdcMirrorIndexer
{
    public partial class ShowFull : Form
    {
        string _path;
        public ShowFull(string path)
        {
            InitializeComponent();
            _path = path;
        }
       
        private void ShowFull_Load(object sender, EventArgs e)
        {
            pictureBox1.Width = this.Width;
            pictureBox1.Height = this.Height;
            button1.Left = this.Right - 200; 
            switch (LeftRightMid.Right(_path, 3))
            {
                case "jpg":
                    pictureBox1.Image = new Bitmap(_path);
                    break;

            } 
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
            this.Close();
        }
    }
}
