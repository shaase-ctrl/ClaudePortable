# ClaudePortable

Windows-only CLI that backs up and restores Claude Desktop, Cowork, and Claude Code state as a single ZIP blob. The ZIP is written to any local folder (USB stick, OneDrive-synced folder, Google Drive Desktop mount, NAS share) - the app itself never talks to cloud APIs.

> Status: alpha. Phase 0-3 of the spec are implemented as a CLI. Phase 4 (scheduler + retention), Phase 5 (WPF GUI + tray), and Phase 6 (signed MSI) are tracked as GitHub issues.

## Scope

In scope:
- Claude Desktop settings, IndexedDB, Local Storage (`%APPDATA%\Claude`)
- Claude Code user profile (`%USERPROFILE%\.claude`), plugins, skills
- Cowork sessions (`%USERPROFILE%\.cowork`, where present)
- Manifest JSON with schema version, content SHA-256, excluded paths
- Safety-preserve pre-restore (moves existing folders to `_backup_<timestamp>` instead of deleting)
- Path rewrite for JSON configs during restore (old Windows user -> new user)

Out of scope (by design):
- OAuth refresh tokens, API keys, DPAPI blobs - user re-authenticates connectors post-restore
- Plugin binary re-install - `.remote-plugins/` excluded; `claude plugin sync` required post-restore
- Cloud-API uploads - ClaudePortable writes to a folder, any sync client propagates

## Download

Grab the latest build from the [Releases page](../../releases). Two options:

| Artifact | Best for | Install | Uninstall |
|---|---|---|---|
| `ClaudePortable-<version>-portable.exe` | Running on any machine without admin | Double-click the exe | Delete the exe + `%LOCALAPPDATA%\ClaudePortable` |
| `ClaudePortable-<version>.msi` | Permanent install with Start-menu entry | Run the MSI | Apps & Features -> ClaudePortable -> Uninstall |

Both artifacts are **self-contained** - they bundle the .NET 10 Windows Desktop runtime, so you do not need to install anything else.

### First-run SmartScreen warning (unsigned)

Releases are not code-signed yet. On first run Windows SmartScreen shows "Windows protected your PC". To unblock:

1. Right-click the downloaded file -> **Properties** -> tick **Unblock** at the bottom -> **OK**.
2. Double-click to run.

For a belt-and-braces check, verify the SHA-256 against the `.sha256` file shipped next to the exe:

```powershell
(Get-FileHash .\ClaudePortable-<version>-portable.exe -Algorithm SHA256).Hash
```

### Build from source

```
dotnet build -c Release
```

Binary lands at `src/ClaudePortable.App/bin/Release/net10.0-windows/claudeportable.exe`.

To build your own portable exe:

```
pwsh scripts/build-exe.ps1 -Version 0.1.1
```

Produces `ClaudePortable-0.1.1-portable.exe` in the repo root.

## Usage

```
claudeportable discover                              # show detected Claude paths + sync clients
claudeportable backup --to <folder> [--tier daily]   # create a backup ZIP in <folder>
claudeportable list --in <folder>                    # list backups in <folder>
claudeportable restore --from <zip> --yes            # restore from a backup ZIP
```

Flags:
- `--dry-run` on `backup` plans without writing
- `--json` on `list` and `discover` emits machine-readable output
- `--target-user <path>` on `restore` overrides the target profile (advanced, for cross-user restore on the same machine)

Exit codes: `0` success, `1` usage error, `2` precondition failed (path missing/unwritable), `3` runtime failure (I/O, invalid backup, etc.).

## Example

```
> claudeportable discover
== Claude paths ==
  [FOUND] claudeDesktopAppData         C:\Users\Sascha Haase\AppData\Roaming\Claude
           source: Spec 1.1 + verified 2026-04-22
  [FOUND] claudeCodeUserProfile        C:\Users\Sascha Haase\.claude
           source: Spec 1.1 + verified 2026-04-22

== Sync clients ==
  [ OK ] OneDrive (Personal)           C:\Users\Sascha Haase\OneDrive
           source: HKCU\Software\Microsoft\OneDrive\UserFolder

> claudeportable backup --to C:\Temp\cpbackup
created: C:\Temp\cpbackup\claude-backup_2026-04-22T23-01-00Z_DESKTOP-XYZ_daily.zip
files:   1234
bytes:   45,678,901
sha256:  a1b2c3...
```

## Retention

Phase 4 (not yet implemented) will add automatic 7/3/2 grandfather-father-son retention: 7 daily, 3 weekly, 2 monthly. For now, run `backup` manually; delete old ZIPs yourself.

## Architecture

```
src/
  ClaudePortable.Core/        # Discovery, archiving, manifest, backup/restore engines, path rewriter
  ClaudePortable.Targets/     # FolderTarget (atomic file I/O), SyncClientDiscovery (registry-based)
  ClaudePortable.App/         # CLI dispatch (System.CommandLine)
  ClaudePortable.Scheduler/   # Phase 4 placeholder
  ClaudePortable.Installer/   # Phase 6 placeholder (WiX MSI planned)
tests/
  ClaudePortable.Tests/       # xUnit: glob, manifest, path rewriter, folder target, backup roundtrip
docs/
  spec.md                     # Original build specification (v2.0)
  discovered-paths.md         # Phase 0 path verification log
```

The app never dereferences OAuth tokens or credentials. `manifest.json` lists every exclusion glob applied (auditable).

## Deviations from spec

| Spec item | Reality | Reason |
|---|---|---|
| .NET 8 LTS | net10.0 / net10.0-windows | Only .NET 10 SDK installed on the dev machine. Pin back with global.json when needed. |
| WPF GUI + Tray | CLI only for now | Phase 5 deferred. [Issue #2](../../issues) |
| WiX MSI + code sign | Not built | Phase 6 deferred. [Issue #3](../../issues) |
| Quartz.NET scheduler | Not built | Phase 4 deferred. [Issue #1](../../issues) |
| Serilog logging | `Console.Error.WriteLine` only | CLI output; structured logging follows in Phase 5. |

## Development

```
dotnet restore
dotnet build
dotnet test
```

Tests cover: exclusion glob matching, manifest (de)serialization, path rewriter regex, FolderTarget I/O, end-to-end backup of a synthetic `.claude` directory including cache exclusion.

CI runs the same commands on `windows-latest` via `.github/workflows/ci.yml`.

## License

MIT, see [LICENSE](LICENSE).

## Acknowledgements

Built from a detailed spec (`docs/spec.md`, v2.0) authored by Sascha Haase.
