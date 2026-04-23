using System.Runtime.Versioning;
using ClaudePortable.Core.Abstractions;
using ClaudePortable.Core.Archive;
using ClaudePortable.Core.Discovery;
using ClaudePortable.Core.Manifest;
using ClaudePortable.Core.Post;

namespace ClaudePortable.Core.Backup;

[SupportedOSPlatform("windows")]
public sealed class BackupEngine : IBackupEngine
{
    private readonly IPathDiscovery _pathDiscovery;
    private readonly IArchiveWriter _archiveWriter;
    private readonly TimeProvider _clock;

    public BackupEngine(
        IPathDiscovery pathDiscovery,
        IArchiveWriter archiveWriter,
        TimeProvider? clock = null)
    {
        _pathDiscovery = pathDiscovery;
        _archiveWriter = archiveWriter;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<BackupOutcome> CreateBackupAsync(
        BackupRequest request,
        CancellationToken cancellationToken = default)
    {
        var destination = request.DestinationFolder
            ?? throw new ArgumentException("DestinationFolder is required.", nameof(request));

        Directory.CreateDirectory(destination);

        var discovered = _pathDiscovery.Discover();
        var existingPaths = discovered.Where(p => p.Exists).ToList();
        if (existingPaths.Count == 0)
        {
            throw new InvalidOperationException(
                "No Claude paths found on this machine. Nothing to back up.");
        }

        var exclusions = new ExclusionGlob(DefaultExclusions.Globs);
        var entries = existingPaths
            .SelectMany(p => FileEnumerator.Enumerate(p.Path, MapArchivePrefix(p.Key), exclusions))
            .ToList();

        var createdAt = _clock.GetUtcNow();
        var filename = BuildFilename(createdAt, request.Tier);
        var zipPath = Path.Combine(destination, filename);

        var manifestSeed = ManifestBuilder.Build(
            discovered,
            DefaultExclusions.Globs,
            request.Tier,
            createdAt,
            claudeDesktopVersion: ClaudeDesktopVersionReader.TryRead());

        if (request.DryRun)
        {
            var preview = manifestSeed with { FileCount = entries.Count };
            return new BackupOutcome(zipPath, preview, WasDryRun: true);
        }

        var checklistEntry = BuildChecklistEntry();
        var combinedEntries = entries.Append(checklistEntry).ToList();
        try
        {
            var manifestJson = ManifestBuilder.Serialize(manifestSeed);
            var archiveResult = await _archiveWriter
                .WriteAsync(zipPath, combinedEntries, manifestJson, cancellationToken)
                .ConfigureAwait(false);

            var finalManifest = manifestSeed with
            {
                SizeBytes = archiveResult.SizeBytes,
                FileCount = archiveResult.FileCount,
                Sha256 = archiveResult.Sha256,
            };

            return new BackupOutcome(zipPath, finalManifest, WasDryRun: false);
        }
        finally
        {
            if (File.Exists(checklistEntry.AbsolutePath))
            {
                try
                {
                    File.Delete(checklistEntry.AbsolutePath);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private static string BuildFilename(DateTimeOffset timestamp, RetentionTier tier)
    {
        var iso = timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH-mm-ssZ", System.Globalization.CultureInfo.InvariantCulture);
        var tierLower = tier.ToString().ToLowerInvariant();
        return $"claude-backup_{iso}_{Environment.MachineName}_{tierLower}.zip";
    }

    private static string MapArchivePrefix(string key) => key switch
    {
        "claudeDesktopAppData" => "claude-desktop/appdata",
        "claudeDesktopLocalAppData" => "claude-desktop/localappdata",
        "claudeCodeUserProfile" => "claude-code/dotclaude",
        "coworkSessions" => "cowork/sessions",
        _ => key,
    };

    private static ArchiveEntry BuildChecklistEntry()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"post-restore-{Guid.NewGuid():N}.md");
        File.WriteAllText(tempFile, PostRestoreChecklistBuilder.Build());
        return new ArchiveEntry("post-restore-checklist.md", tempFile);
    }
}
