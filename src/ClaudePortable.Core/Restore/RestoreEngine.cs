using System.IO.Compression;
using ClaudePortable.Core.Abstractions;
using ClaudePortable.Core.Manifest;

namespace ClaudePortable.Core.Restore;

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

            var newUserProfile = request.TargetUserProfile
                ?? Environment.ExpandEnvironmentVariables("%USERPROFILE%");
            var oldUserProfile = manifest.SourcePaths.TryGetValue("claudeCodeUserProfile", out var oldCodePath)
                ? Path.GetDirectoryName(oldCodePath.TrimEnd('\\', '/'))!
                : newUserProfile;

            _pathRewriter.Rewrite(tempRoot, oldUserProfile, newUserProfile);

            var now = _clock.GetUtcNow();
            var safetyBackups = new List<string>();
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

                var preserved = SafetyBackup.PreserveExisting(targetDir, now);
                if (preserved is not null)
                {
                    safetyBackups.Add(preserved);
                }

                CopyDirectory(sourceDir, targetDir);
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

            return new RestoreOutcome(manifest, safetyBackups, checklistDest);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string RewriteEnvPath(string envRelative, string newUserProfile)
    {
        return envRelative
            .Replace("%USERPROFILE%", newUserProfile, StringComparison.OrdinalIgnoreCase)
            .Replace("%APPDATA%", Path.Combine(newUserProfile, "AppData", "Roaming"), StringComparison.OrdinalIgnoreCase)
            .Replace("%LOCALAPPDATA%", Path.Combine(newUserProfile, "AppData", "Local"), StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, dir);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
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
