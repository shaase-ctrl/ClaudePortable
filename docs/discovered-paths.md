# Discovered Claude Paths (Phase 0)

Tracks which Claude artifact paths have been verified on a real install vs which are still assumptions.

**Last updated:** 2026-04-23 (Session 5: MCP config located, Store-app install confirmed)

**Verification method:** directory listing + content inspection on the dev machine. ProcMon-based verification of write-path dynamics is tracked in `docs/phase0-procmon.md` and issue #4.

## Path inventory

| Artifact | Path | Status | Source |
|---|---|---|---|
| Claude Desktop install | `C:\Program Files\WindowsApps\Claude_<version>_x64__pzs8sxrjxfjjc\` | VERIFIED (Store) | `Get-AppxPackage -Name Claude` 2026-04-23 |
| Claude Desktop package data | `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\` | VERIFIED | `ls` 2026-04-23 |
| Claude Desktop AppData (roaming) | `%APPDATA%\Claude\` | VERIFIED | `ls` 2026-04-22 (Preferences, IndexedDB, Local Storage, Extensions) |
| Claude Desktop IndexedDB | `%APPDATA%\Claude\IndexedDB\` | VERIFIED | `ls` 2026-04-22 |
| Claude Desktop Local Storage | `%APPDATA%\Claude\Local Storage\` | VERIFIED | `ls` 2026-04-22 |
| Claude Desktop Preferences | `%APPDATA%\Claude\Preferences` | VERIFIED | `ls` 2026-04-22 |
| **MCP servers + user preferences** | `%APPDATA%\Claude\claude_desktop_config.json` | VERIFIED | content inspection 2026-04-23 (issue #5) |
| Installed Claude Desktop extensions | `%APPDATA%\Claude\extensions-installations.json` | VERIFIED | content inspection 2026-04-23 |
| Claude Desktop Extensions (MCP server code) | `%APPDATA%\Claude\Claude Extensions\<extensionId>\` | VERIFIED | `ls` 2026-04-23 |
| Extension blocklist | `%APPDATA%\Claude\extensions-blocklist.json` | VERIFIED | `ls` 2026-04-23 |
| Runtime MCP server summary | `%APPDATA%\Claude\logs\mcp-info.json` | VERIFIED (regenerable) | content inspection 2026-04-23 |
| Claude Code user profile | `%USERPROFILE%\.claude\` | VERIFIED | `ls` 2026-04-22 (CLAUDE.md, agents, plugins, projects, skills, settings.json, sessions) |
| Claude Code plugins | `%USERPROFILE%\.claude\plugins\` | VERIFIED | `ls` 2026-04-22 |
| Claude Code skills | `%USERPROFILE%\.claude\skills\` | VERIFIED | `ls` 2026-04-22 |
| Claude Desktop Local AppData | `%LOCALAPPDATA%\Claude\` | VERIFIED (empty or small) | `ls` 2026-04-23 |
| Cowork sessions (Claude CLI agent mode) | `%APPDATA%\Claude\local-agent-mode-sessions\<guid>\...\.claude\` | VERIFIED (many, ephemeral) | `ls` 2026-04-23 - EXCLUDED by glob |

## Security-critical exclusions

These files are deliberately **not** backed up because they hold credentials or maschine-bound secrets:

| Path | Reason |
|---|---|
| `%APPDATA%\Claude\config.json` | Contains `oauth:tokenCache` (encrypted OAuth refresh-token blob). File is regenerated on first sign-in after restore. |
| `**/tokens.dat` | Generic token cache used by some Electron apps. |
| `**/Login Data*`, `**/Cookies*` | Chromium credential and cookie stores. |
| `**/mcp-needs-auth-cache.json` | Per-agent-mode OAuth-flow state, machine-bound. |
| `**/local-agent-mode-sessions/**` | Ephemeral sessions for Claude Code inside Claude Desktop; not user-meaningful to carry between machines. |
| `**/.remote-plugins/**` | Binary plugin cache; re-fetched on first `claude plugin sync`. |

## Operational exclusions (non-credential, just noise)

| Glob | Reason |
|---|---|
| `**/Cache/**`, `**/GPUCache/**`, `**/DawnGraphiteCache/**`, `**/DawnWebGPUCache/**`, `**/Code Cache/**`, `**/Extensions Update Cache/**` | Regenerable Electron/Chromium caches. |
| `**/Crashpad/**`, `**/VideoDecodeStats/**`, `**/Partitions/**`, `**/Network/**` | Electron subsystems. |
| `**/*.ldb.tmp`, `**/*-journal` | LevelDB/SQLite transient files. |
| `**/LOCK` | LevelDB lock file - cannot be read while the owning process is alive. |
| `**/debug/latest` | Reparse-point / symlink to the most recent debug log; unreadable without elevation. |

## Claude Desktop version detection

`Get-AppxPackage -Name Claude | Select-Object -ExpandProperty Version` returns the installed version (e.g. `1.3883.0.0`). The `BackupEngine` calls this and stores the result in `manifest.claudeDesktopVersion`. On restore, `VersionGating.Evaluate(...)` emits an Info/Warn/Block result based on semantic-version comparison (issue #6).

## Still pending

- **ProcMon dynamic capture** (issue #4): observe what Claude Desktop actually writes during cold start, new chat, extension install, connector auth, and MCP-call flows. Playbook in `docs/phase0-procmon.md`.
- **Win32 Claude Desktop installs** (as opposed to the Microsoft Store variant confirmed above): if Anthropic ever ships a non-Store MSI, re-run this inventory on that install and add a second column to this table.
