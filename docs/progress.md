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
| 6 | WiX MSI + signing + release workflow | DONE (MSI + release.yml; signing still pending) | #3 | `Phase 6: WiX MSI installer + release workflow` |

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

### Session 4 (2026-04-23) - Phase 6
- Installed `wix` as a .NET global tool (v7.0.0); accepted the OSMF EULA (`wix eula accept wix7`); added the `WixToolset.UI.wixext` extension.
- Replaced the placeholder Installer csproj with a WiX 7 SDK-style `.wixproj` plus `Product.wxs`. Used WiX 7's `<Files>` element (new in v7) to pick up the 272-file self-contained publish output without hand-maintaining components.
- Install scope: `perMachine` (Program Files 64), matches spec section 6 Phase 6. ICE38/43/57/64 satisfied by keeping the shortcut's KeyPath in HKCU even though the package is perMachine.
- Start-menu shortcut points to `claudeportable.exe --gui`, hides the CLI flash.
- `build-msi.ps1` script drives local MSI build: publish self-contained win-x64 -> invoke wixproj. Produces `ClaudePortable-<version>.msi` (~61 MB).
- `.github/workflows/release.yml`: runs on tag push (`v*`) or workflow_dispatch; installs WiX in CI, publishes + builds MSI + attaches as Release asset.
- `.github/workflows/ci.yml` rewritten to build only the .csproj projects, not the whole solution, so the wixproj doesn't fail CI when `staging/` is absent.
- Added to `.gitignore`: `src/ClaudePortable.Installer/staging/`, `*.msi`, `*.wixpdb`.
- Not yet implemented (deliberate, documented in README + spec handover):
  - Code signing (no cert available; SmartScreen warning on install).
  - .NET Desktop Runtime bootstrapper check (circumvented by self-contained publish; spec called for framework-dependent + pre-req check).
  - Uninstall confirmation dialog for user-data folder `%LOCALAPPDATA%\ClaudePortable\` (WiX MajorUpgrade handles upgrades; manual uninstall leaves user data behind which is the desired default).
- 41 tests still green. MSI built locally in 33s.

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
