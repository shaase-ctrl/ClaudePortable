using System.Reflection;
using System.Text.Json;
using ClaudePortable.Core.Abstractions;
using ClaudePortable.Core.Discovery;

namespace ClaudePortable.Core.Manifest;

public static class ManifestBuilder
{
    public const string Sha256Placeholder = "__SHA256_PLACEHOLDER__";

    public static BackupManifest Build(
        IReadOnlyList<DiscoveredClaudePath> paths,
        IReadOnlyList<string> excludedGlobs,
        RetentionTier tier,
        DateTimeOffset createdAt,
        long sizeBytes = 0,
        int fileCount = 0,
        string sha256 = Sha256Placeholder,
        string? claudeDesktopVersion = null,
        IReadOnlyList<CoworkProjectFolder>? coworkProjects = null,
        IReadOnlyDictionary<string, string>? archiveTargets = null)
    {
        var toolVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0-dev";

        var sourcePaths = paths
            .Where(p => p.Exists)
            .ToDictionary(p => p.Key, p => p.Path, StringComparer.OrdinalIgnoreCase);

        if (coworkProjects is not null)
        {
            foreach (var project in coworkProjects)
            {
                sourcePaths[$"coworkProject:{project.Hash}"] = project.Path;
            }
        }

        var targets = archiveTargets is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(archiveTargets, StringComparer.OrdinalIgnoreCase);

        return new BackupManifest
        {
            CreatedAt = createdAt,
            Hostname = Environment.MachineName,
            WindowsUser = Environment.UserName,
            ClaudeDesktopVersion = claudeDesktopVersion,
            RetentionTier = tier,
            SourcePaths = sourcePaths,
            ArchiveTargets = targets,
            SizeBytes = sizeBytes,
            FileCount = fileCount,
            Sha256 = sha256,
            ExcludedPaths = excludedGlobs,
            ToolVersion = toolVersion,
        };
    }

    public static string Serialize(BackupManifest manifest)
        => JsonSerializer.Serialize(manifest, BackupManifestJsonContext.Default.BackupManifest);

    public static BackupManifest Deserialize(string json)
        => JsonSerializer.Deserialize(json, BackupManifestJsonContext.Default.BackupManifest)
           ?? throw new InvalidDataException("manifest.json could not be parsed.");
}
