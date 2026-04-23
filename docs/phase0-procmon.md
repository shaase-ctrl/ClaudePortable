# Phase 0 - Verify Claude paths via Sysinternals Process Monitor

This playbook fills in the remaining `ASSUMED` / `PENDING` rows in `discovered-paths.md`. Only the user can run it (requires interactive Claude Desktop sessions with actual chat/plugin/connector activity).

Most of the common paths are already verified by the `Get-AppxPackage` and direct `ls` checks that ClaudePortable performs at discover time. ProcMon is for the edge cases: where Claude writes transient state that might still contain credentials, and which files actually change during connector auth flows.

## 30-minute workflow

1. Download Sysinternals Suite: https://download.sysinternals.com/files/SysinternalsSuite.zip
2. Extract anywhere. Run `procmon.exe` (or `procmon64.exe`) as Administrator.
3. In ProcMon, open Filter -> Filter... (Ctrl+L). Remove everything that's there, then add two rules:
   - `Process Name` **is** `Claude.exe` **Include**
   - `Operation` **is** `WriteFile` **Include**
4. Click Apply. The event list will clear and only show writes by Claude Desktop going forward.
5. In ProcMon menu: File -> Capture Events (or press Ctrl+E) - make sure capture is **on**.
6. Now drive Claude Desktop through the scenarios below. Between each, take note of the new write paths that appear in ProcMon.

## Scenarios to walk through

Keep each phase short (30-60 seconds) so the capture stays readable.

### A. Cold start
- Exit Claude Desktop completely (tray icon "Quit").
- Clear the ProcMon event list (Ctrl+X).
- Launch Claude Desktop. Wait for the main window to render.
- Stop capture (Ctrl+E). Save the events: File -> Save -> `C:\Temp\procmon-A-coldstart.csv` (CSV format).

### B. New chat
- Re-enable capture (Ctrl+E). Clear list (Ctrl+X).
- Click "New chat". Send a short message. Wait for the reply to complete.
- Stop capture, save to `C:\Temp\procmon-B-newchat.csv`.

### C. MCP extension install
- Clear list. Re-enable capture.
- Open the in-app extension marketplace (if present in your build), install a small extension (e.g. a PDF tool).
- Stop, save to `C:\Temp\procmon-C-mcpinstall.csv`.

### D. Connector authorization
- Clear list. Re-enable capture.
- Open Settings -> Connectors. Add a connector (Gmail, Calendar, or a similar OAuth flow). Complete the browser OAuth. Return to Claude Desktop.
- Stop, save to `C:\Temp\procmon-D-connectorauth.csv`.

### E. MCP call end-to-end
- Clear list. Re-enable capture.
- In a chat, trigger a message that invokes one of the installed MCP servers (e.g. "use the PDF tool to read...").
- Wait for the response; stop capture; save to `C:\Temp\procmon-E-mcpcall.csv`.

## What we need from the output

For each CSV, run this PowerShell one-liner to get unique write paths:

```powershell
Import-Csv C:\Temp\procmon-A-coldstart.csv |
  Select-Object -ExpandProperty Path |
  Sort-Object -Unique |
  Out-File C:\Temp\procmon-A-coldstart.paths.txt
```

Repeat for B, C, D, E.

Attach those five `.paths.txt` files to issue #4 on GitHub. From them we can:

1. Flip `ASSUMED` rows in `docs/discovered-paths.md` to `VERIFIED`.
2. Identify any path pattern we currently do not back up. Candidates to watch:
   - Writes under `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\...` - Store-app sandboxed state.
   - New JSON files under `%APPDATA%\Claude\` that are not in the current `WindowsPathDiscovery`.
   - Anything under `%APPDATA%\Claude\local-agent-mode-sessions\` that contains real data (not just caches).
3. Identify any credential-bearing file we currently include. Candidates to watch:
   - Files that mention `token`, `oauth`, `credential`, `cookie`, `refresh`, `bearer` in path or content. Grep `.paths.txt` for these strings; if you find new hits, add them to `DefaultExclusions.Globs`.
   - Any `.ldb` / `.log` file under IndexedDB that writes after a connector auth - these need targeted exclusion rules.

## If you find surprises

Open a short comment on issue #4 with the findings. The developer (you or a future Claude session) can then:

- Add new discovered paths to `WindowsPathDiscovery.KnownPaths`.
- Add new exclusions to `DefaultExclusions.Globs`.
- Add a regression test to `ExclusionGlobTests` so the rule stays true.

## When this issue closes

- All `ASSUMED` rows in `discovered-paths.md` are either `VERIFIED` or documented as "not applicable on this Windows build".
- Any newly discovered credential-bearing path is in `DefaultExclusions` with a test.
- The session logs are attached to the issue for future reference.

Until then: the tool works based on the spec + dev-machine `ls` inspection. Live-Claude backup was tested end-to-end (14 GB, 15,063 files) with no observable data loss.
