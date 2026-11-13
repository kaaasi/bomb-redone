# Analyse `feld` – Probe des Spielfeldaufbaus (Spur `gelaende`)

Spur `gelaende`, Runde 2 (Stand 13.11.2026, Claude Opus 5.5). Probe nach R2-D7 = A: Der Originalcode von `BOMB.EXE`/`BOMB.OVR`
läuft unverändert in einem emulierten PC; das Remake (`Core/Playfield`, `UI/Screens/PlayfieldScreen`) wird gegen dessen Bildschirm,
Hintergrundpuffer, DAC und Zustand geprüft. Inhalt des Aufbaus: [runde.md](runde.md) §2, §3, §4; Adressschreibweise:
[re-grundlagen.md](re-grundlagen.md).

## 1. Werkzeug

`tools/reverse-engineering/feld.py` (Ablauf, Fälle), `feld_machine.py` (PC: DOS, BIOS, VGA-Modus 13h mit DAC, Lader),
`feld_cpu.py` (8086-Interpreter, vollständiger Befehlssatz). Keine Bytes der Originale im Skript; Ausgabe nur nach `analyse/feld/`.

| Aufruf | Ergebnis |
|---|---|
| `python3 -I tools/reverse-engineering/feld.py befunde` | SHA-256 der Codebereiche, auf denen die Probe aufsetzt (O8 ganz, O4 `44A7–47A1`, O3 `7847–787A`, O2 `0216–04A1`, Hauptprogramm `00FA–04DF`) |
| `… cpu` | Gegenprobe des Interpreters gegen die `Cpu` aus `real48.py` (Fälle von Welle 1, je Operation 2.000) |
| `… run --seed <hex> --players N --wall W --background B [--mask M] [--wind W] [--invisible] [--no-fall]` | ein Fall: `analyse/feld/<fall>/` mit `<punkt>-screen.bin`, `-background.bin`, `-dac.bin`, `-dac_set.bin`, `result.json` |
| `… hashes [--write]` | feste Fallliste (13 Fälle, alle Wände, Hintergründe 0–10, 2–10 Spieler, RND/USER, Wind-Überlauf #98) → `analyse/feld/hashes.tsv`; Spalte `screen_core` = Bildschirm ohne BGI-Text (§2) |
| `… call [--fill C] [--show] SEG:OFS(args) …` | nach dem Start Routinen fern aufrufen (Pascal-Argumente), SHA-256 des Bildschirms (Einzelvergleich der Zeichenroutinen) |
| `… barrel` | Rohrende von `U431:07BA` für die Winkel 0..180 (Tabelle `Playfield.BarrelOffset`) |

Laufzeit: ein Fall 1–6 s, `hashes` rund 30 s. Voraussetzung: Python 3, `gamedata/`; für Text mit DefaultFont die
8 × 8-Schrift aus dem Referenz-Repo (`~/referenz/schriften/dosbox-0.74-3-vga-8x8.bin`, `--font`) im BIOS-Schriftbereich.

**Ablauf** (alles Originalcode, nichts nachgebaut):

1. Start von `BOMB.EXE` am Einsprung mit einer `BOMB.CFG` aus den Beispielwerten von [konfiguration.md](../formate/konfiguration.md)
   (im Speicher erzeugt). Laufzeit, Crt, Graph mit gelinktem VGA256-Treiber, alle Spieleinheiten, Overlay-Verwaltung, Prozessortest
   `O2[0]` und Konfiguration `O2[3]` laufen echt; nur der Titel (`0000:046E`, O1) wird übersprungen. Halt vor dem Hauptmenü
   (`0000:04DB`) – Grafik, Puffer `DS:E5BC`/`DS:E5C0` und Schriften sind dann eingerichtet.
2. Die zehn Overlays werden mit ihren Fixups an feste Segmente geladen, jeder Stub-Einsprung springt dorthin (die
   Overlay-Verwaltung ist danach unbeteiligt).
3. `O2:0216(n)` für n = 1..10 (Spielersätze wie vor dem Hauptmenü: Name „Player n“, Tastatur, Farbteiler), dann Spielerzahl
   `DS:47DA` und Optionen (`E8A8` Wind, `E8AA` Wand, `E8AC` Hintergrund, `E8AE` Maske, `E8B4` Inviso, `E8B8` FallDown).
4. Fernaufruf `O3[0]` (`O3:7847`). Bei `O3:7876` (nach dem Mischen `O3:2417`, vor `O4[0]`) werden Sätze, Steuerarten und
   Intelligenzzeiger von vor `O3[0]` zurückgeschrieben (Satz i = Spieler i) und `RandSeed` := Startwert; ab hier zählt die Probe
   `Random(n)` (`12CE:1675`) und `Random:Real` (`12CE:168B`) und die geschriebenen DAC-Einträge.
5. Messpunkte in `O4:44A7`: **A** `O4:44CC` (Rückkehr aus `O8[0]` = `O8:0984`), **W** `O4:45F1` (Wind fertig), **K** `O4:46FF`
   (Kopfzeile und Waffenleiste `O8[1]` fertig), **S** `O4:479A` (Startspieler gewählt, Ende des Laufs).

**Umgebung der Probe** (`feld_machine.py`): DOS-Dienste für Dateien (nur lesen aus `gamedata/`, Schreiben bleibt im Speicher),
Vektoren, Zeit (fest 12:00), BIOS-Takt alle 20.000 Befehle, keine Hardware-Interrupts (Palettenrotation `0C79` läuft nicht: DAC
30..59 sind der Stand ohne Rotation), keine Maus, keine Taste. DAC-Modell: Ports `3C7h`/`3C8h`/`3C9h` und INT 10h `1010h`/`1012h`/
`1015h`/`1017h`; Modus 13h lädt eine Grundpalette `[APPROX]` (Modell der VGA-BIOS-Palette, nur für Einträge, die das Spiel nie
setzt; die Vergleiche nutzen nur gesetzte Einträge). `3DAh` wechselt bei jedem Lesen, damit Warteschleifen auf den Strahlrücklauf enden.

## 2. Festlegung des Vergleichs (Nachprüfung #106 N4-1)

Das Original zeichnet in `O8:0984` auch BGI-Text in den Bildschirm: die **Wandbeschriftung** (links außerhalb des Rahmens, §3.1 in
`runde.md`) und je Spieler den **Namen in der Kopfzeile** (Bar (109, 0)–(211, 7) Farbe 0, Name in Farbe i + 19, P12). Beides liest keine
Regel; nach R2-D2/N2-1 liegt BGI-Text in `UI`, der Bildschirmpuffer von `Core/Playfield` enthält ihn nicht. **Festlegung:**

| Messpunkt | Original (Probe) | Remake | Maskierung |
|---|---|---|---|
| A | Bildschirm A000h | Bildschirmpuffer `Core/Playfield`, darüber die Aufbau-Beschriftung aus `PlayfieldScreen` (Wandbeschriftung + Name des letzten Spielers mit Bar in der Kopfzeile) | keine |
| A | Hintergrundpuffer `DS:E5C0` | Hintergrundpuffer `Core/Playfield` | keine |
| A | DAC-Einträge, die seit `O3:7876` geschrieben wurden (`dac_set`: Index + RGB, aufsteigend) | Palette `Core/Playfield`, dieselben Einträge | übrige Einträge stammen aus früheren Bildschirmen |
| A | `RandSeed`, Zahl der `Random(n)`/`Random:Real`, Panzer x/y, Wand der Runde, gezeichneter Hintergrund | Zustand `Core/Playfield` | – |
| K | Bildschirm A000h, DAC (`dac_set`) | ganze Ausgabe von `PlayfieldScreen` (Kopfzeile, Waffenleiste; Teil 3) | keine |
| S | `RandSeed`, Wind `DS:E8A2`, Windstufe `DS:47E4`, Startspieler `DS:47EE`, Satz +72h | Zustand nach Wind und Startspieler (`Core`) | – |

Damit gibt es keine maskierten Spalten: Was `Core` nicht zeichnet, muss `PlayfieldScreen` genau so zeichnen. Der Bildschirm ist an
A außerhalb von Rahmen, Beschriftung und Kopfzeile 0: `O8:0984` leert ihn selbst (geprüft: Vorbelegung von A000h mit 55h ändert
A, K und S nicht; Beschriftung nur in x 0..6, Kopfzeile nur in x 109..211, y 0..7). Die C#-Tests
vergleichen SHA-256 der Puffer gegen die Werte aus `feld.py hashes` (Hashes sind keine Originalbytes); bei Abweichung zeigt
`feld.py run` die Puffer zum Vergleich je Pixel.

## 3. Ergebnisse (Stand Teil 1)

`befunde`: 5 von 5 Bereichen bestätigt; `cpu`: 11 von 11 Operationen gleich. `hashes`: alle 11 Fälle laufen bis S. Die Zählung der
Zufallsaufrufe an A bestätigt [runde.md](runde.md) §3.3–§3.6 und §4 (Beispiele aus `hashes.tsv`):

| Fall | `Random(n)` an A | Aufschlüsselung nach `runde.md` |
|---|---:|---|
| Hintergrund 0, Wand fest | 4 | Gelände 2 + Panzer 2 |
| Hintergrund 1 Stars, 3 Spieler | 923 | 1 + 3 · 306 Sterne + 4 |
| Hintergrund 6 Static, 8 Spieler | 47.554 | 9 Palette + 47.541 Pixel + 4 |
| Hintergrund 7 Rectangle | 14 | 9 + 1 + 4 |
| Hintergrund 9 RND / 10 USER (Maske `0124h`), Wand RND | 6 / 16 | + `Random(5)` Wand, `Random(9)` Hintergrund (bei USER wiederholt) |

`Random:Real` an A = 1 (Rotation) + 2 je Innenschritt des Geländes (§3.5). Zwischen A und S: Wind 1 + Wiederholungen `Random(S)`
und 4 `Random:Real`, Startspieler 1 + Wiederholungen `Random(N)` (§2). Neu gegenüber `runde.md`: **Hintergrund 4 (Spread)** ist
damit bitgenau prüfbar, obwohl §3.3 ihn als `[APPROX]` „nicht ausgewertet“ führt; die Probe liefert Bildschirm und Puffer.

## 3a. Ergebnisse Teil 2 (`Core/Playfield`, `Core/Graphics`)

`Core/Playfield` (`Playfield.Build`, `Backgrounds`, `Wind`, `StartPlayer`) ist an A und S **bitgleich** zur Probe in allen 13 Fällen
(`PlayfieldProbeTests`): `RandSeed` und Zahl der Schritte, Wand, gezeichneter Hintergrund, Panzer x/y, Hintergrundpuffer, Bildschirm
ohne BGI-Text (`screen_core`), gesetzte DAC-Einträge; an S Windstufe, Wind, Startspieler, Durchgang. Dabei aus dem Code gelesen
(Ergänzung zu `runde.md` §3):

- **Spread (4)** = Strahlen `UCC8:09E4` von (160, 100) zu allen Randpunkten des Felds (oben links→rechts, rechts oben→unten, unten
  rechts→links, links unten→oben), Strahl k in (k mod 15) · 2 + 30 (`12CE:0AB8` = LongInt div/mod, `12CE:0A9D` = LongMul).
- **Sky (5)**: DAC 30 + i = (i, 0, 63 − i), i = 0..19; Linien y = 31..190 in (y − 30) div 8 + 30. **Asterisk (8)** von (160, 110),
  Farben (i mod 30) + 30 bzw. ((j + 70) mod 30) + 30. **Circles (3)**: `UCC8:0351` ist eine Mittelpunkt-Ellipse relativ zum
  Fenster (11, 31)–(309, 189) mit Prüfung je Punkt, wenn sie das Fenster verlässt (`VgaDraw.Ellipse`, Einzeltest gegen `call`).
- **Panzerfarbe** `O10:01AC`: v = 63 · Energie div 100, DAC 19 + n = (v div r, v div g, v div b) mit den Farbteilern aus `O2:0216`.
- **Rohr** `U431:07BA`: (x + Trunc(8 cos a) · d, y − Trunc(8 sin a)) mit der Real48-Laufzeit; deren Werte weichen von exakter
  Trigonometrie ab (cos 0° · 8 → 7, sin 30° · 8 → 4, cos 60° · 8 → 3, sin 90° · 8 → 7). Bis `schuss` Sin/Cos portiert, nimmt
  `Core` die gemessene Tabelle (`feld.py barrel`).
- Nach jedem Panzer `O7:0364`: Rahmen neu und bei Wand ∉ [1, 2, 3] (`O7:0344`) Bodenlinie (11, 189)–(309, 189) Farbe 2.
- **BGI-Linie** (`0E26:1C86`, Treiber VGA256): symmetrischer Bresenham ab (x0, y0), bitgleich auf über 1.000 Zufallslinien im
  Bildschirm (`feld.py call`); `Core/Graphics/BgiDraw.Line`, `UI/Text/LineRasterizer` ruft sie auf (vorher `[APPROX]`).
- **Teil 3** `UI/Screens/PlayfieldScreen`: an A (mit Wandbeschriftung, SmallFont senkrecht, und Name in der Kopfzeile, DefaultFont)
  und an K (Kopfzeile `O4:45F1–46FA`, Waffenleiste `O8:1268`) bitgleich in allen 13 Fällen, ebenso die gesetzten DAC-Einträge an K
  (100..119 schwarz). Die Waffenbilder sind Aufrufe relativ zum Fenster des Felds (15i + 10, 18)–(15i + 25, 27) mit Clip; der
  untere Rand des Rahmens (0, 0)–(14, 10) liegt außerhalb. BGI-`Circle` kommt nur mit Radius 1 vor (vier Nachbarpunkte, gemessen
  mit `call`); andere Radien sind nicht abgeleitet (Radius 2 ist wegen des Seitenverhältnisses nur drei Zeilen hoch).
- Wind (#98): Fall `00015446` ergibt −548 · 4³ = −35072 → 30464 → 30000, Fall `002B423B` −977 · 4³ → 3008 (Probe bestätigt).

## 4. Grenzen

- Palettenrotation, Ton, Verzögerungen und Tastatur laufen nicht in Echtzeit; die Probe prüft Indizes, gesetzte DAC-Einträge
  und Zustand, nicht den zeitlichen Ablauf (`Delay(FS)` beim Fallen läuft mit, verändert aber nichts).
- Titel O1 und Hauptmenü sind übersprungen; Sätze kommen aus `O2:0216`. Computer-Spieler (Steuerart 3) prüft die Probe nicht.
- `Random:Real < 0,5 → 0C79:0428` (Laufrichtung der Rotation) wirkt nur auf den Zustand von `0C79`, nicht auf den DAC an A.
