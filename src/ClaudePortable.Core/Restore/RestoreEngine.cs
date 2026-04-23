using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;
using ClaudePortable.Core.Abstractions;
using ClaudePortable.Core.Discovery;
using ClaudePortable.Core.Manifest;

namespace ClaudePortable.Core.Restore;

[SupportedOSPlatform("windows")]
public sealed class RestoreEngine : IRestoreEngine
{
    private static readonly (string ArchivePrefix, string EnvRelative)[] RestoreMap =
    [
        ("claude-desktop/appdata", @"%APPDATA%\Claude"),
        ("claude-desktop/localappdata", @"%LOCALAPPDATA%\Claude"),
        ("claude-code/dotclaude", @"%USERPROFILE%\.claude"),
        ("cowork/sessions", @"%USERPROFILE%\.cowork"),
    ];

    private readonly IPathRewriter _pathRewriter;
    private readonly TimeProvider _clock;

    public RestoreEngine(IPathRewriter pathRewriter, TimeProvider? clock = null)
    {
        _pathRewriter = pathRewriter;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<RestoreOutcome> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceZipPath))
        {
            throw new FileNotFoundException("Backup ZIP not found.", request.SourceZipPath);
        }

        if (!request.Confirmed)
        {
            throw new InvalidOperationException(
                "Restore requires explicit confirmation. Pass --yes on the CLI or set Confirmed=true.");
        }

        var running = GetRunningClaudeProcesses();
        if (running.Count > 0)
        {
            throw new InvalidOperationException(
                $"Claude Desktop is running (PID {string.Join(", ", running)}). Close it before restoring; its open file handles will cause 'Access denied' errors on %LOCALAPPDATA%\\Claude and %APPDATA%\\Claude.");
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "ClaudePortable", $"restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            ZipFile.ExtractToDirectory(request.SourceZipPath, tempRoot, overwriteFiles: true);
            cancellationToken.ThrowIfCancellationRequested();

            var manifestPath = Path.Combine(tempRoot, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException("Backup is missing manifest.json.");
            }

            var manifest = ManifestBuilder.Deserialize(await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));

            var installedVersion = ClaudeDesktopVersionReader.TryRead();
            var gate = VersionGating.Evaluate(manifest.ClaudeDesktopVersion, installedVersion);
            if (gate.Level == VersionGateLevel.Block && !request.IgnoreVersionMismatch)
            {
                throw new InvalidOperationException(gate.Message);
            }

            var newUserProfile = request.TargetUserProfile
                ?? Environment.ExpandEnvironmentVariables("%USERPROFILE%");
            var oldUserProfile = manifest.SourcePaths.TryGetValue("claudeCodeUserProfile", out var oldCodePath)
                ? Path.GetDirectoryName(oldCodePath.TrimEnd('\\', '/'))!
                : newUserProfile;

            _pathRewriter.Rewrite(tempRoot, oldUserProfile, newUserProfile);

            var now = _clock.GetUtcNow();
            var safetyBackups = new List<string>();
            var perTargetReports = new List<RestoreTargetReport>();

            foreach (var (archivePrefix, envRelative) in RestoreMap)
            {
                var sourceDir = Path.Combine(tempRoot, archivePrefix.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(sourceDir))
                {
                    continue;
                }

                var targetDir = Environment.ExpandEnvironmentVariables(envRelative);
                if (request.TargetUserProfile is not null)
                {
                    targetDir = RewriteEnvPath(envRelative, request.TargetUserProfile);
                }

                var warnings = new List<string>();

                var preserved = SafetyBackup.TryPreserveExisting(targetDir, now);
                if (preserved.Error is not null)
                {
                    warnings.Add(preserved.Error);
                }
                if (preserved.MovedAside && preserved.BackupPath is not null)
                {
                    safetyBackups.Add(preserved.BackupPath);
                }

                var (filesWritten, copyErrors) = CopyDirectoryResilient(sourceDir, targetDir);
                warnings.AddRange(copyErrors);

                perTargetReports.Add(new RestoreTargetReport(
                    archivePrefix,
                    targetDir,
                    preserved.MovedAside,
                    preserved.BackupPath,
                    filesWritten,
                    warnings));
            }

            var checklistSource = Path.Combine(tempRoot, "post-restore-checklist.md");
            var checklistDest = Path.Combine(
                Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"),
                "ClaudePortable",
                $"post-restore-checklist-{now.UtcDateTime:yyyy-MM-dd-HHmmss}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(checklistDest)!);
            if (File.Exists(checklistSource))
            {
                File.Copy(checklistSource, checklistDest, overwrite: true);
            }

            return new RestoreOutcome(manifest, safetyBackups, checklistDest, gate, perTargetReports);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static IReadOnlyList<int> GetRunningClaudeProcesses()
    {
        try
        {
            return Process.GetProcessesByName("Claude").Select(p => p.Id).ToList();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<int>();
        }
    }

    private static string RewriteEnvPath(string envRelative, string newUserProfile)
    {
        return envRelative
            .Replace("%USERPROFILE%", newUserProfile, StringComparison.OrdinalIgnoreCase)
            .Replace("%APPDATA%", Path.Combine(newUserProfile, "AppData", "Roaming"), StringComparison.OrdinalIgnoreCase)
            .Replace("%LOCALAPPDATA%", Path.Combine(newUserProfile, "AppData", "Local"), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Copy source -> destination. Collects per-file copy failures as
    /// warnings and returns (filesWritten, errors) - does NOT throw on
    /// a single blocked file (common when a Store-sandbox folder has
    /// some locked or permission-guarded children).
    /// </summary>
    private static (int FilesWritten, IReadOnlyList<string> Errors) CopyDirectoryResilient(string source, string destination)
    {
        var errors = new List<string>();
        try
        {
            Directory.CreateDirectory(destination);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add($"Could not create '{destination}': {ex.Message}");
            return (0, errors);
        }

        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, dir);
            try
            {
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Could not create subdir '{relative}': {ex.Message}");
            }
        }

        var filesWritten = 0;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
                filesWritten++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Skipped '{relative}': {ex.Message}");
            }
        }

        return (filesWritten, errors);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
