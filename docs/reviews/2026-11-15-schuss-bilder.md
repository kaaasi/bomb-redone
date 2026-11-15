# Review PR #133 – Spur `schuss-bilder` (Schüsse der Sitzung 3 gegen `s3_03`–`s3_07`)

Erstreview, geplanter Lauf (Claude Opus 5.5, eigene Sitzung ohne Implementierungskontext). Base `204a8d0b8210f6dc3adf909dd6340e4a3de669f4`,
Head `223219091eb1e7e68e4f1a3ce25f40ed7bf23f3b` (`git rev-parse HEAD` = Head, `git status --porcelain` leer).

## 1. Merge-Empfehlung

**Ja** für genau Head `2232190`. Die vier Schussparameter sind in einer eigenen, erschöpfenden Suche (Winkel 0..90 × Kraft 0..10·Energie,
Richtung je Panzer fest) jeweils der einzige Treffer; `s3_03`–`s3_05` sind im Feld pixelgleich; die Abweichungen in `s3_06`/`s3_07` sind
ausschließlich „Bild Hintergrund, wir Gelände/unterste Panzerzeile“ rund um den getöteten Panzer. Die Nachbildung des Rohrwechsels im
Test ändert nur die 3 Pixel des alten Rohrs, nicht Energien, Endpunkte oder `RandSeed` – sie verdeckt keinen Fehler in `Core/Shot`.
Originalschutz und Dateigrenzen eingehalten, alle drei Läufe grün, CI auf dem Head grün.

## 2. Befunde

Keine P0–P2. Kein P3.

Erwartet offen, bestätigt:

- **Todesexplosion und Kopfzeile/Zug** in `s3_06`/`s3_07` (Runde 3, `runde.md` §5.9 nicht in Runde 2): Abweichungen 371 bzw. 1371 Pixel,
  davon 364/1334 „Bild Hintergrund → bei uns Gelände (2)“ und 7/37 „→ Farbe 21“; keine einzige Abweichung in Gegenrichtung (Bild Gelände
  oder Panzer, wir Hintergrund), d. h. kein fehlender Krater und kein anders gefallener Panzer. Ausdehnung x 211..281 (`s3_06`) bzw.
  211..309 (`s3_07`), y 141..189, höchstens 75,7 px vom toten Panzer (277, 150). Ob das genau `O5[…]` ist, bleibt Runde 3.
- **DAC-Abweichung** nur 20/21 und 98/99 (Rot/Grün getauscht, Wert gleich; R2-D4 = A) – im Test so geprüft.
- **`[APPROX]` nur im Test** (Rohrwechsel `O7:040C`, Befehl 1/2 laut `runde.md` §6.2 „Rohr mit dem Hintergrund löschen und neu“): Der Test
  macht genau das (`VgaDraw.CopyLine` aus dem Hintergrundpuffer, `DrawTank`). Bei Portierung des Zugs in Runde 3 durch den echten Zug ersetzen.

## 3. Ausgeführte Prüfungen

**Zugriff:** `git ls-remote https://github.com/kaaasi/bomb-referenz HEAD` → `e42feec…` (ohne `add_repo`). Daten:
`tools/claude/fetch-reference.sh` (Prüfsummen stimmen) und `… bilder/runde-1` (155 Dateien); SDK 10.0.402 über `setup-workspace.sh`.

**`tools/pruefen.sh`** (alle drei Läufe, Head `2232190`, Ergebnis: bestanden):

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 1061 | 0 | 0 |
| Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 914 | 0 | 71 |
| Pflichtlauf ohne Daten (scheitert wie gewollt) | 914 | 147 | 0 |

CI (`build`) auf dem Head: success.

**(1) Eigene Parametersuche** (temporäre Testdatei im Worktree, nicht committet; eigener Aufbau ab `RandSeed` `D2E3BA6E` aus #126 über
`Playfield.Build`, `Wind.Choose` (−92), `StartPlayer.Choose` (2), dann `BallisticShot.Fire` + `AfterShot.Run` wie der Test; Maß: Pixel der
Spurfarbe 100 − Satz im Bereich x 8..312, y 28..192, die in Bild und Rechnung verschieden sind; volle Feldabweichung zum Treffer):

| Schuss | Satz | Suchraum (vollständig, Schritt 1) | Treffer mit 0 | Feldabweichung |
|---|---|---|---|---:|
| 1 (`s3_03`) | 2 | 0..90 × 0..1000 | nur 75/200 | 0 |
| 2 (`s3_04`) | 1 | 0..90 × 0..1000 | nur 75/272 | 0 |
| 3 (`s3_05`) | 2 | 0..90 × 0..1000 | nur 38/241 | 0 |
| 4 (`s3_06`) | 1 | 0..90 × 0..950 (Energie 95) | nur 30/272 | 371 |

Der Suchraum des PRs (Teilbereiche) ist damit nicht nur plausibel, sondern die Eindeutigkeit gilt im ganzen zulässigen Bereich.
Richtung wurde nicht variiert (der Test prüft `tank.Direction` = Ausgangsrichtung; ein gespiegelter Schuss mit gleicher Spur ist
ausgeschlossen, weil die Spur am Rohr beginnt).

**(2) Pixelvergleich selbst nachgerechnet** (Feld x 8..312, y 28..192, `field.Screen` direkt, ohne `PlayfieldScreen`), mit und ohne
Rohrwechsel:

| Bild | mit Rohrwechsel | ohne Rohrwechsel | Energien | Ende (x, y) | `RandSeed` danach |
|---|---:|---:|---|---|---|
| `s3_03` | 0 | 0 | 100/100 | 134, 144 | `1369FDB0` |
| `s3_04` | 0 | 0 | 100/100 | 73, 135 | `69398D7F` |
| `s3_05` | 0 | 3 (279..280, 133..135, Bild Hgr., wir 21) | 95/99 | 12, 135 | `D6760BD2` |
| `s3_06` | 371 | 374 (+3 Farbe 20 altes Rohr Satz 1) | 95/0 | 276, 140 | `7C99828F` |
| `s3_07` | 1371 | 1374 | 95/0 | 276, 140 | `7C99828F` |

Ohne Nachbildung bleiben Energien, Endpunkte, Panzerplätze und `RandSeed` gleich; der Unterschied sind nur die Rohrpixel → kein
verdeckter Fehler in `Core/Shot`. Außerhalb des Feldes ~2000 Abweichungen (Kopfzeile/Waffenleiste, Zug nicht portiert, nicht verglichen).

**Gegenproben** (Mutation in temporärer Kopie):

| Mutation | Ergebnis |
|---|---|
| Rohrwechsel im Test abgeschaltet (`if (false && …)`) | rot: `Field_after_the_shot…(3)`, `Fourth_shot…` |
| Erdrutsch aus (`AfterShot.Run`: `crumble > 1000`) | rot: `Field_after_the_shot…(1,2,3)`, `Fourth_shot…` |
| Nachbarn enthalten (0, 0) | rot: `Neighbouring_angles_and_powers_do_not_match` |

**(3) Originalschutz und Dateigrenzen:** `python3 -I .github/scripts/originalschutz_pruefen.py` → „Keine Originaldateien eingecheckt.“;
keine gesperrten Dateitypen im Diff; im Code nur Koordinaten, Zählwerte und Farbindizes (keine Bildpixel, keine Palettenwerte aus den
Bildern). Geändert: `tests/BombRedone.UI.Tests/Screens/Playfield*` (inkl. neues `PlayfieldSession.cs`), `docs/analyse/feld.md`, eigener
Statusabschnitt in `docs/arbeitspakete.md` – im Zuschnitt. Auslagerung nach `PlayfieldSession.cs`: nur Sichtbarkeit/Parameter geändert,
DAC-Prüfung 20/21 in `PlayfieldScreenSessionTests` erhalten.

**Akzeptierte `[APPROX]`:** Rohrwechsel des Zugs im Test (s. o.). **Annahmen:** Zuordnung der Restabweichungen zu `O5` nur nach Form und
Lage (Runde 3).
