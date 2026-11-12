# Analyse `re-runde` – Spielfolge, Runde, Spielfeld und Schuss

Spur `re-runde`, Runde 2 (Stand 12.11.2026, Claude Opus 5.5). Eigene Beschreibung mit Dateioffsets, kein Produktcode; Listings
nur im Referenz-Repo (`analyse/re-runde/`, R2-D5 = A). Grundlagen und Adressschreibweise: [re-grundlagen.md](re-grundlagen.md)
(`On:Ofs` = Overlay n, `Seg:Ofs` = Segment des Ladebilds, `DS:xxxx` = Datensegment `14D5`); Variablen und Spielersatz:
[../formate/spielstand.md](../formate/spielstand.md) (Satz i liegt bei `DS:402E + 74h·i`, „+0Fh“ = Feld im Satz).

**Beleg für jede Aussage:** `python3 tools/reverse-engineering/runde.py befunde` prüft die Befunde (Kürzel in Klammern) gegen
`gamedata/` als Befehlsfolgen der eigenen Disassembly (Ergebnis zum Head: **41 von 41 bestätigt**). Weitere Aufrufe: `… procs`
(Prozeduren mit Aufrufern), `… randoms` (alle 158 Aufrufe von `Random`), `… reals` (Real48-Konstanten), `… listings`
(annotierte Listings); Ausgabe nur nach `analyse/re-runde/`. Werkzeuge: Python 3, `ndisasm` (Paket `nasm`).

**Stand der Teile:** 1 (Spielfolge, Rundenbeginn, Spielfeld, Zufallsfolge) fertig; 2 (Schuss) bis auf §5.6 fertig; 3 (Zug,
Kopfzeile, Eingaben O4/O7/O9) offen; 4 (Listings im Referenz-Repo, Liste §7) mit Stand dieses Laufs.

## 1. Spielfolge `O3[0]` = `O3:7847` (`BOMB.OVR 0x102D3`)

Je Spiel der Schlacht (`DS:47E2` = 1 .. `DS:E8A6`) (S1):

1. Ist kein Spielstand geladen (`DS:4817` = 0): **Mischen** `O3:2417`, **Runde** `O4[0]` = `O4:44A7`, Palettenrotation zurück
   (`0C79:003D`); hat ein Team gewonnen (`DS:47E0` ≠ 0), erhalten alle Spieler mit Team (+66h) = `DS:47E0` je
   `1000 · Spielerzahl div Teamgröße` Punkte (+13h, LongInt) und die lebenden (+23h ≠ 0) einen Sieg (+6Ah) (S2).
2. Autosave `O3:3B54` bei `DS:4818` ≠ 0, Wertung `O3:5AEF`, bei `DS:4816` Dialog „SAVE GAME“ `O3:46DE`, bei Team Play Teamwertung
   `O3:6696` und erneut der Dialog, Einkauf `O3:0E1F` außer nach dem letzten Spiel, Bild von der Mitte nach außen schwarz wischen,
   `DS:47E2` + 1.

**Satzreihenfolge (#71, R2-D4 = A):** Die Sätze werden an zwei Stellen dauerhaft umgestellt, beide über **`O3:0000` (Tausch a, b)**:

- **Vor jeder Runde** mischt `O3:2417`: `Random(20) + 10` Tausche, je Tausch `a = Random(N) + 1` und `b = Random(N) + 1`, `b`
  wiederholt bis `b ≠ a` (S3). Damit ist die Reihenfolge auf dem Spielfeld (Spieler 1 links … N rechts, §3.6) zufällig.
- **In der Wertung** `O3:5AEF` sortiert eine Blasensortierung absteigend nach Siegen (+6Ah), danach eine zweite bei gleichen
  Siegen nach Punkten (+13h, mit Vorzeichen) (S5) – also nach (Siege, Punkte) absteigend, stabil.
- `O3:0000` tauscht den ganzen Satz (74h Byte, über Satz 0 als Puffer), setzt in beiden die Spielernummer (+00h, `O10:0161`) auf
  den neuen Index, tauscht die Zeiger der Intelligenz (`DS:3BE8 + 4i`) und setzt deren Spielernummer (Self+10h, `0129:01BF`)
  und tauscht die Steuerart (`DS:E5A6 + 2i`) (S4). **Steuerart und Intelligenz bleiben also beim Satz** – die offene Frage aus
  `spielstand.md` ist damit beantwortet; ein Spielstand gibt stets die aktuelle Reihenfolge wieder.

Umsetzung mit der Spielfolge in Runde 3 (R2-D4): Tausch als eine Operation über Satz, Steuerart, Intelligenz und Nummern.

## 2. Rundenbeginn `O4:44A7` (`BOMB.OVR 0x151AC`)

Reihenfolge (R1): `0431:0006`, Tastatur `0C48:0227`, `SetTextStyle(0, 0, 1)`, **Spielfeld `O8[0]`** (§3), **Wind**, Kopfzeile,
**Waffenleiste `O8[1]`** = `O8:1268`, **Startspieler**, dann die Zugschleife (Teil 3).

**Wind** (R2, R3): Stufe = Option `DS:E8A8`, bei 5 (RND) `Random(5)` → `DS:47E4`. Spanne S = 0, 20, 100, 300, 4000 für Stufe
0..4. Wiederhole `w = S div 2 − Random(S)` bis `|w| ≥ S div 10` (bei S = 0: `Random(0)` = 0, einmal). Danach viermal
`Random:Real` gegen 0,1 / 0,1 / 0,05 / 0,05: jeweils bei kleiner `w := w · 4`; zuletzt auf −30000..30000 begrenzt → `DS:E8A2`.

**Startspieler** (R4): `s = Random(N)` wiederholt, bis Satz s+1 im Feld +72h (Anzahl der Runden als Startspieler) keinen größeren
Wert als jeder andere Satz hat; dann +72h von Satz s+1 erhöhen, Durchgangszähler `DS:47EA` = 1. Der erste Zug gehört **Satz
s+1** (`O4:4770`: Zugindex s + 1, über N hinaus zurück auf 1 mit Durchgang + 1, übersprungen wird ein Satz mit +23h = 0); seine
Nummer steht in `DS:47EE` (R5).

## 3. Spielfeldaufbau `O8[0]` = `O8:0984` (`BOMB.OVR 0x1CA44`)

### 3.1 Wände (P1)

Wand der Runde `DS:47EC` = Option `DS:E8AA` (0 NO, 1 STICK, 2 ELASTIC, 3 ACCEL, 4 WARP, 5 RND), bei 5 `Random(5)`.
`DS:4814` = Wand ∈ {1, 2, 3}, `DS:4815` = (Wand = 4). Beschriftung links senkrecht (SmallFont, Richtung senkrecht, Größe 1,
`SetUserCharSize(1,1,1,1)`, Ausrichtung Mitte/Mitte, Punkt (0, 100)): „STICKEY“, „ELASTIC“, „ACCELERATE“, „WARP“ (bei 0 nichts).
Dazu ein Faktor in `DS:47D2` (Real48): Wand 1 **0,707106** (`80 CE 18 E6 04 35`), Wand 2 **1** (`81 00 00 00 00 00`), Wand 3
**1,41421** (`81 D7 51 D5 04 35`); Wand 0 und 4 lassen ihn unverändert (P7). Verwendung im Schuss: Teil 2.

### 3.2 Ablauf

Palettenrotation aus (`Move` 16 Byte `DS:A0DE` → `DS:3706`, `0C79:003D`); Wände; `SetRGB(19, 63, 63, 63)`, bei „Schuss
unsichtbar“ `SetRGB(19, 0, 0, 0)`; `0E26:0F80`; **Hintergrund** (§3.3); `SetRGB(0, 14, 9, 10)`; Rahmen `Rectangle` Farbe 13
(8, 28)–(312, 192) und (10, 30)–(310, 190); Wandbeschriftung; **Gelände** (§3.5); **Panzer** (§3.6); `DS:4813` = 0; Rotation mit
den Werten des Hintergrunds an (`0C79:003D` mit `DS:A0DE`) (P16).

### 3.3 Hintergrund (P2–P6)

Nummer n: Option `DS:E8AC` ≤ 8 direkt; 9 (RND) → `Random(9)`; 10 (USER) → `Random(9)` wiederholt, bis Bit n in `DS:E8AE` gesetzt
ist (P2). Gezeichnet von **`O8:0000(n)`** (`BOMB.OVR 0x1C0C0`) – damit ist `re-grundlagen` §8 Nr. 4 beantwortet. Zuerst
`FillChar(DS:E5C0^, 64000, 0)` (Hintergrundpuffer leeren), dann je n (Namen aus `O2:0E35`):

| n | Name | Zufall (Reihenfolge) | Zeichnen (Fenster 11..309 × 31..189) | Beleg |
|---|---|---|---|---|
| 0 | None | – | nichts | P6 |
| 1 | Stars | `Random(500)` + 250 Sterne, je `Random(299)`+11 (x), `Random(159)`+31 (y), `Random(20)`+30 (Farbe) | DAC i+30 = (5i, 5i, 5i), i+40 = (5i, 5i, 0) für i = 0..9 (`Trunc(i · 5.0)`); `PutPixel` | P3 |
| 2 | Bars | Palette `0431:000C` | waagrechte Linien x 11..310, y 31..190, Farbe (y mod 30)+30 | P4 |
| 3 | Circles | Palette | Kreise um die Mitte (`0CC8:0351`), Farbe (i mod 30)+30 | P4 |
| 4 | Spread | Palette | `[APPROX]` Muster aus Zeichenkettenoperationen, nicht ausgewertet | P4 |
| 5 | Sky | – | DAC-Verlauf (`SetRGB`), `Move` | P4 |
| 6 | Static | Palette, dann **je Pixel** des Fensters `Random(30)`+30 (x außen, y innen; 299 × 159 = 47.541 Aufrufe) | `0CC8:0BB6` | P5 |
| 7 | Rectangle | Palette, `Random(3)` → 80 / 150 / 159 Rahmen | Rahmen i: (i+10, i+30)–(310−i, 190−i), Farbe (i mod 30)+30 | P5 |
| 8 | Asterisk | Palette | Strahlen von (160, 110) zu den Rändern, Farbe (i mod 30)+30 | P4 |

**Palette `0431:000C`** (`BOMB.EXE 0x059BC`): 3 × 3 `Random(64)` (Index i außen, j innen) als Stützwerte des Verlaufs der
Farben 30..59 (P4). **Ende für alle n, auch 0** (P6): `VideoSeg` = Puffer `DS:E5C0`, Spalten x = 10..310, y = 30..190 vom
Bildschirm in den Puffer kopieren (`0CC8:0AC0`), im Puffer die beiden Rahmen Farbe 13, `VideoSeg` = A000h, dann
**`Random:Real` < 0,5 → `0C79:0428`** (`[APPROX]` Laufrichtung der Rotation). Der Puffer ist das Bild, das Krater später
freilegen (`0CC8:0BD8` kopiert ein Pixel aus dem Puffer auf den Bildschirm).

### 3.4 Hilfsroutinen (eigene VGA-Einheit `0CC8`)

| Routine | Wirkung | Beleg |
|---|---|---|
| `0CC8:0BB6(x, y, c)` | Pixel auf A000h setzen (Adresse y · 320 + x) | Code |
| `0CC8:0BFF(x, y)` | Pixel von A000h lesen | Code |
| `0CC8:0BD8(x, y, seg)` | Pixel aus Segment `seg` nach A000h kopieren | Code |
| `0CC8:09E4(x1, y1, x2, y2, c)` | `[APPROX]` senkrechte bzw. waagrechte Linie direkt | Aufrufmuster |
| `0CC8:0031(x1, y1, x2, y2, c)` | `[APPROX]` Rahmen direkt | Aufrufmuster |

`[APPROX]` Namen der Graph-Routinen nach Argumentgröße und Aufrufmuster: `0E26:1C86` Line, `0E26:10D2` Rectangle,
`0E26:1131` SetFillStyle, `0E26:1CCE` Bar, `0E26:1FE4` PutPixel, `0E26:16E3` SetUserCharSize, `0E26:0F93` SetViewPort.

### 3.5 Gelände (P8–P10)

Farbe 2, gezeichnet als senkrechte Spalten von y = 190 − h bis 189 (h = Höhe über dem Boden). N = Spielerzahl.

```text
x := 11; s := 0                         { Steigung, −4..4 }
d := 1 − 2·Random(2)                    { Richtung von s: +1 oder −1 }
rand := 60 − 7·(N − 2)
h := Random(150)
while x < 310:
  if x < rand + 11 or x > 309 − rand:   { flache Ränder, ohne Zufall }
    Line(x, 190 − h, x, 189)
  else:
    case |s| of                          { Spalte = Spalte(x) zeichnen; „x+“ = x + 1; „h±“ = h + sign(s) }
      0: Spalte, x+, Spalte, x+, Spalte
      1: Spalte, x+, Spalte, x+, h±, Spalte
      2: Spalte, x+, h±, Spalte, x+, h±, Spalte
      3: Spalte, x+, h±, Spalte, h±
      4: Spalte, h±, h±
    if Random:Real < 0.2: s := s + d
    if Random:Real < 0.1 or |s| > 4: d := −d
    s := Clamp(s, −4, 4)
    if h <= 20: d := 1;  if h >= 140: d := −1
    if h <= 3: s := 1;   if h >= 155: s := −1
  x := x + 1
```

Je Schritt im Innenteil genau zwei `Random:Real`. Die Vergleiche laufen in Real48: `Random:Real` liefert `RandSeed / 2^32`
(vorzeichenlos, exakt), die Schranken sind 0,2 = `7E CD CC CC CC 4C` und 0,1 = `7D CD CC CC CC 4C` (beide etwas größer als der
Dezimalwert) (P10).

### 3.6 Panzer (P11–P13)

- **x** (Satz +0Fh): Spieler 1 `Random(28)` + 16, Spieler N 305 − `Random(28)`, Spieler i dazwischen
  x₁ + (i − 1) · ((x_N − x₁) div (N − 1)) (P11).
- Je Spieler i = 1..N (P12): Name in der Kopfzeile (Bar (109, 0)–(211, 7) Farbe 0, Farbe i + 19, zentriert bei (160, 0)), Rohr
  +1Dh = 75 für i ∈ {1, N}, sonst 90; +1Bh = 200; +1Fh = −1 für i = N, sonst 1; +23h = 1 (im Spiel); y (+11h) = 32; +5Eh = 0;
  `O10:0171(Satz, 100)`, `O10:014A(Satz)`.
- **Fallen** (P13): Prüfpixel sind (x − 4 .. x + 4, y + 1). Ist eines davon Farbe 2 oder 13, fällt der Panzer nicht. Sonst: alten
  Panzer mit dem Hintergrundpuffer löschen (`0431:0685`), y + 1, neu zeichnen (`0431:05ED`, Farbe i + 19), `Delay(FS)` (nicht,
  wenn alle Spieler Computer sind oder `0431:09C9` eine Taste meldet), weiter **bis alle neun** Prüfpixel Farbe 2 oder 13 sind.
  Danach `0431:07BA(i)`, `O7[1]`. Kein Zufall.

## 4. Zufallsfolge ab `Randomize` (R2-D3)

`Randomize` steht in `0000:013B` **nach** `O2[0]` (Prozessortest) und `O2[3]` (`BOMB.CFG`), vor Maustest, Titel und Hauptmenü
(P14). Danach hängt die Zahl der Aufrufe von den Eingaben ab (P15):

- jedes Zeichnen des Hauptmenüs (`O2:3B96(0)`): `Random(2)` (100 oder 160 Rahmen);
- je Computer mit Namen „Random“: `Random(7)` + 1 (Art); je angelegter Intelligenz (`O2:0078` → Konstruktor → `0129:0000`):
  `Random(4)` + 1, wiederholt bis in der Menge der Art (`spielstand.md` T5c);
- Teamnamen `O2:35CF`: `Random(11)` (nur Team Play; nicht weiter ausgewertet).

**Ab dem ersten Spiel** liegt die Folge fest (nur Optionen und N gehen ein):

1. Mischen: `Random(20)`, je Tausch zweimal `Random(N)` (+ Wiederholungen bei a = b) (§1).
2. Wand `Random(5)` nur bei RND; Hintergrund `Random(9)` bei RND, bei USER bis passend (§3.3).
3. Aufrufe des Hintergrunds (Tabelle §3.3), dann `Random:Real` (Rotation).
4. Gelände: `Random(2)`, `Random(150)`, je Innenschritt zwei `Random:Real` (§3.5).
5. Panzer: `Random(28)`, `Random(28)` (§3.6).
6. Wind: `Random(5)` bei RND, `Random(S)` (+ Wiederholungen), vier `Random:Real` (§2).
7. Startspieler: `Random(N)` (+ Wiederholungen) (§2).
8. Zug: für Menschen kein Zufall bis zum Schuss; Computer `0129` (Teil 3).
9. Schuss Lead Ball: `Random:Real` (Rückstoß, §5.1); im Flug kein Zufall; bei Volltreffer ggf. `Random:Real` im Schaden (§5.5);
   nach dem Schuss `Random:Real` für den Erdrutsch (§5.6).

Jeder Aufruf von `Random(n)` und `Random:Real` macht genau einen Schritt `RandSeed := RandSeed · 08088405h + 1`
(`re-grundlagen` D). **Vorschlag für das Suchwerkzeug (`gelaende`):** nicht die Menüfolge nachbilden, sondern den Stand von
`RandSeed` **vor dem Mischen** suchen: bei fester Uhrzeit (R2-D3 = C) Startwert aus der Uhrzeit, dann k = 0..K Schritte
(Menü, Intelligenzen), je Kandidat Schritte 1–5 rechnen und das Gelände mit dem Bild vergleichen; ohne feste Uhrzeit alle 2³²
Stände sind zu viele – dann zuerst Panzer-x und Geländehöhe am Rand als Filter.

## 5. Schuss der ballistischen Waffen `O4:01B7` (`BOMB.OVR 0x10EBC`)

Teil 2 bis auf Krater, Explosionsbild, Erdrutsch und Fallen (§5.6) fertig. Waffe = Satz +24h (Index in `ItemCatalog.WeaponTable`),
Lenkung +26h (0 keine, 1 Herb, 2 Vick, 3 beide), Ziel der Lenkung +28h.

### 5.1 Auswahl und Abschuss (Q1, Q2)

Nach der Eingabe des Zugs (`O7[3]` = `O7:040C`, Teil 3) wählt `O4:44A7` nach der Waffe: {0..4, 6, 8..17} → **`O4:01B7`**,
5 (Laser) → `O4:3426`, 7 (MIRV) → `O4:13D4` (Q1). `O4:01B7`: Bestand der Waffe (+2Eh + 2·Waffe) − 1, bei Lenkung 1 oder 3 Herb
(+2Ah) − 1, bei 2 oder 3 Vick (+2Ch) − 1; dann **`Random:Real` < 0,2 → `O10:0000(1, 0, Schütze)`**: jeder Schuss kostet mit
20 % einen Punkt (Q2, Schaden §5.5).

### 5.2 Startwerte (Q3, Q14)

a = Rohrwinkel +1Dh (Grad), r = Richtung +1Fh (±1), P = Kraft +1Bh, (x_P, y_P) = Panzer (+0Fh, +11h); alle Rechnungen Real48:

```text
x  = x_P + cos(a°) · 10 · r          vx = (P / 50) · cos(a°) · r
y  = y_P − sin(a°) · 10              vy = (P / 50) · (−sin(a°))
```

Grad → Bogen über `0DBD:00D9` (x / 180 · π, π = `82 21 A2 DA 0F 49`); `cos` = `12CE:12EE` (Sin(π/2 − x)), `sin` = `12CE:1301`
(TP-6-Laufzeit). Für ganze Grad gibt es zusätzlich eine Tabelle `0B25:0000` (Sin) / `0B25:005A` (Cos = Sin(90 − a)) mit 91
Real48-Werten bei `0B25:00F9` (benutzt vom Repulser) (Q14). Damit ist `re-grundlagen` §8 Nr. 6 beantwortet: `0B25` wird zur
Laufzeit gebraucht.

### 5.3 Flug (Q4, Q5)

SS = Option `SS` (`DS:E8CA`, Teilschritte je Tick); Zähler t zählt Teilschritte, nach SS Teilschritten ein Tick. Je Teilschritt
in dieser Reihenfolge (Q4):

```text
x  := x  + vx / SS
y  := y  + vy / SS
vy := vy + 0.1 / SS                   { Schwerkraft }
vx := vx + (Wind / 5000) / SS         { Wind = DS:E8A2 }
ix := Trunc(x); iy := Trunc(y)
```

Danach: **Repulser** (Q5) – hat ein lebender Panzer Repulser (+5Ah > 0), wirkt jeder solche Panzer im Abstand |dx|, |dy| ≤ 40
(der Schütze erst, wenn der Schuss sein ±40-Feld einmal verlassen hat): d² = dx² + dy² ≤ 1600 → f = −600 / (d² · SS), Winkel
w = Trunc(ArcTan(dy/dx) in Grad) (+180 bei dx < 0; dx = 0: 90 bzw. 270), vx += Cos(w)·f, vy −= Sin(w)·f (Tabellenwerte); als
Effekt eine Linie Panzer–Schuss Farbe 14, die gleich wieder aus dem Puffer `DS:E5BC` gelöscht wird. Lenkung (+26h > 0) greift
hier ebenfalls ein (`O4:0A37`, nicht ausgewertet `[APPROX]`). Ton: `Sound(600 − 2·iy)` bei Fluggeräusch und Ton an. Zeichnen:
alten Punkt aus dem Hintergrundpuffer herstellen (`0431:040F`), bei Spur einen Punkt Farbe 100 − Spieler (`0431:056D`; die Farben
90..99 setzt `O4:0541` auf 32 div Farbteiler +60h, +62h, +64h), neuen Punkt `0431:03C3`; Marken auf dem Rahmen (x bei y = 29 und 191, y bei
x = 9 und 311, Farbe 13), auch wenn der Schuss außerhalb des Bilds fliegt (Q15).

### 5.4 Wände, Treffer, Ende (Q6–Q9)

| Wand (`DS:47EC`) | Regel | Beleg |
|---|---|---|
| 1–3 (Sticky, Elastic, Accel) | ix ≤ 10 und vx < 0 oder ix ≥ 310 und vx > 0: vx := −vx · F, vy := vy · F; iy ≤ 30 und vy < 0: vy := −vy · F, vx := vx · F; iy ≥ 188 und vy > 0: ebenso, und bei \|vy\| < 0,1 Ende mit Einschlag (F aus §3.1) | Q6 |
| 4 (Warp) | ix ≤ 10: x + 300; ix ≥ 310: x − 300 | Q7 |
| 0 (keine) | ix > 310, vx > 1, Wind > 0, ohne Lenkung → y := 300; ix < 10, **vx < 1**, Wind < 0, ohne Lenkung → y := 300 (`[ORIGINAL]` die linke Bedingung ist schwächer als die rechte) | Q7 |

Treffertest nur für 11 < ix < 309 und 33 < iy < 187 (Q9): Pixel (ix + dx, iy + dy) mit |dx| + |dy| ≤ 1 vom Bildschirm; Farbe 2 →
Gelände (`DS:47DE` = 2; Ende, außer eine Sonderregel mit Kraft und Tickzähler, `O4:0D8A`, `[APPROX]` nicht ausgewertet);
Farbe 20..29 → Panzer (`DS:47DE` = Farbe, Ende), bei Waffe 12..16 kein Ende, sondern vy := −vy · 1,2 und Lenkung aus.

Ende (Q8): mehr als 1000 Ticks; bei Wand 0 oder 4: iy < −5000 (**ohne** Einschlag) oder iy ≥ 190; |vx| > 30 oder |vy| > 30.
Danach Rahmenmarken löschen, Treffer auf einen Panzer eines anderen Teams zählt beim Schützen +6Ch + 1, Einschlag `O6[0]` =
`O6:00E0(DS:47DE, y, x, Schütze)`.

### 5.5 Einschlag und Schaden (Q10–Q12)

- **Lead Ball (Waffe 0)** hat **keinen Krater**: nur bei Panzerfarbe `O10:0000(5, 0, getroffener Satz)`, und ist das Team ein
  anderes, erhält der Schütze 10 Punkte je verlorener Energie (`O10:018B`, +13h) (Q10).
- **Schaden** `O10:0000(d, k, Satz)` (Q11): Abwehr k (+52h + 4k, LongInt; 0 Schild, 1 Inertia, 2 Repulser) −= d; ist sie danach
  negativ, geht der Rest von der Energie +21h ab (nicht unter 0); bei k ≠ 1 und Repulser > 0: `Random:Real` < Rest / 40 →
  Repulser − 1; Abwehr k := 0. Rückgabe: verlorene Energie.
- **Explosion** für Waffen {1, 2, 3, 4, 7, 11}: Radius `DS:47D8` = 10 (1), 15 (2, 7), 30 (3), 60 (4), 2 (11); Palette ab Farbe
  130 als Verlauf in 63 div Radius Stufen, Mittelpunkt (Trunc(x − 11), Trunc(y − 31)) im Fenster 11..309 × 31..189, Ringe mit
  `0CC8:0351` und Farbe 130 + i, Ton `Random(500)` + 500 je Ring (Q12). Die übrigen Waffen (Dirt, CRI, Sonic) ab `O6:084E`.

### 5.6 Offen in Teil 2

Krater und Schaden durch Explosion (`O6:0522`–`084E`: Abstand `0DBD:008F`, Vergleich mit Radius · 1,5 und Radius · 1,8 −
Abstand), Dirt-Waffen, **Erdrutsch** `0431:0496` (aufgerufen von `O7:03C1`, wenn `Random:Real` · 100 ≤ Crumble %, Q13),
**Fallen** der Panzer `O7:0020` (`DS:E8B8`, Verzögerung `TS`/`FS`), Explosionen beim Tod `O5[0..5]` (`Random(7)`), Laser und MIRV.

## 6. Zug, Kopfzeile, Eingaben (Teil 3, offen)

## 7. Nicht entscheidbar ohne DOSBox (Stand Teil 1)

1. Verlauf der Palette 30..59 aus den 9 Stützwerten von `0431:000C` und die genaue Zeichnung von Circles, Spread, Sky, Asterisk:
   je Hintergrund ein Bild **vor dem ersten Schuss** mit notierter Uhrzeit (Sitzung 3); Static und Stars liefern zusätzlich den
   Zufallsstand.
2. Wirkung von `0C79:0428` (Richtung der Rotation): zwei Bilder desselben Hintergrunds im Abstand weniger Takte.
3. Gelände bitgleich: ein Rundenbeginn je Spielerzahl 2 und 10 (Randbreite 60 bzw. 4) mit fester Uhrzeit.
4. Flugbahn Lead Ball: je Wandart ein Schuss mit **Spur an**, notiert Winkel, Kraft, Wind (Kopfzeile) und `SS`; Bild nach dem
   Schuss zeigt die Spur pixelgenau (§5.3). Dazu einmal Wand 0 mit Schuss nach links bei Wind < 0 (Randregel §5.4).
5. Rückstoß (§5.1): Energie des Schützen vor und nach dem Schuss in der Panzerinfo (`O9`), mehrere Schüsse.
