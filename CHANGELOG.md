# Changelog

All notable changes to ClaudePortable are documented here. Format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
