using System.Runtime.Versioning;
using System.Windows;
using ClaudePortable.App.Ui.ViewModels;

namespace ClaudePortable.App.Ui.Views;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    public void ShowAndActivate()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Show();
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }
}
