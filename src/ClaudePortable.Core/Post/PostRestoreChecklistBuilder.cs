using ClaudePortable.Core.Manifest;
using ClaudePortable.Core.Restore;

namespace ClaudePortable.Core.Post;

public static class PostRestoreChecklistBuilder
{
    public static string Build(
        BackupManifest manifest,
        VersionGateResult versionGate,
        IReadOnlyList<string> safetyBackups,
        IReadOnlyList<RestoreTargetReport> targetReports)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# Post-Restore Checklist\n");
        sb.AppendLine($"Backup from: **{manifest.Hostname}** ({manifest.WindowsUser}) — {manifest.CreatedAt:yyyy-MM-dd HH:mm} UTC\n");
        sb.AppendLine($"Claude Desktop version in backup: `{manifest.ClaudeDesktopVersion ?? "unknown"}`\n");

        // Version gate warning
        if (versionGate.Level == VersionGateLevel.Warn)
        {
            sb.AppendLine("## ⚠ Version Mismatch Detected\n");
            sb.AppendLine($"> **{versionGate.Message}**\n");
            sb.AppendLine("- [ ] Be aware that chat history migration may not be guaranteed due to LevelDB schema differences\n");
        }
        else if (versionGate.Level == VersionGateLevel.Block)
        {
            sb.AppendLine("## 🚫 Major Version Mismatch\n");
            sb.AppendLine($"> **{versionGate.Message}**\n");
            sb.AppendLine("- [ ] Chat history may be corrupted or unreadable — verify carefully\n");
        }

        // Required steps
        sb.AppendLine("## Required Steps\n");
        sb.AppendLine("- [ ] Start Claude Desktop and sign in once\n");
        sb.AppendLine("- [ ] Claude Code: run `claude login`\n");
        sb.AppendLine("- [ ] Re-authorize each connector (Gmail, Slack, GDrive, etc.)\n");
        sb.AppendLine("- [ ] Run `claude plugin sync` to reload installed plugins\n");

        // Plugin reinstall hint
        var pluginDir = Path.Combine(
            Environment.ExpandEnvironmentVariables("%USERPROFILE%"),
            ".claude", "plugins");
        if (Directory.Exists(pluginDir))
        {
            var plugins = Directory.GetDirectories(pluginDir).Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n)).ToList();
            if (plugins.Count > 0)
            {
                sb.AppendLine("\n## Plugin Reinstallation\n");
                sb.AppendLine($"Found {plugins.Count} plugin(s) in `.claude/plugins/`. Run `claude plugin install <name>` for each:\n");
                foreach (var p in plugins.Take(20))
                {
                    sb.AppendLine($"- [ ] `{p}`");
                }
                if (plugins.Count > 20)
                {
                    sb.AppendLine($"- ... and {plugins.Count - 20} more\n");
                }
            }
        }

        // Safety backups
        sb.AppendLine("\n## Safety Backups\n");
        if (safetyBackups.Count > 0)
        {
            sb.AppendLine("The following folders were backed up before restore:\n");
            foreach (var backup in safetyBackups)
            {
                sb.AppendLine($"- `{backup}`\n");
            }
            sb.AppendLine("You can delete these after verifying the restore succeeded.\n");
        }
        else
        {
            sb.AppendLine("No existing folders were found — nothing was backed up before overlay.\n");
        }

        // Target reports
        if (targetReports.Count > 0)
        {
            sb.AppendLine("## Restore Summary\n");
            foreach (var report in targetReports)
            {
                var status = report.FilesWritten == 0 && report.Warnings.Count == 1 && report.Warnings[0].StartsWith("Not present", StringComparison.Ordinal)
                    ? "SKIPPED (not in backup)"
                    : report.SafetyBackedUp
                        ? $"RESTORED ({report.FilesWritten} files, safety backup created)"
                        : $"OVERLAY ({report.FilesWritten} files written)";
                sb.AppendLine($"### `{report.ArchivePrefix}`\n");
                sb.AppendLine($"Target: `{report.TargetFolder}`\n");
                sb.AppendLine($"Status: {status}\n");
                foreach (var warning in report.Warnings)
                {
                    sb.AppendLine($"> ⚠ {warning}\n");
                }
            }
        }

        // Troubleshooting
        sb.AppendLine("## Troubleshooting\n");
        sb.AppendLine("- **Chat history empty:** Check Claude Desktop version. Mismatch with backup version means chat history migration is not guaranteed.\n");
        sb.AppendLine("- **Plugins not loading:** Run `claude plugin install <name>` manually for each plugin from `.claude/plugins/`.\n");
        sb.AppendLine("- **Connectors not working:** Re-authorize. Tokens are intentionally not backed up.\n");

        // Scope note
        sb.AppendLine("\n## Scope Note\n");
        sb.AppendLine("This tool does NOT back up OAuth refresh tokens, API keys, or DPAPI-protected credentials.\n");
        sb.AppendLine("This is intentional: these artifacts are user- and machine-bound and would be invalid on another PC anyway.\n");

        return sb.ToString();
    }
}
