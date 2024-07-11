using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Igorary.Forms.Forms
{
    public partial class FormExceptionHandler : FormDialogList
    {

        public static void HandleException(Exception ex, string emailAddress) {
            if (MessageBox.Show(string.Format("Wystąpił błąd: {0}. \nCzy wyświetlić szczegóły błędu?", ex.Message), Application.ProductName, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes) {
                using (FormExceptionHandler form = new FormExceptionHandler(emailAddress)) {
                    form.tbErrorDetails.Text = ex.ToString();
                    form.ShowDialog();
                }
            }
        }

        public FormExceptionHandler(string emailAddress) {
            InitializeComponent();
            EmailAddress = emailAddress;
        }

        public string EmailAddress { get; private set; }

        private void btnSend_Click(object sender, EventArgs e) {
            Process.Start(string.Format("mailto:{0}?subject=Błąd Sfinks Publisher&body={1}", EmailAddress, tbErrorDetails.Text));
        }
    }
}
