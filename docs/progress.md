# Progress Log

Checkpoint file. Update after every phase. Designed so a fresh Claude Code session can pick up where the previous one left off without re-reading the entire history.

## Phase status overview

| Phase | Title | Status | Issue | Closing commit |
|---|---|---|---|---|
| 0 | Path discovery | Partially done (ASSUMED rows remain until ProcMon) | #4 | (Phase 1 commit) |
| 1 | Scaffold + Core backup | DONE | (in #0 initial commit) | `Initial scaffold: Phases 0-3 CLI` |
| 2 | Restore engine + path rewrite | DONE | (in initial commit) | `Initial scaffold: Phases 0-3 CLI` |
| 3 | Folder targets + sync client discovery | DONE | (in initial commit) | `Initial scaffold: Phases 0-3 CLI` |
| 4 | Scheduler + retention (7/3/2) | DONE | #1 | `Phase 4: Retention rotation + Task Scheduler emitter` |
| 5 | WPF GUI + tray | DONE (minimal, 5 tabs + tray) | #2 | `Phase 5: WPF GUI + tray` |
| 6 | WiX MSI + signing + release workflow | TODO | #3 | - |

## Session log

### Session 1 (2026-04-22)
- Scope agreed: Phases 0-3 CLI, public repo, gh via winget.
- Deviated from spec: target `net10.0` / `net10.0-windows` (no .NET 8 SDK installed), FluentAssertions v8 skipped (license), inlined Xunit.Assert.
- Real-world issue: live Claude Desktop locks force `FileShare.ReadWrite | FileShare.Delete` + try/catch skip in `ZipArchiveWriter`.
- Real-world exclusions added: `**/LOCK`, `**/debug/latest`.
- 14 GB backup of the real `.claude` succeeded in 52s -> 5 GB ZIP. SHA-256 integrity verified.
- 34 xUnit tests green, CI green on `windows-latest`.
- Repo: https://github.com/shaase-ctrl/ClaudePortable (public).
- 7 follow-up issues created (Phase 4/5/6 + ProcMon + MCP path + Chat history + E2E).

### Session 2 (2026-04-22, same day)
- Phase 4 complete: `RetentionManager` with promote + prune + manifest-in-zip tier rewrite, `TaskSchedulerEmitter` (Windows TS XML), `TaskSchedulerInstaller` (schtasks.exe wrapper). Auto-rotate wired into `backup` CLI; new `rotate` and `schedule install|show|remove|emit` subcommands.
- Tests: 41 total, including 10-week simulated clock run. All green.
- Design note: no Quartz.NET in-process scheduler. Windows Task Scheduler is more reliable for a CLI tool; Quartz would only matter once a persistent tray process exists (Phase 5).

### Session 3 (2026-04-22, same day) - Phase 5
- App project flipped to `UseWPF=true` + `UseWindowsForms=true`, OutputType stays Exe so CLI still prints to console. Console flashes briefly on GUI launch -> Phase 6 MSI Start-menu shortcut mitigates this.
- `Program.Main` dispatches: no args OR `--gui` -> WPF. Args present -> CLI root command.
- Added: `App.cs` (WPF dispatcher), `TrayIcon.cs` (System.Windows.Forms.NotifyIcon), `MainWindow.xaml` (5 tabs: Status, Targets, Discovery, Restore, Logs), `MainViewModel.cs` (commands + observable collections), tiny `ViewModelBase` + `RelayCommand` (no CommunityToolkit dependency to keep NuGet list minimal).
- Auto-suggestion: when no target is configured, MainViewModel suggests `<OneDrive>/ClaudePortable` and creates the folder if missing.
- State persistence: target list in `%LOCALAPPDATA%\ClaudePortable\targets.json`.
- `UiLogSink` is a process-local in-memory log mirror; Logs tab binds directly to it.
- GUI smoke test: launched for 5s, exited cleanly. 41 tests still green. CLI `discover` still prints the same output as before.
- Deviations from spec:
  - Spec called for `CommunityToolkit.Mvvm` + `H.NotifyIcon.Wpf`. Swapped for hand-rolled `ViewModelBase` and `System.Windows.Forms.NotifyIcon` to avoid extra NuGet dependencies in alpha.
  - InvariantGlobalization removed from App csproj (WPF requires real culture data for `en-us` XAML defaults).

## Open risks to carry into Phase 5

1. **Phase 0 ASSUMED paths** still not verified via ProcMon (issue #4). Phase 5 should surface the discovery report in the UI so the user can notice missing paths.
2. **No logging framework wired up yet**; CLI writes to stderr. Phase 5 GUI should introduce Serilog (spec calls for `%LOCALAPPDATA%\ClaudePortable\logs\`).
3. **MCP config path unknown** (issue #5). Phase 5 "Einstellungen" tab should include the MCP config location once known.
4. **Chat-history portability** (issue #6) - Phase 5 Restore flow should surface the version mismatch warning visibly.
5. **FluentAssertions replaced with plain `Assert`.** Not a blocker; just a style note.

## How to resume the build in a fresh session

1. Read `docs/spec.md` (original German specification).
2. Read this file (progress.md).
3. Open the latest open issue ordered by `#number` (Phase 5 = #2).
4. `dotnet build -c Release` and `dotnet test -c Release` to sanity-check current state.
5. Start at the top of the next Phase's issue checklist.
