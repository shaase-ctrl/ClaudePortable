using ClaudePortable.Core.Manifest;

namespace ClaudePortable.Core.Abstractions;

public sealed record BackupRequest(
    string DestinationFolder,
    RetentionTier Tier = RetentionTier.Daily,
    bool DryRun = false);

public sealed record BackupOutcome(
    string ZipPath,
    BackupManifest Manifest,
    bool WasDryRun);

public interface IBackupEngine
{
    Task<BackupOutcome> CreateBackupAsync(BackupRequest request, CancellationToken cancellationToken = default);
}
