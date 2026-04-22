namespace ClaudePortable.Core.Post;

public static class PostRestoreChecklistBuilder
{
    public static string Build()
    {
        return """
            # Post-Restore-Checkliste

            Folgende Schritte musst du manuell erledigen, nachdem das Restore abgeschlossen ist:

            ## Pflicht-Schritte
            - [ ] Claude Desktop starten und einmal einloggen
            - [ ] Claude Code: `claude login` ausführen
            - [ ] Jeden Connector einmal neu autorisieren (Gmail, Slack, GDrive etc.)
            - [ ] `claude plugin sync` ausführen, damit installierte Plugins geladen werden

            ## Gesicherte Ordner (Safety-Backups)
            Alter Zustand wurde unter `<ordner>_backup_YYYY-MM-DD-HHmmss` gesichert.
            Nach erfolgreicher Verifikation kannst du diese Backups loeschen.

            ## Troubleshooting
            - **Chat-Historie leer:** Claude-Desktop-Version pruefen. Bei Mismatch mit Backup-Version ist die Chat-History-Migration nicht garantiert.
            - **Plugins laden nicht:** `claude plugin install <name>` manuell fuer jedes Plugin aus `.claude/plugins/`.
            - **Connectoren funktionieren nicht:** Neu autorisieren. Tokens werden bewusst nicht gesichert.

            ## Scope-Hinweis
            Dieses Tool sichert keine OAuth-Refresh-Tokens, keine API-Keys, keine DPAPI-Blobs.
            Das ist Absicht: diese Artefakte sind user- und maschinengebunden und auf einem
            anderen PC ohnehin ungueltig.
            """;
    }
}
