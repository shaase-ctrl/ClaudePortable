using ClaudePortable.Core.Manifest;
using ClaudePortable.Core.Restore;

namespace ClaudePortable.Core.Abstractions;

public sealed record RestoreRequest(
    string SourceZipPath,
    string? TargetUserProfile = null,
    bool Confirmed = false,
    bool IgnoreVersionMismatch = false);

public sealed record RestoreTargetReport(
    string ArchivePrefix,
    string TargetFolder,
    bool SafetyBackedUp,
    string? SafetyBackupPath,
    int FilesWritten,
    IReadOnlyList<string> Warnings);

public sealed record RestoreOutcome(
    BackupManifest Manifest,
    IReadOnlyList<string> SafetyBackups,
    string PostRestoreChecklistPath,
    VersionGateResult VersionGate,
    IReadOnlyList<RestoreTargetReport> PerTargetReports);

public interface IRestoreEngine
{
    Task<RestoreOutcome> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken = default);
}
