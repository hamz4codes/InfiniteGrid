using System.Windows;

namespace InfiniteGrid
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow();

            mainWindow.WindowStyle = WindowStyle.None;
            mainWindow.WindowState = WindowState.Maximized;
            mainWindow.ShowInTaskbar = false;

            mainWindow.Show();
        }
    }
}