using System.Text.Json.Serialization;

namespace ClaudePortable.Core.Manifest;

public enum RetentionTier
{
    Daily,
    Weekly,
    Monthly,
}

public sealed record BackupManifest
{
    public const int CurrentSchemaVersion = 2;

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("hostname")]
    public string Hostname { get; init; } = string.Empty;

    [JsonPropertyName("windowsUser")]
    public string WindowsUser { get; init; } = string.Empty;

    [JsonPropertyName("claudeDesktopVersion")]
    public string? ClaudeDesktopVersion { get; init; }

    [JsonPropertyName("claudeCodeVersion")]
    public string? ClaudeCodeVersion { get; init; }

    [JsonPropertyName("retentionTier")]
    [JsonConverter(typeof(JsonStringEnumConverter<RetentionTier>))]
    public RetentionTier RetentionTier { get; init; } = RetentionTier.Daily;

    [JsonPropertyName("sourcePaths")]
    public Dictionary<string, string> SourcePaths { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps archive-prefix (as it appears inside the zip, e.g.
    /// "claude-desktop/appdata" or "cowork-projects/a7b3") to the
    /// absolute path the files came from on the backup machine.
    /// Restore reads this to know where to write each archived tree.
    /// Added in 2026-04-23 alongside Cowork-project auto-backup.
    /// </summary>
    [JsonPropertyName("archiveTargets")]
    public Dictionary<string, string> ArchiveTargets { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    [JsonPropertyName("fileCount")]
    public int FileCount { get; init; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("excludedPaths")]
    public IReadOnlyList<string> ExcludedPaths { get; init; } = Array.Empty<string>();

    [JsonPropertyName("toolVersion")]
    public string ToolVersion { get; init; } = string.Empty;
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BackupManifest))]
public partial class BackupManifestJsonContext : JsonSerializerContext
{
}
