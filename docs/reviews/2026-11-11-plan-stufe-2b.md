# Review: Plan Stufe 2b (PR #53)

Prüfer: Claude Sonnet 5.5 (frische Sitzung, keine Mitarbeit am PR) · Base `8e182d39f3a443b0294fd8d00483333ab9508b7f` · Head `d8adbc647e9676dab00b63db30304005cb7bf34c`
Prüfart: **reine Doku-Prüfung** (Codelesen, Querverweise; kein Build, keine Tests, kein SDK – für diesen PR nicht erforderlich).

## 1. Merge-Empfehlung

**Ja** für genau diesen Head. Die Entscheidung R1-schrift-1 = D entspricht Option D der Vorlage in PR #43, die Spur `menue` ist nach
`zusammenarbeit.md` §2 übernahmefähig, die Fundstellen stimmen mit `re-grundlagen` §6, die Schreibausnahme im Referenz-Repo ist eng
begrenzt, Originaldaten gelangen nicht ins Code-Repo. Offen sind nur P3-Hinweise.
Merge-Prüfung §6 Nr. 3 (CI): entfällt, nur Doku – bestätigt (`git diff --stat 8e182d3 d8adbc6`: 2 Dateien, `docs/arbeitspakete.md` +26/−5,
`docs/entscheidungen.md` +1; `merge-base origin/main d8adbc6` = Base). Nr. 4 (Originaldaten-Nachweis): entfällt, kein Code.

## 2. Befunde

Kein P0–P2. Die P3-Punkte sind Hinweise (Verdacht, §5 Punkt 7); die Belege sind Lese-/Grep-Proben.

- **P3 – Zugriff für den Schreib-PR im Referenz-Repo:** Teil 1 verlangt einen PR in `kaaasi/bomb-referenz` (Schreibzugriff). Der
  Zugriffsabsatz des geplanten Laufs fordert nur `read` und nennt `add_repo` „meist nicht vorhanden“
  (`docs/vorlagen/spur-start.md` Zeilen 13–17: `sed -n 8,20p`); `CLAUDE.md`: der Lauf erbt nur die in der anlegenden Sitzung
  eingebundenen Repos. Der Plan sagt nicht, dass die anlegende Sitzung `bomb-referenz` mit `push` einbinden muss, und nicht, was bei
  verweigertem Schreiben gilt. Vorschlag: im Startaufruf „Referenz-Repo mit `push` einbinden“ und im Plan „Schreiben nicht möglich →
  Teil 1 nur mit Ersatzschrift, Referenz-PR meldet die Leitstelle“ (Teil 2/3 brauchen die Referenzschrift nicht zwingend).
- **P3 – Ladeweg der Referenzschrift nicht in den Dateigrenzen:** Neuer Ordner `schriften/` im Referenz-Repo; `docs/originaldateien.md`
  (Tabelle Zeilen 6–13) und `tools/claude/fetch-reference.sh` (Ziele nur `original`, `bilder`) kennen ihn nicht, beide stehen in keiner
  Dateigrenze von `menue`. „Laden wie Originaldaten“ (`TestGameData`/`GameData`) ist ohne Eintrag dort nicht reproduzierbar;
  `src/BombRedone.Formats/GameData.cs` steht ebenfalls nicht in den Dateien. Vorschlag: Leitstelle ergänzt `originaldateien.md` und
  `fetch-reference.sh` nach dem Merge des Referenz-PRs, oder beide mit Begründung in die Dateien aufnehmen.
- **P3 – Merge-Reihenfolge:** PR #43 (`schrift`) ist derzeit `mergeable_state: dirty` (Base `1fc3d68`, `main` inzwischen `8e182d3`;
  beide Pläne ändern `docs/arbeitspakete.md`). Der Plan gated den Start von `menue` korrekt auf den Merge von `schrift`; vor dem
  Merge von #53 oder #43 ist mit einem Konflikt in `docs/arbeitspakete.md` zu rechnen – Reihenfolge der Leitstelle, kein Planfehler.
- **P3 – `docs/anforderungen/bildschirme.md`:** weiterhin keiner Spur zugeordnet (wie im Review zu PR #41); `menue` hat dafür
  `docs/anforderungen/menue.md`, Rückschreiben bleibt Sache der Leitstelle.

### Geprüft ohne Befund

- **(1) R1-schrift-1 = D:** `entscheidungen.md` (Zeile 29): „VGA-Schrift von DOSBox 0.74-3 (GPL) als Datei im Referenz-Repo für Abgleich und
  Tests … gemeinfreie 8 × 8-Schrift mit geprüfter Herkunft als eingebauter Ersatz, wenn die Referenzdatei fehlt“ = Option D der Tabelle in
  PR #43 („C für Abgleich und Tests, B als eingebauter Ersatz“); Empfehlung dort ebenfalls D. Die Plan-Teile 1 deckt die Umsetzung ab.
  Dass der Nutzer so entschieden hat, steht nur in der PR-Beschreibung (Annahme, siehe 3).
- **(2) Übernahmefähigkeit (§2):** Ziel/Inhalt (3 Teile), Abnahme mit Tests (synthetisch, Originaldaten, Prüfsummen, `[ORIGINAL]`/`[APPROX]`),
  Dateien, Abhängigkeiten (Tabelle „braucht `titel`, `schrift`“), Base („`main` nach Merge von `titel` und `schrift`, SHA im Startaufruf“),
  Modell und Review-Modell (Opus 5.5 / Opus 5.5, frische Sitzung) vorhanden. `titel` ist gemergt (#44, `git log` auf `main`), `schrift` offen.
- **Dateigrenzen:** `BitmapFont*.cs`, `UI/Text/*`, `tests/…/BitmapFont*`, `…/Text/*`, `docs/formate/schriften.md` sind Dateien von #43; Überschneidung
  nur nach dessen Merge (`git diff --name-only` merge-base..pr43 listet genau diese; Base von `menue` liegt danach) – das ist die geforderte
  Ausnahme. `ScreenInput.cs` (aus `titel`, auf `main`) „nur additiv“ ✔; `Desktop/*` in „Gemeinsame Dateien“ auf „nur `menue`“ nachgezogen ✔.
  Der Übergang Titel → Menü ist abgedeckt: der Platzhalter wird in `src/BombRedone.Desktop/DesktopGame.cs:49` verdrahtet (`grep -rn Placeholder src`),
  `Desktop/*` ist in den Dateien. `.csproj`-Änderungen nicht nötig (Standardglobbing).
- **Fundstellen gegen `re-grundlagen.md`:** `O2:06BD`/`O2:07BA`/`O2:1E7F`/`O2:0909` (§6 Zeile 139) ✔; `O2:11CC`/`O2:0E35`/`O2:0F81`/`O2:1042` (§6 Zeile 140) ✔;
  EURO Größe 1 für Kopfzeile und START mit Schatten (§4 Zeilen 95–96) ✔; „Main Menu“ = `O2:0909` ✔ (`grep -n` der acht Fundstellen: alle in Zeilen 95–97, 139–140).
- **(3) Schreibausnahme:** eng begrenzt – Branch `daten/vga-schrift-dosbox`, nur `schriften/` mit README (Herkunft, Lizenz, SHA-256), PR, ausdrücklich „nicht mergen“,
  „für diese Spur autorisiert (wie R1-D4)“; R1-D4 begrenzt analog auf `analyse/`. Originaldaten/Spielstände/Bilder: Referenzschrift stammt aus DOSBox, nicht aus dem Original;
  im Code-Repo nur Ersatzschrift und Prüfsummen. `AGENTS.md` §2.1 gewahrt; Dateityp-Eintrag in `.github/originalschutz-erlaubt.txt` nur über die Leitstelle ✔.
  **Lizenzangaben:** GPL (Referenzschrift, bleibt im privaten Referenz-Repo, Herkunft und Lizenz im PR zu nennen) und gemeinfrei (Ersatz, „belegte Herkunft“) beide verlangt ✔.
- **Abnahme/E3:** Pixelgleichheit nur gegen DOSBox-Bilder `runde-1` aus dem Referenz-Repo, bis dahin Videobilder `[APPROX]` ✔; Zeit/Eingabe von außen (`AGENTS.md` §4) ✔.
- **Erwartet offen (nur bestätigt):** Zuschnitt von Stufe 2c erst nach Merge von `menue` – Überschrift „Spätere Stufe 2c (Zuschnitt nach Merge von `menue`)“ ✔.

## 3. Ausgeführte Prüfungen und `[APPROX]`

Befehle: `git fetch origin` + `refs/pull/43/head` + Head-SHA; `git rev-parse origin/main` (= Base `8e182d3`); `git merge-base origin/main d8adbc6` (= Base);
`git diff --stat` und `git diff 8e182d3 d8adbc6` (vollständig gelesen); `git diff --name-only` merge-base..pr43; `grep -n` der acht Fundstellen in `re-grundlagen.md`;
`grep -rn Placeholder src`; Lesen von `zusammenarbeit.md` §2/§5, `spur-start.md`, `originaldateien.md`, `fetch-reference.sh`, PR-Beschreibungen #43 und #53.
`tools/pruefen.sh` **nicht** ausgeführt (reiner Doku-PR); Aussage „nur Doku-Prüfung“, kein Testnachweis nach §6 Nr. 4.

Akzeptierte `[APPROX]` im Plan: Abgleich mit Videobildern bis zum DOSBox-Bild; nicht belegte Lagen/Farben/Tasten sind mit `[APPROX]` zu kennzeichnen (Abnahme).
Annahmen (ungeprüft): Entscheidung „R1-schrift-1 = D“ stammt vom Nutzer (nur durch PR-Text belegt); Lizenz/Herkunft der DOSBox-Quelle und der gemeinfreien Ersatzschrift
wird erst in den Teilen geprüft; „Starts erst nach Go des Nutzers“ (`AGENTS.md` §2.8) bleibt Sache der Leitstelle.
