# Dateiformate

Stand: 11.11.2026 (Spur `formate`). Erstbefund aller Dateien: [../originaldateien.md](../originaldateien.md). Bezeichner im Code
sind englisch, die Doku deutsch.

## `BOMB1.RAW`, `BOMB2.RAW` – Titelbilder

**Aufbau** (kein Kopf, keine Kennung): `[ORIGINAL]` aus Dateigröße und Inhalt der Dateien (`gamedata/BOMB1.RAW`, `BOMB2.RAW`).

| Bereich | Länge | Inhalt |
|---|---:|---|
| 0x0000 | 64.000 | 320 × 200 Palettenindizes, zeilenweise von links oben |
| 0xFA00 | 768 | Palette: 256 × (R, G, B), je 6 Bit (Werte 0–63) |
| gesamt | 64.768 | |

Beide Dateien haben genau diese Länge, alle 768 Palettenbytes liegen bei ≤ 63. Beide Paletten beginnen gleich
(Schwarz, 0x15-Grau, 0x2A-Grau, … wie die VGA-Standardpalette), unterscheiden sich aber in 154 von 768 Bytes; jede Datei hat
also ihre eigene Palette.

| Datei | Verwendete Indizes | Inhalt (Sicht der Leitstelle/Spur) |
|---|---:|---|
| `BOMB1.RAW` | 61 | Hintergrund: Verlauf Orange–Gelb–Dunkel (gerastert), grüne Hügel, Panzer links, Schuss mit Einschlag |
| `BOMB2.RAW` | 53 | Schriftzug „TANK WARS“ und „Copyright 1990, 1991, 1992 Kenneth Morse“ auf Index 0 (Schwarz, 78 % der Pixel) |

**Abgleich mit dem Longplay** (`videos/tank-wars-longplay/00002.png` und `00007.png`, 576 × 360): Motiv, Lage und Farbeindruck
stimmen mit den dekodierten Bildern überein. Der Hintergrund (Sekunde 2) entspricht `BOMB1`; der Schriftzug samt Copyright (Sekunde 7) entspricht `BOMB2`.
Im Video steht der Schriftzug über dem Hintergrund, also legt das Programm `BOMB2` irgendwie über `BOMB1`. **Wie** (durchsichtiger
Index, vermutlich 0; Umgang mit den zwei Paletten) ist nicht belegt – offen für `re-grundlagen`. Das Video ist skaliert (1,8) und komprimiert, daher kein Pixel- oder Farbbeleg.

### Palette: 6 → 8 Bit

VGA-Paletten haben 6 Bit je Kanal (`[ORIGINAL]`, `AGENTS.md` §4). Für die Darstellung wird gedehnt:

- `PaletteScaling.FullRange` (Standard): `(v << 2) | (v >> 4)`, 63 → 255. Weicht von `round(v · 255 / 63)` höchstens um 1 ab.
- `PaletteScaling.Times4`: `v · 4`, 63 → 252.

`[APPROX]`: Welche Dehnung DOSBox bei den Referenzbildern anwendet, ist erst mit einem DOSBox-Bild belegbar (Aufnahme `runde-1`
steht aus). Die Dehnung steckt an einer Stelle (`Palette.DefaultScaling`).

### Code und Werkzeug

- `BombRedone.Formats.RawImage` (`Parse`, `Load`, `ToBytes`, `Pixels`, `Palette`) und `Palette` (`FromBytes`, `GetRgb`, `ToAbgr`).
  Fehler: `FormatException` bei falscher Länge (nicht genau 64.768 Byte) oder Palettenwert > 63; `ToBytes` kehrt `Parse` um.
- `bombtool export [gamedata] [ordner]` schreibt `BOMB1.png` und `BOMB2.png` (indizierte PNG, Palette gedehnt) nach `export/`
  (per `.gitignore` ausgeschlossen). Exportierte Bilder sind Originalmaterial und werden nie committet (`AGENTS.md` §2.1).
- Tests: `tests/BombRedone.Formats.Tests/RawImageTests.cs` (synthetisch: Rundreise, zu kurz/zu lang, Palettenwert 64, Grenzwert 63,
  Dehnung; mit Originaldaten: Größe, Prüfsumme der Dateien, 61 bzw. 53 Farben, Rundreise).

Die Prüfsumme des *dekodierten* Bildes entspricht hier der Dateiprüfsumme, weil die Dekodierung verlustfrei ist und `ToBytes` die
Datei zurückliefert; geprüft werden die ersten 12 Hex-Stellen von SHA-256.
