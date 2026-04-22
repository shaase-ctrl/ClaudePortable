using System.Runtime.Versioning;
using System.Text.Json;
using ClaudePortable.Core.Manifest;

namespace ClaudePortable.Targets;

public sealed record BackupDescriptor(string FileName, string FullPath, long SizeBytes, BackupManifest? Manifest);

[SupportedOSPlatform("windows")]
public sealed class FolderTarget
{
    public string FolderPath { get; }

    public FolderTarget(string folderPath)
    {
        FolderPath = Path.GetFullPath(folderPath);
    }

    public void EnsureExists()
    {
        Directory.CreateDirectory(FolderPath);
    }

    public void EnsureWritable()
    {
        EnsureExists();
        var probe = Path.Combine(FolderPath, $".cpwrite-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "probe");
        File.Delete(probe);
    }

    public bool HasEnoughFreeSpace(long requiredBytes)
    {
        var root = Path.GetPathRoot(FolderPath)
            ?? throw new InvalidOperationException($"Cannot determine drive for '{FolderPath}'.");
        var drive = new DriveInfo(root);
        return drive.AvailableFreeSpace >= requiredBytes;
    }

    public bool HasPendingCloudUploadFlags()
    {
        if (!Directory.Exists(FolderPath))
        {
            return false;
        }
        var attrs = File.GetAttributes(FolderPath);
        const FileAttributes recallOnDataAccess = (FileAttributes)0x00400000;
        return (attrs & (FileAttributes.Offline | recallOnDataAccess)) != 0;
    }

    public IReadOnlyList<BackupDescriptor> ListBackups()
    {
        if (!Directory.Exists(FolderPath))
        {
            return Array.Empty<BackupDescriptor>();
        }

        var result = new List<BackupDescriptor>();
        foreach (var file in Directory.EnumerateFiles(FolderPath, "claude-backup_*.zip"))
        {
            var info = new FileInfo(file);
            var manifest = TryReadManifest(file);
            result.Add(new BackupDescriptor(info.Name, info.FullName, info.Length, manifest));
        }
        return result.OrderByDescending(b => b.FileName, StringComparer.Ordinal).ToList();
    }

    public void Rename(string oldName, string newName)
    {
        var oldPath = Path.Combine(FolderPath, oldName);
        var newPath = Path.Combine(FolderPath, newName);
        if (File.Exists(newPath))
        {
            throw new IOException($"Target name already exists: {newName}");
        }
        File.Move(oldPath, newPath);
    }

    public void Delete(string name)
    {
        var path = Path.Combine(FolderPath, name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static BackupManifest? TryReadManifest(string zipPath)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry("manifest.json");
            if (entry is null)
            {
                return null;
            }
            using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return ManifestBuilder.Deserialize(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
        {
            return null;
        }
    }
}
