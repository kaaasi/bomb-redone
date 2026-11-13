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
| `… hashes [--write]` | feste Fallliste (11 Fälle, alle Wände, Hintergründe 0–10, 2–10 Spieler, RND/USER) → `analyse/feld/hashes.tsv` |

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

## 4. Grenzen

- Palettenrotation, Ton, Verzögerungen und Tastatur laufen nicht in Echtzeit; die Probe prüft Indizes, gesetzte DAC-Einträge
  und Zustand, nicht den zeitlichen Ablauf (`Delay(FS)` beim Fallen läuft mit, verändert aber nichts).
- Titel O1 und Hauptmenü sind übersprungen; Sätze kommen aus `O2:0216`. Computer-Spieler (Steuerart 3) prüft die Probe nicht.
- `Random:Real < 0,5 → 0C79:0428` (Laufrichtung der Rotation) wirkt nur auf den Zustand von `0C79`, nicht auf den DAC an A.
