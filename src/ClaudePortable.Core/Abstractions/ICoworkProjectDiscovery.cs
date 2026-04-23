using ClaudePortable.Core.Discovery;

namespace ClaudePortable.Core.Abstractions;

public interface ICoworkProjectDiscovery
{
    IReadOnlyList<CoworkProjectFolder> Discover();
}

public sealed class NullCoworkProjectDiscovery : ICoworkProjectDiscovery
{
    public static ICoworkProjectDiscovery Instance { get; } = new NullCoworkProjectDiscovery();
    public IReadOnlyList<CoworkProjectFolder> Discover() => Array.Empty<CoworkProjectFolder>();
}
