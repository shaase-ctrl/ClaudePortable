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
        "**/*-wal",
        "**/tokens.dat",
        "**/Login Data*",
        "**/Cookies*",
        "**/.remote-plugins/**",
        "**/LOCK",
        "**/debug/latest",
        "claude-desktop/appdata/config.json",
        // "**/local-agent-mode-sessions/**" used to be here, excluded as
        // "ephemeral agent state". Removed 2026-04-23 after a user-
        // reported restore showed these are actually the Cowork
        // projects' per-session metadata (CLAUDE.md, settings.json,
        // sessions, agents, plugins, skills). We KEEP the OAuth /
        // debug-reparse-point / leveldb-lock guards below.
        "**/mcp-needs-auth-cache.json",
        // Project-folder noise for auto-backed-up Cowork projects.
        // Intentionally NOT included are the short generic names "bin",
        // "obj", "dist", "build", "out", "target" because they
        // frequently appear inside Claude Desktop Extensions
        // (e.g. "Claude Extensions/pdf-server/dist/index.js" is the
        // real MCP-server entry point). The remaining names are narrow
        // enough to only match real project noise.
        "**/node_modules/**",
        "**/.git/objects/**",
        "**/.git/lfs/**",
        "**/.venv/**",
        "**/venv/**",
        "**/__pycache__/**",
        "**/.next/**",
        "**/.nuxt/**",
        "**/.gradle/**",
        "**/.idea/**",
        "**/.vs/**",
        "**/.DS_Store",
        "**/Thumbs.db",
        "**/*.pyc",
        "**/*.swp",
    };
}
