using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Interop;
using ClaudePortable.App.Ui.ViewModels;

namespace ClaudePortable.App.Ui.Views;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        SourceInitialized += OnSourceInitialized;
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

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Opt the title bar into Windows' dark mode so it matches the app background.
        // Supported on Windows 10 1903+ (attribute 19) and Win10 2004+ / Win11 (attribute 20).
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int useDark = 1;
        if (DwmSetWindowAttribute(hwnd, 20, ref useDark, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(hwnd, 19, ref useDark, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int valueSize);
}
