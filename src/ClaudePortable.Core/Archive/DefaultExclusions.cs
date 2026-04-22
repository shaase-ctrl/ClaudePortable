namespace ClaudePortable.Core.Archive;

public static class DefaultExclusions
{
    public static IReadOnlyList<string> Globs { get; } = new[]
    {
        "**/Cache/**",
        "**/GPUCache/**",
        "**/DawnGraphiteCache/**",
        "**/DawnWebGPUCache/**",
        "**/Code Cache/**",
        "**/Extensions Update Cache/**",
        "**/Crashpad/**",
        "**/VideoDecodeStats/**",
        "**/Partitions/**",
        "**/Network/**",
        "**/*.ldb.tmp",
        "**/*-journal",
        "**/tokens.dat",
        "**/Login Data*",
        "**/Cookies*",
        "**/.remote-plugins/**",
        "**/LOCK",
        "**/debug/latest",
    };
}
