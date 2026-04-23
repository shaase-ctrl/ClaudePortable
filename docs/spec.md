# Anleitung für Claude Code: Backup- & Restore-Tool für Claude Desktop / Cowork / Claude Code

**Dokumenttyp:** Build-Instruktion für Claude Code
**Zielartefakt:** Windows-Desktop-Anwendung `ClaudePortable` (Name austauschbar) mit MSI-Installer
**Autor:** Sascha Haase
**Datum:** 2026-04-22
**Version:** 2.0 — Folder-Target-Architektur (keine Cloud-APIs)

---

## Executive Summary

Diese Anleitung beschreibt Schritt für Schritt, wie Claude Code eine Windows-Desktop-Anwendung baut, die **Konfiguration, Chat-Historie, Projekte, Skills, Plugins und Settings** von Claude Desktop (inklusive Cowork) und Claude Code sichert und auf einem anderen Windows-PC wiederherstellt.

**Architektur-Kernentscheidung:** Die App spricht **keine Cloud-APIs**. Sie schreibt Backup-ZIPs in einen oder mehrere **lokale Ordner**, die der User vorgibt. Die Synchronisation zur Cloud übernimmt der jeweils installierte Sync-Client (OneDrive, Google Drive Desktop, Dropbox, Syncthing) oder findet gar nicht statt (USB-Stick, NAS-Mount, Netzwerkfreigabe). Dadurch entfallen OAuth-App-Registrierungen, API-Quotas, Upload-Resume-Logik und je eine Abhängigkeit zu Google und Microsoft.

Retention folgt einer klassischen 7/3/2-Grandfather-Father-Son-Strategie (7 tägliche, 3 wöchentliche, 2 monatliche Snapshots). **Bewusst ausgeschlossen:** OAuth-Tokens, API-Keys, DPAPI-geschützte Credentials. Der User authentifiziert Connectoren, Plugins und Clouds am Ziel-PC manuell neu. Das ist eine bewusste Scope-Entscheidung, die das Projekt machbar hält.

**Ergebnis:** Eine WPF-Anwendung mit Tray-Icon, In-App-Scheduler und signiertem MSI-Installer. Größenordnung: ~2.500 LOC C#, 2-3 Stunden Claude-Code-Laufzeit.

---

## 1. Scope & Nicht-Ziele

### 1.1 In Scope

Das Tool sichert und restored:

| Artefakt | Quelle (mutmaßlich, in Phase 0 zu verifizieren) | Format |
|---|---|---|
| Claude Desktop Settings | `%APPDATA%\Claude\settings.json`, `%APPDATA%\Claude\Preferences` | JSON / Electron-Store |
| Claude Desktop Chat-Historie | `%APPDATA%\Claude\IndexedDB\`, `%APPDATA%\Claude\Local Storage\` | LevelDB |
| Cowork-Sessions | `%APPDATA%\Claude\cowork\sessions\` ODER `%USERPROFILE%\.cowork\` | Dateibaum |
| Cowork Memory-Files | `CLAUDE.md`, `TASKS.md` in Session-Ordnern | Markdown |
| Claude Code Config | `%USERPROFILE%\.claude\` | Dateibaum |
| Claude Code Projekte | `%USERPROFILE%\.claude\projects\` | Dateibaum |
| Installierte Plugins (Manifest) | `%USERPROFILE%\.claude\plugins\`, `%APPDATA%\Claude\plugins\` | Liste (nicht Binärcache) |
| Installierte Skills | `%USERPROFILE%\.claude\skills\` | Dateibaum |
| MCP-Server-Configs | `%APPDATA%\Claude\mcp.json` oder vergleichbar | JSON |
| Connector-Metadaten (ohne Tokens) | Aus Claude Desktop Config-DB extrahiert | JSON-Export |

### 1.2 Nicht-Ziele

- **Keine Cloud-APIs:** App ist Sync-Client-agnostisch, keine OAuth-Registrierungen, keine Microsoft-Graph, keine Google-APIs.
- **Keine Credentials:** OAuth-Refresh-Tokens, API-Keys und DPAPI-Blobs werden explizit ausgeschlossen. Sie sind user- und maschinengebunden und auf einem anderen PC ohnehin ungültig.
- **Kein Full-Disk-Image:** Nur Claude-Artefakte, keine Betriebssystem- oder User-Daten außerhalb der Claude-Verzeichnisse.
- **Keine Client-Server-Architektur:** Reiner Single-User-Client, kein Backend.
- **Keine Cross-Platform-Unterstützung:** Windows 10/11 only.
- **Kein inkrementelles Backup auf Dateiebene:** Jedes Backup ist ein vollständiger Snapshot. Kompression sorgt dafür, dass die Uploads trotzdem klein bleiben.
- **Kein Remote-Plugin-Cache:** `.remote-plugins/` wird beim Restore nicht wiederhergestellt, sondern post-restore via `claude plugin sync` neu gezogen.

### 1.3 Known Limitations (offen kommunizieren, nicht verstecken)

1. **Pfad-Rewrite nötig:** Cowork-Session-Ordner haben zufällig generierte Namen (z. B. `stoic-practical-goodall`). Am Ziel-PC sind die Pfade anders. Das Tool rewritet absolute Pfade in Config-JSONs beim Restore. Referenzen in Markdown-Inhalten (z. B. hart kodierte Links in einer `CLAUDE.md`) werden nicht automatisch gefixt — separate Post-Restore-Warnung.
2. **Connectoren brauchen Re-Auth:** Der User muss nach dem Restore jeden Connector (Gmail, Slack, GDrive etc.) einmal neu autorisieren. Das Tool zeigt eine Checkliste der zu re-authentifizierenden Connectoren.
3. **Plugins werden re-installiert, nicht kopiert:** Das Backup speichert eine Plugin-Manifest-Liste. Beim Restore triggert das Tool `claude plugin install <name>` für jeden Eintrag. Voraussetzung: Claude Code ist am Ziel-PC installiert und im PATH.
4. **Chat-Historie-Portierung ist fragil:** Claude Desktop nutzt eine Electron-LevelDB. Wenn Anthropic das Format ändert, bricht das Restore. Das Tool führt deshalb vor dem Restore einen Version-Check gegen das Backup-Manifest durch und warnt bei Versions-Mismatch.
5. **Cowork-Laufzeit-State nicht portierbar:** Aktive Cowork-Sessions, laufende Scheduled Tasks, Agent-Runs werden nicht portiert — nur persistente Artefakte.
6. **Sync-Status nicht beobachtbar:** Die App weiß nur, dass sie das Backup ins lokale Ordner-Ziel geschrieben hat. Ob OneDrive/GDrive den Upload erfolgreich abgeschlossen hat, prüft die App **nicht**. Bei Cloud-Zielen muss der User den Sync-Client-Status selbst beobachten. Die App warnt bei bekannten Fehlerzuständen (Ordner nicht erreichbar, Platte voll, Pending-Flags im OneDrive-Ordner via `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS`).

---

## 2. Architektur-Übersicht

```
┌─────────────────────────────────────────────────────────────┐
│                    ClaudePortable.exe (WPF)                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │  GUI-Layer   │  │   Tray-Icon  │  │  Scheduler-UI    │   │
│  │  (MainWindow)│  │ (H.NotifyIcon)│  │  (täglich/wöch.) │   │
│  └──────┬───────┘  └──────┬───────┘  └────────┬─────────┘   │
│         │                 │                   │             │
│  ┌──────┴─────────────────┴───────────────────┴─────────┐   │
│  │            ClaudePortable.Core (Class Library)       │   │
│  │  ┌────────────┐ ┌────────────┐ ┌─────────────────┐   │   │
│  │  │ BackupEngine│ │RestoreEngine│ │ RetentionManager│   │   │
│  │  └─────┬──────┘ └─────┬──────┘ └────────┬────────┘   │   │
│  │        │              │                 │            │   │
│  │  ┌─────┴──────────────┴─────────────────┴─────────┐  │   │
│  │  │        IPathDiscovery (Claude-Installs)        │  │   │
│  │  │        IArchiveWriter (ZIP + Manifest)         │  │   │
│  │  │        IPathRewriter (Session-Path-Fix)        │  │   │
│  │  └────────────────────┬──────────────────────────┘   │   │
│  └───────────────────────┼──────────────────────────────┘   │
│                          │                                  │
│  ┌───────────────────────┴──────────────────────────────┐   │
│  │            ClaudePortable.Targets                    │   │
│  │  ┌────────────────┐  ┌──────────────────────────┐    │   │
│  │  │ FolderTarget   │  │ SyncClientDiscovery      │    │   │
│  │  │ (schreibt/list/│  │ (findet OneDrive, GDrive │    │   │
│  │  │  delete in     │  │  Desktop, Dropbox etc.   │    │   │
│  │  │  User-Ordner)  │  │  aus Registry / ProgData)│    │   │
│  │  └────────────────┘  └──────────────────────────┘    │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         ClaudePortable.Scheduler                     │   │
│  │  (Quartz.NET für In-App + Windows Task Scheduler XML)│   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  Backup-ZIP          │
                    │  + manifest.json     │
                    └──────────┬───────────┘
                               │
          ┌──────────────┬─────┴──────┬───────────────┐
          ▼              ▼            ▼               ▼
   ┌───────────┐  ┌─────────────┐ ┌────────┐  ┌────────────┐
   │ OneDrive- │  │ GDrive-Sync-│ │  USB-  │  │ NAS / SMB- │
   │ Ordner    │  │ Ordner      │ │ Stick  │  │ Freigabe   │
   │ (Sync-Cli)│  │ (Sync-Cli)  │ │(direkt)│  │(UNC-Pfad)  │
   └───────────┘  └─────────────┘ └────────┘  └────────────┘

Die App kennt keine Cloud. Sie schreibt in einen Ordner. Die Cloud-
Propagation macht der Sync-Client, der ohnehin auf dem System läuft.
```

**Projekt-Aufteilung (Solution):**

```
ClaudePortable.sln
├── ClaudePortable.App            (WPF, Executable, .NET 8)
├── ClaudePortable.Core           (Class Library, Backup/Restore-Logik)
├── ClaudePortable.Targets        (Class Library, FolderTarget + SyncClientDiscovery)
├── ClaudePortable.Scheduler      (Class Library, Quartz.NET + Task Scheduler)
├── ClaudePortable.Tests          (xUnit, Unit + Integration)
└── ClaudePortable.Installer      (WiX 4, MSI-Build)
```

---

## 3. Tech-Stack (fixiert)

| Zweck | Komponente | Version | Begründung |
|---|---|---|---|
| Runtime | .NET 8 (LTS) | 8.0.x | Lange Support-Zeit, WPF ausgereift |
| UI | WPF | .NET 8 integriert | Native Windows, gute Designer-Tools |
| MVVM | CommunityToolkit.Mvvm | 8.x | Microsoft-Standard, Source-Generatoren |
| Tray-Icon | H.NotifyIcon.Wpf | 2.x | Moderne WPF-Integration, ContextMenu-Support |
| Scheduling (in-app) | Quartz.NET | 3.x | Reiche Cron-Syntax, persistenter State |
| Scheduling (OS-fallback) | Windows Task Scheduler XML | — | Für User ohne Autostart-Wunsch |
| Archivierung | System.IO.Compression | .NET 8 integriert | ZIP64 für Backups > 4 GB |
| Logging | Serilog + Sinks.File + Sinks.Debug | latest | Strukturiertes Logging |
| Config | Microsoft.Extensions.Configuration | 8.x | Standard |
| Tests | xUnit + FluentAssertions + Moq | latest | Standard |
| Installer | WiX Toolset | 4.0 | Modern, declarative |

**Keine Cloud-SDKs, keine OAuth-Libraries, keine Fremd-Abhängigkeiten darüber hinaus.**

---

## 4. Backup-Format-Spezifikation

Jedes Backup ist ein einzelner ZIP-Blob mit folgendem Inhalt:

```
claude-backup_2026-04-22T14-23-11Z_<hostname>_<tier>.zip
├── manifest.json               # Metadaten, Versionen, Inhalts-Index
├── claude-desktop/
│   ├── appdata/                # %APPDATA%\Claude\ (ohne Cache, ohne Credentials)
│   └── localappdata/           # %LOCALAPPDATA%\Claude\ (selektiv)
├── claude-code/
│   └── dotclaude/              # %USERPROFILE%\.claude\ (ohne tokens)
├── cowork/
│   └── sessions/               # Cowork-Session-Ordner (namentlich gelistet)
├── skills/                     # User-installierte Skills
├── plugins/
│   └── manifest.json           # Nur Liste, keine Binaries
├── connectors/
│   └── manifest.json           # Connector-Namen (ohne Tokens)
└── post-restore-checklist.md   # Für den User: was manuell nachzuholen ist
```

### 4.1 manifest.json (Pflichtfelder)

```json
{
  "schemaVersion": 2,
  "createdAt": "2026-04-22T14:23:11Z",
  "hostname": "DESKTOP-XYZ",
  "windowsUser": "Sascha",
  "claudeDesktopVersion": "x.y.z",
  "claudeCodeVersion": "x.y.z",
  "retentionTier": "daily|weekly|monthly",
  "sourcePaths": {
    "appdataClaude": "C:\\Users\\Sascha\\AppData\\Roaming\\Claude",
    "userProfileClaude": "C:\\Users\\Sascha\\.claude"
  },
  "sizeBytes": 123456789,
  "fileCount": 42,
  "sha256": "a1b2c3...",
  "excludedPaths": [
    "**/Cache/**",
    "**/GPUCache/**",
    "**/tokens.dat"
  ]
}
```

### 4.2 Dateinamens-Konvention

`claude-backup_<ISO-Timestamp>_<Hostname>_<Tier>.zip`

Beispiel: `claude-backup_2026-04-22T14-23-11Z_DESKTOP-XYZ_daily.zip`

---

## 5. Retention-Strategie (7/3/2)

**Regel:** Jedes Backup wird initial als `daily` angelegt. Ein Hintergrund-Rotations-Task promoted Backups in die höheren Tiers.

| Tier | Anzahl Vorhaltung | Promotion-Regel |
|---|---|---|
| daily | 7 | Das jüngste Backup jedes Tages |
| weekly | 3 | Das jüngste `daily` des Sonntags wird zu `weekly` |
| monthly | 2 | Das jüngste `weekly` des letzten Monats-Sonntags wird zu `monthly` |

**Promotion statt Kopie:** Ein Backup wird durch Umbenennung (und Manifest-Update) promoted, nicht dupliziert. Speicher-Effizienz. Funktioniert lokal problemlos, Sync-Clients propagieren die Umbenennung an die Cloud.

**Lösch-Reihenfolge:** Alte Backups werden gelöscht, nachdem das neue erfolgreich geschrieben wurde (Nie-Null-Backup-Garantie).

**Algorithmus (Pseudocode):**

```csharp
async Task RotateAsync(FolderTarget target)
{
    var all = target.ListBackups();
    var grouped = all.GroupBy(b => b.Tier);

    // 1. Promote: neuester Sunday-daily → weekly, neuester Month-End-weekly → monthly
    PromoteSundayToWeekly(grouped["daily"]);
    PromoteMonthEndToMonthly(grouped["weekly"]);

    // 2. Prune: zu viele pro Tier → älteste löschen
    target.PruneTier("daily", keep: 7);
    target.PruneTier("weekly", keep: 3);
    target.PruneTier("monthly", keep: 2);
}
```

**Wichtig bei mehreren Ausgabe-Ordnern:** Retention läuft **pro Ordner** unabhängig. Wer einen USB-Stick nur sporadisch ansteckt, hat darauf potenziell einen anderen Snapshot-Stand als im OneDrive-Ordner. Das ist gewollt und wird in der UI transparent angezeigt.

---

## 6. Phasen-Plan für Claude Code

> **Wichtig für Claude Code:** Jede Phase endet mit einem ausführbaren Artefakt (kompiliert, getestet). Nicht zur nächsten Phase weitergehen, bevor die aktuelle grün ist.

### Phase 0 — Discovery (2h)

**Ziel:** Exakte Claude-Install-Pfade auf dem Dev-Rechner verifizieren.

Aufgaben:
1. Claude Desktop installieren (falls nicht vorhanden) und einmal starten.
2. Claude Code installieren und `claude --version` ausführen.
3. Mit Sysinternals Process Monitor (`procmon.exe`) die Write-Paths beobachten, während Claude Desktop läuft und man einmal einen Chat startet.
4. Dokumentieren in `docs/discovered-paths.md`:
   - Welche Ordner werden beschrieben?
   - Wo liegt die Chat-Historie (LevelDB? SQLite?)
   - Wo die MCP-Configs?
   - Wo die Plugin-Installationen?
5. Die Tabelle in Abschnitt 1.1 dieser Anleitung mit den verifizierten Pfaden überschreiben.

**Artefakt:** `docs/discovered-paths.md` mit echten, verifizierten Pfaden.

**Abnahmekriterium:** Liste ist vollständig; jeder Pfad ist getestet (existiert, enthält die erwarteten Daten).

---

### Phase 1 — Scaffold & Core-Backup (4h)

**Ziel:** CLI-Prototyp, der lokal ein Backup-ZIP erzeugt.

Aufgaben:
1. Solution-Struktur anlegen (siehe Abschnitt 2).
2. `ClaudePortable.Core`:
   - Interfaces `IPathDiscovery`, `IArchiveWriter`, `IBackupEngine` definieren.
   - `WindowsPathDiscovery` implementieren (liest die Pfade aus Phase 0).
   - `ZipArchiveWriter` implementieren mit ZIP64-Support und Exclude-Globs.
   - `BackupEngine.CreateBackupAsync(destFolder)` implementieren.
   - `manifest.json` generieren (inkl. SHA-256 des Inhalts).
3. Mini-CLI in `ClaudePortable.App` (temporär, wird später GUI): `dotnet run -- backup --to C:\temp\`.
4. Unit-Tests für Exclusion-Globs, Manifest-Generierung, Hash-Berechnung.

**Artefakt:** `ClaudePortable.exe backup --to <path>` erzeugt einen gültigen Backup-ZIP lokal.

**Abnahmekriterium:** Backup enthält alle erwarteten Ordner, Manifest ist parsebar, SHA-256 stimmt nach Re-Hash.

---

### Phase 2 — Restore-Engine & Path-Rewrite (3h)

**Ziel:** Backup-ZIP zurück auf den lokalen Rechner spielen, mit Pfad-Anpassung.

Aufgaben:
1. `RestoreEngine.RestoreAsync(zipPath)` implementieren.
2. `PathRewriter` implementieren:
   - Alle JSON-Dateien im Backup nach Strings durchsuchen, die auf den **alten** Windows-User-Pfad zeigen (`C:\Users\OldUser\…`).
   - Durch den aktuellen User-Pfad ersetzen.
   - Cowork-Session-Ordnernamen (`<adjective-adjective-name>`) stehenlassen — die sind stabil, der Windows-User-Pfad ändert sich.
3. **Safety:** Vor dem Restore existierende Claude-Ordner nach `<Ordner>_backup_YYYY-MM-DD-HHmmss` umbenennen, nicht löschen.
4. Unit-Tests mit synthetischen JSONs.
5. `ClaudePortable.exe restore --from <zip>` als CLI-Befehl.

**Artefakt:** Roundtrip Backup → Restore funktioniert lokal. Pfade in Configs sind korrekt angepasst.

**Abnahmekriterium:** Nach Restore startet Claude Desktop und zeigt dieselben Settings, Chat-Historie und Projekte wie vor dem Backup. Connectoren zeigen „Re-Auth required" (erwartet).

---

### Phase 3 — Folder-Targets & Sync-Client-Discovery (1h)

**Ziel:** Mehrere Ausgabe-Ordner, mit Auto-Vorschlag bekannter Sync-Ordner.

Aufgaben:
1. `FolderTarget`-Klasse in `ClaudePortable.Targets`:
   - `WriteAsync(byte[] blob, string filename)` — atomar (schreibt als `.tmp`, dann Rename).
   - `ListBackups()` — listet vorhandene ZIPs plus parsed Manifests.
   - `Rename(oldName, newName)` — für Promotion.
   - `Delete(name)` — für Prune.
   - Validierung: Ordner existiert, schreibbar, genug freier Platz.
2. `SyncClientDiscovery`:
   - Liest aus der Registry:
     - `HKCU\Software\Microsoft\OneDrive\UserFolder` → Personal-OneDrive
     - `HKCU\Software\Microsoft\OneDrive\Accounts\Business1\UserFolder` → OneDrive for Business
     - Google-Drive-Desktop-Mount-Point (aus `%LOCALAPPDATA%\Google\DriveFS\root_preference_sqlite`)
     - Dropbox (`%APPDATA%\Dropbox\info.json` → `personal.path` / `business.path`)
   - Gibt eine Liste `DiscoveredSyncClient { Name, Path, IsAvailable }` zurück.
3. **Pending-Upload-Warning:** Wenn die App in einen OneDrive-Ordner schreibt und der parent hat `FILE_ATTRIBUTE_OFFLINE` oder `FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS` → Log-Warnung, dass der Ordner nicht vollständig gesynct ist.
4. Unit-Tests mit einem temporären Ordner als Target.

**Artefakt:** User kann in der App mehrere Ausgabe-Ordner konfigurieren, bekommt Vorschläge aus Auto-Discovery.

**Abnahmekriterium:** Backup schreibt zuverlässig in alle konfigurierten Ordner. Rename/Delete funktioniert. Discovery findet OneDrive und (falls installiert) GDrive/Dropbox.

---

### Phase 4 — Scheduler & Retention (3h)

**Ziel:** Automatische Backups nach 7/3/2-Strategie.

Aufgaben:
1. `ClaudePortable.Scheduler`:
   - Quartz.NET-Setup für In-App-Scheduling (Service läuft im Tray).
   - Default-Cron: täglich 23:00 Uhr.
   - Alternative: Windows Task Scheduler XML generieren (wenn User App nicht ständig laufen lassen will).
2. `RetentionManager` implementieren (Algorithmus aus Abschnitt 5) **pro konfiguriertem FolderTarget**.
3. Nach jedem Backup: `RotateAsync` für jedes Target triggern.
4. Erster-Run-Test mit beschleunigter Zeit (mocked clock) über 10 simulierte Wochen.

**Artefakt:** Scheduled Backups laufen, Rotation produziert am Ende exakt 7+3+2 = 12 Backups pro Target.

**Abnahmekriterium:** Simulierter 10-Wochen-Lauf produziert korrektes Endergebnis auf jedem Target.

---

### Phase 5 — WPF-GUI + Tray (4h)

**Ziel:** User-facing Oberfläche.

Aufgaben:
1. `MainWindow.xaml` mit folgenden Tabs:
   - **Backup-Status:** Letztes Backup, nächstes geplantes, Liste der Snapshots pro Target.
   - **Einstellungen:** Zeitplan editieren, Retention anpassen (7/3/2 default, anpassbar).
   - **Ausgabe-Ordner:** Liste aller konfigurierten FolderTargets. Add-Button öffnet Auto-Discovery-Dialog mit Vorschlägen plus „Eigenen Ordner auswählen". Entfernen, Pausieren, Manuell testen.
   - **Restore:** Snapshot-Liste mit „Restore from this"-Button. Snapshots werden aus allen aktiven Ordner-Targets aggregiert.
   - **Logs:** Letzte 100 Log-Einträge (Serilog).
2. Tray-Icon via `H.NotifyIcon.Wpf` mit ContextMenu:
   - „Backup jetzt"
   - „Status"
   - „Einstellungen öffnen"
   - „Beenden"
3. Restore-Workflow: Warnung-Dialog mit expliziter Bestätigung („Claude-Daten werden überschrieben, alter Stand wird als `_backup_...` gesichert").
4. Post-Restore-Dialog: zeigt die `post-restore-checklist.md` inline an.

**Artefakt:** Voll bedienbare Desktop-App, ohne CLI-Interaktion nutzbar.

**Abnahmekriterium:** Usability-Test: Nicht-technischer User kann Backup einrichten und einen Restore durchführen.

---

### Phase 6 — WiX-Installer (3h)

**Ziel:** Signiertes MSI für Verteilung.

Aufgaben:
1. `ClaudePortable.Installer` mit WiX 4:
   - Installations-Ziel: `%ProgramFiles%\ClaudePortable\`
   - Startmenü-Eintrag + optional Autostart (Checkbox im Installer).
   - Voraussetzung: .NET 8 Desktop Runtime (Installer prüft, bietet Auto-Install).
   - Uninstaller entfernt Programm, aber **nicht** User-Daten unter `%LOCALAPPDATA%\ClaudePortable\` (fragt nach).
2. Code-Signing:
   - SignTool-Integration im Build-Skript.
   - Zertifikat-Pfad als Env-Variable (nicht ins Repo checken).
   - Fallback: Self-Signed für Dev, Kommentar im README mit Hinweis auf SmartScreen-Warning.
3. GitHub-Actions-Workflow `release.yml`:
   - Baut MSI bei Tag-Push.
   - Lädt als GitHub Release Asset hoch.
4. README mit Installations-Anleitung.

**Artefakt:** `ClaudePortable-x.y.z.msi` (signiert), installierbar per Doppelklick.

**Abnahmekriterium:** MSI installiert auf frischer Windows-11-VM ohne manuelle Schritte. Nach Install startet App ohne Fehler.

---

## 7. Test-Strategie

### 7.1 Unit-Tests (Phase 1-4)
- Exclusion-Globs
- Manifest-Generierung
- Path-Rewriter-Logik
- Retention-Rotation (mit mocked clock)
- SyncClientDiscovery mit mocked Registry

### 7.2 Integration-Tests (Phase 3+)
- FolderTarget gegen echte temp-Ordner (500 MB Stress)
- Pending-Upload-Detection mit simulierten OneDrive-Attributen
- Multi-Target-Write (3 Ordner parallel)

### 7.3 End-to-End-Szenario (Phase 6)
Dokumentieren in `docs/e2e-test.md`:
1. Fresh Windows-11-VM, Claude Desktop + Code installieren, konfigurieren.
2. OneDrive-Client installieren, einloggen, syncen lassen.
3. ClaudePortable installieren, als Target den OneDrive-Ordner wählen, Backup ausführen.
4. Warten bis OneDrive-Sync-Status = grün.
5. Zweite Fresh Windows-11-VM, OneDrive-Client installieren, syncen lassen.
6. ClaudePortable installieren, Backup aus OneDrive-Ordner auswählen, Restore.
7. Verifizieren: Chat-Historie, Projekte, Settings, Skills, Plugins (per Re-Install), Connectoren (nach Re-Auth) funktionieren.

---

## 8. Post-Restore-Checklist (generiert ins Backup)

Das Tool schreibt folgende Datei ins Backup-ZIP (`post-restore-checklist.md`) und zeigt sie nach dem Restore:

```markdown
# Post-Restore-Checkliste

Folgende Schritte musst du manuell erledigen, nachdem das Restore abgeschlossen ist:

## Pflicht-Schritte
- [ ] Claude Desktop starten und einmal einloggen
- [ ] Claude Code: `claude login` ausführen
- [ ] Jeden Connector (Liste unten) einmal neu autorisieren
- [ ] `claude plugin sync` ausführen, damit installierte Plugins geladen werden

## Verbundene Connectoren zum Zeitpunkt des Backups
- Gmail (<account-email>)
- Google Calendar (<account-email>)
- Slack (<workspace>)
- ...

## Falls Cowork-Skills/Plugins fehlen
Im Backup sind folgende Plugins enthalten (Re-Install nötig):
- cowork-plugin-management
- productivity
- ...

## Troubleshooting
- **Chat-Historie leer:** Claude-Desktop-Version prüfen. Bei Mismatch mit Backup-Version: Chat-History-Migration nicht garantiert.
- **Plugins laden nicht:** `claude plugin install <name>` manuell für jedes aus der Liste.
```

---

## 9. Build-Umgebung (für den Claude-Code-Dev-Rechner)

**Minimal-Voraussetzungen:**
- Windows 10/11, 64-bit
- Visual Studio 2022 Build Tools oder komplettes VS 2022
- .NET 8 SDK
- WiX Toolset 4 (`dotnet tool install --global wix`)
- Git
- Sysinternals Suite (für Phase 0)

**Keine OAuth-Apps, keine Cloud-SDKs, keine externen Secrets nötig.**

Für E2E-Tests nützlich, aber nicht zwingend:
- Zweite Windows-11-VM
- OneDrive-Account mit installiertem Sync-Client

---

## 10. Start-Kommando für Claude Code

Wenn Claude Code diese Anleitung entgegennimmt, soll es in genau dieser Reihenfolge vorgehen:

1. Diese Anleitung vollständig lesen.
2. Verifizieren, dass die Build-Umgebung (Abschnitt 9) steht.
3. **Phase 0 starten und nicht überspringen.** Die Pfad-Verifikation ist die riskanteste Unbekannte im Projekt.
4. Nach Phase 0 dem User die `docs/discovered-paths.md` zeigen und Freigabe einholen, bevor Phase 1 beginnt.
5. Danach Phasen 1-6 sequenziell, jeweils mit Abnahmekriterium gegen den User gegenchecken.
6. Am Ende: E2E-Test auf zwei VMs (falls verfügbar) oder zumindest Backup-Roundtrip auf dem Dev-Rechner.

**Commit-Strategie:** Pro Phase ein Feature-Branch (`feature/phase-1-backup-core`), PR auf `main` mit Phasen-Label.

---

## 11. Offene Punkte, die Claude Code in Phase 0 klären muss

1. **Wo liegt die Chat-Historie genau?** (LevelDB im IndexedDB-Ordner ist die Hypothese, aber zu verifizieren.)
2. **Wie heißen die Cowork-Session-Ordner unter Windows?** (Linux-Sandbox zeigt `/sessions/<adjective-adjective-name>/` — Windows-Pendant mutmaßlich unter `%APPDATA%\Claude\cowork\sessions\` oder `%USERPROFILE%\.cowork\`.)
3. **Gibt es eine zentrale `plugins.json` oder sind Plugin-Manifests pro Plugin-Ordner verteilt?**
4. **Ist die MCP-Server-Config in einer einzigen Datei oder pro Server in einer eigenen?**

**Sobald diese vier Punkte geklärt sind, ist der Rest der Anleitung ausführbar ohne weitere Unbekannte.**

---

## Anhang A — Lizenz & Distribution

Empfehlung: **MIT-Lizenz** (falls Open Source geplant) oder proprietär mit interner Nutzung.

Nicht vergessen: `LICENSE`-Datei im Repo, `THIRD-PARTY-NOTICES.md` für die NuGet-Abhängigkeiten.

---

## Anhang B — Security-Überlegungen

- Das Backup enthält **keine Credentials**, aber potenziell sensible Daten (Chat-Historie, Memory-Files, Projekte).
- Beim Schreiben in einen OneDrive- oder GDrive-Ordner landet das Backup unverschlüsselt in der Cloud des jeweiligen Anbieters. Wer das nicht will, konfiguriert ein lokales Target (USB-Stick, NAS) oder aktiviert später in einer v2 client-side AES-256 mit User-Passphrase (bewusst out of scope für v1).
- Logs (`%LOCALAPPDATA%\ClaudePortable\logs\`) enthalten keine PII außer Datei-Pfaden und Sizes.

---

## Anhang C — Was sich gegenüber v1 geändert hat

| Aspekt | v1 | v2 |
|---|---|---|
| Cloud-Zugriff | OAuth + Google/Microsoft-APIs | Kein Cloud-Zugriff, schreibt nur in lokale Ordner |
| OAuth-App-Registrierungen nötig | Ja (2x) | Nein |
| NuGet-Abhängigkeiten | +4 (Google.Apis.*, Microsoft.Graph, MSAL) | Keine zusätzlichen |
| Code-Umfang | ~4.000 LOC | ~2.500 LOC |
| Phase 3 Aufwand | 5h (Cloud-Adapter) | 1h (Folder-Targets) |
| Gesamt-Laufzeit | 25h | 18h |
| Unterstützte Ziele | GDrive, OneDrive | Jeder Sync-Client + USB/NAS/lokal |
| Upload-Resume-Logik | Ja, manuell implementiert | Entfällt (File-Copy ist atomar) |
| Sync-Status-Beobachtung | App kann Cloud-Status prüfen | App sieht nur bis zum Sync-Ordner |

---

**Ende der Anleitung. Gesamte geschätzte Claude-Code-Laufzeit: 18 Stunden über 6 Phasen.**

---

## Claude-Code-Fortschritt (fuer Resume)

<!--
HANDOVER-NOTE fuer kuenftige Claude-Code-Sessions.
Stand: 2026-04-22 (wird bei jedem Phase-Abschluss aktualisiert)
-->

**Projekt-Repo:** https://github.com/shaase-ctrl/ClaudePortable (public, MIT).
**Source lokal:** `C:\Users\Sascha Haase\dev\ClaudePortable\`.
**Checkpoint-Dokument:** `docs/progress.md` im Repo - ENTHAELT pro Session den aktuellen Stand, offene Risiken, Next-Steps. IMMER zuerst lesen.

### Phasen-Fortschritt

| Phase | Status | Issue |
|---|---|---|
| 0 Discovery | Weitgehend komplett (Store-Install + MCP-Pfade in Session 5 verifiziert; ProcMon-Capture offen) | shaase-ctrl/ClaudePortable#4 |
| 1 Backup-Core | DONE (initial commit) | - |
| 2 Restore + Path-Rewrite | DONE (initial commit) | - |
| 3 Folder-Targets + Sync-Discovery | DONE (initial commit) | - |
| 4 Scheduler + Retention | DONE (2026-04-22 session 2) | shaase-ctrl/ClaudePortable#1 |
| 5 WPF-GUI + Tray | DONE (2026-04-22 session 3, minimal aber lauffaehig) | shaase-ctrl/ClaudePortable#2 |
| 6 WiX MSI + Signing | DONE MSI + release.yml (ohne Cert-Signing) (2026-04-23 session 4) | shaase-ctrl/ClaudePortable#3 |

### So resume ich in einer neuen Claude-Code-Session

1. Clone/open: `C:\Users\Sascha Haase\dev\ClaudePortable\`
2. `docs/progress.md` lesen - das ist der Einstiegspunkt.
3. `gh auth status` pruefen; falls unauth: `gh auth login --web` (User macht den Browser-Flow).
4. `dotnet build -c Release` + `dotnet test -c Release` zur Sanity.
5. Naechstes offenes Issue-Item oeffnen und daran weiterarbeiten.
6. Nach Phase-Ende: `docs/progress.md` aktualisieren, Commit + Push, Issue schliessen, diesen Abschnitt hier ebenfalls aktualisieren.

### Bekannte Abweichungen von der Spec

- Target-Framework `net10.0` / `net10.0-windows` statt `net8.0` (nur .NET 10 SDK installiert).
- Kein Quartz.NET in Phase 4 - Windows Task Scheduler Integration statt in-process Scheduler. Begruendung in `docs/progress.md`.
- FluentAssertions v8 skipped (licensed); plain `Xunit.Assert` stattdessen.
- Zwei zusaetzliche Exclusions in `DefaultExclusions.cs`: `**/LOCK`, `**/debug/latest` (aus Live-Backup-Test gelernt).
- `FileShare.ReadWrite | FileShare.Delete` + try/catch-skip in `ZipArchiveWriter`, damit live-laufendes Claude Desktop gesichert werden kann.
- MSI ist **nicht signiert**. SmartScreen zeigt "Unbekannter Herausgeber". Signing via `SignTool` + echtem Code-Signing-Zert folgt, sobald ein Cert verfuegbar ist.
- MSI verwendet **self-contained publish** (bundles .NET 10 Runtime), daher keine .NET-Runtime-Vorbedingung fuer den User. Das macht das MSI ~61 MB gross (sonst waere es ~5 MB). Spec wollte framework-dependent + Pre-req-Check; wurde zugunsten von einfacher Distribution getauscht.
- Phase 4 Retention + Phase 5 GUI + Phase 6 MSI sind alle minimale aber lauffaehige Implementierungen. Feinschliff (H.NotifyIcon, Quartz.NET, Signed MSI, .NET-Runtime-Check, Code-Signing-Workflow) ist in `docs/progress.md` und den offenen Issues gelistet.

