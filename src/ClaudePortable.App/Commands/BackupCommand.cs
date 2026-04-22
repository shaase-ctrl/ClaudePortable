using System.CommandLine;
using System.Runtime.Versioning;
using ClaudePortable.Core.Archive;
using ClaudePortable.Core.Backup;
using ClaudePortable.Core.Discovery;
using ClaudePortable.Core.Manifest;
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

        var cmd = new Command("backup", "Create a backup ZIP from local Claude data.")
        {
            toOption,
            tierOption,
            dryRunOption,
        };

        cmd.SetHandler(async (toValue, tier, dryRun) =>
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
        }, toOption, tierOption, dryRunOption);

        return cmd;
    }
}
