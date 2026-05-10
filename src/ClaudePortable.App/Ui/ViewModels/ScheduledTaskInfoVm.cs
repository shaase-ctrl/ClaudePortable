using System.Globalization;
using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.App.Ui.ViewModels;

public sealed class ScheduledTaskInfoVm : ViewModelBase
{
    public ScheduledTaskInfo Info { get; }

    public ScheduledTaskInfoVm(
        ScheduledTaskInfo info,
        Func<ScheduledTaskInfoVm, Task> runNow,
        Func<ScheduledTaskInfoVm, Task> disable,
        Func<ScheduledTaskInfoVm, Task> enable,
        Func<ScheduledTaskInfoVm, Task> delete,
        Func<ScheduledTaskInfoVm, Task> viewXml)
    {
        ArgumentNullException.ThrowIfNull(info);
        Info = info;
        RunNowCommand = new AsyncRelayCommand(() => runNow(this));
        DisableCommand = new AsyncRelayCommand(() => disable(this), () => !IsDisabled);
        EnableCommand = new AsyncRelayCommand(() => enable(this), () => IsDisabled);
        DeleteCommand = new AsyncRelayCommand(() => delete(this));
        ViewXmlCommand = new AsyncRelayCommand(() => viewXml(this));
    }

    public AsyncRelayCommand RunNowCommand { get; }
    public AsyncRelayCommand DisableCommand { get; }
    public AsyncRelayCommand EnableCommand { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand ViewXmlCommand { get; }

    public string Name => Info.Name;
    public string FullName => Info.FullName;
    public string FolderPath => Info.FolderPath;
    public string State => Info.State;
    public string TriggerSummary => Info.TriggerSummary;
    public string? Author => Info.Author;
    public string ActionDisplay => string.IsNullOrEmpty(Info.Action.Arguments)
        ? Info.Action.Executable
        : $"{Info.Action.Executable} {Info.Action.Arguments}";
    public ManagedBy ManagedBy => Info.ManagedBy;
    public string ManagedByLabel => Info.ManagedBy switch
    {
        ManagedBy.ClaudePortable => "ClaudePortable",
        ManagedBy.ForeignRelevant => "Claude-related",
        _ => "Other",
    };

    public string NextRunDisplay => Info.NextRunTime is { } t
        ? t.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        : "-";

    public string LastRunDisplay => Info.LastRunTime is { } t
        ? Info.LastResult is { } r
            ? $"{t.LocalDateTime:yyyy-MM-dd HH:mm} (exit {r})"
            : t.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
        : "-";

    public bool IsDisabled => string.Equals(Info.State, "Deaktiviert", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Info.State, "Disabled", StringComparison.OrdinalIgnoreCase);
}
