using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using ClaudePortable.App.Ui.Services;
using ClaudePortable.Scheduler.Scheduling;

namespace ClaudePortable.App.Ui.ViewModels;

public enum ScheduledTasksFilter
{
    Relevant,
    Managed,
    All,
}

[SupportedOSPlatform("windows")]
public sealed class ScheduledTasksViewModel : ViewModelBase
{
    private readonly TaskSchedulerInstaller _installer;
    private readonly List<ScheduledTaskInfo> _allTasks = new();
    private ScheduledTasksFilter _filter = ScheduledTasksFilter.Relevant;
    private bool _isLoading;
    private string _statusLine = "Click Refresh to load scheduled tasks.";

    public ScheduledTasksViewModel()
        : this(new TaskSchedulerInstaller())
    {
    }

    public ScheduledTasksViewModel(TaskSchedulerInstaller installer)
    {
        _installer = installer;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SetFilterAllCommand = new RelayCommand(() => Filter = ScheduledTasksFilter.All);
        SetFilterManagedCommand = new RelayCommand(() => Filter = ScheduledTasksFilter.Managed);
        SetFilterRelevantCommand = new RelayCommand(() => Filter = ScheduledTasksFilter.Relevant);
    }

    public ObservableCollection<ScheduledTaskInfoVm> Tasks { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand SetFilterAllCommand { get; }
    public RelayCommand SetFilterManagedCommand { get; }
    public RelayCommand SetFilterRelevantCommand { get; }

    public ScheduledTasksFilter Filter
    {
        get => _filter;
        set
        {
            if (SetField(ref _filter, value))
            {
                Raise(nameof(IsFilterAll));
                Raise(nameof(IsFilterManaged));
                Raise(nameof(IsFilterRelevant));
                ApplyFilter();
            }
        }
    }

    public bool IsFilterAll
    {
        get => _filter == ScheduledTasksFilter.All;
        set
        {
            if (value)
            {
                Filter = ScheduledTasksFilter.All;
            }
        }
    }

    public bool IsFilterManaged
    {
        get => _filter == ScheduledTasksFilter.Managed;
        set
        {
            if (value)
            {
                Filter = ScheduledTasksFilter.Managed;
            }
        }
    }

    public bool IsFilterRelevant
    {
        get => _filter == ScheduledTasksFilter.Relevant;
        set
        {
            if (value)
            {
                Filter = ScheduledTasksFilter.Relevant;
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetField(ref _isLoading, value);
    }

    public string StatusLine
    {
        get => _statusLine;
        set => SetField(ref _statusLine, value);
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        StatusLine = "Loading scheduled tasks...";
        try
        {
            var infos = await Task.Run(() => _installer.EnumerateAsync(CancellationToken.None)).ConfigureAwait(true);
            _allTasks.Clear();
            _allTasks.AddRange(infos);
            ApplyFilter();
            var managed = _allTasks.Count(t => t.ManagedBy == ManagedBy.ClaudePortable);
            var relevant = _allTasks.Count(t => t.ManagedBy == ManagedBy.ForeignRelevant);
            StatusLine = $"{_allTasks.Count} task(s) total · {managed} managed by ClaudePortable · {relevant} Claude-related";
        }
        catch (Exception ex)
        {
            StatusLine = $"Failed to enumerate tasks: {ex.Message}";
            UiLogSink.Instance.Append($"schedule enumerate failed: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        Tasks.Clear();
        IEnumerable<ScheduledTaskInfo> filtered = _filter switch
        {
            ScheduledTasksFilter.All => _allTasks,
            ScheduledTasksFilter.Managed => _allTasks.Where(t => t.ManagedBy == ManagedBy.ClaudePortable),
            _ => _allTasks.Where(t => t.ManagedBy is ManagedBy.ClaudePortable or ManagedBy.ForeignRelevant),
        };
        foreach (var info in filtered.OrderBy(t => t.ManagedBy).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            Tasks.Add(new ScheduledTaskInfoVm(
                info,
                RunNowAsync,
                DisableAsync,
                EnableAsync,
                DeleteAsync,
                ViewXmlAsync));
        }
    }

    private async Task RunNowAsync(ScheduledTaskInfoVm row)
    {
        var exit = await _installer.RunNowAsync(row.FullName).ConfigureAwait(true);
        UiLogSink.Instance.Append(exit == 0
            ? $"schedule run: '{row.FullName}'"
            : $"schedule run failed: '{row.FullName}' exit={exit}");
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task DisableAsync(ScheduledTaskInfoVm row)
    {
        var ok = System.Windows.MessageBox.Show(
            $"Disable scheduled task '{row.FullName}'?\n\nThe task will no longer trigger, but can be re-enabled later. Nothing is deleted.",
            "Disable task",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;
        if (!ok)
        {
            return;
        }
        var exit = await _installer.DisableAsync(row.FullName).ConfigureAwait(true);
        UiLogSink.Instance.Append(exit == 0
            ? $"schedule disable: '{row.FullName}'"
            : $"schedule disable failed: '{row.FullName}' exit={exit}");
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task EnableAsync(ScheduledTaskInfoVm row)
    {
        var exit = await _installer.EnableAsync(row.FullName).ConfigureAwait(true);
        UiLogSink.Instance.Append(exit == 0
            ? $"schedule enable: '{row.FullName}'"
            : $"schedule enable failed: '{row.FullName}' exit={exit}");
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task DeleteAsync(ScheduledTaskInfoVm row)
    {
        var ok = System.Windows.MessageBox.Show(
            $"Delete scheduled task '{row.FullName}'?\n\nThis is permanent. To re-create it you would have to re-install via ClaudePortable or recreate the XML manually.",
            "Delete task",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
        if (!ok)
        {
            return;
        }
        var exit = await _installer.DeleteAsync(row.FullName).ConfigureAwait(true);
        UiLogSink.Instance.Append(exit == 0
            ? $"schedule delete: '{row.FullName}'"
            : $"schedule delete failed: '{row.FullName}' exit={exit}");
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task ViewXmlAsync(ScheduledTaskInfoVm row)
    {
        var (exit, xml) = await _installer.GetTaskXmlAsync(row.FullName).ConfigureAwait(true);
        if (exit != 0 || string.IsNullOrWhiteSpace(xml))
        {
            System.Windows.MessageBox.Show($"Could not read XML for '{row.FullName}' (exit {exit}).", "View XML", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }
        try
        {
            System.Windows.Clipboard.SetText(xml);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Clipboard occasionally throws on contested access; the XML is shown in the dialog regardless.
        }
        System.Windows.MessageBox.Show(
            xml.Length > 4000 ? xml[..4000] + "\n...(truncated, full XML copied to clipboard)" : xml,
            $"XML: {row.FullName}",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }
}
