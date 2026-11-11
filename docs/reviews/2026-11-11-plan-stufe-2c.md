# Review: Plan Stufe 2c (PR #62)

Prüfer: Claude Sonnet 5.5 (frische Sitzung, keine Mitarbeit am PR) · Base `b94551247f1e0c6ce129dccd4ed5abc4471b667e` · Head `8f927b15b396b1e716c3b729ce83ab21c254388e`
Prüfart: **reine Doku-Prüfung** (Codelesen, Querverweise; kein Build, keine Tests, kein SDK – für diesen PR nicht erforderlich).
Stand geprüft: `git rev-parse HEAD` = Head, `git status --porcelain` leer, `git merge-base origin/main HEAD` = Base = `origin/main` (frisch geholt).

## 1. Merge-Empfehlung

**Ja** für genau diesen Head. Beide Spuren sind nach `zusammenarbeit.md` §2 übernahmefähig, die Dateigrenzen überschneiden sich nicht
und passen zum gemergten Code, die Fundstellen stimmen (eine Ausnahme: P3, Quelle ist `menue.md`). Es gibt nur P3-Hinweise.
Merge-Prüfung §6 Nr. 3 (CI): entfällt, nur Doku – bestätigt (`git diff --stat b945512 8f927b1`: 1 Datei, `docs/arbeitspakete.md`
+36/−5; PR-Dateiliste `gh api …/pulls/62/files`: 1 Datei; `b945512` = Parent des Head). Nr. 4 (Originaldaten-Nachweis): entfällt, kein Code.

## 2. Befunde

Kein P0–P2. Die P3-Punkte sind Hinweise; Belege sind Lese-/Grep-Proben.

- **P3 – „ohne Zufall von außen“ widerspricht `AGENTS.md` §4 in der Lesart** (`docs/arbeitspakete.md` Zeile 237–238, `spielerauswahl`,
  Inhalt): Die Spielaufstellung soll „ohne Fenster und ohne Zufall von außen“ sein. Der Maßstab ist das Gegenteil: Zufall und Zeit sind
  *Eingaben* des Spielkerns (nie `Random.Shared`). Beleg: `docs/anforderungen/bildschirme.md` B3a listet „Random (?)“ als Gegnertyp –
  dessen Auflösung braucht einen übergebenen Zufallsgenerator (`src/BombRedone.Desktop/TurboPascalRandom.cs` existiert).
  Vorschlag: „ohne Fenster, Zeit und Zufall nur als übergebene Eingabe (Gegnertyp ‚Random‘ erst beim Spielstart aufgelöst)“.
- **P3 – `O2:52E4` steht nicht in `re-grundlagen` §6:** Der Plan zitiert `O2:52E4` mit „`re-grundlagen` §6, `menue.md`“. §6 (Zeilen 139–141) enthält
  `O2:07BA`, `O2:509C`, `O2:42E8`, `O2:481B`, `O2:2749`; `grep -n "52E4" docs/analyse/re-grundlagen.md` → kein Treffer. Der Aufrufer steht in
  `docs/anforderungen/menue.md:45` und `docs/reviews/2026-11-11-menue.md:13` („`O2:07BA` wird nur von `O2:52E4` gerufen“); `MenuScreen.cs:15` nennt
  `O2:52DD` als Sprung zur Spielerauswahl. Beides ist mit „… `menue.md`“ gedeckt, die Leitstelle kann §6 bei Gelegenheit ergänzen.
- **P3 – Registrierungszeile ohne Zweck bei `spielerauswahl`:** Abschnitt „Stufe 2c“ (Zeile 229–230) gibt „je Spur höchstens eine Registrierungszeile in
  `src/BombRedone.Tools/Program.cs`“, aber die Dateiliste von `spielerauswahl` (Zeile 239–241) nennt weder `Program.cs` noch `Tools/*`; nur
  `wertung-einkauf` hat `Tools/Render*` + Zeile. Vorschlag: „`spielerauswahl` nur falls nötig“ oder streichen (kleinere Konfliktfläche an der Schalter-Liste `Program.cs:13–17`).
- **P3 – `ScreenInput.cs` in keiner Dateiliste:** Beide Spuren brauchen Texteingabe/Tasten (Namenseingabe, Kauf mit `+`/`-`/`P`/`D`, B6). `src/BombRedone.UI/Screens/ScreenInput.cs`
  gehört nach Stufe 2b „nur additiv“ `menue`; für 2c fehlt die Ausnahme (`Screens/Score*`, `Shop*`, `Status*`, `PlayerSelect*` decken sie nicht). Vorschlag:
  „`ScreenInput.cs` nur additiv“ bei `spielerauswahl` (Integrationsfolge macht `wertung-einkauf` zum Rebase-Nutzer).
- **P3 (Verdacht, §5 Punkt 7) – Umfang `wertung-einkauf` in einem Teil:** vier Bildschirme (Wertung einzeln/Teams, Endergebnis, Einkauf, Statusfenster), Core-Modell mit
  Preisliste und `bombtool`-Befehl, „ohne Teilliste (ein Teil)“. `menue` (Stufe 2b) war in drei Teile geschnitten. Gegenmaßnahme steht im Plan (Fortsetzungen legt die
  Leitstelle an, höchstens 3, `spur-start.md` „Teile und Fortsetzung“); Vorschlag nur: Teilliste vorsehen (Core/Tabelle → Einkauf → Wertung/Endergebnis/Status), damit die
  Fortsetzung eine Aufgabenliste im Draft-PR hat (`spur-start.md` Zeile 63–64 setzt sie für „Teile und Fortsetzung“ voraus).
- **P3 – `docs/anforderungen/bildschirme.md` weiterhin keiner Spur zugeordnet** (wie in den Reviews zu #41 und #53); die Spuren legen eigene Anforderungsdateien an, das
  Rückschreiben bleibt bei der Leitstelle.

### Geprüft ohne Befund

- **Übernahmefähigkeit (§2), je Spur:** Ziel/Inhalt, Abnahme (synthetische Tests, Originaldaten-Tests mit Prüfsummen und Fundstelle, `[APPROX]` für Videobilder), Branch-Schema
  (Abschnitt „Stufe 2c“ + `paket/<spur>`), Base `b945512…` (= `git rev-parse origin/main`), Dateigrenzen, Abhängigkeit (`menue` gemergt, `git log`: `b945512 Merge pull request #55`),
  Modell und Review-Modell (Opus 5.5 / Opus 5.5, frische Sitzung) vorhanden. Aufstellung der Tabelle Zeile 172 „`menue` (gemergt 11.11.2026)“ stimmt.
- **Dateigrenzen ohne Überschneidung:** `spielerauswahl`: `Core/Setup/*`, `UI/Screens/PlayerSelect*`, `Desktop/*`, `tests/…Core.Tests/Setup/*`, `tests/…UI.Tests/Screens/PlayerSelect*`,
  `docs/anforderungen/spielerauswahl.md`, `Screens/Menu*` nur additiv. `wertung-einkauf`: `Core/Economy/*`, `UI/Screens/Score*|Shop*|Status*`, `Tools/Render*`, zugehörige Tests,
  `docs/anforderungen/wertung-einkauf.md`. Keine gemeinsame Datei außer der geregelten `Program.cs`; `Desktop/*` nur `spielerauswahl` (Zeile 269 nachgezogen).
  Verträglich mit dem Code: `src/BombRedone.Core/` enthält nur `GameLimits.cs` (Namensräume `Setup`/`Economy` frei), `src/BombRedone.UI/Screens/` kein `PlayerSelect*|Score*|Shop*|Status*`
  (`ls`), Tests-Ordner `Setup/`/`Economy/` neu; `Program.cs` hat die Schalter-Liste (`info`, `export`, `cfg`), eine Zeile je Spur ist additiv. Der START-Übergang ist vorhanden:
  `MenuScreen.cs:15/389` (`MenuChoice.Start`), `DesktopGame.cs:131` `_ => new PlaceholderScreen()` – der Desktop-Anschluss liegt in `Desktop/*` bei `spielerauswahl` ✔.
  `wertung-einkauf` ohne Desktop-Übergang (nur Tests und `bombtool`) ist in sich schlüssig („Runde fehlt noch“) – **erwartet offen, bestätigt**; ebenso Spielfeld nach START als Platzhalter.
- **Keine Abhängigkeit zwischen den Spuren:** „Spieler nur als Name und Kennzahl (keine Abhängigkeit von `Setup`)“ (Zeile 251–252); Integrationsfolge nur wegen `Program.cs`/Doku.
- **Fundstellen gegen `re-grundlagen` §6:** `O2:509C`, `O2:42E8`, `O2:481B`, `O2:2749` (Zeile 141) ✔; `O2:07BA` (Zeilen 96, 139) ✔; `O3:5AEF`, `O3:6696`, `O3:7566` (Zeile 144) ✔;
  `O3:0E1F` (Zeile 145) ✔; `0431:0F1B` (Zeile 143, `BOMB.EXE 0x068CB`) ✔; `O2:52E4` siehe P3.
- **Abnahmeangaben:** 18 Waffen/2 Zielhilfen/3 Abwehrsysteme = `bildschirme.md` Abschnitt 4 (Zeile 267–268, DOC) bzw. Zeile 286; Videoabschnitte B3 (Zeile 146), B5 (193), B6 (207) vorhanden;
  „offene P3 … (#19)“ = Issue #19 „R11-05: Waffenliste – CRI 20/D fehlt, „nicht käuflich“ unbelegt (Abschnitt 4)“ (`gh api repos/kaaasi/bomb-redone/issues/19`) – **erwartet offen, nur bestätigt**.
- **Architektur (`AGENTS.md` §4):** `Core` ohne `UI`/MonoGame (Setup/Economy nur Modell), Bildschirme in `UI/Screens` zeichnen in `Surface`, Desktop nur Anschluss; Zeit/Zufall: siehe P3 oben.
- **Originalschutz (§2.1):** alle Abnahmen nennen „Prüfsummen“, Texte aus `BOMB.EXE`/`BOMB.OVR` und „Abgleich mit Videobildern … `[APPROX]`“; keine Bilder, Spielstände oder Originaldateien im Repo
  (Diff: nur Markdown, `git diff --stat`).

## 3. Ausgeführte Prüfungen und `[APPROX]`

Befehle: `git fetch origin <head>`/`origin main`, `git checkout 8f927b1`, `git rev-parse HEAD`, `git status --porcelain`, `git diff b945512 8f927b1` (vollständig gelesen), `git merge-base`,
`grep -n` der Fundstellen in `docs/analyse/re-grundlagen.md`, `docs/anforderungen/*.md`, `docs/reviews/2026-11-11-menue.md`, `src/**`; `ls` der Zielordner; `gh api repos/kaaasi/bomb-redone/{pulls/62,pulls/62/files,issues/19}`.
Kein `tools/pruefen.sh`, kein Build (reine Doku, vom Auftrag so vorgegeben). Check-Runs am Head: keine gemeldet (`check-runs` leer) – Doku-PR, CI entfällt laut Auftrag.
**Akzeptierte `[APPROX]`:** keine neuen im Plan (Abnahme verlangt Markierung nach Regel 2.7). **Annahmen (nicht geprüft):** Inhalt von `BOMB.DOC` (18/2/3) nur über `bildschirme.md` Abschnitt 4 belegt; Disassembly-Inhalt der Prozeduren
nur über die Kartentabelle §6, nicht gegen `BOMB.EXE`/`BOMB.OVR` (nicht Teil dieser Prüfung, keine Originaldaten).
