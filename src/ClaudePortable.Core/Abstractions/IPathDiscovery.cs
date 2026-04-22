namespace ClaudePortable.Core.Abstractions;

public sealed record DiscoveredClaudePath(string Key, string Path, bool Exists, string Source);

public interface IPathDiscovery
{
    IReadOnlyList<DiscoveredClaudePath> Discover();
}
