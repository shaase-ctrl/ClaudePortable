using System.Text.Json;
using ClaudePortable.Core.Abstractions;
using ClaudePortable.Core.Archive;
using ClaudePortable.Core.Manifest;

namespace ClaudePortable.Tests;

public class ManifestBuilderTests
{
    [Fact]
    public void Build_ProducesManifestWithSchemaVersion2()
    {
        var paths = new List<DiscoveredClaudePath>
        {
            new("claudeCodeUserProfile", @"C:\Users\Test\.claude", true, "test"),
        };

        var manifest = ManifestBuilder.Build(
            paths,
            DefaultExclusions.Globs,
            RetentionTier.Daily,
            new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, manifest.SchemaVersion);
        Assert.Equal(RetentionTier.Daily, manifest.RetentionTier);
        Assert.Single(manifest.SourcePaths);
        Assert.Equal(ManifestBuilder.Sha256Placeholder, manifest.Sha256);
    }

    [Fact]
    public void SerializeThenDeserialize_IsRoundtripStable()
    {
        var paths = new List<DiscoveredClaudePath>
        {
            new("claudeCodeUserProfile", @"C:\Users\Test\.claude", true, "test"),
        };

        var original = ManifestBuilder.Build(
            paths,
            DefaultExclusions.Globs,
            RetentionTier.Weekly,
            new DateTimeOffset(2026, 4, 22, 12, 0, 0, TimeSpan.Zero),
            sizeBytes: 12345,
            fileCount: 7,
            sha256: "abcdef");

        var json = ManifestBuilder.Serialize(original);
        var parsed = ManifestBuilder.Deserialize(json);

        Assert.Equal(original.SchemaVersion, parsed.SchemaVersion);
        Assert.Equal(original.RetentionTier, parsed.RetentionTier);
        Assert.Equal(original.SizeBytes, parsed.SizeBytes);
        Assert.Equal(original.FileCount, parsed.FileCount);
        Assert.Equal(original.Sha256, parsed.Sha256);
        Assert.Equal(original.CreatedAt, parsed.CreatedAt);
    }

    [Fact]
    public void Serialize_EmitsCamelCaseFields()
    {
        var paths = new List<DiscoveredClaudePath>();
        var manifest = ManifestBuilder.Build(paths, DefaultExclusions.Globs, RetentionTier.Daily, DateTimeOffset.UtcNow);
        var json = ManifestBuilder.Serialize(manifest);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("schemaVersion", out _));
        Assert.True(doc.RootElement.TryGetProperty("retentionTier", out _));
    }
}
