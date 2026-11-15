# Review PR #126 – `gelaende-bilder`

Prüfer: Claude Opus 5.5, frische Sitzung ohne Implementierungskontext (geplanter Lauf, unbeaufsichtigt). Base
`8202e50f132a2e04503843c3bc2a13eec702b697`, Head `938937dc4c1b517d62cf0d256ec71ac516a28292` (ausgecheckt, `git rev-parse HEAD`
= Head, `git status --porcelain` leer). Originaldaten und `bilder/runde-1` aus `kaaasi/bomb-referenz` (`fetch-reference.sh`,
Prüfsummen stimmen). Konfliktfrei gegen aktuellen `main` `5a7c806` (`git merge-tree`).

## 1. Merge-Empfehlung

**Ja** für diesen Head. Die Startwertsuche lässt sich über den ganzen Tag nachstellen und liefert genau den einen Treffer
17:12:39.69, k = 1, `RandSeed` nach dem Mischen `D2E3BA6E`. Die Vorfilter verwerfen keinen echten Kandidaten (Gegenprobe ohne
Vorfilter über eine ganze Minute, Zufallsprobe mit 300.000 Startwerten). Mein eigener Pixelvergleich (PIL, unabhängig vom
PNG-Leser des Tests) ergibt genau die 248 Pixel Farbe 14 in vier Feldern der Kopfzeile und dazu DAC 20/21 vertauscht sowie
DAC 100/101/111 abweichend. Weitere Abweichungen gibt es nicht; die Begründungen (Zug fehlt, R2-D4 = A) passen zu Satzparität
und Farbteilern. Keine Bildpixel im Repo. Offen sind nur P3 (zwei Testlücken, die Mutationen überleben, und zwei
Doku-Ungenauigkeiten).

## 2. Befunde

**P3-1 Testlücke: der Rand-Ausschluss `y < 189` ist ungetestet.** Der Test
[PlayfieldSeedCommandTests.cs:63](https://github.com/kaaasi/bomb-redone/blob/938937dc4c1b517d62cf0d256ec71ac516a28292/tests/BombRedone.UI.Tests/Screens/PlayfieldSeedCommandTests.cs#L63)
soll ihn prüfen, legt den Punkt aber auf `(64, 189)`. Bei 3 Spielern reicht der Rand nur bis x = 63
(`marginEnd = 60 − 7 + 11 = 64`, exklusiv), der Punkt wird also schon über x verworfen.
Mutation in [RenderCommand.cs](https://github.com/kaaasi/bomb-redone/blob/938937dc4c1b517d62cf0d256ec71ac516a28292/src/BombRedone.Tools/RenderCommand.cs)
`p.Y < 189` → `p.Y <= 189`: `dotnet test tests/BombRedone.UI.Tests --filter PlayfieldSeedCommandTests` gibt **Passed 4/4**.
Mit dieser Mutation würde ein Geländepunkt auf y = 189 h = 1 festlegen, und ein echter Kandidat mit h = 0 fiele weg.
Vorschlag: den Punkt auf x = 63 legen (oder x = 20).

**P3-2 Testlücke: die letzte Sekunde des Fensters ist ungetestet.** Mutation `Parallel.For(first, last + 1, …)` →
`Parallel.For(first, last, …)` (die letzte Sekunde entfällt): `PlayfieldSeedCommandTests` gibt **Passed 4/4**, weil der Treffer
in
[Zeile 48](https://github.com/kaaasi/bomb-redone/blob/938937dc4c1b517d62cf0d256ec71ac516a28292/tests/BombRedone.UI.Tests/Screens/PlayfieldSeedCommandTests.cs#L48)
in der mittleren Sekunde liegt. Der Code selbst ist richtig. Vorschlag: `--time-to 08:15:30.40`, damit der Treffer am Fensterende
liegt.

**P3-3 Doku/Test: „Wind 92“ ist keine fehlende Zugangabe.** Laut
[feld.md:269](https://github.com/kaaasi/bomb-redone/blob/938937dc4c1b517d62cf0d256ec71ac516a28292/docs/analyse/feld.md#L269)
fehlt „Wind 92“. Der Windwert steht aber in Farbe 9 in „Wind: 92 ←“ und ist gleich (`PlayfieldScreen.DrawHeader`). Die 248 Pixel
verteilen sich auf Winkel 45, Kraft 97, Richtungspfeil 15 und „Men“-Wert 91 (bei x 248..270, nicht bei 145..175). Das erlaubte
Rechteck `(145, 10, 175, 16)` in
[PlayfieldScreenSessionTests.cs:26](https://github.com/kaaasi/bomb-redone/blob/938937dc4c1b517d62cf0d256ec71ac516a28292/tests/BombRedone.UI.Tests/Screens/PlayfieldScreenSessionTests.cs#L26)
deckt 0 Pixel ab und ist überflüssig. Probe: PIL-Zählung je Rechteck, siehe Teil 3. Ebenso prüft der Test die DAC 102..110 und
112..119 nicht (`i < WeaponColorBase`), obwohl sie im Bild gleich sind. Beides ist harmlos; Rechteck streichen, Doku berichtigen,
DAC ≥ 100 außer 100/101/111 mitprüfen.

**P3-4 Doku: abgebrochener Satz.** In
[feld.md:241–242](https://github.com/kaaasi/bomb-redone/blob/938937dc4c1b517d62cf0d256ec71ac516a28292/docs/analyse/feld.md#L241)
steht „Die Hundertstel- und / Je Sekunde des Fensters ein Stück …“.

**Erwartet offen, bestätigt:** Die Abweichungen in `s3_02` sind nur die Zugwerte der Kopfzeile (samt DAC 100/101/111) und DAC
20/21 (R2-D4), erklärt in `feld.md` §6. Bild: Farbe 20 = (0, 255, 0) und 21 = (255, 0, 0), bei uns umgekehrt. Das passt zu
27 Täuschen (ungerade, Satz 1 hält „Player 2“ mit dessen Teilern) und `ColorDivisors[tank.Number − 1]`. Die Sitzung-4-Liste
bleibt offen.

## 3. Ausgeführte Prüfungen

`tools/pruefen.sh` (alle drei Läufe, Head `938937d`, SDK 10.0.402):

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 1012 | 0 | 0 |
| Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 885 | 0 | 62 |
| Pflichtlauf ohne Daten (scheitert wie gewollt) | 885 | 127 | 0 |

- **Ganzer Tag:** `bombtool seed --players 2 --time-from 00:00:00.00 --max-steps 30 --background 8 --wind -92 --tanks 25,281
  --terrain 20:136,100:132,160:161,200:185,250:141,300:141` → `17:12:39.69 k 1: before shuffle 4983853D, after D2E3BA6E`,
  `1 candidate(s), 28.5 s` (2 Kerne).
- **Gegenprobe ohne Vorfilter** (eigenes Konsolenprogramm gegen `Core`, im Scratchpad): 17:12:00.00–17:12:59.99, k ≤ 30. Für
  alle 186.000 Kandidaten wurden Spielfeld und Wind gebaut und nur gegen `PlayfieldTarget` geprüft. Ergebnis: genau ein Treffer
  `17:12:39.69 k1 D2E3BA6E bg8 wind-92`, gleich dem Ergebnis mit Vorfilter. Also auch ohne die Filter Hintergrund und Wind
  eindeutig.
- **Vorfilter = Aufbau:** 300.000 Zufallsstartwerte, 2–10 Spieler, Wand 0–5, Hintergrund 0–5/7/8/RND/USER, Windoption 0–4.
  Mit den Ist-Werten aus `Playfield.Build`/`Wind.Choose` gebaut, scheitert `Passes` 0-mal. Ausgenommen sind h ≤ 1 (dort
  `TerrainTop` = 189, der Fall, den `BuildPrefilter` richtig ausschließt) und Hintergrund 6 (langsam; der bestehende Test deckt
  ihn ab).
- **Rand flach, Panzer verdeckt ihn nicht:** 3.000 Felder; `TerrainTop(x) = 190 − h` für alle x = 11..70 (177.480 Spalten, 0
  Abweichungen).
- **Pixelvergleich** mit PIL: `bombtool render playfield gamedata export/pf.png --seed D2E3BA6E` gegen `s3_02`. Das ergibt
  248 × (14→0) in (56..69, 0..6) 45, (56..79, 10..16) 97, (290..302, 1..5) 15 und (248..270, 10..16) 91. Dazu kommen 20 Pixel
  im Namen, weil `render` die Namen ungemischt ausgibt; der Test mischt sie richtig. DAC-Abweichungen der benutzten Farben:
  nur 20, 21, 100, 101 und 111.
- **Mutationen** (temporärer Worktree), jeweils rot: Schrittweite in `SkipTerrain` Fall 3 (6 Tests), Schritte von Hintergrund 7
  (2), `DosTime.Seed` mit vertauschten Wörtern (4), Fensterende `<` statt `<=` (1), ungemischte Namen im Bildtest (1),
  Rahmenfarbe 12 (1). Überlebt haben P3-1 und P3-2.
- **Originalschutz:** `python3 -I .github/scripts/originalschutz_pruefen.py` → „Keine Originaldateien eingecheckt.“ Der Diff
  enthält nur `.cs` und `.md`. Der PNG-Leser steht nur in `PlayfieldScreenSessionTests.cs`, nicht in `Support/`, und nutzt
  BCL `ZLibStream`. Im Test stehen nur Koordinaten und Zählwerte, keine Bildpixel.
- **Dateigrenzen:** Alle Dateien liegen im Zuschnitt (`Core/Playfield/*`, `RenderCommand.cs` für Teil 1, eigene Testordner,
  `feld.md`, eigener Statusabschnitt).

**Akzeptierte `[APPROX]`:** keine neuen. **Annahmen:** Hundertstel 0..99 sind eine Obermenge der DOS-Werte. Eindeutig ist der
Treffer nur im Suchraum k ≤ 30; das passt zu k = 1 bei zwei Menschen.
