namespace ClaudePortable.Core.Restore;

public enum VersionGateLevel
{
    Ok,
    Info,
    Warn,
    Block,
}

public sealed record VersionGateResult(VersionGateLevel Level, string Message);

public static class VersionGating
{
    public static VersionGateResult Evaluate(string? backupVersion, string? installedVersion)
    {
        if (string.IsNullOrEmpty(backupVersion) && string.IsNullOrEmpty(installedVersion))
        {
            return new VersionGateResult(VersionGateLevel.Info, "Claude Desktop version unknown on both sides; no gating applied.");
        }
        if (string.IsNullOrEmpty(backupVersion))
        {
            return new VersionGateResult(VersionGateLevel.Info, $"Backup has no claudeDesktopVersion field; installed is {installedVersion}.");
        }
        if (string.IsNullOrEmpty(installedVersion))
        {
            return new VersionGateResult(VersionGateLevel.Warn, $"Claude Desktop is not installed on this machine (or not detected); backup was taken with version {backupVersion}. Install Claude Desktop before opening the restored data.");
        }
        if (string.Equals(backupVersion, installedVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new VersionGateResult(VersionGateLevel.Ok, $"Claude Desktop version matches: {installedVersion}.");
        }

        if (!Version.TryParse(Normalize(backupVersion), out var b) || !Version.TryParse(Normalize(installedVersion), out var i))
        {
            return new VersionGateResult(VersionGateLevel.Warn, $"Version strings are not parseable; treating as mismatch. Backup: {backupVersion}, installed: {installedVersion}.");
        }

        if (b.Major != i.Major)
        {
            return new VersionGateResult(VersionGateLevel.Block, $"Major-version mismatch. Backup was taken with Claude Desktop {backupVersion}; installed is {installedVersion}. IndexedDB schema may differ - restore could leave chat history unreadable. Pass --ignore-version-mismatch to override.");
        }

        return new VersionGateResult(VersionGateLevel.Warn, $"Minor-version mismatch. Backup: {backupVersion}, installed: {installedVersion}. Most data migrates fine; LevelDB schema changes can still corrupt chat history.");
    }

    private static string Normalize(string v)
    {
        var parts = v.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 4)
        {
            return string.Join('.', parts[..4]);
        }
        while (parts.Length < 4)
        {
            parts = parts.Append("0").ToArray();
        }
        return string.Join('.', parts);
    }
}
