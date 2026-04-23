using System.CommandLine;
using System.Runtime.Versioning;
using ClaudePortable.Core.Restore;

namespace ClaudePortable.App.Commands;

[SupportedOSPlatform("windows")]
public static class RestoreCommand
{
    public static Command Build()
    {
        var fromOption = new Option<FileInfo>(
            aliases: new[] { "--from", "-f" },
            description: "Backup ZIP to restore from.")
        {
            IsRequired = true,
        };

        var yesOption = new Option<bool>(
            aliases: new[] { "--yes", "-y" },
            description: "Confirm that existing Claude data will be moved aside.",
            getDefaultValue: () => false);

        var targetUserOption = new Option<string?>(
            aliases: new[] { "--target-user" },
            description: "Override the target Windows user profile path (advanced).");

        var ignoreVersionOption = new Option<bool>(
            aliases: new[] { "--ignore-version-mismatch" },
            description: "Proceed even when the backup's claudeDesktopVersion is a major version behind the installed Claude Desktop.",
            getDefaultValue: () => false);

        var cmd = new Command("restore", "Restore Claude data from a backup ZIP.")
        {
            fromOption,
            yesOption,
            targetUserOption,
            ignoreVersionOption,
        };

        cmd.SetHandler(async (from, yes, targetUser, ignoreVersion) =>
        {
            if (!yes)
            {
                Console.Error.WriteLine("error: restore requires --yes. Existing Claude data will be moved to <folder>_backup_<timestamp>.");
                Environment.ExitCode = 1;
                return;
            }

            var engine = new RestoreEngine(new PathRewriter());
            try
            {
                var outcome = await engine.RestoreAsync(new(from.FullName, targetUser, Confirmed: true, IgnoreVersionMismatch: ignoreVersion)).ConfigureAwait(false);
                Console.WriteLine("restore complete.");
                Console.WriteLine($"manifest schema:     {outcome.Manifest.SchemaVersion}");
                Console.WriteLine($"backup created at:   {outcome.Manifest.CreatedAt:yyyy-MM-ddTHH:mm:ssZ}");
                Console.WriteLine($"original host:       {outcome.Manifest.Hostname}");
                Console.WriteLine($"version gate:        {outcome.VersionGate.Level} - {outcome.VersionGate.Message}");
                Console.WriteLine($"safety backups:      {outcome.SafetyBackups.Count}");
                foreach (var sb in outcome.SafetyBackups)
                {
                    Console.WriteLine($"  - {sb}");
                }
                Console.WriteLine($"checklist saved to:  {outcome.PostRestoreChecklistPath}");
            }
            catch (FileNotFoundException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                Environment.ExitCode = 2;
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"error: backup appears invalid: {ex.Message}");
                Environment.ExitCode = 3;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"error: {ex.Message}");
                Environment.ExitCode = 3;
            }
        }, fromOption, yesOption, targetUserOption, ignoreVersionOption);

        return cmd;
    }
}
