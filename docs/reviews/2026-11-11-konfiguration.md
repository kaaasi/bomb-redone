# Review PR #10 – `konfiguration`

Prüfer: Claude Opus 5.5, frische Sitzung (geplanter Lauf, unbeaufsichtigt, kein Implementierungskontext). Base
`ca689e9590c93292c59aae9e8083eb659b9cfa50`, Head `d643359b06bab8b3edd06c4979b3650055dbe4cd` (ausgecheckt, `git status
--porcelain` leer). Originaldateien und Beispieldatei aus `kaaasi/bomb-referenz` (HEAD `7e4bd33`), Prüfsummen stimmen.

## 1. Merge-Empfehlung

**Ja** für diesen Head. Alle 27 Felder plus Kennung sind im Code selbst nachgewiesen, nicht nur über die Beispieldatei: Reihenfolge,
Offsets und Größen aus den 28 `BlockRead` der Leseroutine (`BOMB.OVR 0x6D36`–`0x7002`, Summe 45), deckungsgleich mit den 28
`BlockWrite` von `BOMBCFG.EXE 0x6B86`; die Bedeutung jeder Variablen über die Debug-Ausgabe (`BOMB.OVR 0x79AE` ff.) und die
Befehlszeilenauswertung (`0x70EF` ff.). Jede Fundstelle in `GameConfig.cs` und `konfiguration.md`, die ich nachgeschlagen habe,
stimmt. Fehlerfälle stimmen mit dem Original überein bzw. sind korrekt als `[APPROX]` markiert. Dateigrenzen eingehalten. Es gibt
nur P3-Befunde.

**Vor dem Merge (Leitstelle):** `main` ist seit der Base neu (#11/#13). `git merge-tree origin/main d643359` meldet einen
Konflikt in `docs/arbeitspakete.md` – zwei Statusabschnitte, beide unten angefügt; Lösung: beide behalten. Damit greift die
Ausnahme „Rebase ohne Konflikt“ (§6 Nr. 1) nicht wörtlich; der Code ist davon nicht betroffen (`range-diff` muss nur diese
Datei zeigen).

**Erwartet offen – bestätigt:** Crumble 75 / Walls None in `BOMBCFG.EXE` gegen „Default 50%“ / „Default Random“ in `BOMB.DOC`
(Z. 69, 77). Beleg: `BOMBCFG.EXE 0x703B` lädt die Zeichenkette bei `0x6FFC` (`02 37 35` = „75“) nach `[0xff6]`, `0x7030`
`xor ax,ax; mov [0xff4],ax`; der Schreiber schreibt `[0xff6]` an Offset 9 und `[0xff4]` an Offset 11. Zur Entscheidung des
Nutzers zusätzlich: `BOMB.EXE` belegt vor dem Laden andere Werte vor (`BOMB.OVR 0x6C14` Crumble = 10, `0x6C1A` Wall = 0) – die
sind aber nie wirksam, weil eine gültige `BOMB.CFG` Pflicht ist.

## 2. Befunde

- **P3 – Feldbreite von `Wind`, `Wall`, `BossScreenMemoryKiB` ungetestet.** Die Tests nutzen nur Werte < 256
  ([GameConfigTests.cs:81–109](https://github.com/kaaasi/bomb-redone/blob/d643359b06bab8b3edd06c4979b3650055dbe4cd/tests/BombRedone.Formats.Tests/GameConfigTests.cs#L81-L109),
  Originaltest: 63); die Theorie `NumberFieldIsLittleEndianWordAtItsOffset` erfasst nur `int`-Eigenschaften. Beleg: Mutationen
  M20 `(WindSetting)data[7]`, M21 `(WallSetting)data[11]`, M06 `BossScreenMemoryKiB = data[43]` – jeweils alle 53
  GameConfig-Tests grün. Wirkung heute gering (Werte ≥ 256 kommen aus `BOMBCFG.EXE` nicht), aber die Abnahme verlangt Tests je
  Feld. Vorschlag: je ein Wert mit gesetztem High-Byte (z. B. `0x0104`, `0x0140`).
- **P3 – Zahlentyp ist belegbar `Integer` (vorzeichenbehaftet); `[APPROX]` „nicht verfolgt“ lässt sich auflösen**
  ([GameConfig.cs:203](https://github.com/kaaasi/bomb-redone/blob/d643359b06bab8b3edd06c4979b3650055dbe4cd/src/BombRedone.Formats/GameConfig.cs#L203),
  [konfiguration.md:90](https://github.com/kaaasi/bomb-redone/blob/d643359b06bab8b3edd06c4979b3650055dbe4cd/docs/formate/konfiguration.md#L90)).
  Beleg: `ndisasm -b16 -o 0x79AE -e 0x79AE BOMB.OVR` – die Debug-Ausgabe erweitert alle 2-Byte-Felder außer Boss-Speicher mit
  `cwd` vor `Write(LongInt)` (z. B. `0x7A47 mov ax,[0xe8a8]; cwd`), Turbo Pascal tut das nur für `Integer`; der Rundenaufbau
  vergleicht `BackControl` vorzeichenbehaftet (`BOMB.OVR 0x1CAED cmp word [0xe8ac],8; jg`). Folge: `Background = 0xFFFF` ist im
  Remake `BackgroundKind.Unknown`
  ([GameConfig.cs:132–139](https://github.com/kaaasi/bomb-redone/blob/d643359b06bab8b3edd06c4979b3650055dbe4cd/src/BombRedone.Formats/GameConfig.cs#L132-L139)),
  im Original −1 ≤ 8 → fester Hintergrund −1. Ebenso ist „bei 0..8 Hintergrund n“ (`konfiguration.md:60`) genauer „≤ 8“. Nur
  für Werte ≥ 32768 relevant; `BossScreenMemoryKiB` bleibt korrekt vorzeichenlos (`jna` bei `0x7024`).
- **P3 – `bombtool cfg` ohne Test.** `rg ConfigCommand tests` findet nichts. Händisch geprüft: Beispieldatei → Rückgabe 0,
  Ausgabe stimmt; ohne Argument 2; fehlende Datei, Verzeichnis, 44-Byte-Datei, `BOMB.EXE` als CFG → je 1 mit Meldung. Kein
  Abnahmepunkt, daher nur Hinweis.

Kein Befund zu (2): Der Kennungsvergleich bei `BOMB.OVR 0x6D46` ist ein Pascal-Stringvergleich (Länge + Inhalt) gegen
`04 33 2E 32 00` (`BOMB.EXE 0x16446`, `BOMBCFG.EXE 0x19EEE`, beides per `od` geprüft) – also genau 5 Byte, wie
`data[..5].SequenceEqual`. Länge: keine Prüfung im Original (Reset/`BlockRead` ohne Ergebnisvariable, nur `[0xec36]` nach
`Reset` → „nicht gefunden“ bei `0x7039`); Ablehnen ist als `[APPROX]` mit Begründung korrekt markiert. Der Originaldaten-Test
liest `nutzer-2026-11-11.CFG` (45 Byte, Kennung 3.2) und vergleicht 11 Voreinstellungen mit `BOMB.DOC` Z. 57–178 (alle per
`grep -n Default BOMB.DOC` nachgeschlagen) sowie die übrigen mit `BOMBCFG.EXE 0x7008`.

Kein Befund zu (3): Geänderte Dateien (`git diff --stat`) liegen alle innerhalb der Dateigrenzen der Spur
(`docs/arbeitspakete.md` Z. 123–124, 174–176); `Program.cs` hat genau eine Zeile (`case "cfg": …`), Status nur im eigenen
Abschnitt.

## 3. Ausgeführte Prüfungen

- Nachprüfung der Fundstellen mit eigenen Skripten (`ndisasm`, außerhalb des Repos): Liste aller `BlockRead`
  (`call 0x12ce:0x1d8d`, `0x6D8C`–`0x7036`) → 27 Felder, Variablen/Größen/Offsets = Tabelle in `konfiguration.md`; Debug-Namen
  je Variable (`0x79AE` ff.) = Feldnamen; Befehlszeilen-Speicherungen je Schlüsselwort (`0x70EF` ff.: `S±`, `F±`, `BK*`, `W`,
  `R*`, `C±`, `I±`, `T±`, `X±`, `D±`, `TEAM`, `B±`, `TS/FS/ES/SS/LS`, `M±` setzt `ForceMouse` 1, `H±`/`DV±`, `CV±`/`CRUDE`,
  `P±`, `PV`/`PT`) = Bedeutungen; `BlockWrite`-Liste in `BOMBCFG.EXE 0x6B86` (28 × , Summe 45); Boss-Umrechnung
  `0x7014`–`0x702C`; Hintergrundauswahl `0x1CAED`–`0x1CB33`; MZ-Köpfe `0x16A0`/`0x1D20`.
- CI auf dem Head: `build` success (Check-Run 130494521774).
- `tools/pruefen.sh` (alle drei Läufe), SDK 10.0.402, Commit `d643359`, Ergebnis: bestanden:

  | Lauf | bestanden | fehlgeschlagen | übersprungen |
  |---|---:|---:|---:|
  | Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 81 | 0 | 0 |
  | Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 68 | 0 | 9 |
  | Pflichtlauf ohne Daten (scheitert wie gewollt) | 68 | 13 | 0 |

- Rückmutationen (temporärer Worktree, je gezielter Filter, mit Originaldaten): M01 Offset `SoundEffects` 5→6, M02 `Crumble`
  9→10, M03 `Wind` 7→8, M04 `Wall & 3`, M05 `Mask & 0x1FF`, M07 Boss ohne 16-Bit-Cast, M08 `10 => Random`, M09 `== 1`, M10
  Crumble auf 100 begrenzt, M11 `Length != Size`, M12 Längenprüfung `< 5`, M13 Kennung nur Byte 1..3, M14 `Load` liest nichts,
  M15 `TeamPlay` negiert, M16 Kennung „3.1“, M17 `Size = 44`, M18 `Wall` aus Offset 7, M19 `TurretSpeed` aus Offset 33 → **alle
  rot**. M06/M20/M21 (nur Low-Byte) → grün, siehe P3 oben.

**Akzeptierte `[APPROX]`:** zu kurze Datei wird abgelehnt (Original prüft nicht); Wahrheitswert „Byte ≠ 0“. Das dritte
(vorzeichenlos) ist akzeptabel, aber nach P3 oben belegbar zu ersetzen.
**Annahmen (nicht geprüft):** Bedeutung der Geschwindigkeitswerte („Verzögerung“, „Teilschritte“) und welche Bilder die
Hintergründe 1..8 sind – laut Doku selbst nicht untersucht; `BOMBCFG.EXE`-Menü → Bitwerte (Liste „Zweite Beispieldatei“).
