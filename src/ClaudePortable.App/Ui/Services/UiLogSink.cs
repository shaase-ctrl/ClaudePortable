using System.Collections.ObjectModel;
using System.Globalization;

namespace ClaudePortable.App.Ui.Services;

public sealed class UiLogSink
{
    private static readonly Lazy<UiLogSink> _instance = new(() => new UiLogSink());
    public static UiLogSink Instance => _instance.Value;

    public ObservableCollection<string> Entries { get; } = new();

    public void Append(string line)
    {
        var stamped = $"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}  {line}";
        Entries.Add(stamped);
        while (Entries.Count > 500)
        {
            Entries.RemoveAt(0);
        }
    }
}
