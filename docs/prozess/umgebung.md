# Eingeschränkte Arbeitsumgebungen: früh erkennen, gezielt ausweichen

Ergänzung zu [AGENTS.md](../../AGENTS.md) §3 und §5. Die Hinweise stammen aus der Vorlage
`eishockey-manager-redone`; sie ändern keine Abnahme- oder Sicherheitsregeln. Lesen, sobald Build, Tests, Fetch oder
GitHub-Schreibaktionen in der eigenen Umgebung scheitern – nicht vorsorglich in jeder Sitzung.

- **SDK vor Build und Review nutzbar machen:** Benötigt wird das .NET SDK gemäß `global.json`, nicht das ältere
  .NET Framework. Zuerst Vorhandenes prüfen: im Repo-Verzeichnis `dotnet --version` (aufgelöste Version gegen
  `global.json`), bei `dotnet: command not found` vorhandene lokale SDKs suchen und ihr Verzeichnis zu `PATH`
  hinzufügen; NuGet-Paketcache und vorhandene `obj/project.assets.json` (`packageFolders`) prüfen. Ein frischer
  Worktree braucht eigene Restore-Dateien, auch wenn der Hauptcheckout schon gebaut wurde. Fehlt das SDK, es auf dem
  vorgesehenen Weg einrichten (`AGENTS.md` §3; in Claude-Sitzungen `tools/claude/setup-workspace.sh`, siehe
  `CLAUDE.md`) – nicht zusätzlich auf anderem Weg oder mehrfach herunterladen. Danach erneut `dotnet --version`
  prüfen. Ist die Einrichtung blockiert, Ursache dokumentieren; fehlendes SDK und gesperrter VSTest-Testhost sind
  unterschiedliche Fehler. Ein ausdrücklich vom Nutzer auf Codelesen beschränkter Review bleibt auf diesen Umfang
  beschränkt.
- **Netzwerkversuche begrenzen:** z. B. `timeout 45s git -c http.lowSpeedLimit=1 -c http.lowSpeedTime=15 fetch origin`;
  bei `curl` `--connect-timeout 5 --max-time 30`. Nach einem Proxy-/Berechtigungsfehler dessen Ursache klären
  oder auf einen verfügbaren GitHub-Connector wechseln; denselben blockierten Versuch nicht wiederholt starten.
  Eine ausstehende Werkzeugfreigabe ist kein laufender Fetch: Status nennen und unabhängige Arbeit fortsetzen,
  statt minutenlang ohne Rückmeldung darauf zu warten. Abgebrochene/abgelehnte Freigaben nicht automatisch erneut
  anfordern. Proxy und Sandbox nicht umgehen.
- **Connector gezielt einsetzen:** bekannte Dateien mit festem Ref und bei Bedarf Zeilenbereich abrufen;
  große Verzeichnisse nicht vollständig in den Chat ausgeben. Antworten erst filtern, dann nur benötigte Metadaten
  zeigen. Base64, vollständige API-Antworten und Download-URLs mit Zugriffstoken nicht protokollieren.
  Binärdaten lokal in kleinen Blöcken schreiben (sonst droht „Argument list too long“), Prüfsummen kontrollieren;
  Originaldateien/Referenzbilder nach `AGENTS.md` §2.1 außerhalb versionierter Dateien halten.
- **GitHub-Schreibaktionen eindeutig beauftragen:** Im Nutzer-Kurzaufruf Remote-Branch, Commit der vorgesehenen
  Dateien und PR-Erstellung ausdrücklich nennen (Beispiel in [review-anfrage.md](../vorlagen/review-anfrage.md)). Eine Ablehnung der
  automatischen Freigabeprüfung wegen unklarer Autorisierung ist kein Nachweis fehlender GitHub-Schreibrechte.
  Bei einer solchen Ablehnung Aktion und Begründung nennen; dieselbe Remote-Änderung nicht über ein anderes
  Werkzeug umgehen. Ohne zulässigen Schreibweg die fertige Datei bereitstellen. Repo-Anweisungen ersetzen keine
  Werkzeugfreigabe; bei tatsächlich fehlenden GitHub-Rechten ist dagegen die App-/Repository-Berechtigung zu klären.
- **Prüfstand erhalten:** Ein Connector-Abruf ersetzt nicht automatisch einen Checkout. Bei einer Rekonstruktion
  alle geänderten/gelöschten Pfade und Dateimodi berücksichtigen und den vollständigen Git-Tree gegen den Zielcommit
  prüfen. Ohne diesen Nachweis keine Prüfung des exakten Heads behaupten. Einen Review-Branch weiterhin von
  aktuellem `main` anlegen; der geprüfte Code bleibt unverändert.

**Offline-Restore und Build:** Bei vorhandenem vollständigem Paketcache bzw. Offline-Spiegel kann eine einzelne
lokale Quelle verwendet werden. `BOMB_OFFLINE_SOURCE` im folgenden Beispiel auf deren tatsächlichen Pfad setzen.
Das Abschalten von NuGet Audit betrifft nur diesen Offline-Lauf; im Prüfbericht angeben, keine dauerhafte
Projekt-/CI-Änderung daraus machen.

```sh
dotnet restore BombRedone.slnx --source "$BOMB_OFFLINE_SOURCE" --disable-parallel -p:NuGetAudit=false -m:1
DOTNET_PROCESSOR_COUNT=1 dotnet build BombRedone.slnx -c Release --no-restore -m:1 \
  -p:UseSharedCompilation=false -warnaserror
```

Bei einem Build-Abbruch ohne brauchbare Fehlermeldung zuerst ein begrenztes Diagnoseprotokoll lesen.
Ein Prozessor, `-m:1` und `UseSharedCompilation=false` halfen hier gegen Probleme mit parallelem MSBuild und
dem Compiler-Server. Für das unveränderte Prüfskript lassen sich die Eigenschaften pro Aufruf setzen:

```sh
NuGetAudit=false UseSharedCompilation=false DOTNET_PROCESSOR_COUNT=1 tools/pruefen.sh
```

**VSTest benötigt lokale TCP-Sockets**, auch ohne Internetzugriff. Vor langen Testläufen kann die Umgebung mit
`python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1", 0)); s.close()'` geprüft werden.
Scheitert VSTest beim `TcpListener` mit `SocketException (13): Permission denied`, sind Teststarts abgebrochen,
nicht Tests fehlgeschlagen oder bestanden. Nur die vorgesehene Umgebungsfreigabe verwenden; bleibt sie aus,
Standardprüfung als blockiert dokumentieren. Nullwerte der Skripttabelle sind dann **keine Testergebnisse**,
und ein solcher Pflichtlauf ist kein gewolltes Scheitern wegen fehlender Originaldaten.

Eine direkte xUnit-Ausführung im Prozess ist höchstens eine zusätzliche Diagnose, kein Ersatz für
`tools/pruefen.sh`. Keinen aufwendigen eigenen Runner bauen, um den Standardlauf als erledigt erscheinen zu lassen.
Falls ein vorhandener Runner genutzt wird: keine Projektverweise auf eine andere Test-/Produktkopie (sonst können
Mutationen gegen bereits geladene Originalassemblies laufen); Assembly-Pfade prüfen, Varianten in getrennten
Prozessen ausführen und vor `AssemblyRunner.Dispose()` auf `Idle` warten.
