using System.Runtime.Versioning;

namespace ClaudePortable.App.Ui;

[SupportedOSPlatform("windows")]
public sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu;

    public event EventHandler? OpenRequested;
    public event EventHandler? QuitRequested;

    public TrayIcon()
    {
        _menu = new System.Windows.Forms.ContextMenuStrip();
        _menu.Items.Add("Open ClaudePortable", image: null, OnOpen);
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add("Quit", image: null, OnQuit);

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "ClaudePortable",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _icon.DoubleClick += (s, e) => OpenRequested?.Invoke(s, e);
    }

    private void OnOpen(object? sender, EventArgs e) => OpenRequested?.Invoke(sender, e);
    private void OnQuit(object? sender, EventArgs e) => QuitRequested?.Invoke(sender, e);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
