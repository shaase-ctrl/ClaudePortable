using ClaudePortable.Core.Abstractions;

namespace ClaudePortable.Core.Discovery;

public sealed class WindowsPathDiscovery : IPathDiscovery
{
    // Each known Claude artefact has a list of candidate paths. We use the
    // FIRST one that looks accessible at discovery time. This matters for
    // the Store-installed Claude Desktop where %APPDATA%\Claude is a
    // reparse point into %LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\...
    // Some process contexts (e.g. a portable exe flagged with
    // Mark-of-the-Web after being downloaded / synced through OneDrive)
    // fail Directory.Exists on the reparse point even though the target
    // is fully accessible. Enumerating the Packages path directly bypasses
    // the reparse-point resolution.
    private static readonly (string Key, string[] Candidates, string Source)[] KnownPaths =
    [
        (
            "claudeDesktopAppData",
            new[]
            {
                @"%APPDATA%\Claude",
                @"%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude",
            },
            "Spec 1.1 + Store-app reparse fallback"
        ),
        (
            "claudeCodeUserProfile",
            new[] { @"%USERPROFILE%\.claude" },
            "Spec 1.1 + verified 2026-04-22"
        ),
        (
            "coworkSessions",
            new[] { @"%USERPROFILE%\.cowork" },
            "Spec 1.1 (assumed, ProcMon pending)"
        ),
        (
            "claudeDesktopLocalAppData",
            new[]
            {
                @"%LOCALAPPDATA%\Claude",
                @"%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Local\Claude",
            },
            "Spec 1.1 + Store-app reparse fallback"
        ),
    ];

    public IReadOnlyList<DiscoveredClaudePath> Discover()
    {
        return KnownPaths
            .Select(kp =>
            {
                string? first = null;
                foreach (var rel in kp.Candidates)
                {
                    var expanded = Environment.ExpandEnvironmentVariables(rel);
                    first ??= expanded;
                    if (SafeDirectoryExists(expanded))
                    {
                        return new DiscoveredClaudePath(kp.Key, expanded, true, kp.Source);
                    }
                }
                return new DiscoveredClaudePath(kp.Key, first ?? string.Empty, false, kp.Source);
            })
            .ToList();
    }

    private static bool SafeDirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        try
        {
            if (Directory.Exists(path))
            {
                return true;
            }
            // Some reparse points + ACL combos make Directory.Exists lie.
            // Double-check by asking the DirectoryInfo cache and, as a last
            // resort, by having the parent directory list its children.
            var info = new DirectoryInfo(path);
            if (info.Exists)
            {
                return true;
            }
            var parent = info.Parent;
            if (parent is null || !parent.Exists)
            {
                return false;
            }
            return parent.EnumerateDirectories(info.Name, SearchOption.TopDirectoryOnly).Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
