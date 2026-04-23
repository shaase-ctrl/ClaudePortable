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
    private const string DefaultBackupFolderName = "ClaudePortable";

    private readonly TargetStore _store = new();
    private string _status = "Ready.";
    private string _targetUserProfileOverride = string.Empty;
    private bool _ignoreVersionMismatch;

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

    /// <summary>
    /// Optional override for the restore destination root. When set, the
    /// RestoreEngine routes %USERPROFILE%, %APPDATA%, and %LOCALAPPDATA%
    /// under this path instead of the current user's home. Leave empty to
    /// use the process's %USERPROFILE%.
    /// </summary>
    public string TargetUserProfileOverride
    {
        get => _targetUserProfileOverride;
        set => SetField(ref _targetUserProfileOverride, value);
    }

    public bool IgnoreVersionMismatch
    {
        get => _ignoreVersionMismatch;
        set => SetField(ref _ignoreVersionMismatch, value);
    }

    public AsyncRelayCommand BackupNowCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand AddTargetCommand { get; }
    public RelayCommand RemoveTargetCommand { get; }
    public AsyncRelayCommand RestoreCommand { get; }
    public AsyncRelayCommand RestoreFromFileCommand { get; }
    public RelayCommand PickTargetProfileCommand { get; }

    public TargetEntry? SelectedTarget { get; set; }
    public BackupEntry? SelectedBackup { get; set; }

    public MainViewModel()
    {
        foreach (var path in _store.Load())
        {
            Targets.Add(new TargetEntry(path));
        }

        var discoveredCount = AutoDiscoverSyncedTargets();
        if (Targets.Count == 0)
        {
            var suggested = SuggestDefaultTarget();
            if (suggested is not null)
            {
                Targets.Add(new TargetEntry(suggested));
                _store.Save(Targets.Select(t => t.Path).ToArray());
            }
        }
        if (discoveredCount > 0)
        {
            _store.Save(Targets.Select(t => t.Path).ToArray());
            UiLogSink.Instance.Append($"auto-discovered {discoveredCount} ClaudePortable folder(s) from sync clients.");
        }

        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        AddTargetCommand = new RelayCommand(AddTarget);
        RemoveTargetCommand = new RelayCommand(RemoveTarget, () => SelectedTarget is not null);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => SelectedBackup is not null);
        RestoreFromFileCommand = new AsyncRelayCommand(RestoreFromFileAsync);
        PickTargetProfileCommand = new RelayCommand(PickTargetProfile);

        _ = RefreshAsync();
    }

    public void PickTargetProfile()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Pick the target user profile folder (C:\\Users\\<name>)",
            InitialDirectory = @"C:\Users",
        };
        if (dlg.ShowDialog() == true)
        {
            TargetUserProfileOverride = dlg.FolderName;
        }
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
                Backups.Add(new BackupEntry(t.Path, b.FileName, b.FullPath, b.SizeBytes, b.Manifest, b.IsCloudOnly));
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
        if (SelectedBackup.IsCloudOnly)
        {
            var proceed = System.Windows.MessageBox.Show(
                $"'{SelectedBackup.FileName}' is currently cloud-only (placeholder). The restore will trigger a download and may take several minutes depending on backup size.\n\nProceed?",
                "Cloud-only backup",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Information);
            if (proceed != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }
        await RunRestoreAsync(SelectedBackup.FullPath, SelectedBackup.FileName).ConfigureAwait(true);
    }

    public async Task RestoreFromFileAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pick a ClaudePortable backup ZIP",
            Filter = "ClaudePortable backups (*.zip)|claude-backup_*.zip|All ZIP files (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }
        await RunRestoreAsync(dlg.FileName, Path.GetFileName(dlg.FileName)).ConfigureAwait(true);
    }

    private async Task RunRestoreAsync(string zipPath, string displayName)
    {
        var confirm = System.Windows.MessageBox.Show(
            $"Restore from:\n{displayName}\n\nExisting Claude data will be moved to <folder>_backup_<timestamp> where possible, or files will be overlaid if the target is a Store-app reparse point. Nothing is deleted.\n\nProceed?",
            "Confirm restore",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        if (!await EnsureClaudeDesktopClosedAsync().ConfigureAwait(true))
        {
            Status = "Restore cancelled - Claude Desktop still running.";
            return;
        }

        Status = "Restoring...";
        UiLogSink.Instance.Append($"restore starting: {displayName}");
        try
        {
            var engine = new RestoreEngine(new PathRewriter());
            var targetOverride = string.IsNullOrWhiteSpace(TargetUserProfileOverride) ? null : TargetUserProfileOverride.Trim();
            if (targetOverride is not null)
            {
                UiLogSink.Instance.Append($"using override target user profile: {targetOverride}");
            }
            var outcome = await engine.RestoreAsync(
                new RestoreRequest(
                    zipPath,
                    TargetUserProfile: targetOverride,
                    Confirmed: true,
                    IgnoreVersionMismatch: IgnoreVersionMismatch))
                .ConfigureAwait(true);

            foreach (var report in outcome.PerTargetReports)
            {
                UiLogSink.Instance.Append(
                    $"  {report.ArchivePrefix}: {report.FilesWritten} files -> {report.TargetFolder} " +
                    $"(safety-backup: {(report.SafetyBackedUp ? report.SafetyBackupPath : "overlay / skipped")})");
                foreach (var warning in report.Warnings)
                {
                    UiLogSink.Instance.Append($"    warning: {warning}");
                }
            }

            var totalWarnings = outcome.PerTargetReports.Sum(r => r.Warnings.Count);
            UiLogSink.Instance.Append($"restore complete. safety backups: {outcome.SafetyBackups.Count}, warnings: {totalWarnings}");
            UiLogSink.Instance.Append($"version gate: {outcome.VersionGate.Level} - {outcome.VersionGate.Message}");
            Status = totalWarnings == 0
                ? $"Restore complete. Checklist: {outcome.PostRestoreChecklistPath}"
                : $"Restore complete with {totalWarnings} warning(s). See Logs tab.";
        }
        catch (Exception ex)
        {
            UiLogSink.Instance.Append($"restore failed: {ex.Message}");
            Status = $"Restore failed: {ex.Message}";
        }
        await RefreshAsync().ConfigureAwait(true);
    }

    private static async Task<bool> EnsureClaudeDesktopClosedAsync()
    {
        var running = System.Diagnostics.Process.GetProcessesByName("Claude");
        if (running.Length == 0)
        {
            return true;
        }

        var result = System.Windows.MessageBox.Show(
            $"Claude Desktop is running (PID {string.Join(", ", running.Select(p => p.Id))}). " +
            "Its open file handles will cause 'Access denied' errors during restore.\n\n" +
            "Close Claude Desktop now? (Yes = close for me, No = cancel restore)",
            "Close Claude Desktop?",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return false;
        }

        foreach (var p in running)
        {
            try
            {
                p.CloseMainWindow();
                if (!p.WaitForExit(5000))
                {
                    p.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }

        // Give Windows a beat to release handles + flush pending writes.
        await Task.Delay(1500).ConfigureAwait(true);
        return System.Diagnostics.Process.GetProcessesByName("Claude").Length == 0;
    }

    /// <summary>
    /// For every detected sync client (OneDrive, Dropbox, GDrive Desktop, ...)
    /// check whether the user already has a folder called "ClaudePortable"
    /// inside it. If yes and it is not already tracked, add it. This is how a
    /// second machine (laptop) sees backups created on the first machine
    /// (workstation) without the user having to configure anything.
    /// </summary>
    private int AutoDiscoverSyncedTargets()
    {
        var added = 0;
        foreach (var client in new SyncClientDiscovery().Discover())
        {
            if (!client.IsAvailable || string.IsNullOrEmpty(client.Path) || !Directory.Exists(client.Path))
            {
                continue;
            }
            var candidate = Path.Combine(client.Path, DefaultBackupFolderName);
            if (!Directory.Exists(candidate))
            {
                continue;
            }
            if (Targets.Any(t => string.Equals(t.Path, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            Targets.Add(new TargetEntry(candidate));
            added++;
        }
        return added;
    }

    /// <summary>
    /// Fallback when no target is configured and no existing ClaudePortable
    /// folder was found: propose one inside the first available sync client.
    /// </summary>
    private static string? SuggestDefaultTarget()
    {
        foreach (var c in new SyncClientDiscovery().Discover())
        {
            if (c.IsAvailable && Directory.Exists(c.Path))
            {
                var proposed = Path.Combine(c.Path, DefaultBackupFolderName);
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
    BackupManifest? Manifest,
    bool IsCloudOnly)
{
    public string StatusLabel => IsCloudOnly
        ? "Cloud-only"
        : Manifest is null
            ? "Unreadable"
            : "Synced";
}
