using System.Globalization;

namespace ClaudePortable.Core.Restore;

public sealed record SafetyBackupResult(bool MovedAside, string? BackupPath, string? Error);

public static class SafetyBackup
{
    public static SafetyBackupResult TryPreserveExisting(string targetFolder, DateTimeOffset when)
    {
        if (!Directory.Exists(targetFolder))
        {
            return new SafetyBackupResult(MovedAside: false, BackupPath: null, Error: null);
        }

        // Reparse points (NTFS junctions / symlinks, common for Store-app
        // sandboxed app-data) refuse Directory.Move on their target. Stay
        // away from them and let the caller overlay files instead.
        if (IsReparsePoint(targetFolder))
        {
            return new SafetyBackupResult(
                MovedAside: false,
                BackupPath: null,
                Error: $"'{targetFolder}' is a reparse point (Store app data). Overlaying files instead of renaming.");
        }

        var suffix = when.UtcDateTime.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture);
        var parent = Path.GetDirectoryName(targetFolder);
        if (string.IsNullOrEmpty(parent))
        {
            return new SafetyBackupResult(
                MovedAside: false,
                BackupPath: null,
                Error: $"Unable to determine parent of '{targetFolder}'.");
        }
        var leaf = Path.GetFileName(targetFolder.TrimEnd('\\', '/'));
        var backupName = Path.Combine(parent, $"{leaf}_backup_{suffix}");

        try
        {
            Directory.Move(targetFolder, backupName);
            return new SafetyBackupResult(MovedAside: true, BackupPath: backupName, Error: null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SafetyBackupResult(
                MovedAside: false,
                BackupPath: null,
                Error: $"Could not rename '{targetFolder}' aside: {ex.Message}. Overlaying files instead.");
        }
    }

    /// <summary>Obsolete throwing variant kept for compatibility.</summary>
    public static string? PreserveExisting(string targetFolder, DateTimeOffset when)
    {
        var result = TryPreserveExisting(targetFolder, when);
        if (!string.IsNullOrEmpty(result.Error) && !result.MovedAside)
        {
            throw new IOException(result.Error);
        }
        return result.BackupPath;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            var attrs = File.GetAttributes(path);
            return (attrs & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
