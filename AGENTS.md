# Regeln für alle Agenten

Gilt für **jeden** Agenten (Claude Code, ChatGPT, andere) und für Menschen; setzt keinen Chatverlauf und kein Werkzeug voraus.
Werkzeugspezifisches steht getrennt (Claude Code: [CLAUDE.md](CLAUDE.md)) und darf nicht widersprechen. Zusammenarbeit, Rollen,
Übergabe, Merge-Prüfung: [docs/prozess/zusammenarbeit.md](docs/prozess/zusammenarbeit.md); Sonderfälle:
[docs/prozess/anhang.md](docs/prozess/anhang.md). Aktuelle Pakete: [docs/arbeitspakete.md](docs/arbeitspakete.md) („Aktuelle Runde“).

## 1. Projekt

Portierung von **Tank Wars** (Kenneth Morse, MS-DOS, Release 3.2 von 1992, Arbeitstitel BOMB) nach C#/.NET 10: zuerst
originalgetreu (Pixel, Regeln, Abläufe), danach Erweiterungen. Grundsatzentscheidungen: [docs/entscheidungen.md](docs/entscheidungen.md).

**Sprache:** Deutsch in Doku, Commit-Nachrichten, PR-Texten und Reviews. **Englisch im Quelltext:** Bezeichner und Kommentare
(C#, Skripte, Workflows). Ausgaben der Prüfskripte, die in PRs eingefügt werden, sind deutsch.

## 2. Harte Regeln

1. **Originaldateien, Spielstände, Bildschirmfotos und Ausschnitte daraus niemals committen** – auch nicht in Tests, Doku oder
   PR-Texten als Bild. Alles Originale liegt nur im privaten Repo `kaaasi/bomb-referenz` und wird nie in dieses Repo kopiert.
   **Ablage im Arbeitsverzeichnis nur in den Schutzordnern** `gamedata/`, `aufnahmen/`, `capture/`, `bilder/`, `videos/`,
   `analyse/`, `referenz/`, `original/` (alle im Stamm, per `.gitignore` gesperrt) oder außerhalb des Repos (`~/referenz`) –
   Dateinamen schützen nicht, umbenannte Konfigurationen und Bildschirmfotos sehen aus wie eigene Dateien. Die CI prüft
   zusätzlich die eingecheckten Dateien (`.github/scripts/originalschutz_pruefen.py`: Prüfsummen der Originale, gesperrte
   Dateitypen wie `.png`, `.cfg`, `.tws`); eigene Dateien solcher Typen brauchen einen Eintrag in
   `.github/originalschutz-erlaubt.txt` mit Begründung im PR. Tests ohne Originaldaten arbeiten mit synthetischen Daten.
2. **Nie direkt auf `main` schreiben.** Jede Änderung über Branch und Pull Request – in beiden Repos.
3. **Ein Branch – ein Agent.** Wer an einem Branch arbeitet, öffnet sofort einen Draft-PR und trägt sich als Bearbeiter ein.
   Fremde Branches nur nach dokumentierter Übergabe weiterführen.
4. **Keine Freigabe der eigenen Arbeit;** dafür gibt es ein unabhängiges Review (anderer Agent oder frische Sitzung ohne
   Implementierungskontext).
5. **Nur die eigenen Dateien ändern** (Dateigrenzen der Spur in `docs/arbeitspakete.md`). Gemeinsame Dateien (`BombRedone.slnx`,
   `Directory.Build.props`, `README.md`, `tests/**/Support/`, weitere laut `docs/arbeitspakete.md`) höchstens minimal und mit
   Begründung im PR.
6. **Erst reproduzieren, dann reparieren:** Befund nachstellen → roter Regressionstest → beheben.
7. **Nicht aus dem Original Belegtes bekommt im Code `[APPROX]`** mit kurzer Begründung, Belegtes `[ORIGINAL]` mit Fundstelle
   (Datei und Dateioffset `BOMB.EXE 0x…`/`BOMB.OVR 0x…`, Abschnitt der Anleitung oder Referenzbild). Keine ungeprüfte Originaltreue
   behaupten.
8. **Spuren starten erst nach dem Go des Nutzers.** Die Leitstelle legt Plan und Startaufrufe vor, startet aber nichts selbst.
9. **Entscheidungen gesammelt vorlegen:** je Entscheidung das Problem, 3–5 Optionen mit Vor- und Nachteilen und eine Empfehlung;
   die Leitstelle bündelt offene Fragen, statt einzeln nachzufragen. Ergebnis mit Datum in `docs/entscheidungen.md`.

## 3. Einrichtung

| Was | Woher |
|---|---|
| .NET-SDK | Version aus `global.json`, jede Quelle; ohne Microsoft-Server: Release `claude-werkzeuge` (`tools/claude/setup-workspace.sh`, braucht `gh`). |
| NuGet-Pakete | `dotnet restore BombRedone.slnx` über nuget.org; ohne Zugang Offline-Spiegel im selben Release (`nuget-packages.tar.gz`). Versionen fest (`.github/scripts/versionen_pruefen.py`). |
| Originaldateien | Privates Repo [`kaaasi/bomb-referenz`](https://github.com/kaaasi/bomb-referenz): ZIP `original/bomb32.zip`, entpackt `original/gamedata/`; Verknüpfung nach `gamedata/`: [docs/originaldateien.md](docs/originaldateien.md). Ohne Leserechte beim Nutzer anfragen. |
| Spielstände, Referenzbilder | Referenz-Repo, `original/spielstaende/` und `bilder/` (Spuren und Reviews nur lesen; ablegen nur Leitstelle und Nutzer per PR), `tools/claude/fetch-reference.sh`. |

Prüfen: `tools/pruefen.sh --schnell` (Build + Tests). Scheitern Build, Tests, Fetch oder GitHub-Schreibaktionen an der Umgebung
(kein `dotnet`, kein Netz, VSTest-Socket gesperrt, Freigabe abgelehnt): [docs/prozess/umgebung.md](docs/prozess/umgebung.md).
Blockierte Läufe sind keine Testergebnisse und werden als blockiert dokumentiert; Proxy, Sandbox und Freigaben nicht umgehen.

## 4. Aufbau

`Desktop (MonoGame) → UI (Bildschirme, 320×200-Palettenpuffer) → Core (Spielzustand, Regeln) → Formats (Originaldateien)`

- `src/BombRedone.Formats` – Originaldateien finden und lesen (`GameData`); Formate in `docs/formate/`.
- `src/BombRedone.Core` – Spielzustand und Regeln, ohne Fenster, Zeit und Dateizugriff von außen steuerbar.
- `src/BombRedone.UI` – Bildschirme zeichnen in `Surface` (Palettenindizes + Palette; Größe als Parameter, Original 320 × 200);
  `Presentation` rechnet die Darstellung im Fenster (ganzzahlig, optional 4:3).
- `src/BombRedone.Audio` – Tonfolgen (bis nach Runde 1 nur Gerüst), `src/BombRedone.Tools` (`bombtool`: Untersuchen, Export),
  `src/BombRedone.Desktop` (Fenster); `tests/` – xUnit, je Schicht ein Projekt.

**Verbindliche Architekturregeln:**

- **Abhängigkeiten nur in Pfeilrichtung.** `Core` kennt weder `UI` noch MonoGame; `UI` kennt MonoGame nicht.
- **Zufall und Zeit sind Eingaben des Spielkerns** (Zufallsgenerator und Takt werden übergeben, nie `Random.Shared`, nie die
  Systemuhr), damit Abläufe wiederholbar und gegen das Original prüfbar sind.
- **Bildschirme verändern keinen Spielzustand durch bloßes Zeichnen.**
- Weitere Regeln kommen mit den Paketen hierher; jede mit Datum und Entscheidung.

**Originaltreue** (Maßstab: `docs/entscheidungen.md` E3): (1) **Vorlage:** Bildschirmfotos aus DOSBox (Referenz-Repo `bilder/`),
Disassembly von `BOMB.EXE`/`BOMB.OVR`, Anleitung `BOMB.DOC`. (2) **Vermessen:** pixelgenauer Vergleich gegen die Bildschirmfotos
(320 × 200, verlustfrei). (3) **Farben:** VGA-Paletten haben 6 Bit je Kanal. (4) **Markieren** nach Regel 2.7. (5) **Prüfen:**
Bildschirm rendern, mit dem Referenzbild vergleichen, Test schreiben.

## 5. Tests

- **Einheitliche Prüfung `tools/pruefen.sh`** (Warnungen als Fehler, gibt die Tabelle für den PR aus):
  - **Normaler PR:** `--schnell` – ein Lauf mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`, enthält alle synthetischen Tests); den
    Lauf ohne Daten übernimmt die CI. Ohne Originaldaten läuft `--schnell` wie die CI → im PR „Originaldaten nicht geprüft“.
  - **Alle drei Läufe** (ohne Option: mit Daten, ohne Daten `BOMB_SKIP_GAMEDATA=1`, Pflichtlauf ohne Daten, der scheitern
    **muss**) nur bei Integrations-PRs, Erstreviews und Änderungen an der Test-Infrastruktur (`tests/**/Support/`, Testprojekte,
    `tools/pruefen.sh`, `.github/`). Fixläufe und Nachprüfungen: `--schnell` (`zusammenarbeit.md` §5).
  - Wer `tools/pruefen.sh` ändert, führt zusätzlich `tools/pruefen-selbsttest.sh` aus (Testdouble, kein SDK nötig); wer
    `.gitignore` oder den Originalschutz ändert, `tools/originalschutz-selbsttest.sh`.
  - Während der Arbeit nur betroffene Tests (`dotnet test <projekt> --filter …`), die volle Prüfung erst vor dem Push.
- Tests mit Originaldaten **nur** mit `[GameDataFact]`/`[GameDataTheory]` und `TestGameData.Load()`
  (`tests/BombRedone.Formats.Tests/Support/GameDataFact.cs`). Nie „ohne Daten einfach `return`“.
- **Jedes Testprojekt hat mindestens einen Test** (sonst liefert es keine Ergebniszeile und die Prüfung scheitert).
- Die CI hat **keine** Originaldaten: grün belegt nur den synthetischen Teil; den Lauf mit Originaldaten lokal ausführen und im PR
  mit Zahlen nachweisen. Testanzahl ist keine Abnahme.

## 6. Git und Pull Requests

- **Branches:** `paket/<spur>`, `fix/<thema>`, `prozess/<thema>`, `integration/<runde>`, `review/<thema>` (nur die Review-Datei);
  im Referenz-Repo `daten/<thema>`. Vor jedem Push `git fetch origin && git rebase origin/main` und `tools/pruefen.sh` grün.
- **Commits** auf Deutsch, aussagekräftig, mit Agenten-Trailer (z. B. `Co-Authored-By: ChatGPT <noreply@openai.com>`) – alle
  Agenten schreiben über dasselbe GitHub-Konto.
- **Kleine PRs gegen `main`**, Beschreibung nach `.github/pull_request_template.md`; per Weboberfläche oder REST
  (`POST /repos/kaaasi/bomb-redone/pulls`), GraphQL ist nicht überall erreichbar.
- **Status** einer Spur nur im eigenen Abschnitt von `docs/arbeitspakete.md`.
- **Übergabe immer:** Jede Sitzung endet – fertig wie bei Abbruch – mit einem Übergabekommentar im PR (`docs/handoffs/VORLAGE.md`,
  `zusammenarbeit.md` §3).
- **Gemergt wird nur von Leitstelle oder Nutzer** nach der Merge-Prüfung (`zusammenarbeit.md` §6).

## 7. Sparsam arbeiten

Jede Nachricht und jede gelesene Datei kostet Tokens – bei allen Agenten:

- **Antworten an den Nutzer knapp:** Ergebnis, Entscheidungsbedarf, nächster Schritt, mit Links. Keine Zusammenfassung des
  Arbeitswegs, keine Wiederholung dessen, was in PR, Review oder Repo steht, keine Tabelle, wo ein Satz reicht; ausführlich nur auf
  Wunsch.
- **Vorlagen nicht ausschreiben:** Spurstart, Review-Anfrage und Leitstelle laufen über die **Kurzaufrufe** in `docs/vorlagen/`; die
  Leitstelle gibt dem Nutzer nur den ausgefüllten Kurzaufruf.
- **Modell nennen:** Die Leitstelle nennt für jede Aufgabe ein passendes Claude-Modell (`zusammenarbeit.md` §5 „Modellwahl“).
- **Gezielt lesen:** in `docs/arbeitspakete.md` nur die aktuelle Runde und die eigene Spur; `docs/archiv/`, ältere Reviews und
  `docs/prozess/anhang.md` nur bei Bedarf. Diffs zuerst mit `--stat`, Logs gefiltert (`tail`, `grep`).
- **Lange Inhalte ins Repo** (Bericht, Plan, Review), im Chat nur der Verweis.
- **Tests nach Bedarf** (§5).

## 8. Wo was steht

| Thema | Datei |
|---|---|
| Grundsatzentscheidungen des Nutzers | `docs/entscheidungen.md` |
| Zusammenarbeit, Rollen, Übergabe, Review, Merge-Prüfung | `docs/prozess/zusammenarbeit.md` |
| Sonderfälle: Vertretung, GitHub Free, Gesamtreview, Codex, geplante Läufe der Leitstelle | `docs/prozess/anhang.md` |
| Eingeschränkte Arbeitsumgebungen (SDK, Netz, VSTest, Freigaben) | `docs/prozess/umgebung.md` |
| Arbeitspakete, Dateigrenzen, Abnahme, Status | `docs/arbeitspakete.md` |
| Kurzaufrufe: Spurstart, Review-Anfrage, Leitstelle, DOSBox-Aufnahme, Videobilder | `docs/vorlagen/` |
| Unabhängige Reviews / abgeschlossene Runden | `docs/reviews/` / `docs/archiv/` |
| Originaldateien: Ablage, Laden, Erstbefund | `docs/originaldateien.md` |
| Dateiformate, Analysen, Bildschirme (ab Runde 1) | `docs/formate/`, `docs/analyse/`, `docs/anforderungen/` |
