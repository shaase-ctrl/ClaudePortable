namespace ClaudePortable.Core.Abstractions;

public sealed record ArchiveEntry(string RelativePath, string AbsolutePath);

public sealed record ArchiveResult(long SizeBytes, int FileCount, string Sha256);

public interface IArchiveWriter
{
    Task<ArchiveResult> WriteAsync(
        string destinationZipPath,
        IEnumerable<ArchiveEntry> entries,
        string manifestJson,
        CancellationToken cancellationToken = default);
}
