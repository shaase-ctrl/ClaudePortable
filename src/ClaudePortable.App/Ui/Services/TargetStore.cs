using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudePortable.App.Ui.Services;

[SupportedOSPlatform("windows")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822", Justification = "Instance methods for DI and mocking.")]
public sealed class TargetStore
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"),
        "ClaudePortable",
        "targets.json");

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return Array.Empty<string>();
        }
        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize(json, TargetStoreJsonContext.Default.StringArray) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    public void Save(IReadOnlyList<string> targets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var json = JsonSerializer.Serialize(targets.ToArray(), TargetStoreJsonContext.Default.StringArray);
        File.WriteAllText(ConfigPath, json);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(string[]))]
internal sealed partial class TargetStoreJsonContext : JsonSerializerContext
{
}
