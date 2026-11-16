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

`befunde`: 5 von 5 Bereichen bestätigt; `cpu`: 11 von 11 Operationen gleich. `hashes`: alle 11 Fälle von Teil 1 laufen bis S (die
Liste hat seitdem 13 Fälle, §1 und §4; #112). Die Zählung der
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
- Rundenfolge (#111, Spur `p3-runde-2`): `Core/Playfield/RoundSequence` – vor jeder Runde Mischen (richtig: je Spiel der Schlacht
  genau eine Runde, `runde.md` §1 S1/S3), dann Spielfeld, Wind und Startspieler mit den über die Schlacht gehaltenen Zählern +72h
  (`runde.md` §2 R4). Vorher legte die Desktop-Vorschau die Zähler je Runde neu an, sodass derselbe Satz mehrmals hintereinander
  beginnen konnte (`RoundSequenceTests` vorher rot); das Mischen je Runde war kein Fehler. `[APPROX]`: Zähler bleiben bei der
  Position (Sätze nicht getauscht, R2-D4 = A), Zufallsaufrufe zwischen den Runden (Wertung, Einkauf, Computer) fehlen bis Runde 3.

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
| `python3 -I tools/reverse-engineering/feld_schuss.py befunde` | SHA-256 von `O4:01B7–1378`, O6 ganz, `U431:03C3–05EC`, `UCC8:0BB6–0C1E`, O7 `0000–040B` (Fallen, Rahmen, Erdrutsch), `O4:4960–4A0A` (nach dem Schuss); 6/6 ok (#117) |
| `… run --seed <hex> [Fall wie feld.py] --shooter N --angle A --power P [--weapon W] [--ss S] [--trail] [--wind-value V] [--shot-seed HEX] [--sound]` | ein Schuss nach `analyse/feld/schuss-…/` |
| `… hashes [--only I …]` | feste Liste (33 Schüsse; 25–32 aus #118: linke Wand je Wandart, Wand 0 bei Wind 0, Tickgrenze) → `analyse/feld/schuss-hashes.tsv`; daraus `tests/…/Shot/ShotProbeData.cs` |

Die Liste deckt ab: Lead Ball auf Gelände und Panzer, Baby Missile auf Gelände (ohne und mit Flächenschaden), Volltreffer, auf den
Schützen selbst, alle fünf Wandarten, Wand 0 rechts/links mit Wind > 0/< 0, Kraft = M und > M (G2), Spur, unsichtbarer Schuss mit
Spur, SS = 1 und 20, 4 und 5 Spieler mit Hintergrund 2 und 3, Selbstschaden (`Random:Real` < 0,2 bei `RandSeed` 0), Baby Missile
mit Ton (209 `Random(n)`: 11 Ringe + 3 · 66 Palettenschritte), Kraft = M mit Explosion unter dem Gelände (Waffen 1 und 3). Verglichen wird der
Bildschirm nur in Rahmen und Feld (x 8..312, y 28..192, Spalte `screen_field`), weil Kopfzeile, Waffenleiste und Wandbeschriftung
BGI-Ausgaben der UI sind (§2).

### 5.3 Ergebnis (Teile 2 und 3)

`Core/Shot` (`BallisticShot`, `Impact`, `Damage`) ist in allen 25 Schüssen **bitgleich**: jeder Zustand der Flugschleife, Ende,
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

### 5.4 Nach dem Schuss: Erdrutsch und Fallen (Teil 4)

`feld_schuss.py post` setzt einen Schuss der Liste fort wie `O4:49CB`: `U431:07BA` für alle Sätze, Crumble (`E8A4`) > 0 → `O7:03C1`,
FallDown → `O7:0020`; aufgezeichnet wie in §5.2 (feste Liste mit 9 Fällen → `analyse/feld/schuss-post-hashes.tsv`, dazu die Schüsse
23 und 24: Kraft = M durch das Gelände, Explosion am Boden unter dem Gelände). `Core/Shot/AfterShot` ist in allen 9 Fällen
bitgleich (`AfterShotProbeTests`: `RandSeed`, Rahmen und Feld, Puffer, DAC, Energie, Inertia, Lage):

- **Erdrutsch** `U431:0496`: Suchlauf rückwärts ab Adresse 64000 (`repne scasb`, Ende bei di = 0); ein Pixel Farbe 2 mit „Luft“
  darunter (0 oder 30..99, also auch Spur 90..99) rückt sofort eine Zeile tiefer, die neue Adresse kommt in die Liste. Dann Durchgänge
  in Listenreihenfolge (unten rechts zuerst), bis alle Einträge 0 sind. Ein Krater an der Oberfläche bewegt nichts (kein Überhang);
  erst ein Loch unter der Oberfläche (Schüsse 23/24) lässt Gelände rutschen.
- **Fallen** `O7:0020`: Rohr einmal mit dem Puffer löschen (`UCC8:0AC0`), dann je Zeile Körper zeichnen, Farbe, y + 1, Abgleiten,
  Prüfung der neun Pixel, `Random(3) + 1` Schaden auf Inertia, alter Körper aus dem Puffer; der fallende Körper ist also nie zu sehen,
  der Puffer frisst dabei Gelände unter dem alten Körper weg. Zum Schluss jeder Panzer neu (`U431:07BA`). Fall 7: Panzer 2 fällt 28
  Zeilen, 59 Schaden.
- **Crumble** `O7:03C1`: Rahmen neu, dann Erdrutsch bei `Random:Real · 100 ≤ Crumble` (auch bei 100 ein `Random:Real`).

### 5.5 Darstellung und Werkzeuge (Teil 5)

- `Core` meldet über `ShotSettings.Frame` jedes Bild, das das Original eine Weile zeigt (Tick des Flugs, Ringe der Explosion, jeder
  Palettenschritt mit `Delay(ES)`, Krater, Erdrutsch, jede Zeile eines fallenden Panzers mit `Delay(FS)`); die Hooks ändern nichts am
  Ablauf (die Probe-Tests laufen ohne). `UI/Screens/PlayfieldShotScreen` hält diese Bilder (Rahmen und Feld über
  `PlayfieldScreen.RefreshField`, DAC) und spielt sie ab; Spur, Explosion, Krater, Erdrutsch und Fallen sind so genau die Pixel von
  `Core`. `[APPROX]` Der Flug hat im Original keine Verzögerung (Tempo der Maschine), hier 15 ms je Tick. Test: das letzte Bild von
  Schuss 3 mit Fallen hat den Feld-Hash des Probe-Falls `post` 0.
- `bombtool render playfield … --shot WINKEL,KRAFT[,WAFFE] [--shooter N]`: nach dem Aufbau Startspieler, ein Schuss (Waffe 0 oder
  Sprengwaffe) mit SS, Spur, Ton, Crumble und Fallen aus der Konfiguration, dann Erdrutsch und Fallen; PNG zeigt das Feld danach.
- Desktop: in der Vorschau zielt der Spieler am Zug mit ←/→ und ↑/↓, W wechselt Lead Ball/Baby Missile, Leertaste schießt, Enter
  baut die nächste Runde (`[APPROX]` Ersatz für den Zug `O7:040C`, ohne Kopfzeile und Bestand; danach der nächste lebende Spieler).

### 5.6 Grenzen

Nicht umgesetzt (Runde 2, R2-D1 = A; `NotSupportedException`): Repulser im Feld (Q5, braucht `ArcTan` `12CE:14D8` und die
Tabelle `0B25`), Einschläge der Waffen außer 0 und {1, 2, 3, 4, 7, 11}. Lenkung (+26h) ist nicht modelliert: `PlayfieldTank` hat kein
solches Feld, der Flug läuft immer wie ohne Lenkung, ohne Prüfung und ohne Ausnahme (#119). Der Bestand (+2Eh) wird noch nicht abgezogen. Ton und
Verzögerungen spielen nicht; mit Ton zieht die Explosion die `Random`-Aufrufe wie das Original (Schuss 22).

## 6. Bildvergleich mit Sitzung 3 (Spur `gelaende-bilder`, Stand 15.11.2026)

### 6.1 Startwert (Teil 1)

`bombtool seed` sucht jetzt auch über ein **Zeitfenster** (Review #123 P2-1): `--time-from`/`--time-to` (je `HH:MM:SS.cc`, fehlt
eines, gilt 00:00:00.00 bzw. 23:59:59.99; `--time-from 00:00:00.00` allein = ganzer Tag), k = 0..K (Standard 30 im Fenster, 2000
bei `--time`). Je Hundertstel `RandSeed` wie `Randomize` (`DosTime`: Wort `SS·256 + cc` oben, `HH·256 + MM` unten), k Schritte,
Mischen, dann billige Vorfilter in Aufrufreihenfolge (`PlayfieldPrefilter`, ohne Zeichnen): gezeigter Hintergrund
(`--background`), Höhe h des linken flachen Rands (aus den `--terrain`-Punkten im Rand, y < 189), x von Panzer 1 und N (`--tanks`),
Wind (`--wind`, Windoption aus der Konfiguration); erst danach das ganze Spielfeld gegen alle Panzer und Geländepunkte. Alle
Hundertstel 0..99 werden versucht, obwohl DOS sie aus dem 18,2-Hz-Takt ableitet (Obermenge). Je Sekunde des Fensters bilden die
Hundertstel mit ihren k Schritten ein Stück, die Stücke laufen parallel; die letzte Sekunde gehört dazu (Test mit Treffer am
Fensterende, #130/#132).

**s3_02:** Optionen aus `ablauf1-GAME1.TWS` = Voreinstellungen von `BOMBCFG.EXE` mit 2 Spielern. Aus dem Bild gemessen (Werte,
keine Bildpixel): Hintergrund 8 (Asterisk), Panzer x 25 und 281, oberstes Geländepixel (20, 136), (100, 132), (160, 161),
(200, 185), (250, 141), (300, 141) – also h = 54 am linken Rand –, Wind −92. Dateizeiten der DOSBox-Originale lagen nicht vor
(weder im PR noch in `bilder/runde-1/README.md`), daher der ganze Tag:

```text
bombtool seed --players 2 --time-from 00:00:00.00 --max-steps 30 --background 8 --wind -92 --tanks 25,281 \
  --terrain 20:136,100:132,160:161,200:185,250:141,300:141
→ 17:12:39.69 k 1: before shuffle 4983853D, after D2E3BA6E      1 candidate(s), 47.8 s
```

Suchraum 8.640.000 Zeiten × 31 = 267,8 Mio. Kandidaten, 47,8 s auf 2 Kernen. Genau ein Treffer: `Randomize` um 17:12:39.69 (DOSBox-
Uhr), ein Menüschritt (`Random(2)` des Hauptmenüs, zwei Menschen ohne Intelligenz), 27 Tausche beim Mischen (ungerade: Satz 1 hält
danach „Player 2“, Satz 2 „Player 1“), `RandSeed` nach dem Mischen `D2E3BA6E`. Test: `SeedSearchTests.Window_search_finds_the_start_value_of_session_3`
(Fenster 17:12:39.00–17:12:40.99).

### 6.2 Pixelvergleich (Teil 2)

`PlayfieldScreen` an K aus diesem Startwert (Namen in Satzreihenfolge „Player 2“, „Player 1“) gegen `s3_02`, Bildindizes und
DAC (`PlayfieldScreenSessionTests`, `[GameDataFact]`, Bild aus `~/referenz/bilder/runde-1`):

- **Gleich:** Rahmen, Hintergrund Asterisk, Gelände, beide Panzer mit Rohr (x, y = 25/135 und 281/140), Beschriftungen der Kopfzeile,
  Name des Spielers am Zug („Player 1“ in Farbe 21 = Satz 2, auch der Startspieler), Waffenleiste – alle 64.000 Indizes außer den
  unten genannten 248. DAC aller benutzten Farben gleich, auch der Verlauf 30..59 (noch kein Rotationsschritt sichtbar).
- **Abweichung 1 (erklärt, Zug):** 248 Pixel Farbe 14 in der Kopfzeile fehlen – die Werte, die erst der Zug schreibt (`runde.md`
  §6.1, Zug in Runde 2 nicht umgesetzt): Winkel 75 (45 Pixel in x 56..69, y 0..6), Kraft 200 (97 Pixel in x 56..79, y 10..16),
  Richtungspfeil (15 Pixel in x 290..302, y 1..5) und der Wert nach „Men“ (91 Pixel in x 248..270, y 10..16). Der Windwert („Wind: 92 ←“,
  Farbe 9) gehört zur Kopfzeile des Rundenbeginns und ist gleich (#131). Ebenso setzt der Zug die DAC-Einträge 100, 101, 111 der
  Waffenleiste (bei uns an K noch schwarz); die übrigen Einträge 102..110 und 112..119 sind gleich und werden mitgeprüft.
- **Abweichung 2 (erklärt, R2-D4 = A):** DAC 20 und 21 sind vertauscht. Die Farbteiler gehören zum Satz (+60h..+64h) und wandern
  beim Mischen mit; `Core` mischt die Sätze erst in Runde 3 mit (R2-D4 = A) und nimmt die Teiler nach Satznummer. Indizes gleich.

Keine Abweichung im Spielfeldaufbau, also kein Fix an `Core/Playfield` nötig.

## 7. Schüsse der Sitzung 3 (Spur `schuss-bilder`, Stand 15.11.2026)

Runde 1 aus §6 (Startwert, `RandSeed` nach dem Mischen `D2E3BA6E`, Wind −92), danach Startspieler `Random(2)` → Satz 2 („Player 1“),
dann je Schuss wie `O4:44A7`: Waffe `O4:01B7`, Erdrutsch (Crumble 75), Fallen. Optionen aus `ablauf1-GAME1.TWS` = Voreinstellungen
(SS 10, Spur, Ton). Waffe beider Spieler: Baby Missile (rotes Feld 1 der Waffenleiste; Lead Ball weicht schon im Krater von Schuss 1
ab). Test: `PlayfieldShotSessionTests` (`[GameDataFact]`, Bilder aus `~/referenz/bilder/runde-1`).

### 7.1 Schussparameter (Teil 1)

Die Kopfzeile zeigt den Spieler am Zug mit seinen letzten Werten, nicht den letzten Schuss (Review #123 P3-3). Gesucht wurde je
Schuss über Winkel × Kraft mit dem Maß „Pixel der Spurfarbe 100 − Satz, die in Bild und Rechnung verschieden sind“ (Rahmen und
Feld), nach den vorigen Schüssen; jeweils genau ein Treffer mit 0:

| Schuss | Satz (Name) | Winkel, Richtung | Kraft | Suchraum | Bild | Ende (x, y) |
|---|---|---|---|---|---|---|
| 1 | 2 („Player 1“) | 75, ← | 200 | Werte der Kopfzeile von `s3_02` | `s3_03` | 134, 144 |
| 2 | 1 („Player 2“) | 75, → | 272 | 60..90 × 150..400 (Schritt 5), dann 70..80 × 255..290 | `s3_04` | 73, 135 |
| 3 | 2 („Player 1“) | 38, ← | 241 | 10..89 × 150..600 (Schritt 10), dann 30..46 × 200..320 | `s3_05` | 12, 135 |
| 4 | 1 („Player 2“) | 30, → | 272 | Werte der Kopfzeile von `s3_06` | `s3_06`, `s3_07` | 276, 140 |

Stimmig mit den Kopfzeilen: `s3_05` zeigt für „Player 2“ Kraft 272 (Schuss 2), `s3_06` Winkel 30 und Kraft 272 (Schuss 4).
Je ein Schritt in Winkel oder Kraft ändert die Spur (Test `Neighbouring_angles_and_powers_do_not_match`).

### 7.2 Vergleich (Teil 2)

- **Schüsse 1–3 (`s3_03`–`s3_05`):** Rahmen und Feld (x 8..312, y 28..192) **pixelgleich**: Spur, Krater, Erdrutsch, Panzer. DAC aller
  Feldfarben gleich (Hintergrund 30..59 ohne sichtbaren Rotationsschritt), außer 20/21 und 98/99: Rot und Grün vertauscht, Wert
  gleich – die Farbteiler wandern beim Mischen mit dem Satz (R2-D4 = A, wie §6.2). Die Helligkeit der Panzerfarben folgt der
  Energie: in `s3_05` Farbe 20 = 59 (Satz 1, Energie 95 nach dem Einschlag von Schuss 3 am linken Rand; Kopfzeile „Men 95“),
  Farbe 21 = 62 (Satz 2, Energie 99, Selbstschaden `Random:Real` < 0,2 bei Schuss 3).
- **Zug (erklärt, `[APPROX]` im Test):** Ohne Zug bleibt beim Winkelwechsel von Schuss 3 das alte Rohr (75°) stehen: 3 Pixel Farbe 21
  bei (279..280, 133..135). Das Original löscht es im Zug `O7:040C` (Befehl 1/2: Rohr aus dem Puffer, neu zeichnen; `runde.md` §6.2).
  Der Test bildet nur diesen Schritt nach (`VgaDraw.CopyLine` aus dem Puffer, `DrawTank`); die Zwischenschritte der Drehung hinterlassen
  nichts (Bild gleich). Der Zug selbst kommt in Runde 3.
- **Schuss 4 (`s3_06`, `s3_07`):** trifft Satz 2 (Energie 0). Flug, Spur, Krater und Fallen (Panzer nach (277, 150)) gleich. Alle
  Abweichungen (371 bzw. 1371 Pixel, x 211..309, y 141..189) zeigen im Bild Hintergrund, wo wir Gelände (2) oder die unterste
  Körperzeile (21) haben: die **Todesexplosion** `O5` (§5.9, Runde 3). Nach dem Schuss gibt unser `RandSeed` `7C99828F` für
  `Random(7)` den Wert **1** (`O5:0DA0` Teilchen; `Random(n)` = hohes Wort mod n – hier früher irrtümlich 0); Bildvergleich in §9.5.
- Kopfzeile und Waffenleiste nicht verglichen (Zug, Teil 3 von `runde.md`, nicht umgesetzt).

Kein Befund in `Core/Shot` oder `Core/Playfield`, also kein Fix.

## 8. Alle Waffen (Spur `waffen`, Stand 15.11.2026)

### 8.1 Probe (Teil 1)

`feld_schuss.py` wählt den Einsprung wie `O4:44A7` (§5.1 `runde.md`): Waffe 5 → `O4:3426` (Laser), 7 → `O4:13D4` (MIRV), sonst
`O4:01B7`. Zusätzlich setzbar (Satz des Schützen bzw. Option, direkt vor dem Schuss): Lenkung +26h mit Ziel +28h (Herb +2Ah, Vick
+2Ch ≥ 1), Abwehr je Satz (Schild +52h, Inertia +56h, Repulser +5Ah, LongInt), Bestand +2Eh + 2 · Waffe (Laser: mindestens die
Kraft, denn der Laserbestand begrenzt die Kraft, `runde.md` §5.10), Crumble `E8A4`. Der **MIRV-Dialog** läuft im Original mit:
Eingabe Tastatur (`DS:E5A6` + 2 · Satz = 1), an den Schleifenköpfen `O4:15BE` (Muster `[bp−102h]`) und `O4:18E8` (Detonation
`[bp−108h]`) setzt die Probe die Wahl und das Annahme-Flag `DS:4563`; ab `O4:1AD0` (Bild aus `DS:E5BC` zurück) zählen die
DAC-Einträge – die Dialogfarben 130..134 gehören zu `zug`. Aufgezeichnet werden außerdem alle Aufrufe von `O6:00E0` (Farbe, x, y,
Schütze) und `O10:0000` (Schaden d, Abwehr k, Satz, Rückgabe = verlorene Energie, Trap am `retf 8` `O10:0147`).

| Aufruf | Ergebnis |
|---|---|
| `… befunde` | zusätzlich `O4:1379–3425` (MIRV mit Dialogtexten), `O4:3426–44A6` (Laser), O10 ganz; 9/9 ok |
| `… run … [--guidance G --target T] [--pattern M --detonation D] [--defense S:schild,inertia,repulser …]` | ein Schuss |
| `… waffen [--only I …] [--jobs N]` | feste Liste (182 Schüsse, `weapon_shots()`) → `analyse/feld/waffen-hashes.tsv`, mit `--jobs 2` etwa 10 min |

Die Liste: je Waffe 0..17 Gelände neben Panzer 2, Volltreffer, Kraft 400 gegen jede Wandart 1–4 und mit Ton (126); Laser durch
Gelände, gegen Panzer 2 ohne, mit verbrauchtem und mit ausreichendem Schild, mit Ton, Bestand unter der Kraft, Wände, nach links
(11); MIRV je Muster 0..4 und Detonation 0..2, dazu mit Ton (16); Herb, Vick, beide, Wand 0 mit Lenkung, Ziel = Schütze (9);
Abwehr: Schild teilweise und ganz, Inertia beim Fallen (mit `post`), Repulser lenkt ab (auch zurück auf den Schützen) und nutzt sich
ab (`Random:Real` < Rest / 40) (14); Erdrutsch nach Sonic Blaster und Xplosive Dirt bei Crumble 0, 10, 100 (6). Spalten: Fall,
Schuss, `RandSeed` vorher, Pfad (nur `O4:01B7`), Einschläge, Schäden, `RandSeed`/Zufallsaufrufe danach, `screen_field`, Puffer,
DAC (Hash und Einträge), Sätze (Hash der Rohbytes und Kurzform), bei `post` dasselbe nach Erdrutsch und Fallen.

Befunde der Probe: Die bisherigen Hashes (`hashes`, `post`; alle Werte in `ShotProbeData.cs` und `AfterShotProbeTests.cs`) bleiben
gleich. Der MIRV ruft kein `Random:Real` für den Selbstschaden (§5.1 gilt nur für `O4:01B7` und den Laser). Sonic Blaster ruft den
Erdrutsch unabhängig von Crumble; Xplosive Dirt zieht immer ein `Random:Real`. Stick, Elastic und Accel geben beim Laser dasselbe
Ergebnis (Spiegelung ohne Faktor, `runde.md` §5.10 L3).

### 8.2 Sprengwaffen, Dirt, Xplosive Dirt, `Circle` (Teil 2)

- **Sprengwaffen** {1, 2, 3, 4, 11}: liefen schon über `Impact.ExplosionRadius`; jetzt je Gelände, Volltreffer, Wand 1–4 und Ton
  gegen die Probe geprüft (`WeaponProbeTests`), ohne Änderung. MIRV (7) folgt mit Teil 5.
- **Graph `Circle`** `0E26:11EA` (`BgiDraw.Circle`): y-Radius = r · XAsp div YAsp (`0E26:11B7`), dann Bogen 0..360° über
  Treiberfunktion 14h, die VGA256 2.00 der Emulation der Graph-Unit überlässt (Sprungziel nach InitGraph: Verteiler
  `BOMB.EXE 0x11EEB`, Bogen `0x12346`); der geschlossene Bogen endet in der Ganzzahl-Ellipse `BOMB.EXE 0x12913` (beide Radien
  ≥ 1, S = max(rx, ry)² · 100, Kx = S div rx div rx, Ky = S div ry div ry, je Schritt vier Punkte, erst x-, dann y-geführt bis
  y = 0; der Winkeltest lässt bei Start = Ende jeden Punkt durch). Abgleich mit der Probe: Radius 0..63 an zwei Mittelpunkten
  bitgleich, dazu Kreise am Rand der Fensters 11..309 × 31..189 mit Clip (je Punkt). **Seitenverhältnis:** der Treiber setzt
  8500 / 10000, das Spielfeld aber `SetAspectRatio(1, 1)` in `O8:09F7` – im Spiel sind die Kreise rund.
- **Dirt** {13..16} (`O6:1014`): Fenster mit Clip, Mittelpunkt x − 11, y − 31 (Real48), je i = 0..R drei Kreise um
  (Trunc(x + 1), Trunc(y)), (Trunc(x − 1), Trunc(y)), (Trunc(x), Trunc(y)), Farbe 2. Kein Schaden, kein Zufall.
- **Xplosive Dirt** 17 (`O6:1197`): erst alle Lagen (je Ring i, Korn j: `Random(20 · i)`, dann `Random(r + 1)`), dann alle
  Körner `PutPixel` (Treiberfunktion 30h, Clip am Fenster) mit `Random(200)` bei Ton, am Ende `Random:Real · 100 < 2 · Crumble`
  → Erdrutsch. Die Probe trägt jetzt den tatsächlichen Crumble-Wert vor dem Schuss in die Liste ein (Voreinstellung 75).
- Abnahme: `WeaponProbeTests` (alle Fälle der Waffen 0–4, 11, 13–17 ohne Lenkung, Abwehr und `post`) und `BgiDrawTests`
  (Kreise mit Treiber- und Spielfeld-Seitenverhältnis, Clip).

### 8.3 CRI, Sonic Blaster, Abwehr (Teil 3)

- **CRI** {6, 8, 9, 10} (`O6:084E`, `Impact.Cri`): wie `runde.md` §5.7 K5; ergänzt: Startpunkt ab (Trunc(x), Trunc(y)) abwärts, bis
  ein Nachbar Gelände ist; je Durchgang erst alle Äste bewegen, dann übernimmt jeder tote Ast die Lage des ersten lebenden
  Eintrags 1..20 (ohne sich selbst) der **globalen** Liste `DS:E820` – auch Einträge jenseits der Astzahl aus früheren CRIs; Ende,
  wenn zu Beginn dieser Prüfung alle Äste tot waren. `[APPROX]` Die Liste lebt hier im Spielfeld (je Runde leer).
- **Sonic Blaster** 12 (`O6:0E66`): Schusspunkt, 2 × 100 Takte Farbe 19 bzw. 19 und 2, dann Farbe 19 weiß, Farbe 2 = (0, 42, 0),
  Erdrutsch unabhängig von Crumble.
- **Abwehr:** Schild und Inertia (über das Fallen, `post`) laufen durch `Damage.Apply` (O10:0000). **Repulser im Flug** (Q5,
  `O4:0788–0A34`, `BallisticShot.Repel`): d² = Trunc(dy · Int(dy) + dx · Int(dx)) mit dx = x_Panzer − ix, dy = iy − y_Panzer,
  f = −600 / d² / SS, Winkel über `ArcTan` (`12CE:14D8`, jetzt `Real48.ArcTan`: `real48.py` Op `arctan`, 20000 Fälle bitgleich)
  und Grad (`0DBD:011B`), Geschwindigkeit mit der Gradtabelle `0B25` (`Real48.SinDegrees`/`CosDegrees`; die Tabelle ist
  Sin(a · π / 180) der Laufzeit, Test gegen die Bytes des Originals). Die Linie Farbe 14 zum Panzer wird sofort wieder aus
  `DS:E5BC` hergestellt und bleibt nicht sichtbar.
- Abnahme: `WeaponProbeTests` für alle Waffen von `O4:01B7` (ohne Laser und MIRV) mit allen Abwehrarten, ohne Lenkung (132 Fälle,
  davon 2 mit Erdrutsch und Fallen), `Real48TrigProbeTests` (`arctan`), `Real48DegreeTableTests`.

### 8.4 Laser (Teil 4)

`LaserShot` nach `O4:3426`, über `WeaponShot.Fire` (Auswahl wie `O4:4977`): Kraft 0 → nichts; Kraft ≤ Laserbestand +38h, der
Bestand verliert sie; Farbe 254 = Farbe des Schützen (`O10:021C` ohne Prüfung Energie > 0), Rohr in 254, Panzerkörper;
**Aufladebild:** Verlauf DS:E5C4 + 3i = Trunc(c + i · (63 − c) / 100) (Blau gegen 0) für i = 1..100, zweimal auf- und abwärts mit
`Delay(LS div k)`, einmal aufwärts – kein Zufall. Dann E = 10 · Kraft, Richtung a bzw. 180 − a, `Random:Real` < 0,2 → 1 Schaden am
Schützen, Start 7 Pixel entlang des Rohrs, Spurfarben 100 − i. **Strahl:** je Schritt Farbe 254 = (g, g, 0); Gelände → 15 Funken
(x − Random(20) · vx, y − Random(20) · vy) aus dem Hintergrund, außer auf 254 und Panzerpixeln; Panzer mit Energie > 0 → erster
Kontakt +1 Treffer (auch gleiches Team, nicht der Schütze), E − 10, ohne Schild 15 Funken in der Panzerfarbe (neu gewürfelt, bis
das Pixel kein anderer Panzer und nicht 254 ist; je `Delay(LS)`), wieder aus dem Hintergrund, 3 Schaden, ein Schritt zurück; keine
Punkte. Wände wie L3; Spitze in 254. Am Ende Farbe 254 schwarz und alle Pixel der Farbe 254 (Strahl **und Rohr**) aus dem
Hintergrund (`U431:0594`, Adressen 64000..1, also nicht Pixel 0). Abnahme: alle 18 Laserfälle der Liste bitgleich.

### 8.5 Lenkung und MIRV (Teil 5)

- **Lenkung** (`O4:0A37–0C01`, `BallisticShot.Guide`) wie `runde.md` §5.12 G1, nach dem Repulser im Teilschritt; ergänzt: Wand-0-Regel
  (y := 300) nur ohne Lenkung, und bei Waffe 12..16 setzt der Abprall am Panzer die Lenkung +26h auf 0 (`O4:0E31`). Abnahme: alle
  9 Lenkfälle der Liste bitgleich.
- **MIRV** (`O4:1AD0–3423`, `MirvShot`, Muster und Detonation als Eingabe; der Dialog `O4:13D4–1AB9` gehört zu `zug`): Bestand − 1,
  Lenkung aus; Mutter wie `O4:01B7` (Startwerte Q3, Teilschritt Q4, Repulser Q5), aber ohne Lenkung, ohne Kraftregel G2 und ohne
  Trefferzähler; Teile enden an Gelände oder lebendem Panzer, über 30, nach 1000 Ticks, ohne Prallwände bei y < −5000 oder y ≥ 190,
  mit Prallwänden am Boden erst bei |vx| und |vy| < 0,1. Die Mutter endet zusätzlich bei vy > 0 (Scheitel); dann starten fünf Teile
  an ihrer Stelle mit ihrer Geschwindigkeit, geändert je Muster (§5.11 `runde.md`, Vorzeichen jetzt geklärt). Während Teile fliegen,
  ist SS je Tick SS div (fliegende Teile), aufgerundet. Detonation 0 explodiert jedes Teil bei seinem Ende (auch außerhalb des
  Felds), 1 alle fünf am Schluss, 2 keine; nach jeder Explosion alle Panzer neu. Abnahme: alle 23 MIRV-Fälle der Liste bitgleich –
  damit laufen alle 182 Fälle der Probe (`WeaponProbeTests`).

### 8.6 Darstellung, `bombtool`, Bestand (Teil 6)

- `WeaponShot.Fire` (Auswahl `O4:4977`) für alle Waffen; `PlayfieldShotScreen.Fire` nutzt es (MIRV-Muster und Detonation als
  Parameter, `Result` nur für `O4:01B7`) und spielt Laser-Aufladebild, Strahl, MIRV-Teile und alle Einschläge als Bilder ab.
- `bombtool render playfield --shot ANGLE,POWER[,WEAPON[,PATTERN[,DETONATION]]] [--guidance KIND,TARGET]`: jede Waffe 0..17, Crumble
  aus der Konfiguration auch für Xplosive Dirt. `[APPROX]` Ohne Konten setzt das Werkzeug den Bestand wie die Probe (Laser: Kraft,
  sonst mindestens 1).
- **Bestand:** `PlayfieldTank.Stock` (18 Waffen), `Herbs`, `Vicks`, `Guidance`, `GuidanceTarget`; `O4:01B7` zieht Waffe und
  Lenkkörper ab (Q1), der MIRV die Waffe, der Laser die Kraft. Übertrag `TakeStock(PlayerAccount)` beim Rundenbeginn und
  `ReturnStock(PlayerAccount)` beim Rundenende über die öffentliche API von `PlayerAccount` (unverändert); aufrufen müssen ihn
  `spielfolge`/`runde`, im Original sind Konto und Panzer derselbe Satz.

## 9. Rundenablauf (Spur `runde`, Stand 16.11.2026)

### 9.1 Probe `tod.py` (Teil 1)

`tools/reverse-engineering/tod.py` setzt einen Fall von `feld.py` bei **S** (`O4:479A`) fort und lässt `O4:44A7` bis zu seinem
`retf` (`O4:4EF2`) laufen: Zugbeginn Z1, Waffe (`O4:01B7`, Laser `O4:3426`, MIRV `O4:13D4` mit Dialog über die Tastaturflags wie
`feld_schuss.py`), Panzer, Crumble und Fallen (K11), Schussprotokoll `DS:4A80`, Todesschleife K12 mit den Todesexplosionen `O5`
(K15, K16), Blitz `O6:14C5` (K13), Rundenende K14. **Nicht** läuft der Zug `O7:040C` selbst (Spur `zug`, [zug.md](zug.md)): beim
Aufruf `O4:4960` schreibt die Probe das Ergebnis des Zugs aus dem Skript (Winkel +1Dh, Kraft +1Bh, Waffe +24h, Lenkung +26h/+28h,
Bestand der Waffe mindestens 1 bzw. beim Laser die Kraft) und springt nach `O4:4965`. `Crt.Delay` wird übersprungen und gezählt.

| Aufruf | Ergebnis |
|---|---|
| `python3 -I tools/reverse-engineering/tod.py befunde` | SHA-256 von `O4:4704–4A0A` (Zugfolge, Z1, Waffenwahl), `O4:4A0B–4EF2` (Schussprotokoll bis Rundenende), O5 ganz, O6 ganz, O7 `0000–040B`, O10 `0000–02A3`, `U431:0000–000B`; 7/7 ok |
| `… run --seed <hex> [Fall wie feld.py] --turns "W,K[,Waffe[,Lenkung,Ziel[,Muster,Detonation]]];…" [--set [N:]FELD=W …] [--sound] [--crumble C] [--pass P] [--death-seed HEX,…] [--lightning-seed HEX,…] [--json]` | eine Runde nach `analyse/feld/runde-…/` (`result.json`, Bildschirm, Puffer, DAC und Sätze je Messpunkt) |
| `… runden [--only I …] [--jobs N]` | feste Liste (26 Runden, `ROUNDS`) → `analyse/feld/runden-hashes.tsv` (eine Zeile je Messpunkt) und `runden-hashes.json` (Züge, Tode, Blitze, Einschläge, Schäden); mit `--jobs 2` etwa 13 min |

`--set` schreibt Felder der Sätze bei S (vor Z1 des ersten Zugs; ohne `N:` der Startspieler), `--pass` den Durchgangszähler
`DS:47EA` bei S, `--death-seed` setzt `RandSeed` vor dem `Random(7)` des j-ten Tods (`O4:4C10`), `--lightning-seed` vor dem j-ten
Blitz (`O4:4D9A`). **Messpunkte:** `turn` (`O4:4960`, Zugergebnis geschrieben, nach Z1), `post` (`O4:4A0B`, nach Waffe, Panzern,
Crumble, Fallen), `death` (`O4:4D50`, nach einem Tod samt Bild, Punkten, Rahmen, `O7:03C1` und Fallen), `bolt` (`O4:4D9F`, nach
einem Blitz), `next` (`O4:4E68`, Runde geht weiter), `end` (`O4:4EF2`, nach K14), `exhausted` (Skript ohne Zug). Je Messpunkt:
`RandSeed`, Zahl der `Random(n)`/`Random:Real` seit S, Rahmen und Feld (`field_crop` wie §5.2), Puffer `DS:E5C0`, seit S gesetzte
DAC-Einträge, Rohbytes aller Sätze und Kurzform (x, y, Energie, lebt, Punkte, Abwehr, +5Eh, Treffer +6Ch, Tode +6Eh, Züge +70h,
Rundensumme +68h, Waffe), Schussprotokoll, Durchgang, Spieler am Zug, Siegerteam `DS:47E0`, Zahl und Summe der Delays.

**Liste** (`ROUNDS`): Fall A (2 Spieler, Startspieler 2) – Tod durch Baby Missile mit der Wahl des Originals (0), jede der sieben
Todesexplosionen über `--death-seed` (1–7), dieselben mit Ton für die Wahl 0, 1, 2, 5, 6 (8–12), Selbstmord (13), Tod im eigenen
Team (14), zehn Züge bis zum Tod mit Punkten +1500 (15), Blitze bei Durchgang 40–61 ohne und mit Ton und bei Energie 3 (16–18),
Durchgang 9 bis 16 mit `Random(100)` ohne Blitz (19), kein Fallen und Crumble 0 (24), Laser, MIRV und CRI in einer Runde (25); Fall
B (4 Spieler, Startspieler 3) – zwei Tote durch eine Nuke, niemand bleibt (Sieger 0, 20), Nuke mit Selbsttod, die Runde geht
weiter (21), Teams zu zweit mit zweimal −1000 (22), vier Panzer mit Energie 1 und Splittern (23). Die Läufe sind byte-gleich
wiederholbar; 14 Runden enden mit `end`, 7 laufen bis zum Ende ihres Skripts (`exhausted`).

**Befunde** (Ergänzung zu [runde.md](runde.md) §5.9, alles aus Code und Probe):

- Ein Tod: `U431:07BA(Satz)`, Kopfzeile `Bar(109, 0, 211, 7)` mit dem Namen zentriert bei x = 160 in Farbe Satz + 19 (BGI,
  außerhalb des Felds), `Random(7)`, Bild, Punkte, `O7:0364`, `O7:03C1` (Rahmen,
  `Random:Real · 100 ≤ Crumble`, also **ein `Random:Real` je Tod**), Fallen. Bei einem Panzer, der nach dem Schuss Energie ≤ 0 hat, aber nicht mehr lebt (+23h = 0), passiert nichts.
- Zufallsaufrufe der Bilder ohne Ton (einschließlich `Random(7)`, ohne `O7:03C1`): `O5:001F` 2032, `O5:0DA0(0)` 22.897,
  `O5:1BDE` 1064, `O5:0DA0(1)` 25.537, `O5:2662` 14.759, `O5:28DE` keiner, `O5:29A0` 2 + Explosionen; mit Ton mehr. `O5:001F`
  und `O5:1BDE` ziehen außerdem 22 `Random:Real`. Delays: `O5:001F` 245, `O5:1BDE` 285, `O5:28DE` 202, `O5:29A0` 436 Aufrufe
  (Summe 1960, 2280, 1616, 264 ms bei ES/FS des Beispiels), `O5:0DA0` und `O5:2662` keine.
- Rundenende: `DS:4744` = Team des letzten lebenden Satzes, `DS:4813` = 1, solange alle lebenden dasselbe Team haben – auch wenn
  keiner lebt (dann Team 0); Runde aus bei `DS:4813` = 1, Sieger `DS:47E0`. Rundensumme +68h += 100 − Energie, bei Energie < 0 oder
  > 100 aber + 100 (Fall 13: Energie 84 → 16; Tote mit −1 → 100).
- Der Blitz wiederholt sich, solange kein Tod folgt und `Random(100)` < Durchgang ist (bei Durchgang ≥ 100 also bis zum ersten Tod).

### 9.2 Rundenschleife `Core/Round` (Teil 2)

`RoundPlay` (`Core/Round/RoundPlay.cs`) spielt `O4:44A7` ab dem Startspieler: Z1 über `TurnSession.Begin`, der Zug als Rückruf
(setzt Winkel, Kraft, Waffe, Lenkung am Panzer, gibt die MIRV-Wahl zurück), Waffe wie `O4:4977`, `AfterShot.Run`, Schussprotokoll
(`ShotLog`, Einträge wie §9.1; x über 32767 → 7FFFh; der MIRV lässt den Schusszustand stehen, der Laser setzt ihn auf das Ende des
Strahls), `WeaponBar.SetColors` (`O4:0000`), Todesschleife, Blitzprüfung, Rundenende mit Rundensumme. `RoundPoint` meldet die
Messpunkte der Probe, dazu `DeathStart` vor `Random(7)` (Name in der Kopfzeile; dort setzt der Test `--death-seed` über
`TurboPascalRandom.SetSeed`). Neu am Panzer: `Kills` (+6Eh), `RoundSum` (+68h), `ColorDivisors` (+60h..+64h, #153: der Index der
Farbe folgt der Lage, die RGB-Teiler wandern mit dem Satz; `Playfield.Build(…, divisors)`).

Abnahme (`RoundProbeTests`): zum Stand von Teil 2 alle 26 Runden bis zum ersten noch nicht portierten Bild bitgleich (Art,
`RandSeed`, Rahmen und Feld, Puffer, DAC, Schussprotokoll, Durchgang, Spieler am Zug, Sieger, Sätze); vollständig die Runden ohne
Tod und die mit Todesbild 5 (`O5:28DE`: Energie 0..100..0 über `O10:0171`, dann −1 und Panzer aus dem Puffer `U431:08D7`).

### 9.3 Todesbilder und Blitz (Teil 3)

`Core/Round/DeathExplosion` (alle sieben Wahlen von `Random(7)`) und `Core/Round/Lightning`; Satz 0 (`DS:402E`) ist ein eigener
Panzer (`RoundPlay.RecordZero`), Schütze der Explosionen von `O5:29A0` und des Blitzes. Abnahme: **alle 26 Runden bis zum Ende
bitgleich**, mit und ohne Ton (`RoundProbeTests`). Aus dem Code (Ergänzung zu `runde.md` §5.9 K15/K16):

- `O5:001F` (0) Splitter: Rampen 130..134/140..144/150..154, 104-mal `O5:0000` (Panzerfarbe := drei Bytes des Speichers ab
  `0000:Random(65535)` – `[APPROX]` hier aus dem Zufallswert, am Ende überschrieben); Fenster 11..309 × 31..189, x/y des Satzes
  relativ; 11 Splitter nacheinander im Schusszustand `DS:4796–47AC` (vx = 0,5 − R, vy = 0,1 − R, vy + 0,007, vx := Wind/5000 + vx),
  Panzerpixel `Random(10) + 5` Schaden, je Ende ein Ringbündel; 15 Doppelringe; Ausblenden mit `Random(63) · n div 16`; danach
  steht der Schusszustand auf dem letzten Splitter (relativ zum Fenster) – für das Schussprotokoll eines späteren MIRV.
  `Graph.GetPixel` `[APPROX]` als Fensterursprung + (x, y) in 16 Bit, jenseits 64000 null (Treiber 2Eh nicht zerlegt).
- `O5:1BDE` (2) wie 0, aber alle Splitter zugleich (ein `O5:0000` je Durchgang), `Random(10) + 10`, Grenze 0..298 × 0..158, ohne
  Schusszustand, Einzelringe, Ton `Random(200)`, am Ende Farben 130..154 aus dem Puffer.
- `O5:0DA0` (1 Teilchen, 3 Funken): DAC 200..215 := DAC 0..15 (`UCC8:0C20`, `[APPROX]` nie gesetzte Einträge 0..15 = Modell der
  BIOS-Palette wie die Probe), `Random(5) + 5` Wellen zu `Random(390) + 2` Teilchen in fünf Schritten und acht Formen
  (`Random(8) + 1`), einmal ins Fenster gespiegelt; `O5:09EC` Farben 1..14 Schritt für Schritt, `O5:0C6C` Farben 201..214 (202 wird
  Gelände); Panzerpixel `Random(3) + 1`; am Ende Farben 201..220 aus dem Puffer.
- `O5:2662` (4) Erdfontäne `Random(30) + 30` Ringe zu `Random(400)` Körnern Farbe 2; `O5:29A0` (6) dreimal Energie 0..100,
  Satz → Satz 0, `Random(10)` → 1–4 Explosionen der Waffen 1..k am Panzer; `O5:28DE` (5) wie §9.2.
- Blitz wie `runde.md` §5.9 K13; das Produkt (y − 189) · o ist ein 16-Bit-`mul` mit `cwd` danach, die zweite Bahn rechnet
  o · ((y − 189) / 189); Schaden 3 auf den Schild, Explosion Waffe (Blitze div 3, 0 → 11) bei (x₁, 189) mit Satz 0.

### 9.4 Schlacht und Desktop (Teil 4)

- `Core/Round/RoundGame`: Runde auf den Sätzen der Schlacht. `Prepare` baut das Spielfeld mit den Farbteilern der Spieler (#153),
  Wind und Startspieler (Zähler +72h), übergibt je Satz Bestand (`TakeStock`, #147), Punkte, Team, Waffe, Lenkung und die Zähler
  +68h..+70h an den Panzer und reicht die CRI-Liste der vorigen Runde weiter (`Playfield.TakeOverCriList`, #165); `Finish` gibt
  alles zurück (`ReturnStock`) samt „lebt“ für die Siege. `InitializeRecords`: Bestand neuer Sätze wie `O2:0216` (`[APPROX]` das
  Original setzt ihn einmal beim Programmstart, hier bei jeder Schlacht).
- `BattleSequence` ohne `IBattleRound` meldet nach dem Mischen `BattleStepKind.Round`; der Aufrufer spielt die Runde und meldet das
  Siegerteam mit `CompleteRound`. `RoundPlay.Steps()` meldet `TurnStart` nach Z1; der Zug setzt `NextTurn`.
- UI: `PlayfieldRoundScreen` (Züge über `PlayfieldTurnDriver`, aus `PlayfieldTurnScreen` gelöst; Bilder zwischen zwei Zügen wie
  `PlayfieldShotScreen`; Name des Toten in der Kopfzeile `PlayfieldTurnView.DeathStarted`). `ScoreBattleFlow` erhält den
  Rückruf `playRound` (gemeinsame Datei, minimal). Desktop: Titel → Menü → Spielerwahl → je Spiel Runde, Wertung, Teamwertung,
  Einkauf → nach dem letzten Spiel zurück ins Menü; das Endergebnis (`ScoreResults`, `O3[1]`) beim Programmende auf der Konsole.
  `[APPROX]` Computer schießen sofort mit Winkel und Kraft ihres Satzes (Runde 4), kein Ton.
- Test: `PlayfieldRoundScreenTests` (Runde zweier Computer: Bilder abgespielt, Ende, Sieger, `RandSeed` und Feld gleich Core
  allein; Schlachtfolge über den Rückruf), `RoundGameTests`.

### 9.5 Abnahme gegen Sitzung 3 (Fixlauf 1, Review #171)

- **`s3_07`** (`PlayfieldRoundSessionTests`): Schlacht über `BattleSequence` → `RoundGame.Prepare` → `RoundPlay` mit den vier Schüssen
  aus §7. Nach dem Mischen `RandSeed` `D2E3BA6E`, Wind −92, Startspieler 2; Schuss 4 tötet Satz 2, `Random(7)` mit `7C99828F` = 1
  (`O5:0DA0`). Am letzten Bild des Todesbilds ist das Feld **pixelgleich** zu `s3_07`, die DAC aller vom Spiel gesetzten Feldfarben
  gleich (Farben 2 und 13 setzt das Spiel nie: BIOS-Farben). Nach `O7:0364`/`O7:03C1` bleiben 30 Pixel, alle in y = 189,
  x 219..307: das Bild zeigt den Hintergrundpuffer, wir die neu gezeichnete Bodenlinie (2). Die Probe zeichnet sie ebenso neu
  (`tod.py run … --set 1:energy=5 1:y=150`: am Messpunkt `death` in y = 189 nur Farbe 2) – `s3_07` ist der Stand zwischen Bild und
  `O7:0364`, kein Fehler. `s3_06` liegt mitten im Bild (nicht vergleichbar).
- **`s3_08`**: Wertung nach Spiel 1 über `ScoreBattleFlow` – „Player 2“ 1 Sieg, 4490 Punkte, „Player 1“ 0 und 50. Bild gleich bis auf
  den Mauszeiger (x 165..175, y 154..169) und 8 Pixel in den Glyphen „5“ und „a“ (P3-2, #175, Spur `p3-runde-3`); DAC aller
  benutzten Farben gleich außer Farbe 0: das Original behält DAC 0 = (14, 9, 10) der Runde über den Bildschirmwechsel, unser
  `ScoreScreen` setzt Schwarz (P3-1, #174, offen: braucht einen DAC-Eingang des Score-Bildschirms, Welle 1).
- **Schaden an Nachbarn** (P2-2, #173): Runden 26–31 der Liste (Fall B, Satz 2 mit Energie 0, Satz 3 auf x = 125 daneben) – Splitter
  (0, 2), Teilchen (1) und Funken (3) treffen den lebenden Nachbarn (91 → 22/29/41, bei 2 auch Satz 1), Folgetod durch Funken bzw.
  Splitter (29, 30) mit −1000 für den eigenen Satz, Teilchen auf einem Schild (31: Schild 30 → 25). Die Mutationen `+ 5` → `+ 6`,
  `+ 10` → `+ 11`, `+ 1` → `+ 2` und `Damage.Apply` → nur `Random(3)` fallen damit rot.
- **CRI-Liste** (P3-3, #176): der Desktop reicht sie über alle Schlachten des Programmlaufs weiter; alle Zugriffe auf `DS:E820`
  liegen im CRI (`BOMB.OVR 0x19964–0x19D35`).
