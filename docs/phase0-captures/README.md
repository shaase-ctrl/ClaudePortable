# Phase 0 dynamic captures

This directory holds the output of `scripts/claude-path-diff.ps1` runs. Each file captures a before/after snapshot diff of Claude Desktop's persistent state during a named scenario.

Filename convention: `<YYYY-MM-DD>_<scenario>.diff.txt`

## 2026-04-23 idle60s (the reference capture)

Scenario: Claude Desktop running, window in background, no user input for 60 seconds. Scope: `%APPDATA%\Claude`, `%LOCALAPPDATA%\Claude`, `%USERPROFILE%\.claude`, `%USERPROFILE%\.cowork`, `%LOCALAPPDATA%\Packages\Claude_*`.

### Key findings

1. **`%APPDATA%\Claude` is a reparse point** pointing at `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude`. Both paths resolve to the same files. `WindowsPathDiscovery` only lists `%APPDATA%\Claude`, which is correct - adding the Packages path would double-count every byte. `Get-Item` confirms `LinkType` is empty but `Target` points to the Packages location.

2. **44 files modified in 60 seconds while idle**, all of them either:
   - Diagnostic logs (`logs/*.log`) - low-value but harmless
   - LevelDB / SQLite journal/WAL files (`*-journal`, `*-wal`, `*.log` inside `leveldb/`, `*.baj` inside `Cache/`) - transient
   - Cookie stores (`Network/Cookies*`) - already excluded
   - Sentry error-reporting state (`sentry/*.json`) - small and session-local

3. **New rule added to `DefaultExclusions.Globs`**: `**/*-wal`. Matches `DIPS-wal` (Chromium Distributed Incident Permissions Service) and SQLite WAL files. Not credential-bearing; purely transient.

4. **Nothing security-critical was written during idle**. No tokens, no OAuth blobs, no new credential files appeared. Existing exclusions for `config.json`, `**/Login Data*`, `**/Cookies*`, `**/tokens.dat`, `**/local-agent-mode-sessions/**` remain correct.

### Not yet captured

Per `docs/phase0-procmon.md`, these scenarios still need runs:

- B. New chat roundtrip
- C. MCP extension install
- D. Connector OAuth authorization (browser OAuth - manual only)
- E. MCP server call end-to-end

For each, run `pwsh scripts/claude-path-diff.ps1 -Scenario <name> -Duration 45` between the two relevant user actions. Drop the output here and update `discovered-paths.md` if new write paths appear.
