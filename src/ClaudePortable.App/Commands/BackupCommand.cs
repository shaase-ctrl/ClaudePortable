using System.CommandLine;
using System.Runtime.Versioning;
using ClaudePortable.Core.Archive;
using ClaudePortable.Core.Backup;
using ClaudePortable.Core.Discovery;
using ClaudePortable.Core.Manifest;
using ClaudePortable.Scheduler.Retention;
using ClaudePortable.Targets;

namespace ClaudePortable.App.Commands;

[SupportedOSPlatform("windows")]
public static class BackupCommand
{
    public static Command Build()
    {
        var toOption = new Option<DirectoryInfo>(
            aliases: new[] { "--to", "-t" },
            description: "Destination folder for the backup ZIP.")
        {
            IsRequired = true,
        };

        var tierOption = new Option<RetentionTier>(
            aliases: new[] { "--tier" },
            description: "Retention tier to mark this backup with.",
            getDefaultValue: () => RetentionTier.Daily);

        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run" },
            description: "Plan the backup without writing any data.",
            getDefaultValue: () => false);

        var noRotateOption = new Option<bool>(
            aliases: new[] { "--no-rotate" },
            description: "Skip the post-backup retention rotation.",
            getDefaultValue: () => false);

        var cmd = new Command("backup", "Create a backup ZIP from local Claude data.")
        {
            toOption,
            tierOption,
            dryRunOption,
            noRotateOption,
        };

        cmd.SetHandler(async (toValue, tier, dryRun, noRotate) =>
        {
            var target = new FolderTarget(toValue.FullName);
            try
            {
                target.EnsureWritable();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"error: destination folder '{toValue.FullName}' is not writable: {ex.Message}");
                Environment.ExitCode = 2;
                return;
            }

            if (target.HasPendingCloudUploadFlags())
            {
                Console.Error.WriteLine($"warning: destination '{target.FolderPath}' has cloud pending-upload flags. Sync client may be behind.");
            }

            var engine = new BackupEngine(new WindowsPathDiscovery(), new ZipArchiveWriter());
            var outcome = await engine.CreateBackupAsync(new(target.FolderPath, tier, dryRun)).ConfigureAwait(false);

            if (dryRun)
            {
                Console.WriteLine($"[dry-run] would create {outcome.ZipPath}");
                Console.WriteLine($"[dry-run] files (before manifest+checklist): {outcome.Manifest.FileCount}");
                return;
            }

            Console.WriteLine($"created: {outcome.ZipPath}");
            Console.WriteLine($"files:   {outcome.Manifest.FileCount}");
            Console.WriteLine($"bytes:   {outcome.Manifest.SizeBytes:N0}");
            Console.WriteLine($"sha256:  {outcome.Manifest.Sha256}");

            if (noRotate)
            {
                return;
            }

            try
            {
                var manager = new RetentionManager();
                var report = manager.Rotate(target);
                Console.WriteLine($"rotation: promoted={report.Promoted.Count} pruned={report.Pruned.Count} -> daily={report.DailyAfter} weekly={report.WeeklyAfter} monthly={report.MonthlyAfter}");
                foreach (var item in report.Promoted)
                {
                    Console.WriteLine($"  promoted: {item}");
                }
                foreach (var item in report.Pruned)
                {
                    Console.WriteLine($"  pruned:   {item}");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException)
            {
                Console.Error.WriteLine($"warning: rotation failed: {ex.Message}");
            }
        }, toOption, tierOption, dryRunOption, noRotateOption);

        return cmd;
    }
}
