# Changelog

All notable changes to ClaudePortable are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.2] - 2026-06-03

### Fixed

- **Restore button stayed disabled after selecting a snapshot.** On the Restore
  tab, "Restore selected snapshot" is gated on a backup being selected, but
  `SelectedBackup` raised no change notification and `AsyncRelayCommand` does not
  hook `CommandManager.RequerySuggested`, so its `CanExecute` was evaluated once
  (nothing selected -> disabled) and never re-queried when a row was picked - the
  button was therefore permanently greyed out. `SelectedBackup` is now a notifying
  property that raises `RestoreCommand` `CanExecuteChanged`, matching the existing
  `SelectedTarget` fix. "Restore from file..." was unaffected and was the
  workaround on 0.3.1.

## [0.3.1] - 2026-06-01

### Fixed

- **Discovery no longer shows a permanently-empty `coworkSessions` row.**
  The `%USERPROFILE%\.cowork` candidate was an unverified Spec 1.1 guess that
  does not exist on the Store-app Claude Desktop, so its read-only EXISTS box
  always rendered unchecked and looked like a broken toggle. Removed the
  vestigial entry from `WindowsPathDiscovery` and its `cowork/sessions`
  archive-prefix mapping. Cowork data is unaffected: session state under
  `%APPDATA%\Claude\local-agent-mode-sessions` is already covered by the
  Claude Desktop AppData path, and project folders are captured by
  `CoworkProjectDiscovery` under `cowork-projects/<hash>`. The Discovery
  caption now explains that EXISTS is a status indicator, not a toggle. The
  restore-side legacy mapping for `cowork/sessions` is retained so older ZIPs
  still restore correctly.

## [0.3.0] - 2026-05-29

### Added

- **Serilog file logging.** CLI and GUI write a rolling daily log to
  `%LOCALAPPDATA%\ClaudePortable\logs\claudeportable-.log` (30-day
  retention). The GUI Logs tab still mirrors live output.
- **Dynamic post-restore checklist** generated from the restore result -
  version-gate warnings, `.claude/plugins` reinstall hints, safety-backup
  paths, and a per-target restore summary - written to
  `%LOCALAPPDATA%\ClaudePortable\post-restore-checklist-<timestamp>.md`, with
  an "Open Checklist" button in the Restore tab after a restore completes.
- **`scripts/e2e-verify.ps1`** - automated backup-ZIP verification: manifest
  schema and required fields, expected content directories, credential
  exclusions (`tokens.dat`, `Login Data*`, `Cookies*`,
  `claude-desktop/appdata/config.json`), MCP-server key comparison, and
  post-restore checklist section checks.

### Changed

- Post-restore checklist text is now English (was German).
- `scripts/build-exe.ps1` and `src/ClaudePortable.Installer/build-msi.ps1`
  default version bumped from `0.2.0` to `0.3.0`.

### Fixed

- The logging/checklist/e2e work did not compile; restored a buildable
  state - added the real Serilog sink packages (`Serilog.Sinks.File`,
  `Serilog.Sinks.Console`), a missing `Program` class brace, `CA1305`
  format-provider arguments, an unresolved `RestoreTargetReport` import, the
  `StringToVisibleConverter` `System.Windows` import, and a parameterless
  `PostRestoreChecklistBuilder.Build()` overload so the backup path compiles.
- `scripts/e2e-verify.ps1` correctness - assert the manifest's content hash
  is a valid digest (it is not the zip-file hash), match credential
  exclusions by file name (`-Filter **/...` matched nothing and always
  passed), and count manifest dictionary keys correctly.

## [0.2.0] - 2026-05-10

### Added

- **Schedule sidebar section** that enumerates every Windows scheduled task
  via `schtasks.exe /Query /FO CSV /V` and flags Claude relevance with a
  three-tier classifier (green = ClaudePortable-managed, orange =
  foreign-but-Claude-related, gray = unrelated). Use it to spot a legacy
  `\Claude-Desktop-Backup`-style PowerShell task that writes loose-file
  backups into a long-path OneDrive folder and breaks sync.
- Per-row Run / Disable / Enable / Delete / View XML buttons in the new
  Schedule view, with confirmation dialogs on destructive actions and
  clipboard copy of the raw Task Scheduler XML.
- CLI: `claudeportable schedule list [--all|--managed|--relevant]
  [--json]` to enumerate tasks from the terminal, plus
  `schedule disable|enable|run <name>` for symmetry with the new GUI
  buttons.
- `ScheduledTaskClassifier` in `ClaudePortable.Scheduler` with a stable,
  testable marker list (`.claude`, `Claude_pzs8sxrjxfjjc`, `Cowork`,
  `CoWork\Backup`, `local-agent-mode-sessions`, `claude-desktop`,
  `anthropic`, etc.).
- `TaskSchedulerInstaller.EnumerateAsync` / `DisableAsync` /
  `EnableAsync` / `RunNowAsync` / `GetTaskXmlAsync`, all going through a
  single internal `Func<>` seam so unit tests can assert exact `schtasks`
  argv without invoking the executable.
- Fixture-driven CSV-parser tests covering German `schtasks` headers,
  quoted-path executables (`"C:\Program Files\..."`), unquoted paths with
  spaces, subfolder task names, disabled state, and `ManagedBy`
  classification.

### Changed

- README upgraded from "Five sections" to "Six sections" GUI overview and
  refreshed test count (61 -> 98 xUnit cases).
- `scripts/build-exe.ps1` and `src/ClaudePortable.Installer/build-msi.ps1`
  default version bumped from `0.1.x` to `0.2.0`.

### Not changed (out of scope, intentionally)

- ClaudePortable does NOT auto-disable or auto-delete any task it finds.
  The user explicitly clicks each action; confirmation dialogs gate
  Disable and Delete.
- The existing backup engine, restore engine, and archive format are
  untouched. A 0.1.x backup ZIP restores identically on 0.2.0.

## [0.1.x]

Initial alpha releases focused on backup, restore, retention rotation,
sync-client discovery, and a single-task scheduler install command. See
git history and the GitHub Releases page for per-version detail.
