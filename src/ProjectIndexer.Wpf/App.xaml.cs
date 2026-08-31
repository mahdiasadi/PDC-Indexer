using System.Windows.Threading;

namespace ProjectIndexer.Wpf;

public partial class App : System.Windows.Application
{
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            System.Windows.MessageBox.Show(
                $"Unhandled exception: {((Exception)e.ExceptionObject).Message}\n{((Exception)e.ExceptionObject).StackTrace}",
                "ProjectIndexer Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (_, e) =>
        {
            System.Windows.MessageBox.Show(
                $"Dispatcher exception: {e.Exception.Message}\n{e.Exception.StackTrace}",
                "ProjectIndexer Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            e.Handled = true;
        };
    }
}

