using System.Runtime.Versioning;
using System.Text.Json;
using ClaudePortable.Targets.Models;
using Microsoft.Win32;

namespace ClaudePortable.Targets;

[SupportedOSPlatform("windows")]
public sealed class SyncClientDiscovery
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method for DI and mock-ability.")]
    public IReadOnlyList<DiscoveredSyncClient> Discover()
    {
        var results = new List<DiscoveredSyncClient>();
        TryAdd(results, FindOneDrivePersonal);
        TryAdd(results, FindOneDriveBusiness);
        TryAdd(results, FindDropbox);
        TryAdd(results, FindGoogleDriveDesktop);
        return results;
    }

    private static void TryAdd(List<DiscoveredSyncClient> results, Func<DiscoveredSyncClient?> discover)
    {
        try
        {
            var client = discover();
            if (client is not null)
            {
                results.Add(client);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    private static DiscoveredSyncClient? FindOneDrivePersonal()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive");
        var path = key?.GetValue("UserFolder") as string;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        return new DiscoveredSyncClient("OneDrive (Personal)", path, Directory.Exists(path), @"HKCU\Software\Microsoft\OneDrive\UserFolder");
    }

    private static DiscoveredSyncClient? FindOneDriveBusiness()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts\Business1");
        var path = key?.GetValue("UserFolder") as string;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        return new DiscoveredSyncClient("OneDrive (Business)", path, Directory.Exists(path), @"HKCU\Software\Microsoft\OneDrive\Accounts\Business1\UserFolder");
    }

    private static DiscoveredSyncClient? FindDropbox()
    {
        var infoPath = Path.Combine(
            Environment.ExpandEnvironmentVariables("%APPDATA%"),
            "Dropbox",
            "info.json");
        if (!File.Exists(infoPath))
        {
            return null;
        }
        var json = File.ReadAllText(infoPath);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("personal", out var personal)
            && personal.TryGetProperty("path", out var pathEl)
            && pathEl.GetString() is { Length: > 0 } dropboxPath)
        {
            return new DiscoveredSyncClient("Dropbox (Personal)", dropboxPath, Directory.Exists(dropboxPath), infoPath);
        }
        if (doc.RootElement.TryGetProperty("business", out var business)
            && business.TryGetProperty("path", out var bpathEl)
            && bpathEl.GetString() is { Length: > 0 } bPath)
        {
            return new DiscoveredSyncClient("Dropbox (Business)", bPath, Directory.Exists(bPath), infoPath);
        }
        return null;
    }

    private static DiscoveredSyncClient? FindGoogleDriveDesktop()
    {
        var prefPath = Path.Combine(
            Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"),
            "Google",
            "DriveFS",
            "root_preference_sqlite.db");
        if (!File.Exists(prefPath))
        {
            return null;
        }
        return new DiscoveredSyncClient(
            "Google Drive (Desktop)",
            "<mount-point-not-parsed>",
            IsAvailable: true,
            prefPath);
    }
}
