namespace PdcMirrorIndexer.Core
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.


            //ApplicationConfiguration.Initialize();
            //Application.Run(new Form1());

            System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
            Application.EnableVisualStyles();
            //FrmMain f;
            //using (FrmSplash splash = new FrmSplash()) {
            //    splash.Show();
            //    splash.Refresh();
            //    f = new FrmMain(splash.llStatus);
            //}
            //Application.Run(f);
            FrmMain.Instance = new FrmMain();
            Application.Run(FrmMain.Instance);
        }
    }
}