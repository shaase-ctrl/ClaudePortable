using System.Globalization;

namespace ClaudePortable.Core.Restore;

public static class SafetyBackup
{
    public static string? PreserveExisting(string targetFolder, DateTimeOffset when)
    {
        if (!Directory.Exists(targetFolder))
        {
            return null;
        }

        var suffix = when.UtcDateTime.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);
        var parent = Path.GetDirectoryName(targetFolder)
            ?? throw new InvalidOperationException($"Unable to determine parent of '{targetFolder}'.");
        var leaf = Path.GetFileName(targetFolder.TrimEnd('\\', '/'));
        var backupName = Path.Combine(parent, $"{leaf}_backup_{suffix}");

        Directory.Move(targetFolder, backupName);
        return backupName;
    }
}
