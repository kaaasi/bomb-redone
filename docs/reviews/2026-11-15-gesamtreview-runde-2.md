# Gesamtreview Runde 2 – Zwischenstand

Unabhängige Sitzung: Codex. Base `39fdbcbc35dd8bbc45df843b2487f82ad69cb21f`,
Head `ce05f0eb3d5d715e6c9c2468b9fd74b0cbfc47ff` (main). Vor der Prüfung `git rev-parse HEAD`
= Head, `git status --porcelain` leer. Originaldaten aus `kaaasi/bomb-referenz`; `original/SHA256SUMS` erfüllt.

## 1. Merge-Empfehlung

**Noch keine Empfehlung für diesen Head.** Die beiden Gegenbeispiele und der schnelle Prüflauf sind grün;
die risikoorientierte Gesamtsichtung ist in dieser Sitzung aber noch nicht ausreichend abgeschlossen. Das ist ein
Zwischenstand, kein negatives Produkturteil. Fortsetzung als neue Datei `2026-11-15-gesamtreview-runde-2-teil2.md`
auf neuem Review-Branch: insbesondere Emulator-Hooks und Grenzen unabhängig prüfen, Real48-Randfälle,
Baby-Missile/Krater und Erdrutsch, Startwertsuche und Bildvergleich sowie Übergänge und Bedienung gezielt prüfen;
anschließend Gesamturteil bilden. Geld/Einkauf und Computer nur im Rahmen des für Runde 2 umgesetzten Umfangs prüfen.

## 2. Befunde

Bisher kein neuer nachgewiesener Produktfehler. Die bisherige Sichtung reicht nicht für die Aussage, dass keine vorliegen.

**Erwartet offen:** GitHub-Abfrage bestätigt #39, #71 (R2-D4), #102, #140 und #141 jeweils `open`.
Sitzung 4 und die `[APPROX]`-Restlücken aus `docs/arbeitspakete.md` bleiben offen; ebenso Todesexplosion,
Zugfolge und Computer für Runde 3. Sie werden hier nicht als neue Befunde gewertet.

## 3. Ausgeführte Prüfungen und Annahmen

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 1095 | 0 | 0 |

`tools/pruefen.sh --schnell`, Build Release mit Warnungen als Fehler, SDK 10.0.402, Ergebnis bestanden.
`REFERENZ`, `BOMB_SPIELSTAENDE` und `BOMB_BILDER` zeigten auf den separaten Referenzklon.

- **#78:** `dotnet test tests/BombRedone.Formats.Tests --filter FullyQualifiedName~SavedGameOriginalTests -v q`
  mit Originaldaten: 15 bestanden, darunter vier echte Stände gelesen und bytegleich zurückgeschrieben.
- **#98:** `dotnet test tests/BombRedone.Core.Tests --filter FullyQualifiedName~Wind_multiplies_in_16_bits_before_clamping -v q`:
  2 bestanden. `python3 -I tools/reverse-engineering/feld.py --gamedata gamedata --font
  /tmp/bomb-referenz-review/schriften/dosbox-0.74-3-vga-8x8.bin --root-out /tmp/feld-review hashes --only 11 12`
  ergibt für `00015446` Wind 30000 und für `002B423B` 3008, wie die Core-Tests.
- `feld.py befunde`: 5/5; `feld.py cpu`: 14 Operationen je 2.000 Fälle grün;
  `feld_schuss.py befunde`: 6/6; `real48.py befunde`: 4/4. Das prüft nicht alle Emulator-Hooks unabhängig.
- `feld_schuss.py --gamedata gamedata --font /tmp/bomb-referenz-review/schriften/dosbox-0.74-3-vga-8x8.bin
  --root-out /tmp/feld-shot-review hashes --only 0 5 10`: Bahn-Hashes, Endzustände, RandSeed und Feld-Hashes
  stimmen mit den entsprechenden `ShotProbeData`-Zeilen überein; die zugehörigen Core-Tests sind im Prüflauf grün.
- Gelesen: `SavedGame`, `RoundSequence`/`RoundStart`, Teile von `SeedSearch`, `AfterShot`, Desktop-Übergang und
  die Entscheidungen R2-D1–D10. Keine eigenständige vollständige Verifikation dieser Bereiche behauptet.

**Akzeptierte `[APPROX]` im vorläufigen Umfang:** die in „Restlücken Welle 2“ genannten Punkte und die beim
TWS-Leser dokumentierten Grenzen (u. a. unvollständige Stände, Kürzung/Validierung und Zeichensatz) bleiben bestehen.
**Annahmen / nicht geprüft:** Bildschirmfotos wurden in dieser Sitzung nicht unabhängig verglichen; die Tests und
Originalproben ersetzen diesen Vergleich nicht. Bedienung, Geld/Einkauf, Computer, Emulator-Grafik-Hooks und die
übrigen Randfälle sind noch nicht hinreichend geprüft. Keine Aussage zur CI auf diesem Head.
