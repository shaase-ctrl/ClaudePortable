using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using ClaudePortable.Core.Manifest;

namespace ClaudePortable.Scheduler.Retention;

internal static class BackupRenamer
{
    private static readonly Regex TierInFilename = new(
        @"^(?<prefix>claude-backup_.+_)(?<tier>daily|weekly|monthly)(?<suffix>\.zip)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ReplaceTierInFilename(string fileName, RetentionTier newTier)
    {
        var match = TierInFilename.Match(fileName);
        if (!match.Success)
        {
            throw new ArgumentException($"Filename does not match retention naming convention: '{fileName}'.", nameof(fileName));
        }
        var tierLower = newTier.ToString().ToLowerInvariant();
        return match.Groups["prefix"].Value + tierLower + match.Groups["suffix"].Value;
    }

    public static void UpdateManifestTier(string zipPath, RetentionTier newTier)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Update);
        var entry = archive.GetEntry("manifest.json")
            ?? throw new InvalidDataException($"manifest.json not found in '{zipPath}'.");

        BackupManifest manifest;
        using (var reader = new StreamReader(entry.Open()))
        {
            manifest = ManifestBuilder.Deserialize(reader.ReadToEnd());
        }

        var updated = manifest with { RetentionTier = newTier };
        entry.Delete();
        var newEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        using var writer = new StreamWriter(newEntry.Open(), new UTF8Encoding(false));
        writer.Write(ManifestBuilder.Serialize(updated));
    }
}
