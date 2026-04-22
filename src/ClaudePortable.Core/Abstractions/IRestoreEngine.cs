using ClaudePortable.Core.Manifest;

namespace ClaudePortable.Core.Abstractions;

public sealed record RestoreRequest(
    string SourceZipPath,
    string? TargetUserProfile = null,
    bool Confirmed = false);

public sealed record RestoreOutcome(
    BackupManifest Manifest,
    IReadOnlyList<string> SafetyBackups,
    string PostRestoreChecklistPath);

public interface IRestoreEngine
{
    Task<RestoreOutcome> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken = default);
}
