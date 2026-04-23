# End-to-End Test Playbook (OneDrive roundtrip, two VMs)

Spec reference: section 7.3. This playbook verifies that a backup taken on one Windows 11 machine survives the round-trip through OneDrive and restores correctly on a second Windows 11 machine.

Run this before tagging a release that changes anything in `ClaudePortable.Core`, `ClaudePortable.Targets`, or the manifest schema.

## Prerequisites

- Two clean Windows 11 VMs (`VM-A`, `VM-B`) with internet access and a working Microsoft account.
- `ClaudePortable-<version>.msi` built from the release tag you are testing.
- A OneDrive Personal account (same account logged in on both VMs).

## VM-A: produce a backup

1. Install Claude Desktop from the Microsoft Store.
2. Install Claude Code: download the installer from https://claude.com/download and run it. Verify `claude --version` works in a new PowerShell.
3. Launch Claude Desktop; sign in; exchange at least two chat messages so the IndexedDB has content; install one MCP extension from the in-app marketplace; authorize one connector (Gmail or Calendar are good candidates).
4. Launch Claude Code; sign in; run `claude plugin install engineering` and one more plugin so `~/.claude/plugins/` is populated.
5. Sign in to OneDrive Personal; let the initial sync complete (system tray icon shows "Up to date").
6. Install `ClaudePortable-<version>.msi` via double-click. Accept the SmartScreen warning ("Run anyway"); this is expected until the MSI is code-signed.
7. Launch ClaudePortable from the Start menu.
8. Targets tab: confirm the auto-suggested target is `<OneDrive>\ClaudePortable\` (or add it manually).
9. Discovery tab: all four Claude path rows show FOUND; at least OneDrive (Personal) shows OK.
10. Status tab: click "Backup now". Wait for the row in the grid to appear.
11. Right-click the OneDrive tray icon: "View sync problems". Resolve any before continuing. The new `.zip` must show a green check.
12. Note the filename and the `sha256` value from the ClaudePortable Status tab.

## VM-B: restore

1. Install Claude Desktop, Claude Code, OneDrive client. Sign in to the same Microsoft account; let sync complete. Do **not** open Claude Desktop yet.
2. Install `ClaudePortable-<version>.msi`.
3. Launch ClaudePortable.
4. Targets tab: add `<OneDrive>\ClaudePortable\` if it is not auto-added.
5. Restore tab: the backup from VM-A is in the list. Select it, click "Restore selected", confirm the dialog.
6. When the restore finishes, open `post-restore-checklist-*.md` (path shown in the status bar).
7. Check the Logs tab for the `version gate:` line. Expected levels:
   - `Ok` - same Claude Desktop version on both VMs.
   - `Warn - Minor-version mismatch` - small version diff; restore proceeds but LevelDB schema changes may affect chat history.
   - `Block` - major version difference; restore is aborted. Upgrade/downgrade the installed Claude Desktop to match, or re-run with `--ignore-version-mismatch` from the CLI.

## Verify

1. Close and re-open Claude Desktop. Chat history should be present (if the version gate was `Ok` or `Warn`).
2. Open Claude Code. Run `claude plugin list` - installed plugins from VM-A should appear. Re-run `claude plugin sync` if anything is stale.
3. Confirm one previously-authorized connector. Expected: it prompts for re-auth. This is intentional - tokens are never backed up.
4. Open `%APPDATA%\Claude\claude_desktop_config.json` - MCP server list matches VM-A.
5. `%APPDATA%\Claude\config.json` is **absent from the restored state** on the first re-launch (file is regenerated on sign-in). Confirm `oauth:tokenCache` is empty or newly minted. This is the intended exclusion.

## Pass criteria

- [ ] `sha256` of the restored `claude-backup_*.zip` on VM-B matches the value noted on VM-A.
- [ ] Chat history is readable in Claude Desktop after restore.
- [ ] Plugin list in Claude Code is identical to VM-A.
- [ ] `claude_desktop_config.json` is restored verbatim.
- [ ] Token-cache entries (`oauth:tokenCache`) are **not** restored from the backup.
- [ ] `%APPDATA%\Claude_backup_<timestamp>\` exists on VM-B (safety backup of the empty pre-restore state).
- [ ] Post-restore checklist markdown is saved to `%LOCALAPPDATA%\ClaudePortable\`.

## Common failures and what they mean

| Symptom | Likely cause | Fix |
|---|---|---|
| Restore aborts with "Major-version mismatch" | Claude Desktop on VM-B is a major version ahead or behind. | Install matching Claude Desktop version, or re-run with `--ignore-version-mismatch` on the CLI and accept the IndexedDB risk. |
| `sha256` differs | File was modified in the OneDrive folder after backup (AV scan, re-indexing). | Re-run backup on VM-A; verify OneDrive sync completes before listing on VM-B. |
| Chat history empty | Claude Desktop version on VM-B mutated the LevelDB schema on first start after restore. | Track the issue; matches spec known-limitation #4. |
| Connectors appear to work without re-auth | Stale tokens survived. | File a bug: exclusion globs should have prevented `tokens.dat` / `Login Data*` / `Cookies*`. |

## Automating the verification (optional)

A PowerShell verification script template lives in `scripts/e2e-verify.ps1` (TODO, not yet written). It should:
1. Check `Test-Path` for each path in `WindowsPathDiscovery`.
2. Read the restored `claude_desktop_config.json`, assert `mcpServers` keys match the pre-backup capture.
3. Read `extensions-installations.json`, diff extensions list against the backup manifest `sourcePaths`.
4. Assert `config.json` was NOT restored (file size should be 0 or absent).

This script is a follow-up; see the "Automate E2E verification" issue on the repo.
