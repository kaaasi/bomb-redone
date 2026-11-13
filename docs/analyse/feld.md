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
| `… run --seed <hex> --players N --wall W --background B [--mask M] [--wind W] [--invisible] [--no-fall] [--before-shuffle]` | ein Fall: `analyse/feld/<fall>/` mit `<punkt>-screen.bin`, `-background.bin`, `-dac.bin`, `-dac_set.bin`, `result.json` |
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

## 3b. Werkzeuge (Teil 4)

- `bombtool render playfield [gamedata] [out.png] [--seed HEX] [--players N] [--cfg BOMB.CFG]`: Runde aus `RandSeed` nach dem
  Mischen, Ausgabe PNG (Stand K) und `RandSeed`, Wand, Hintergrund, Wind, Panzer.
- `bombtool seed --time HH:MM:SS.cc --players N [--cfg …] [--max-steps K] [--tanks X1,…] [--terrain X:Y,…]`: Suchwerkzeug nach
  `runde.md` §4 „Vorschlag“ (R2-D3): `Randomize` aus der Uhrzeit, k = 0..K Schritte (Menü, Intelligenzen), Mischen
  (`SeedSearch.SkipShuffle`: `Random(20)` + 10 Tausche, je `Random(N)` + Wiederholungen, ohne Tausch der Sätze, R2-D4), Aufbau,
  Vergleich mit Panzer-x und oberstem Geländepixel je Spalte. **Befund:** Weil das Mischen unterschiedlich viele Schritte
  verbraucht, laufen verschiedene k oft in denselben Stand nach dem Mischen zusammen (Test: k = 26, 37, 60) – das Bild legt den
  Stand nach dem Mischen fest, nicht die Zahl der Menüschritte.
- Gegenprobe des Mischens: `feld.py run --before-shuffle` setzt `--seed` vor `O3[0]` und meldet `seed_after_shuffle`; für 2, 5 und 10
  Spieler gleich `SkipShuffle` (`SeedSearchTests`).
- Desktop: nach der Spielerauswahl eine Vorschau der Runde (`PlayfieldScreen`), jede Taste baut die nächste (`[APPROX]` bis Zug
  und Schuss folgen); nicht gesetzte DAC-Einträge 0..15 aus der VGA-Grundpalette (`MenuGraphics.DefaultColors`, `[APPROX]`).

## 4. Grenzen

- Palettenrotation, Ton, Verzögerungen und Tastatur laufen nicht in Echtzeit; die Probe prüft Indizes, gesetzte DAC-Einträge
  und Zustand, nicht den zeitlichen Ablauf (`Delay(FS)` beim Fallen läuft mit, verändert aber nichts).
- Titel O1 und Hauptmenü sind übersprungen; Sätze kommen aus `O2:0216`. Computer-Spieler (Steuerart 3) prüft die Probe nicht.
- `Random:Real < 0,5 → 0C79:0428` (Laufrichtung der Rotation) wirkt nur auf den Zustand von `0C79`, nicht auf den DAC an A.

## 5. Schuss (Spur `schuss`, Stand 13.11.2026)

### 5.1 Real48-Trigonometrie (Teil 1)

`tools/reverse-engineering/real48.py` hat drei neue Einsprünge (`sin` `12CE:1301`, `cos` `12CE:12EE`, `sqrt` `12CE:128F`) und die
Bereiche `12CE:1202–138B` (Hilfen, `Int` `122A`, `Frac` `127B`, Sqrt, Cos, Sin, Koeffiziententabelle `1362`) und `12CE:160D–1674`
(Polynom) in `befunde`; dafür kann der Emulator zusätzlich `SAR` und `PUSH CS`. `hashes` gibt je 20.000 Fälle und die Zeile `deg`
(ganze Grad −360..720 über `Rad` `0DBD:00D9`, Sin, Cos). Umsetzung `Core/Numerics/Real48.Trig.cs`, bitgleich (`Real48TrigProbeTests`):

- **Sin:** Exponent < 6Ch → Argument zurück; nur für |x| ≥ 2π `Frac(x / 2π) · 2π`, negativ + 2π; ab π − π und Vorzeichen merken,
  ab π/2 → π − x; Polynom `12CE:160D`: t = x², s = c₀ · t, s = (s + cₖ) · t (k = 1..6), Ergebnis (s + 1) · x mit den sieben
  Koeffizienten der Tabelle (Taylor-Reihe ±1/(2k+1)!). **Cos** = Sin(−(x + (−π/2))).
- **Sqrt:** Newton mit Startwert „Exponent halbiert“, g := (x / g + g) / 2 (Halbieren durch Exponent − 1), Ende, wenn der Exponent
  der Änderung unter Startexponent − 14h fällt; negativ → Laufzeitfehler 207.
- **Rad** `0DBD:00D9` = x / 180 · π, **Distanz** `0DBD:008F` = Sqrt(a² + b²) (Sqr `12CE:11C2`).
- `Playfield.BarrelOffset` rechnet jetzt `Trunc(Cos(Rad(a)) · 8)`, `Trunc(Sin(Rad(a)) · 8)` wie `U431:07BA`; die gemessene Tabelle
  (`feld.py barrel`) ist Test (`Real48TrigBarrelTests`, alle 181 Winkel gleich).

### 5.2 Probe `feld_schuss.py`

Setzt einen Fall von `feld.py` an **S** (`O4:479A`) fort: Satz des Schützen (+1Dh Winkel, +1Bh Kraft, +24h Waffe, +26h = 0, Bestand
≥ 1), Ton und Fluggeräusch aus (`E8B3`, `E8B2`), Spur `E8B5`, optional SS `E8CA`, Wind `E8A2` und `RandSeed` direkt vor dem Schuss;
dann Fernaufruf `O4:01B7(Schütze)`. Aufgezeichnet: der Zustand x, y, vx, vy (`DS:4796`, `479C`, `47A2`, `47A8`) mit Teilschritt
`DS:4740` und Ticks `[bp−2Eh]` bei jedem Durchlauf von `O4:060A` (SHA-256 „path“), das Ende bei `O4:1343` (Treffer `47DE`,
Explodieren `4750`), die Argumente von `O6:00E0`, danach Bildschirm, Puffer `DS:E5C0`, gesetzte DAC-Einträge, `RandSeed`, Zahl der
Zufallsaufrufe und alle Sätze.

| Aufruf | Ergebnis |
|---|---|
| `python3 -I tools/reverse-engineering/feld_schuss.py befunde` | SHA-256 von `O4:01B7–1378`, O6 ganz, `U431:03C3–05EC`, `UCC8:0BB6–0C1E` |
| `… run --seed <hex> [Fall wie feld.py] --shooter N --angle A --power P [--weapon W] [--ss S] [--trail] [--wind-value V] [--shot-seed HEX] [--sound]` | ein Schuss nach `analyse/feld/schuss-…/` |
| `… hashes [--only I …]` | feste Liste (23 Schüsse) → `analyse/feld/schuss-hashes.tsv`; daraus `tests/…/Shot/ShotProbeData.cs` |

Die Liste deckt ab: Lead Ball auf Gelände und Panzer, Baby Missile auf Gelände (ohne und mit Flächenschaden), Volltreffer, auf den
Schützen selbst, alle fünf Wandarten, Wand 0 rechts/links mit Wind > 0/< 0, Kraft = M und > M (G2), Spur, unsichtbarer Schuss mit
Spur, SS = 1 und 20, 4 und 5 Spieler mit Hintergrund 2 und 3, Selbstschaden (`Random:Real` < 0,2 bei `RandSeed` 0), Baby Missile
mit Ton (209 `Random(n)`: 11 Ringe + 3 · 66 Palettenschritte). Verglichen wird der
Bildschirm nur in Rahmen und Feld (x 8..312, y 28..192, Spalte `screen_field`), weil Kopfzeile, Waffenleiste und Wandbeschriftung
BGI-Ausgaben der UI sind (§2).

### 5.3 Ergebnis (Teile 2 und 3)

`Core/Shot` (`BallisticShot`, `Impact`, `Damage`) ist in allen 23 Schüssen **bitgleich**: jeder Zustand der Flugschleife, Ende,
`RandSeed`, gesetzte DAC-Einträge, Puffer, Rahmen und Feld, Energie, Punkte, Schild und Treffer aller Sätze (`ShotProbeTests`). Dabei
aus dem Code gelesen (Ergänzung zu `runde.md` §5):

- **Teilschritte:** Der Zähler `DS:4740` läuft 0..SS (also SS + 1 Teilschritte je Tick) und beginnt mit dem Rest der Satzschleife
  davor (= Spielerzahl N); der erste Tick ist daher kürzer. Die Bedingung „letzter Teilschritt“ in G2 ist `DS:4740 = SS`.
- **Reihenfolge der Operanden** wie im Code (z. B. x := vx / SS + x; x₀ = x_P + r · (cos a · 10); vy₀ = −sin a · (P / 50)), wichtig,
  weil Real48-Add und -Mul nicht in jedem Fall vertauschbar runden.
- **Treffertest** liest alle fünf Punkte, auch nach einem Treffer; bei Kraft = M setzt ein Geländepunkt das Ende wieder zurück, auch
  wenn ein Panzerpunkt davor es gesetzt hat. Treffertest und Wände schließen sich aus: Wände nur, wenn der Punkt außerhalb von
  11 < ix < 309, 33 < iy < 187 liegt oder der Schuss schon endet.
- **Zeichnen:** alter Punkt (Kreuz aus fünf Pixeln, `U431:040F`) aus dem Puffer, Spur `U431:056D` in Bildschirm **und** Puffer
  (Farbe 100 − Schütze, Farben 90..99 = 32 div Farbteiler); neuer Punkt `U431:03C3` in Farbe 19, Marken auf dem Rahmen; am Ende ein
  schwarzes Rechteck (9, 29)–(311, 191) (`UCC8:0031`) über die Marken. Unsichtbar: weder Punkt noch Marken, die Spur aber schon.
- **Explosion:** Rampe 130 + j = (R − j) · (63 div R) nur im Rotanteil; Mittelpunkt (Trunc(x − 11), Trunc(y − 31)) im Fenster
  (11, 31)–(309, 189); drei Palettenläufe enden mit allen Einträgen 130..130 + R auf 0. Krater `UCC8:0687` = Ellipse `UCC8:0351` mit
  Kopie aus dem Puffer (`VgaDraw.CopyEllipse`). Danach x + 11, y + 31 (Real48, nicht exakt das alte x) für den Flächenschaden.
- **Schaden** `O10:0000` setzt die Farbe des Panzers nur neu, wenn die Abwehr negativ wurde; Punkte `O10:018B` als Wort mit
  Vorzeichen (10 je verlorener Energie).

Nicht umgesetzt (Runde 2, R2-D1 = A; `NotSupportedException`): Lenkung, Repulser im Feld (Q5, braucht `ArcTan` `12CE:14D8` und die
Tabelle `0B25`), Einschläge der Waffen außer 0 und {1, 2, 3, 4, 7, 11}; der Bestand (+2Eh) wird noch nicht abgezogen. Ton und
Verzögerungen spielen nicht; mit Ton zieht die Explosion die `Random`-Aufrufe wie das Original (Schuss 22).
