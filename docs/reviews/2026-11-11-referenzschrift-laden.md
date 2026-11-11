# Review PR #66 – Referenzschrift laden und mit Originaldaten verpflichtend prüfen

Base `9e93d94`, Head `8c0a26a` (geprüft: `git rev-parse HEAD` = Head, `git status --porcelain` leer). Reviewer: Claude (Sonnet 5.5),
frische Sitzung ohne Beteiligung an #66. Zugriff auf `kaaasi/bomb-referenz` ok (`git ls-remote` → `70201591…`).

## 1. Merge-Empfehlung

**Ja** für genau diesen Head. Kein P0–P2. Zwei P3-Hinweise, keine Blocker.

## 2. Befunde

- **P3 – Prüfsummenprüfung für `schriften/` entfällt still, wenn `SHA256SUMS` fehlt**
  ([`fetch-reference.sh`](https://github.com/kaaasi/bomb-redone/blob/8c0a26a16db1f6c54e677a33c0c32f48d3e1dd21/tools/claude/fetch-reference.sh)
  am Ende, `if [ -f "$TARGET/schriften/SHA256SUMS" ]`). Probe: Kopie von `~/referenz`, `schriften/SHA256SUMS` gelöscht und die
  `.bin` verändert → `REFERENZ=… tools/claude/fetch-reference.sh` meldet Exit 0, keine Warnung. Gleiches Muster wie bei `original/`
  (nicht neu). Gemildert: `BitmapFontSource.Load` prüft die SHA-256 der Schrift selbst (Fehler statt Ersatz). Optional: fehlende
  `SHA256SUMS` bei ausdrücklich geladenem Ordner als Fehler melden.
- **P3 – Pflichtlauf ohne Daten (Lauf 3) prüft nur noch „irgendein Fehler mit ‚Original data missing‘“.** Die 4 Schrifttests
  tragen jetzt dasselbe Präfix; Lauf 3 bliebe „wie gewollt“ grün, selbst wenn die Originaldaten-Tests leer durchliefen und nur die
  Schrifttests scheiterten. Kein Fehlverhalten heute (Lauf 3: 45 Fehler = 41 + 4), nur eine schwächere Schranke. Das Präfix ist
  nötig: Gegenprobe unten.
- Hinweis (kein Befund): `GameConfigOriginalTests.cs:36` nennt weiter `fetch-reference.sh original`; wer nur das ausführt, bekommt im
  Lauf mit Daten künftig rote Schrifttests (Meldung benennt das Fehlen und `schriften/`). Dokumentiert ist der Weg ohne Argumente.

Alle übrigen Schwerpunkte ohne Beanstandung:

- `fetch-reference.sh` ohne Argumente lädt `original` + `schriften` und prüft beide `SHA256SUMS` (auch bei vorhandenem Klon, der
  Prüfblock steht hinter dem Klon-/Aktualitätsteil). Nachweis: Lauf unten, „Prüfsummen stimmen“ ×2.
- `[ReferenceFontFact]`: `BOMB_REQUIRE_GAMEDATA=1` oder `BOMB_REQUIRE_REFERENCE_FONT=1` = Pflicht; `BOMB_SKIP_GAMEDATA=1` blendet aus
  (Lauf 2: 31 übersprungen; Suche liefert dann `null`).
- Meldung: Das Präfix „Original data missing“ steht nur in `TestReferenceFont.Path` im Pflichtfall; andere Fehler (Format/SHA) behalten ihre Meldung.
- Doku (`CLAUDE.md`, `docs/originaldateien.md`, `docs/formate/schriften.md`, `tools/claude/README.md`) stimmig zum Skript und Test.

## 3. Ausgeführte Prüfungen

Einrichtung: `fetch-reference.sh` (18 + 6 Dateien, Prüfsummen stimmen), `setup-workspace.sh`, SDK `10.0.402`.

- `REFERENZ=/tmp/keine BOMB_REQUIRE_GAMEDATA=1 dotnet test tests/BombRedone.Formats.Tests -c Release --filter Category=ReferenceFont` → **Failed: 4**, Passed 0.
- Gleicher Filter, `BOMB_REQUIRE_GAMEDATA=1`, Schrift geladen → **Passed: 4**, Failed 0.
- Gegenprobe (temporäre Mutation, zurückgesetzt): Präfix `TestGameData.MissingMessage:` aus `TestReferenceFont.Path` entfernt →
  `tools/pruefen.sh` Lauf 3: „FEHLER: kein Scheitern wegen fehlender Originaldaten (andere-fehler:4, Exit 1)“, Ergebnis nicht bestanden.
- `tools/pruefen.sh` (alle drei Läufe):

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 348 | 0 | 0 |
| Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 303 | 0 | 31 |
| Pflichtlauf ohne Daten (scheitert wie gewollt) | 303 | 45 | 0 |

Commit `8c0a26a16db1f6c54e677a33c0c32f48d3e1dd21`, Ergebnis: bestanden.
- `tools/originalschutz-selbsttest.sh` und `tools/pruefen-selbsttest.sh`: alle Fälle ok.

**Akzeptierte `[APPROX]`:** keine neuen. **Annahmen:** Die CI läuft ohne Originaldaten und wurde nicht geprüft; Windows/macOS-Pfade nicht ausgeführt.
