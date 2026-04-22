using System.CommandLine;
using System.Runtime.Versioning;
using System.Text.Json;
using ClaudePortable.Core.Discovery;
using ClaudePortable.Targets;

namespace ClaudePortable.App.Commands;

[SupportedOSPlatform("windows")]
public static class DiscoverCommand
{
    public static Command Build()
    {
        var jsonOption = new Option<bool>(
            aliases: new[] { "--json" },
            description: "Emit machine-readable JSON.",
            getDefaultValue: () => false);

        var cmd = new Command("discover", "Report detected Claude paths and sync clients.")
        {
            jsonOption,
        };

        cmd.SetHandler(asJson =>
        {
            var paths = new WindowsPathDiscovery().Discover();
            var syncClients = new SyncClientDiscovery().Discover();

            if (asJson)
            {
                var payload = new
                {
                    claudePaths = paths.Select(p => new { p.Key, p.Path, p.Exists, p.Source }),
                    syncClients = syncClients.Select(s => new { s.Name, s.Path, s.IsAvailable, s.Source }),
                };
                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            Console.WriteLine("== Claude paths ==");
            foreach (var p in paths)
            {
                Console.WriteLine($"  [{(p.Exists ? "FOUND" : " MISS")}] {p.Key,-28} {p.Path}");
                Console.WriteLine($"           source: {p.Source}");
            }

            Console.WriteLine();
            Console.WriteLine("== Sync clients ==");
            if (syncClients.Count == 0)
            {
                Console.WriteLine("  (none detected)");
            }
            foreach (var s in syncClients)
            {
                Console.WriteLine($"  [{(s.IsAvailable ? " OK " : "MISS")}] {s.Name,-26} {s.Path}");
                Console.WriteLine($"           source: {s.Source}");
            }
        }, jsonOption);

        return cmd;
    }
}
