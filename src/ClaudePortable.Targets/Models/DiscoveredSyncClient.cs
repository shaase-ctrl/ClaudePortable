namespace ClaudePortable.Targets.Models;

public sealed record DiscoveredSyncClient(
    string Name,
    string Path,
    bool IsAvailable,
    string Source);
