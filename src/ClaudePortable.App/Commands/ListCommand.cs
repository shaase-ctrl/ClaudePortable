using System.CommandLine;
using System.Runtime.Versioning;
using System.Text.Json;
using ClaudePortable.Targets;

namespace ClaudePortable.App.Commands;

[SupportedOSPlatform("windows")]
public static class ListCommand
{
    public static Command Build()
    {
        var inOption = new Option<DirectoryInfo>(
            aliases: new[] { "--in", "-i" },
            description: "Folder to list backups from.")
        {
            IsRequired = true,
        };

        var jsonOption = new Option<bool>(
            aliases: new[] { "--json" },
            description: "Emit machine-readable JSON.",
            getDefaultValue: () => false);

        var cmd = new Command("list", "List backups in a folder.")
        {
            inOption,
            jsonOption,
        };

        cmd.SetHandler((inValue, asJson) =>
        {
            var target = new FolderTarget(inValue.FullName);
            var backups = target.ListBackups();

            if (asJson)
            {
                var payload = backups.Select(b => new
                {
                    fileName = b.FileName,
                    fullPath = b.FullPath,
                    sizeBytes = b.SizeBytes,
                    tier = b.Manifest?.RetentionTier.ToString().ToLowerInvariant(),
                    createdAt = b.Manifest?.CreatedAt,
                    hostname = b.Manifest?.Hostname,
                    sha256 = b.Manifest?.Sha256,
                });
                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            if (backups.Count == 0)
            {
                Console.WriteLine("no backups found.");
                return;
            }

            Console.WriteLine($"{"tier",-8} {"created",-20} {"size",10}  file");
            foreach (var b in backups)
            {
                var tier = b.Manifest?.RetentionTier.ToString().ToLowerInvariant() ?? "?";
                var created = b.Manifest?.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) ?? "?";
                Console.WriteLine($"{tier,-8} {created,-20} {b.SizeBytes,10:N0}  {b.FileName}");
            }
        }, inOption, jsonOption);

        return cmd;
    }
}
