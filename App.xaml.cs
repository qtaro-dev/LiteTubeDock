using System.Windows;
using LiteTubeDock.Constants;
using LiteTubeDock.Services;

namespace LiteTubeDock;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startupOptions = StartupArgumentService.Parse(e.Args);
        if (startupOptions.ShowHelp)
        {
            System.Windows.MessageBox.Show(
                AppConstants.StartupArgumentHelpText,
                AppConstants.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow(startupOptions);
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
