using ClaudePortable.Core.Abstractions;

namespace ClaudePortable.Core.Archive;

public static class FileEnumerator
{
    public static IEnumerable<ArchiveEntry> Enumerate(
        string absoluteRoot,
        string archivePrefix,
        ExclusionGlob exclusions)
    {
        if (!Directory.Exists(absoluteRoot))
        {
            yield break;
        }

        var rootFull = Path.GetFullPath(absoluteRoot).TrimEnd('\\', '/');
        foreach (var file in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
        {
            var relativeToSource = Path.GetRelativePath(rootFull, file).Replace('\\', '/');
            var archivePath = string.IsNullOrEmpty(archivePrefix)
                ? relativeToSource
                : $"{archivePrefix.TrimEnd('/')}/{relativeToSource}";

            if (exclusions.IsExcluded(archivePath) || exclusions.IsExcluded(relativeToSource))
            {
                continue;
            }

            yield return new ArchiveEntry(archivePath, file);
        }
    }
}
