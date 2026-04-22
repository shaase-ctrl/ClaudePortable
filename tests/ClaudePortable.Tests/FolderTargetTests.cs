using System.IO.Compression;
using System.Text;
using ClaudePortable.Targets;

namespace ClaudePortable.Tests;

public class FolderTargetTests : IDisposable
{
    private readonly string _root;

    public FolderTargetTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cp-foldertarget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void EnsureWritable_SucceedsOnTempFolder()
    {
        var target = new FolderTarget(_root);
        target.EnsureWritable();
    }

    [Fact]
    public void ListBackups_ReturnsOnlyClaudePortableZips()
    {
        WriteFakeBackupZip("claude-backup_2026-04-22T10-00-00Z_HOST_daily.zip");
        File.WriteAllText(Path.Combine(_root, "something-else.zip"), "junk");
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "junk");

        var target = new FolderTarget(_root);
        var backups = target.ListBackups();

        Assert.Single(backups);
        Assert.StartsWith("claude-backup_", backups[0].FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void Rename_MovesZipWithinFolder()
    {
        WriteFakeBackupZip("claude-backup_2026-04-22T10-00-00Z_HOST_daily.zip");
        var target = new FolderTarget(_root);

        target.Rename(
            "claude-backup_2026-04-22T10-00-00Z_HOST_daily.zip",
            "claude-backup_2026-04-22T10-00-00Z_HOST_weekly.zip");

        Assert.False(File.Exists(Path.Combine(_root, "claude-backup_2026-04-22T10-00-00Z_HOST_daily.zip")));
        Assert.True(File.Exists(Path.Combine(_root, "claude-backup_2026-04-22T10-00-00Z_HOST_weekly.zip")));
    }

    [Fact]
    public void Delete_RemovesBackupFile()
    {
        WriteFakeBackupZip("claude-backup_2026-04-22T10-00-00Z_HOST_daily.zip");
        var target = new FolderTarget(_root);

        target.Delete("claude-backup_2026-04-22T10-00-00Z_HOST_daily.zip");

        Assert.Empty(target.ListBackups());
    }

    [Fact]
    public void HasEnoughFreeSpace_ReturnsTrueForTinyRequest()
    {
        var target = new FolderTarget(_root);
        Assert.True(target.HasEnoughFreeSpace(1024));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        GC.SuppressFinalize(this);
    }

    private void WriteFakeBackupZip(string name)
    {
        var path = Path.Combine(_root, name);
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("manifest.json");
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        writer.Write("""
            {
              "schemaVersion": 2,
              "createdAt": "2026-04-22T10:00:00+00:00",
              "hostname": "HOST",
              "windowsUser": "tester",
              "retentionTier": "Daily",
              "sourcePaths": {},
              "sizeBytes": 0,
              "fileCount": 0,
              "sha256": "",
              "excludedPaths": [],
              "toolVersion": "test"
            }
            """);
    }
}
