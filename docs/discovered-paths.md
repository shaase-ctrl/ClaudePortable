# Discovered Claude Paths (Phase 0)

This file tracks which Claude artefact paths have been verified on the dev machine vs which are still assumptions taken from the spec.

**Last updated:** 2026-04-22

**Verification method so far:** directory listing on the dev machine.

**Pending:** User-led Process Monitor run to confirm write paths while Claude Desktop is active.

## Path inventory

| Artefact | Path | Status | Source |
|---|---|---|---|
| Claude Desktop AppData | `%APPDATA%\Claude\` | VERIFIED | `ls` 2026-04-22 (Preferences, IndexedDB, Local Storage, Extensions Settings all present) |
| Claude Desktop IndexedDB | `%APPDATA%\Claude\IndexedDB\` | VERIFIED | `ls` 2026-04-22 |
| Claude Desktop Local Storage | `%APPDATA%\Claude\Local Storage\` | VERIFIED | `ls` 2026-04-22 |
| Claude Desktop Preferences | `%APPDATA%\Claude\Preferences` | VERIFIED | `ls` 2026-04-22 |
| Claude Code user profile | `%USERPROFILE%\.claude\` | VERIFIED | `ls` 2026-04-22 (CLAUDE.md, agents, plugins, projects, skills, settings.json, sessions) |
| Claude Code plugins | `%USERPROFILE%\.claude\plugins\` | VERIFIED | `ls` 2026-04-22 |
| Claude Code skills | `%USERPROFILE%\.claude\skills\` | VERIFIED | `ls` 2026-04-22 |
| Claude Desktop Local AppData | `%LOCALAPPDATA%\Claude\` | ASSUMED | Spec 1.1; presence not confirmed |
| Cowork sessions | `%USERPROFILE%\.cowork\` (fallback: `%APPDATA%\Claude\cowork\sessions\`) | ASSUMED | Spec 1.1; ProcMon pending |
| MCP-Server-Config | unknown | PENDING | Spec 11.4 - not yet located |
| Installed Connectors (metadata) | unknown (likely inside Claude Desktop config DB) | PENDING | Spec 1.1 item "Connector-Metadaten (ohne Tokens)" |

## Excluded on purpose

These paths are never written to the backup (see `DefaultExclusions` in source):

- `**/Cache/**`, `**/GPUCache/**`, `**/Code Cache/**`, `**/Extensions Update Cache/**`
- `**/DawnGraphiteCache/**`, `**/DawnWebGPUCache/**`
- `**/Crashpad/**`, `**/VideoDecodeStats/**`, `**/Partitions/**`, `**/Network/**`
- `**/tokens.dat`, `**/Login Data*`, `**/Cookies*`
- `**/*.ldb.tmp`, `**/*-journal`
- `**/.remote-plugins/**`

## Next step (Phase 0 completion)

Run Process Monitor while Claude Desktop starts, opens a chat, installs a plugin, and authenticates a connector. Filter by process name `Claude.exe`. Export the CSV and:

1. Confirm the paths above.
2. Add any newly observed write paths.
3. Identify MCP-Server-Config location (Spec 11.4).
4. Verify whether Cowork state lives in `%APPDATA%` or `%USERPROFILE%` on this Windows build.

Once verified, flip the `ASSUMED`/`PENDING` rows above to `VERIFIED` and close the Phase 0 GitHub issue.
