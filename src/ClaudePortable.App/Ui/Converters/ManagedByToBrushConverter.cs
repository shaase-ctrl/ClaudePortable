using System.Globalization;
using System.Windows.Data;
using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.App.Ui.Converters;

public sealed class ManagedByToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ManagedBy managed)
        {
            return System.Windows.Application.Current?.TryFindResource("TextMutedBrush")
                ?? System.Windows.Media.Brushes.Gray;
        }

        var key = managed switch
        {
            ManagedBy.ClaudePortable => "SuccessBrush",
            ManagedBy.ForeignRelevant => "WarnBrush",
            _ => "TextMutedBrush",
        };
        return System.Windows.Application.Current?.TryFindResource(key)
            ?? System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
