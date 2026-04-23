using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClaudePortable.Core.Discovery;

/// <summary>
/// A folder that was selected by the user from inside a Cowork session
/// ("userSelectedFolders" in local_*.json). These are the real project
/// roots on disk, independent of Claude Desktop's appdata.
/// </summary>
/// <param name="Hash">
/// Stable hash of the folder path. Used as the archive-prefix segment so
/// the path can round-trip between backup and restore without leaking the
/// original Windows username into the archive directory names.
/// </param>
/// <param name="Path">Original absolute path on the backup machine.</param>
/// <param name="SessionNames">Cowork-session friendly names that referenced this folder.</param>
public sealed record CoworkProjectFolder(string Hash, string Path, IReadOnlyList<string> SessionNames);

public sealed class CoworkProjectDiscovery : ClaudePortable.Core.Abstractions.ICoworkProjectDiscovery
{
    IReadOnlyList<CoworkProjectFolder> ClaudePortable.Core.Abstractions.ICoworkProjectDiscovery.Discover() => Discover();

    /// <summary>
    /// Paths ClaudePortable refuses to treat as Cowork project roots
    /// because they are either system locations or are already covered by
    /// the primary Claude discovery paths. Comparison is case-insensitive
    /// on Windows.
    /// </summary>
    private static readonly string[] ForbiddenRoots =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),      // %APPDATA%
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), // %LOCALAPPDATA%
    ];

    public static IReadOnlyList<CoworkProjectFolder> Discover()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            return Array.Empty<CoworkProjectFolder>();
        }

        var sessionsRoot = Path.Combine(appData, "Claude", "local-agent-mode-sessions");
        if (!Directory.Exists(sessionsRoot))
        {
            return Array.Empty<CoworkProjectFolder>();
        }

        var projectsByPath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> localJsons;
        try
        {
            localJsons = Directory.EnumerateFiles(
                sessionsRoot,
                "local_*.json",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = 0,
                });
        }
        catch (IOException)
        {
            return Array.Empty<CoworkProjectFolder>();
        }

        foreach (var jsonPath in localJsons)
        {
            // Files inside a local_<guid>/ subtree are the INNER session
            // state, not the outer session metadata we want to parse.
            var dirName = Path.GetFileName(Path.GetDirectoryName(jsonPath) ?? string.Empty);
            if (dirName.StartsWith("local_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? raw;
            try
            {
                raw = File.ReadAllText(jsonPath);
            }
            catch (IOException)
            {
                continue;
            }

            ProcessRecord(raw, projectsByPath);
        }

        return projectsByPath
            .Where(kv => IsAcceptableProjectRoot(kv.Key))
            .Select(kv => new CoworkProjectFolder(
                Hash: ShortHash(kv.Key),
                Path: kv.Key,
                SessionNames: kv.Value.Distinct().ToArray()))
            .ToList();
    }

    private static void ProcessRecord(string raw, Dictionary<string, List<string>> accumulator)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var sessionName = root.TryGetProperty("processName", out var pn) && pn.ValueKind == JsonValueKind.String
                ? pn.GetString() ?? "unknown"
                : "unknown";
            if (!root.TryGetProperty("userSelectedFolders", out var folders) || folders.ValueKind != JsonValueKind.Array)
            {
                return;
            }
            foreach (var f in folders.EnumerateArray())
            {
                if (f.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                var path = f.GetString();
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }
                var normalised = Path.GetFullPath(path.TrimEnd('\\', '/'));
                if (!accumulator.TryGetValue(normalised, out var names))
                {
                    accumulator[normalised] = names = new List<string>();
                }
                names.Add(sessionName);
            }
        }
        catch (JsonException)
        {
            // Malformed session file - skip it.
        }
    }

    private static bool IsAcceptableProjectRoot(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        // Refuse drive roots.
        var root = Path.GetPathRoot(path)?.TrimEnd('\\', '/');
        if (string.Equals(path.TrimEnd('\\', '/'), root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Refuse any of the user's profile roots.
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\', '/');
        if (!string.IsNullOrEmpty(profile) &&
            string.Equals(path.TrimEnd('\\', '/'), profile, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Refuse system folders (Windows, Program Files, %APPDATA%, etc.).
        foreach (var forbidden in ForbiddenRoots)
        {
            if (string.IsNullOrEmpty(forbidden))
            {
                continue;
            }
            var fb = forbidden.TrimEnd('\\', '/');
            if (string.Equals(path.TrimEnd('\\', '/'), fb, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(fb + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string ShortHash(string path)
    {
        var bytes = Encoding.UTF8.GetBytes(path.ToLowerInvariant());
        var hash = SHA256.HashData(bytes);
        // 10 hex chars is 5 bytes = 40 bits of entropy; collision-free for
        // tens of thousands of project folders.
        return Convert.ToHexString(hash)[..10].ToLowerInvariant();
    }
}
