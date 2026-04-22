using ClaudePortable.Core.Abstractions;

namespace ClaudePortable.Core.Discovery;

public sealed class WindowsPathDiscovery : IPathDiscovery
{
    private static readonly (string Key, string EnvRelative, string Source)[] KnownPaths =
    [
        ("claudeDesktopAppData", @"%APPDATA%\Claude", "Spec 1.1 + verified 2026-04-22"),
        ("claudeCodeUserProfile", @"%USERPROFILE%\.claude", "Spec 1.1 + verified 2026-04-22"),
        ("coworkSessions", @"%USERPROFILE%\.cowork", "Spec 1.1 (assumed, ProcMon pending)"),
        ("claudeDesktopLocalAppData", @"%LOCALAPPDATA%\Claude", "Spec 1.1 (assumed)"),
    ];

    public IReadOnlyList<DiscoveredClaudePath> Discover()
    {
        return KnownPaths
            .Select(kp =>
            {
                var expanded = Environment.ExpandEnvironmentVariables(kp.EnvRelative);
                return new DiscoveredClaudePath(kp.Key, expanded, Directory.Exists(expanded), kp.Source);
            })
            .ToList();
    }
}
