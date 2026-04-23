using System.IO.Compression;
using ClaudePortable.Core.Abstractions;
using ClaudePortable.Core.Archive;
using ClaudePortable.Core.Backup;
using ClaudePortable.Core.Manifest;
using ClaudePortable.Core.Restore;

namespace ClaudePortable.Tests;

public class BackupRoundtripTests : IDisposable
{
    private readonly string _root;
    private readonly string _fakeUserProfile;
    private readonly string _fakeAppData;

    public BackupRoundtripTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cp-roundtrip-{Guid.NewGuid():N}");
        _fakeUserProfile = Path.Combine(_root, "user");
        _fakeAppData = Path.Combine(_root, "appdata");
        Directory.CreateDirectory(_fakeUserProfile);
        Directory.CreateDirectory(_fakeAppData);
    }

    [Fact]
    public async Task BackupCreatesZipWithManifestAndContents()
    {
        var dotClaude = Path.Combine(_fakeUserProfile, ".claude");
        Directory.CreateDirectory(dotClaude);
        File.WriteAllText(Path.Combine(dotClaude, "settings.json"), @"{""theme"":""dark""}");
        File.WriteAllText(Path.Combine(dotClaude, "CLAUDE.md"), "# test memory");

        var destination = Path.Combine(_root, "backups");
        Directory.CreateDirectory(destination);

        var discovery = new FakeDiscovery(new List<DiscoveredClaudePath>
        {
            new("claudeCodeUserProfile", dotClaude, Directory.Exists(dotClaude), "test"),
        });

        var engine = new BackupEngine(discovery, new ZipArchiveWriter(), NullCoworkProjectDiscovery.Instance);
        var outcome = await engine.CreateBackupAsync(new(destination, RetentionTier.Daily));

        Assert.True(File.Exists(outcome.ZipPath));
        Assert.True(outcome.Manifest.FileCount >= 2);
        Assert.False(string.IsNullOrEmpty(outcome.Manifest.Sha256));

        using var archive = ZipFile.OpenRead(outcome.ZipPath);
        Assert.NotNull(archive.GetEntry("manifest.json"));
        Assert.NotNull(archive.GetEntry("post-restore-checklist.md"));
        Assert.NotNull(archive.GetEntry("claude-code/dotclaude/settings.json"));
        Assert.NotNull(archive.GetEntry("claude-code/dotclaude/CLAUDE.md"));
    }

    [Fact]
    public async Task DryRun_DoesNotCreateZip()
    {
        var dotClaude = Path.Combine(_fakeUserProfile, ".claude");
        Directory.CreateDirectory(dotClaude);
        File.WriteAllText(Path.Combine(dotClaude, "settings.json"), "{}");

        var destination = Path.Combine(_root, "backups");
        Directory.CreateDirectory(destination);

        var discovery = new FakeDiscovery(new List<DiscoveredClaudePath>
        {
            new("claudeCodeUserProfile", dotClaude, true, "test"),
        });

        var engine = new BackupEngine(discovery, new ZipArchiveWriter(), NullCoworkProjectDiscovery.Instance);
        var outcome = await engine.CreateBackupAsync(new(destination, RetentionTier.Daily, DryRun: true));

        Assert.True(outcome.WasDryRun);
        Assert.False(File.Exists(outcome.ZipPath));
    }

    [Fact]
    public async Task BackupExcludesCacheFolders()
    {
        var dotClaude = Path.Combine(_fakeUserProfile, ".claude");
        var cache = Path.Combine(dotClaude, "Cache", "something");
        Directory.CreateDirectory(cache);
        File.WriteAllText(Path.Combine(dotClaude, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(cache, "garbage.bin"), "junk");

        var destination = Path.Combine(_root, "backups");
        Directory.CreateDirectory(destination);

        var discovery = new FakeDiscovery(new List<DiscoveredClaudePath>
        {
            new("claudeCodeUserProfile", dotClaude, true, "test"),
        });

        var engine = new BackupEngine(discovery, new ZipArchiveWriter(), NullCoworkProjectDiscovery.Instance);
        var outcome = await engine.CreateBackupAsync(new(destination, RetentionTier.Daily));

        using var archive = ZipFile.OpenRead(outcome.ZipPath);
        Assert.Null(archive.GetEntry("claude-code/dotclaude/Cache/something/garbage.bin"));
        Assert.NotNull(archive.GetEntry("claude-code/dotclaude/settings.json"));
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

    private sealed class FakeDiscovery : IPathDiscovery
    {
        private readonly IReadOnlyList<DiscoveredClaudePath> _paths;
        public FakeDiscovery(IReadOnlyList<DiscoveredClaudePath> paths) => _paths = paths;
        public IReadOnlyList<DiscoveredClaudePath> Discover() => _paths;
    }
}
