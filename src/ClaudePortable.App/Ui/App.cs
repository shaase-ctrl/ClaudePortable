using System.Runtime.Versioning;
using System.Windows;
using ClaudePortable.App.Ui.Views;

namespace ClaudePortable.App.Ui;

[SupportedOSPlatform("windows")]
public static class App
{
    public static int RunGui()
    {
        var app = new System.Windows.Application
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
        };

        var tray = new TrayIcon();
        var mainWindow = new MainWindow();
        tray.OpenRequested += (_, _) => mainWindow.ShowAndActivate();
        tray.QuitRequested += (_, _) =>
        {
            tray.Dispose();
            app.Shutdown();
        };
        mainWindow.Closing += (_, args) =>
        {
            args.Cancel = true;
            mainWindow.Hide();
        };

        mainWindow.Show();
        return app.Run();
    }
}
