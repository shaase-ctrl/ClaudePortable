using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using ClaudePortable.App.Ui.Services;
using ClaudePortable.Core.Abstractions;
using ClaudePortable.Core.Archive;
using ClaudePortable.Core.Backup;
using ClaudePortable.Core.Discovery;
using ClaudePortable.Core.Manifest;
using ClaudePortable.Core.Restore;
using ClaudePortable.Scheduler.Retention;
using ClaudePortable.Targets;
using ClaudePortable.Targets.Models;

namespace ClaudePortable.App.Ui.ViewModels;

[SupportedOSPlatform("windows")]
public sealed class MainViewModel : ViewModelBase
{
    private readonly TargetStore _store = new();
    private string _status = "Ready.";

    public ObservableCollection<TargetEntry> Targets { get; } = new();
    public ObservableCollection<BackupEntry> Backups { get; } = new();
    public ObservableCollection<DiscoveredClaudePath> ClaudePaths { get; } = new();
    public ObservableCollection<DiscoveredSyncClient> SyncClients { get; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Instance binding target for XAML.")]
    public ObservableCollection<string> LogEntries => UiLogSink.Instance.Entries;

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public AsyncRelayCommand BackupNowCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand AddTargetCommand { get; }
    public RelayCommand RemoveTargetCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }

    public TargetEntry? SelectedTarget { get; set; }
    public BackupEntry? SelectedBackup { get; set; }

    public MainViewModel()
    {
        foreach (var path in _store.Load())
        {
            Targets.Add(new TargetEntry(path));
        }
        if (Targets.Count == 0)
        {
            var suggested = SuggestDefaultTarget();
            if (suggested is not null)
            {
                Targets.Add(new TargetEntry(suggested));
                _store.Save(Targets.Select(t => t.Path).ToArray());
            }
        }

        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddTargetCommand = new RelayCommand(AddTarget);
        RemoveTargetCommand = new RelayCommand(RemoveTarget, () => SelectedTarget is not null);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => SelectedBackup is not null);

        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        ClaudePaths.Clear();
        foreach (var p in new WindowsPathDiscovery().Discover())
        {
            ClaudePaths.Add(p);
        }
        SyncClients.Clear();
        foreach (var c in new SyncClientDiscovery().Discover())
        {
            SyncClients.Add(c);
        }
        Backups.Clear();
        foreach (var t in Targets)
        {
            var target = new FolderTarget(t.Path);
            foreach (var b in target.ListBackups())
            {
                Backups.Add(new BackupEntry(t.Path, b.FileName, b.FullPath, b.SizeBytes, b.Manifest));
            }
        }
        await Task.CompletedTask.ConfigureAwait(true);
    }

    public async Task BackupNowAsync()
    {
        if (Targets.Count == 0)
        {
            Status = "No target configured.";
            return;
        }
        var target = Targets.First();
        UiLogSink.Instance.Append($"backup starting -> {target.Path}");
        Status = "Backing up...";
        try
        {
            var engine = new BackupEngine(new WindowsPathDiscovery(), new ZipArchiveWriter());
            var outcome = await engine.CreateBackupAsync(new BackupRequest(target.Path, RetentionTier.Daily)).ConfigureAwait(true);
            UiLogSink.Instance.Append($"backup done: {Path.GetFileName(outcome.ZipPath)} ({outcome.Manifest.FileCount} files, {outcome.Manifest.SizeBytes:N0} bytes)");
            var manager = new RetentionManager();
            var report = manager.Rotate(new FolderTarget(target.Path));
            UiLogSink.Instance.Append($"rotation: promoted={report.Promoted.Count} pruned={report.Pruned.Count}");
            Status = "Backup complete.";
        }
        catch (Exception ex)
        {
            UiLogSink.Instance.Append($"backup failed: {ex.Message}");
            Status = $"Backup failed: {ex.Message}";
        }
        await RefreshAsync().ConfigureAwait(true);
    }

    public void AddTarget()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a folder to write backups into",
        };
        if (dlg.ShowDialog() == true)
        {
            if (!Targets.Any(t => string.Equals(t.Path, dlg.FolderName, StringComparison.OrdinalIgnoreCase)))
            {
                Targets.Add(new TargetEntry(dlg.FolderName));
                _store.Save(Targets.Select(t => t.Path).ToArray());
                _ = RefreshAsync();
            }
        }
    }

    public void RemoveTarget()
    {
        if (SelectedTarget is null)
        {
            return;
        }
        Targets.Remove(SelectedTarget);
        _store.Save(Targets.Select(t => t.Path).ToArray());
        _ = RefreshAsync();
    }

    public async Task RestoreAsync()
    {
        if (SelectedBackup is null)
        {
            return;
        }
        var result = System.Windows.MessageBox.Show(
            $"Restore from:\n{SelectedBackup.FileName}\n\nExisting Claude data will be moved to <folder>_backup_<timestamp>. Proceed?",
            "Confirm restore",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }
        Status = "Restoring...";
        UiLogSink.Instance.Append($"restore starting: {SelectedBackup.FileName}");
        try
        {
            var engine = new RestoreEngine(new PathRewriter());
            var outcome = await engine.RestoreAsync(new RestoreRequest(SelectedBackup.FullPath, Confirmed: true)).ConfigureAwait(true);
            UiLogSink.Instance.Append($"restore complete. safety backups: {outcome.SafetyBackups.Count}");
            Status = $"Restore complete. Checklist: {outcome.PostRestoreChecklistPath}";
        }
        catch (Exception ex)
        {
            UiLogSink.Instance.Append($"restore failed: {ex.Message}");
            Status = $"Restore failed: {ex.Message}";
        }
        await RefreshAsync().ConfigureAwait(true);
    }

    private static string? SuggestDefaultTarget()
    {
        foreach (var c in new SyncClientDiscovery().Discover())
        {
            if (c.IsAvailable && Directory.Exists(c.Path))
            {
                var proposed = Path.Combine(c.Path, "ClaudePortable");
                Directory.CreateDirectory(proposed);
                return proposed;
            }
        }
        return null;
    }
}

public sealed record TargetEntry(string Path);

public sealed record BackupEntry(
    string TargetFolder,
    string FileName,
    string FullPath,
    long SizeBytes,
    BackupManifest? Manifest);
