using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ClaudePortable.Core.Abstractions;

namespace ClaudePortable.Core.Archive;

public sealed class ZipArchiveWriter : IArchiveWriter
{
    private readonly TextWriter _warningSink;

    public ZipArchiveWriter() : this(Console.Error) { }

    public ZipArchiveWriter(TextWriter warningSink)
    {
        _warningSink = warningSink;
    }

    public async Task<ArchiveResult> WriteAsync(
        string destinationZipPath,
        IEnumerable<ArchiveEntry> entries,
        string manifestJson,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new OperationProgress("Preparing"));
        var orderedEntries = entries
            .OrderBy(e => e.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var readableEntries = new List<ArchiveEntry>(orderedEntries.Count);
        for (var i = 0; i < orderedEntries.Count; i++)
        {
            if ((i & 0xFF) == 0)
            {
                progress?.Report(new OperationProgress("Checking accessibility", i, orderedEntries.Count));
            }
            var entry = orderedEntries[i];
            if (CanOpenForReadShared(entry.AbsolutePath, out var reason))
            {
                readableEntries.Add(entry);
            }
            else
            {
                _warningSink.WriteLine($"warning: skipping locked or unreadable file '{entry.AbsolutePath}': {reason}");
            }
        }

        var contentHash = await ComputeContentHashAsync(readableEntries, progress, cancellationToken).ConfigureAwait(false);
        var finalManifest = manifestJson.Replace("__SHA256_PLACEHOLDER__", contentHash, StringComparison.Ordinal);

        long totalBytes = 0;
        var tempPath = destinationZipPath + ".tmp";
        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        await using (var fs = File.Create(tempPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var ms = manifestEntry.Open())
            await using (var writer = new StreamWriter(ms, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(finalManifest).ConfigureAwait(false);
            }

            for (var i = 0; i < readableEntries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = readableEntries[i];
                if ((i & 0x3F) == 0)
                {
                    progress?.Report(new OperationProgress("Writing archive", i, readableEntries.Count));
                }
                FileStream source;
                try
                {
                    source = OpenReadShared(entry.AbsolutePath);
                }
                catch (IOException ex)
                {
                    _warningSink.WriteLine($"warning: could not open '{entry.AbsolutePath}' during archive: {ex.Message}");
                    continue;
                }
                catch (UnauthorizedAccessException ex)
                {
                    _warningSink.WriteLine($"warning: access denied to '{entry.AbsolutePath}': {ex.Message}");
                    continue;
                }

                await using (source)
                {
                    var zipEntry = zip.CreateEntry(entry.RelativePath, CompressionLevel.Optimal);
                    await using var dest = zipEntry.Open();
                    try
                    {
                        await source.CopyToAsync(dest, cancellationToken).ConfigureAwait(false);
                        totalBytes += source.Length;
                    }
                    catch (IOException ex)
                    {
                        _warningSink.WriteLine($"warning: copy failed on '{entry.AbsolutePath}': {ex.Message}");
                    }
                }
            }
            progress?.Report(new OperationProgress("Writing archive", readableEntries.Count, readableEntries.Count));
        }

        progress?.Report(new OperationProgress("Finalising archive"));
        File.Move(tempPath, destinationZipPath, overwrite: true);
        return new ArchiveResult(totalBytes, readableEntries.Count, contentHash);
    }

    private static bool CanOpenForReadShared(string path, out string reason)
    {
        try
        {
            using var probe = OpenReadShared(path);
            reason = string.Empty;
            return true;
        }
        catch (IOException ex)
        {
            reason = ex.Message;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    private static FileStream OpenReadShared(string path)
    {
        return new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
    }

    private async Task<string> ComputeContentHashAsync(
        List<ArchiveEntry> entries,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        var buffer = new byte[81920];
        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i];
            if ((i & 0x3F) == 0)
            {
                progress?.Report(new OperationProgress("Hashing content", i, entries.Count));
            }
            var headerBytes = Encoding.UTF8.GetBytes(entry.RelativePath + "\n");
            sha.TransformBlock(headerBytes, 0, headerBytes.Length, null, 0);
            FileStream stream;
            try
            {
                stream = OpenReadShared(entry.AbsolutePath);
            }
            catch (IOException ex)
            {
                _warningSink.WriteLine($"warning: skipping '{entry.AbsolutePath}' for hash: {ex.Message}");
                continue;
            }
            await using (stream)
            {
                try
                {
                    int read;
                    while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        sha.TransformBlock(buffer, 0, read, null, 0);
                    }
                }
                catch (IOException ex)
                {
                    _warningSink.WriteLine($"warning: read failed on '{entry.AbsolutePath}' during hash: {ex.Message}");
                }
            }
        }
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }
}
