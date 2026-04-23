# Progress Log

Checkpoint file. Update after every phase. Designed so a fresh Claude Code session can pick up where the previous one left off without re-reading the entire history.

## Phase status overview

| Phase | Title | Status | Issue | Closing commit |
|---|---|---|---|---|
| 0 | Path discovery | DONE (Session 6 ran a 60s idle capture; Store-app reparse-point confirmed; residual interactive scenarios documented but not required) | #4 closed | `Phase 0: idle capture + -wal exclusion` |
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

### Session 6 (2026-04-23) - Phase 0 capture + -wal exclusion (#4 closed)
- Wrote `scripts/claude-path-diff.ps1`: before/after snapshot diff across `%APPDATA%\Claude`, `%LOCALAPPDATA%\Claude`, `%USERPROFILE%\.claude`, `%USERPROFILE%\.cowork`, and `%LOCALAPPDATA%\Packages\Claude_*`. Optional `-AutoLaunch` to kill/restart Claude Desktop for cold-start capture; without it, just waits `-Duration` seconds for the scenario to run manually.
- Ran `idle60s` scenario (Claude Desktop already up, 60 second idle window). Two `claude.exe` processes active. 49,244 files before, 49,244 after. 44 modified (all transient: logs, leveldb journals, sentry, cookies, DIPS-wal). Raw output committed as `docs/phase0-captures/2026-04-23_idle60s.diff.txt`; analysis in the sibling `README.md`.
- **Store-app reparse-point finding**: `Get-Item` confirmed `%APPDATA%\Claude` has a `Target` of `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude`. The two paths share storage. `WindowsPathDiscovery` was intentionally NOT changed - adding the Packages dir would double-count every byte. Documented in `discovered-paths.md`.
- Added `**/*-wal` to `DefaultExclusions.Globs` (matches `DIPS-wal` and any SQLite WAL). Three regression test cases added; 60 xUnit cases pass.
- Decided against `**/*.log` or `**/logs/**` exclusions: log files are not credential-bearing and a user can still use them during post-restore troubleshooting.
- Residual scenarios (new chat, MCP install, connector OAuth, MCP call) are user-interactive. They're documented with the same script invocation pattern; whoever runs one drops the diff in `docs/phase0-captures/`. No code change expected for those without actual evidence.

### Session 5 (2026-04-23) - Backlog batch (#5, #6, #7 closed; #4 scoped)
- #5 MCP config located: `%APPDATA%\Claude\claude_desktop_config.json` (user-editable mcpServers), `extensions-installations.json`, and `Claude Extensions\<id>\` for the server code. The credential file `config.json` (contains `oauth:tokenCache`) is now in `DefaultExclusions` as an explicit archive path. Live-agent-mode sessions and their ephemeral OAuth-flow caches are excluded too. Dry-run file count dropped from 15,114 to 10,157 on the dev machine as a result.
- #6 Claude Desktop is installed via Microsoft Store on the dev machine (`Get-AppxPackage -Name Claude` returns `1.3883.0.0`). `ClaudeDesktopVersionReader.TryRead()` shells out to `powershell.exe` to pick it up. `BackupEngine` plumbs it into `manifest.claudeDesktopVersion`. `VersionGating.Evaluate(backup, installed)` returns Info/Ok/Warn/Block; `RestoreEngine` throws on Block unless `--ignore-version-mismatch` is set. Surfaced in the GUI log tab and the CLI `restore` output.
- #7 Full two-VM OneDrive roundtrip playbook in `docs/e2e-test.md` - pre-reqs, step-by-step on VM-A and VM-B, pass criteria, troubleshooting matrix.
- #4 Phase 0 ProcMon playbook in `docs/phase0-procmon.md` - five exact capture scenarios, post-processing one-liner. Issue stays open since the actual run is interactive and user-driven.
- `docs/discovered-paths.md` rewritten around the verified Store install, with security-critical exclusions and operational noise exclusions listed separately.
- Tests: +9 cases (VersionGating + new exclusion globs). 57 total now passing. CI green.

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
