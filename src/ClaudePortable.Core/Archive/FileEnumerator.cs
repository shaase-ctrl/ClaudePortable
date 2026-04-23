using ClaudePortable.Core.Abstractions;

namespace ClaudePortable.Core.Archive;

public static class FileEnumerator
{
    // EnumerationOptions with IgnoreInaccessible=true is the key knob:
    // without it, the FIRST UnauthorizedAccessException or IOException
    // during recursive enumeration terminates the iterator and we lose
    // every file past the failure. That's exactly what happens on
    // %APPDATA%\Claude when it's a reparse point into a Store-app
    // sandbox - some per-child ACL denies access and the whole
    // 10,000-file enumeration collapses to the files that the walker
    // managed to yield before the throw. AttributesToSkip=0 makes sure
    // we don't silently drop System/Hidden marked files either, since
    // the Store app uses those flags liberally on its own data.
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
    };

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
        foreach (var file in Directory.EnumerateFiles(rootFull, "*", Options))
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
