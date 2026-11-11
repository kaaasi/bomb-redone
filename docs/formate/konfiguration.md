# `BOMB.CFG` – Konfiguration (Release 3.2)

Spur `konfiguration`, Runde 1 (R1-D3 = A: nur lesen). Leser: `src/BombRedone.Formats/GameConfig.cs`,
Werkzeug: `bombtool cfg <datei>`.

## Quellen

| Was | Fundstelle |
|---|---|
| Leseroutine von `BOMB.EXE` | `BOMB.OVR 0x6BF9` (Overlay-Segment ab Dateioffset `0x5E8`, im Segment `0x6611`): Voreinstellungen im Speicher, `Assign`/`Reset(f, 1)`, dann 28 × `BlockRead` direkt in globale Variablen, `Close` bei `0x7032` |
| Feldnamen | Debug-Ausgabe von `BOMB.EXE` (Parameter `DEBUG`), `BOMB.OVR 0x79AE` ff. – druckt Name und Variable, z. B. `Effsound`, `Crumbper`, `BackControl` |
| Bedeutung der Werte | Befehlszeilenauswertung `BOMB.OVR 0x70EF` ff. (schreibt in dieselben Variablen), Hilfetext `BOMB.OVR 0x62D7` ff., `BOMB.DOC` Abschnitt „COMMAND LINE PARAMETERS“ |
| Schreibroutine von `BOMBCFG.EXE` | `BOMBCFG.EXE 0x6B86` (Segment 0, `0x4E66`): 28 × `BlockWrite` mit denselben Größen in derselben Reihenfolge |
| Voreinstellungen von `BOMBCFG.EXE` („Restore Defaults“) | `BOMBCFG.EXE 0x7008` (Segment 0, `0x52E8`) |
| Versionskennung | `BOMB.EXE 0x16446` (Datensegment `0x56`), `BOMBCFG.EXE 0x19EEE` (Datensegment `0x11E`) |
| Beispieldatei | Referenz-Repo `original/spielstaende/nutzer-2026-11-11.CFG` (Voreinstellungen, 45 Byte) |

Dateioffsets in `BOMB.EXE`/`BOMBCFG.EXE` zählen ab Dateianfang (Kopf `0x16A0` bzw. `0x1D20` Byte), in `BOMB.OVR` ab
Dateianfang. Die Listings selbst liegen nicht in diesem Repo (R1-D2).

## Aufbau

45 Byte, ohne Prüfsumme. Wahrheitswerte 1 Byte (Turbo-Pascal-`Boolean`), Zahlen 2 Byte Little Endian. Spalte „Lesen“:
Dateioffset in `BOMB.OVR`, an dem `BOMB.EXE` das Feld liest.

| Offset | Byte | Feld (`GameConfig`) | Name im Debug-Dump | Befehlszeile | Werte | Lesen | Beispiel |
|---:|---:|---|---|---|---|---|---|
| 0 | 5 | Versionskennung | – | – | `04 33 2E 32 00` (string[4] „3.2“ + NUL) | `0x6D36`, Vergleich `0x6D3B` | `04 33 2E 32 00` |
| 5 | 1 | `SoundEffects` | Effsound | `S+`/`S-` | 0/1 | `0x6D92` | 1 |
| 6 | 1 | `FlySound` | Flysound | `F+`/`F-` | 0/1 | `0x6DAA` | 0 |
| 7 | 2 | `Wind` | WindControl | `Wn` | 0 None, 1 Low, 2 Normal, 3 High, 4 Gale, 5 Random | `0x6DC2` | 2 |
| 9 | 2 | `CrumblePercent` | Crumbper | `Cnnn`, `C+` = 100 | 0..100 | `0x6DDA` | 75 |
| 11 | 2 | `Wall` | Wallcontrol | `Rn`, `R+` = 2, `RR` = 5 | 0 None, 1 Sticky, 2 Elastic, 3 Accel, 4 Warp, 5 Random | `0x6DF2` | 0 |
| 13 | 1 | `InvisibleShot` | Inviso | `I+`/`I-` | 1 = Schuss unsichtbar | `0x6E0A` | 0 |
| 14 | 1 | `ShotTrail` | Trail | `T+`/`T-` | 0/1 | `0x6E22` | 1 |
| 15 | 2 | `GamesToPlay` | GamesToPlay | `Gnn` | Anzahl | `0x6E3A` | 10 |
| 17 | 1 | `TanksFall` | FallDown | `D+`/`D-` | 0/1 | `0x6E52` | 1 |
| 18 | 2 | `Background` | BackControl | `BKx` | 0 keiner (`BKN`, `BK-`), 1..8 fest (`BKA`..`BKH`), 9 zufällig (`BKR`, `BK+`), 10 Auswahl (`BKU`) | `0x6E6A` | 9 |
| 20 | 2 | `UserBackgroundMask` | – | – | Bit n = Hintergrund n zugelassen (n = 0..8) | `0x6E82` | `0xFFFF` |
| 22 | 1 | `ForceMouse` | ForceMouse | `M+` und `M-` setzen 1 | 0/1 | `0x6E9A` | 0 |
| 23 | 1 | `MouseEnabled` | Mousein | `M+`/`M-` | 0/1 | `0x6EB2` | 1 |
| 24 | 1 | `DirectVideo` | Fastpal | `DV+`/`DV-` (alt `H+`/`H-`) | 0/1 | `0x6ECA` | 1 |
| 25 | 1 | `ExtendedKeyboard` | Xkey | `X+`/`X-` | 0/1 | `0x6EE2` | 1 |
| 26 | 1 | `PaletteRotation` | Palette | `P+`/`P-` (`PV`, `PT` setzen 1) | 0/1 | `0x6EFA` | 1 |
| 27 | 1 | `CrudeVideo` | CrudeCycle | `CV+`/`CV-`, `CRUDE`/`NOCRUDE` | 0/1 | `0x6F12` | 1 |
| 28 | 2 | `PaletteSpeed` | Pspeed | – | 0 = am schnellsten | `0x6F2A` | 0 |
| 30 | 1 | `ComputersBuy` | CompuBuy | `B+`/`B-` | 0/1 | `0x6F42` | 1 |
| 31 | 2 | `TurretSpeed` | TurretSpeed | `TSnn` | Verzögerung | `0x6F5A` | 8 |
| 33 | 2 | `FallSpeed` | FallSpeed | `FSnn` | Verzögerung | `0x6F72` | 5 |
| 35 | 2 | `ExplosionSpeed` | ExplodeSpeed | `ESnn` | Verzögerung | `0x6F8A` | 8 |
| 37 | 2 | `ShotSpeed` | ShotSpeed | `SSnn` | Teilschritte je Bewegung | `0x6FA2` | 10 |
| 39 | 2 | `LaserSpeed` | LaserSpeed | `LSnn` | – | `0x6FBA` | 5 |
| 41 | 1 | `PaletteUsesRetrace` | Video Interrupt | `PV` = 1, `PT` = 0 | 1 Bildrücklauf-, 0 Timer-Interrupt | `0x6FD2` | 1 |
| 42 | 1 | `TeamPlay` | Team Play | `TEAM` | 0/1 | `0x6FEA` | 0 |
| 43 | 2 | `BossScreenMemoryKiB` | Boss Screen Memory Required | – | KiB; benutzt: `(wert << 10)` in 16 Bit, höchstens 64000 | `0x7002`, Umrechnung `0x7014`–`0x702C` | 63 → 64000 |

Alle 28 Felder sind über die Leseroutine belegt; kein Feld ist unbelegt. Die Spalte „Befehlszeile“ belegt die Bedeutung,
die Debug-Namen die Zuordnung Variable ↔ Bedeutung. Die Spielerzahl wird **nicht** gespeichert.

**Hintergrundauswahl (Offset 18/20):** Der Rundenaufbau ruft bei 0..8 Hintergrund n, bei 9 `Random(9)`, bei 10 so lange
`Random(9)`, bis `1 shl n and UserBackgroundMask ≠ 0` (`BOMB.OVR 0x1CAED`–`0x1CB33`). Welche Bilder die Nummern 1..8 sind,
ist hier nicht untersucht.

## Verhalten beim Laden

- **Datei fehlt:** `BOMB.EXE` meldet „A valid BOMB.CFG file was not found …“ und beendet sich (`BOMB.OVR 0x7039`).
- **Falsche Kennung:** „An invalid BOMB.CFG has been found.“ und Ende (`BOMB.OVR 0x6D4D`). Der Vergleich ist ein
  Pascal-Stringvergleich über Länge und Inhalt, also genau die 5 Byte. `BOMBCFG.EXE` liest zusätzlich die Kennungen `2.5` und
  `3.0` (eigene Leseroutinen, `BOMBCFG.EXE 0x6A87` ff.) und schreibt sie als 3.2 neu; das ist nicht Teil dieses Lesers.
- **Werte:** werden ohne Bereichsprüfung übernommen; der Leser übernimmt sie ebenso (Enum-Werte außerhalb 0..5 bleiben erhalten).
- **Zusätzliche Byte:** werden nicht gelesen; der Leser ignoriert sie ebenso.

## Abweichungen von `BOMB.DOC`

Die Beispieldatei enthält die Voreinstellungen von `BOMBCFG.EXE` (Routine `0x7008`). Zwei davon widersprechen der Anleitung:

| Einstellung | `BOMB.DOC` | `BOMBCFG.EXE` / Beispieldatei |
|---|---|---|
| Crumbling Hills | „Default 50%“ | 75 (Zeichenkette `'75'`, `BOMBCFG.EXE 0x6FFC`) |
| Walls | „Default Random“ | 0 = None (`BOMBCFG.EXE 0x7030`: `xor ax,ax; mov [0xff4],ax`) |

Alle übrigen in `BOMB.DOC` genannten Voreinstellungen stimmen überein (Test `ExampleMatchesDocumentedDefaults`). Welche
Voreinstellung das Remake übernimmt, ist offen (Entscheidung Leitstelle/Nutzer).

## Restlücken (`[APPROX]` im Code)

- **Zu kurze Datei:** Das Original prüft die Länge nicht (`BlockRead` ohne Ergebnisvariable unter `$I-`, laut Debug-Dump
  „I/O Checking OFF“), ungelesene Felder behalten vermutlich ihre Vorbelegung. Der Leser lehnt kürzere Dateien ab.
- **Wahrheitswerte:** gelesen als „Byte ≠ 0“; wie `BOMB.EXE` andere Werte als 0/1 auswertet, ist nicht verfolgt.
- **Zahlen:** vorzeichenlos gelesen; ob der Pascal-Typ `Integer` oder `Word` ist, ist nicht verfolgt (nur für Werte ≥ 32768
  von Bedeutung).

## Zweite Beispieldatei

Der Leser ist aus dem Code belegt; eine Datei allein belegt keine Zuordnung. Eine zweite Datei aus `BOMBCFG.EXE`, in der
jeweils genau diese Einstellungen vom Standard abweichen, würde den Weg Menü → Datei bestätigen:

1. Walls (anderer Wert als None) und Crumbling Hills (anderer Wert als 75) – die Abweichungen von `BOMB.DOC`.
2. Background „USER“ mit einer Auswahl weniger Hintergründe – Bitreihenfolge von `UserBackgroundMask` in `BOMBCFG.EXE`.
3. Mouse aus und ausdrücklich gesetzt – `ForceMouse`/`MouseEnabled`.
4. Palette über Timer (`PT`) und Palette Speed ≠ 0 – `PaletteUsesRetrace`, `PaletteSpeed`.
5. Boss Screen Memory ≠ 63, Extended Keyboard aus, Direct Video aus, Crude Video aus.
6. Turret/Fall/Explosion/Shot/Laser Speed mit fünf verschiedenen Werten – Reihenfolge der fünf Geschwindigkeiten (heute nur
   über die Größen, die Debug-Namen und die Voreinstellungen 8/5/8/10/5 zugeordnet).
