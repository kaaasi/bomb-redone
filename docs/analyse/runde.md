# Analyse `re-runde` – Spielfolge, Runde, Spielfeld und Schuss

Spur `re-runde`, Runde 2 (Stand 12.11.2026, Claude Opus 5.5). Eigene Beschreibung mit Dateioffsets, kein Produktcode; Listings
nur im Referenz-Repo (`analyse/re-runde/`, R2-D5 = A). Grundlagen und Adressschreibweise: [re-grundlagen.md](re-grundlagen.md)
(`On:Ofs` = Overlay n, `Seg:Ofs` = Segment des Ladebilds, `DS:xxxx` = Datensegment `14D5`); Variablen und Spielersatz:
[../formate/spielstand.md](../formate/spielstand.md) (Satz i liegt bei `DS:402E + 74h·i`, „+0Fh“ = Feld im Satz).

**Beleg für jede Aussage:** `python3 tools/reverse-engineering/runde.py befunde` prüft die Befunde (Kürzel in Klammern) gegen
`gamedata/` als Befehlsfolgen der eigenen Disassembly (Ergebnis zum Head: **69 von 69 bestätigt**). Weitere Aufrufe: `… procs`
(Prozeduren mit Aufrufern), `… randoms` (alle 158 Aufrufe von `Random`), `… reals` (Real48-Konstanten), `… listings`
(annotierte Listings); Ausgabe nur nach `analyse/re-runde/`. Werkzeuge: Python 3, `ndisasm` (Paket `nasm`).

**Stand der Teile:** 1 (Spielfolge, Rundenbeginn, Spielfeld, Zufallsfolge) fertig; 2 (Schuss) fertig (Rest nur Bilddetails, §5.13); 3 (Zug,
Kopfzeile, Eingaben) fertig bis auf O9, `O8:1268`, Computer (§6.4); 4 (Listings im Referenz-Repo, Liste §7) mit Stand dieses Laufs.

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
8. Zug: für Menschen kein Zufall bis zum Schuss (§6); Computer `0129` (§6.4).
9. Schuss Lead Ball: `Random:Real` (Rückstoß, §5.1); im Flug kein Zufall; bei Volltreffer ggf. `Random:Real` im Schaden (§5.5);
   Explosionen: `Random` nur bei Ton (§5.6); nach dem Schuss `Random:Real` für den Erdrutsch bei Crumble > 0, je Fallzeile
   `Random(3)`, je Tod `Random(7)` und `Random:Real`, ab Durchgang 11 `Random(100)` (§5.8, §5.9).

Jeder Aufruf von `Random(n)` und `Random:Real` macht genau einen Schritt `RandSeed := RandSeed · 08088405h + 1`
(`re-grundlagen` D). **Vorschlag für das Suchwerkzeug (`gelaende`):** nicht die Menüfolge nachbilden, sondern den Stand von
`RandSeed` **vor dem Mischen** suchen: bei fester Uhrzeit (R2-D3 = C) Startwert aus der Uhrzeit, dann k = 0..K Schritte
(Menü, Intelligenzen), je Kandidat Schritte 1–5 rechnen und das Gelände mit dem Bild vergleichen; ohne feste Uhrzeit alle 2³²
Stände sind zu viele – dann zuerst Panzer-x und Geländehöhe am Rand als Filter.

## 5. Schuss der ballistischen Waffen `O4:01B7` (`BOMB.OVR 0x10EBC`)

Teil 2 fertig (offene Bilddetails §5.13). Waffe = Satz +24h (Index in `ItemCatalog.WeaponTable`),
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
hier ebenfalls ein (`O4:0A37`, §5.12). Ton: `Sound(600 − 2·iy)` bei Fluggeräusch und Ton an. Zeichnen:
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
Gelände (`DS:47DE` = 2; Ende, außer bei hoher Kraft, §5.12);
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
  `0CC8:0351` und Farbe 130 + i, Ton `Random(500)` + 500 je Ring **nur bei Ton** (Q12, K1). Die übrigen Waffen §5.7.

### 5.6 Explosion, Krater und Schaden (K1–K4)

`O6:00E0(Farbe, y, x, Schütze)` (Real-Koordinaten): Lead Ball §5.5; Sprengwaffen {1, 2, 3, 4, 7, 11} mit Radius R (Q12):

1. **Bild** (K1): je Ring i = 0..R bei Ton `Sound(Random(500) + 500)`, drei Ellipsen `0CC8:0351` um (x+1, y), (x−1, y), (x, y)
   (Fensterkoordinaten, x − 11, y − 31) mit Radius i und Farbe 130 + i. Danach drei Palettenläufe über die Farben 130..130+R
   (nur Rotanteil, Stufe 63 div R), je Schritt `SetPal` und `Delay(ES)`, bei Ton `Sound(Random(500) + 250)`, `Random(500) + 3`
   bzw. `Random(250) + 3`. **Ohne Ton keine `Random`-Aufrufe** im Bild.
2. **Krater** (K2): Ring i = 0..R, je drei Ellipsen `0CC8:0687` an denselben Mittelpunkten, die die Pixel aus dem Hintergrundpuffer
   `DS:E5C0` kopieren – das Gelände im Kreis wird zum Hintergrund (auch Panzerpixel; neu gezeichnet wird nach dem Schuss).
3. **Volltreffer** (K3): Farbe 20..29 → `O10:0000(200, 0, Satz Farbe − 19)`, Punkte für den Schützen wie §5.5.
4. **Flächenschaden** (K4) je lebendem Satz (+23h ≠ 0, auch Schütze und Volltreffer): d = `Sqrt(dx² + dy²)` (`0DBD:008F`,
   `12CE:11C2` = Sqr, `12CE:128F` = Sqrt `[APPROX]` nach Aufruf) zwischen Panzer (+0Fh, +11h) und Einschlag, S = 0;
   d ≤ 1,5·R: S += Trunc(1,8·R − d); d < R: S += 10 + 2·Trunc(1,8·R − d); d < 0,7·R: S += 150. Dann `O10:0000(S, 0, Satz)`
   (Schild zuerst, Q11) und bei anderem Team 10 Punkte je verlorener Energie an den Schützen. `Random:Real` in `O10:0000` nur, wenn
   die Abwehr negativ wird (Q11).

### 5.7 Übrige Waffen (K5–K8)

| Waffe | Wirkung | Zufall | Beleg |
|---|---|---|---|
| CRI {6, 8, 9, 10} (Satz {5..10}) | Panzer getroffen: `Random(6 · Waffe) + 1` Schaden, Sirene 1000→30000→1000 Hz. Gelände: Astzahl R = 4, 4, 8, 8, 20, 20 für Waffe 5..10; Start senkrecht unter dem Einschlag, bis ein Nachbar (oben, unten, links, rechts) Farbe 2 ist; je Durchgang je Ast die Nachbarn mit Farbe 2 sammeln (Richtung 1 oben, 3 rechts, 5 unten, 7 links; Diagonalen 2, 4, 6, 8 nur für {6, 8, 10} – „/D“), eine davon wählen, Ast dorthin, Pixel aus dem Hintergrund (Gelände frisst sich weg); Ast ohne Nachbarn erlischt und übernimmt die Lage des nächsten lebenden Asts; Ende, wenn alle erloschen | je Schritt und Ast `Random(Anzahl) + 1`, bei Ton `Random(500)` | K5 |
| Sonic Blaster 12 | Schusspunkt, 100 Takte Farbe 19 = (Random(64), 0, 0), 100 Takte zusätzlich Farbe 2 = 3 × (Random(32) + 32), je `Delay(ES)`; danach Farbe 19 = (63, 63, 63), **Farbe 2 = (0, 42, 0)**, Erdrutsch `0431:0496` (§5.8) – sonst kein Schaden | je Takt 1 bzw. 4 `Random`, bei Ton +1 | K6 |
| Dirt 13..16 | Radius 10 / 15 / 30 / 60, Farbe 2, je i = 0..R drei Kreise `0E26:11EA` (`[APPROX]` Circle) um (x+1, y), (x−1, y), (x, y) – Erdkugel, kein Schaden | keiner | K7 |
| Xplosive Dirt 17 | m = `Random(100) + 300` Körner je Ring, k = `Random(8) + 3` Ringe; Ring i, Korn j: r = `Random(20·i)`, Korn bei (x + Trunc(Random(r + 1) − r / 2), y − r + 2) im Fenster; alle Körner Farbe 2 (`PutPixel`), bei Ton `Sound(Random(200) + 100·i)`; danach `Random:Real · 100 < 2 · Crumble` → Erdrutsch | wie links | K8 |

### 5.8 Erdrutsch und Fallen (K9, K10)

**Erdrutsch** `0431:0496(Puffer DS:E5C0, Liste DS:DFA2)` (K9), kein Zufall, keine Verzögerung: Bildschirm von Adresse 64000
rückwärts nach Farbe 2 absuchen; ist das Pixel darunter „Luft“ (0 oder 30..99), wird es Farbe 2, das alte Pixel kommt aus dem
Hintergrundpuffer und die neue Adresse in die Liste. Danach Durchgänge über die Liste: jedes Pixel rückt eine Zeile tiefer, solange
darunter Luft ist, sonst fällt es aus der Liste; Ende, wenn alle stehen. Panzer (20..29), Rahmen (13) und andere Farben halten das
Gelände. Aufruf nach dem Schuss `O7:03C1`: Rahmen neu (`O7:0364`), dann `Random:Real · 100 ≤ Crumble` (Q13).

`O7:0364` zeichnet die Rahmen Farbe 13 und **bei Wand ∉ {1, 2, 3} eine Bodenlinie Farbe 2** (11, 189)–(309, 189) (K11).

**Fallen** `O7:0020` (Option „Fall down“ `DS:E8B8`) (K10), je lebendem Spieler: steht, wenn eines der Pixel (x − 3 .. x + 3, y + 1)
oder beide Pixel (x ± 4, y + 1) Farbe 2 oder 13 sind. Sonst je Zeile: Rohr mit dem Hintergrund löschen (`0CC8:0AC0` von (x, y) bis
(x + Trunc(cos a · 8) · r, y − Trunc(sin a · 8))), Panzer zeichnen (`0431:05ED`, Farbe i + 19), y + 1; ist (x + 4, y + 1) fest,
x − 1; ist (x − 4, y + 1) fest, x + 1 (Abgleiten); **`O10:0000(Random(3) + 1, 1, Satz)`** (Schaden zuerst an Inertia),
`Delay(FS)`, alten Panzer löschen (`0431:0685`); weiter, bis alle neun Pixel (x − 4 .. x + 4, y + 1) fest sind; dann `0431:07BA`.

### 5.9 Nach dem Schuss: Tod, Blitz, Rundenende (K11–K16)

Reihenfolge in `O4:44A7` nach der Waffe (K11): alle Panzer neu (`0431:07BA`), **Crumble > 0 → `O7:03C1`**, **Fallen an →
`O7:0020`**, Schussprotokoll des Schützen (`DS:4A80 + 7DAh · Satz + 10 · (+5Eh)`: Rohr, Kraft, x, Einschlag x (≤ 32767) und y;
für die Computer, Teil 3), `O4:0000`. Dann die **Todesschleife** (K12): je Satz mit Energie ≤ 0 und +23h ≠ 0: +23h = 0, Name in
der Kopfzeile, **`Random(7)`** wählt die Todesexplosion `O5[0]`, `O5[1]`, `O5[2]`, `O5[1]`, `O5[3]`, `O5[4]`, `O5[5]` (0..6); der
Spieler am Zug (`DS:47EE`) erhält bei anderem Team **+1500 Punkte und +6Eh + 1**, bei eigenem Team (auch Selbstmord) **−1000**;
dann `O7:0364`, **`O7:03C1` ohne Prüfung von Crumble** (ein `Random:Real` je Tod) und Fallen. Wiederholen, solange jemand starb.

Gab es im letzten Durchgang keinen Tod und ist der Durchgang `DS:47EA` > 10: **`Random(100)`**, bei Durchgang > Zufallswert
**Blitz** `O6:14C5(0)` und zurück zur Todesschleife (K13). Blitz: Farben 200 = (63, 63, 63), 201 = (0, 0, 50); oben x₀ =
`Random(300) + 10`, unten x₁ = `Random(300) + 10`; Bahn y = 31..189 mit o += `Random(7)` − 3 und x(y) = x₀ + Trunc((y − 189) · o / 189
+ y · (x₁ − x₀) / 189); `Random(8) + 1` Blitze, je Blitz die Bahn zeichnen (Farbe 200, daneben 201; Panzerpixel auf der Bahn: 3
Schaden), dabei die nächste Bahn würfeln, alte Bahn löschen (bei Ton `Random(600) + 600`). Zum Schluss Explosion bei (x₁, 189) mit
Schütze Satz 0 und Waffe (Anzahl div 3), 0 → 11 (Hand Grenade), 1 → Incinerator, 2 → Mark II; dann Panzer neu, Erdrutsch bei
Crumble > 0, Fallen.

**Rundenende** (K14): leben nur noch Spieler eines Teams (+66h), ist `DS:47E0` = dieses Team (keiner am Leben: 0) und die Runde zu
Ende: `0431:0000`, je Satz +68h += 100 − Energie (Energie außerhalb 0..100: 100), Tastatur zurück (`0C48:0269`). Sonst nächster Zug
ab `O4:4770` (§2).

**Todesexplosionen O5** (K15, K16, Bild nur `[APPROX]` nicht ausgewertet): `O5:001F` und `O5:1BDE` Splitter mit Wind (Wind / 5000),
Schaden `Random(10) + 5` bzw. `Random(10) + 10` je getroffenem Panzer (`0E26:1FCC`, `[APPROX]` GetPixel); `O5:0DA0` Teilchen über
`O5:09EC`/`O5:0C6C`, `Random(3) + 1` je getroffenem Panzerpixel; `O5:2662`, `O5:28DE` ohne Schaden; `O5:29A0`: Panzer flackert
(`Random(2)` = 1 → ohne Verzögerung), Satz wird nach Satz 0 kopiert, `Random(10)` → k = 1 (0–1), 2 (2–4), 3 (5–7), 4 (8–9), dann
Explosionen der Waffen 1..k nacheinander am Panzer (Schütze Satz 0, also ohne Punkte für einen Spieler). Die Zahl der
`Random`-Aufrufe der Bilder hängt von Ton, Treffern und Splitterbahnen ab – für das Suchwerkzeug zählt nur die Folge bis zum
ersten Schuss (§4).

### 5.10 Laser `O4:3426` (L1–L3)

Vorab ein Aufladebild mit Verzögerung `LS` (`DS:E8CC`) und den Farben 130.. (`[APPROX]` nicht im Einzelnen ausgewertet). Dann (L1):
Energie E = 10 · Kraft (+1Bh); Richtung b = a bei r > 0, sonst 180 − a; **`Random:Real` < 0,2 → 1 Schaden am Schützen** (wie §5.1);
Start (x_P + cos a · 7 · r, y_P − sin a · 7), Schritt (cos b, −sin b) je Takt, keine Schwerkraft, kein Wind.

Je Schritt (L2): Position + Schritt, E − 10, Farbe 254 = Grau Trunc(E · 53 / (10 · Kraft) + 10). Pixel an der Spitze:

- **Gelände (2):** 15 Funken bei (x − Random(20) · cos b, y − Random(20) · (−sin b)) – im Fenster und nicht Farbe 254 oder Panzer:
  Pixel aus dem Hintergrund; bei Ton `Random(500 · k) + 500`. Der Strahl brennt sich so durch das Gelände.
- **Panzer (20..29) mit Energie > 0:** beim ersten Kontakt Schütze +6Ch + 1; E − 10 zusätzlich; ohne Schild (+52h ≤ 0) 15 Funken
  `Random(40)` um die Spitze; dann **3 Schaden** (`O10:0000(3, 0, Satz)`) und ein Schritt zurück – der Strahl steht am Panzer, bis E
  verbraucht ist (je Schritt 3 Schaden für 20 Energie).

Wände (L3): Stick, Elastic, Accel spiegeln die Richtung bei x ≤ 11, x ≥ 309, y ≤ 31, y ≥ 189 **ohne** Faktor; Warp: x ≤ 10 → +300,
x ≥ 310 → −300; ohne Spiegelung endet der Strahl, wenn er am Rand nach außen zeigt. Sonst Spitze Farbe 254 (`0CC8:0BB6`); Ende bei
E ≤ 0. Danach `0431:0594` und `NoSound` (`[APPROX]` Löschen des Strahls nicht ausgewertet).

### 5.11 MIRV `O4:13D4` (M1)

Vor dem Abschuss ein Dialog „MIRV Setup“ (Maus `0E11`, Tasten) mit dem Muster (Random, Horizontal, Vertical, + 4 Way, X 4 Way →
0..4) und „Detonations“ (Impact, Simultanious, None) – Bedienung mit Teil 3. Die Mutter fliegt wie §5.3 und **teilt sich im Scheitel** (vy > 0, M2) in
fünf Teile mit Lage und Geschwindigkeit der Mutter, verändert je Muster (M1): 0 `vx, vy += Random:Real · 6 − 3` (je Teil zwei
`Random:Real`), 1 vx += (i − 3) · 1,75, 2 vy += (i − 3) · 1,75, 3 und 4 je Teil ±1,75 in x und y (`[APPROX]` Zuordnung der
Vorzeichen zu den Teilen nicht ausgewertet). Einschläge über `O6:00E0` (als Waffe 7: Radius 15, §5.5): Detonation 0 (Impact) je Teil bei seinem Einschlag,
1 (Simultanious) alle zusammen am Ende, 2 (None) keine Explosion (M2).

### 5.12 Lenkung und Gelände im Flug (G1, G2)

**Lenkung** im Teilschritt (G1), Ziel = Satz +28h, dx = x_Ziel − ix, dy = y_Ziel − iy:

- **Vick** (+26h ∈ {2, 3}): bei |dx| ≤ 2 wird vx = 0 und y um 0,5 zum Ziel hin verschoben (Lage, nicht Geschwindigkeit).
- **Herb** (+26h ∈ {1, 3}): bei 0 < dy ≤ 4 (Ziel bis 4 Pixel tiefer) wird vy = 0; zeigt vx vom Ziel weg (dx · vx < 0), vx :=
  vx · (−1,01), sonst vx · 1,01, bei vx = 0 vx := 0,1.
- Greift eine Lenkung, wird Farbe 19 (Schuss) rot (63, 0, 0).

**Gelände** (G2, Sonderregel aus §5.4): M = 3 · Energie + 643 des Schützen (beim Abschuss). Trifft der Schuss Gelände, endet er bei
Kraft < M sofort, bei Kraft > M nur im letzten Teilschritt eines Ticks (`DS:4740` = SS), **bei Kraft = M gar nicht** – der Schuss
fliegt dann durch das Gelände (`[ORIGINAL]` so im Code; die Höchstkraft ist 10 · Energie, §6.1, M liegt darunter – bei Energie 100 genau Kraft 943).

### 5.13 Offen in Teil 2

Bilder `O5` im Einzelnen (`[APPROX]`, nur für die Darstellung nötig); Zuordnung der Vorzeichen bei MIRV-Muster 3 und 4.

## 6. Zug, Kopfzeile, Eingaben (Teil 3)

### 6.1 Zugbeginn und Kopfzeile (Z1, Z2)

In `O4:44A7` je Zug (Z1): Panzer neu (`0431:07BA`), +5Eh + 1 (Index im Schussprotokoll, §5.9) und +70h + 1 (Zahl der Züge); hat die
gewählte Waffe keinen Bestand (+2Eh + 2w = 0), nächste Waffe w + 1 (nach 17 wieder 0) bis eine mit Bestand. Dann `O4:0000` (Farben der
Waffenleiste), Kopfzeile: `Bar` (109, 0)–(211, 7) Farbe 0, Name des Spielers Farbe i + 19 zentriert bei (160, 0); Farbe 9 „Wind: “ +
|Wind| + Leerzeichen + Pfeil zentriert bei (160, 10), Pfeil = Zeichen `DS:0064` (27, ←) bei Wind < 0, sonst `DS:0066` (26, →) – auch
bei Wind 0. **Kraft ≤ 10 · Energie** (+1Bh wird gekürzt). Dann der Zug `O7[3]` = `O7:040C`.

**Waffenleiste** `O4:0000(Satz)` (Z2): Farbe 100 + w (w = 0..17) = (63, 63, 63) bei Bestand, sonst (0, 0, 0); die gewählte Waffe
(63, 0, 0). Farbe 118 (Herb, +2Ah) und 119 (Vick, +2Ch) ebenso; ohne Bestand wird das Lenkbit in +26h gelöscht (Herb Bit 0, Vick
Bit 1), ist es gesetzt, ist die Farbe rot. Die Leiste selbst zeichnet `O8:1268` mit diesen Farben (Grenzen `DS:E874`, `E898`, `E89A`,
`E89C` für die Maus, §6.3; Zeichnung `[APPROX]` nicht ausgewertet).

### 6.2 Zug `O7:040C` (Z3)

`O4:0000`, Rahmen (`O7:0364`), Farbe 14: Felder `Bar` + Text für **Winkel** +1Dh bei (56, 0)–(70, 7), **Kraft** +1Bh bei
(56, 10)–(86, 17), **Richtung** (Pfeil wie §6.1 aus +1Fh) bei (296, 0)–(302, 7), **Energie** +21h bei (248, 10)–(270, 17); Farbe 15.
Dann Befehle von `O7:0EF7(Satz, Steuerart DS:E5A6 + 2i)`, bis Befehl 5:

| Befehl | Wirkung |
|---|---|
| 1 / 2 | Winkel a := a + r bzw. a − r; a > 90 → r := −r, a := 180 − a; a < 0 → r := −r, a := \|a\| (das Rohr dreht stetig über die Senkrechte); Rohr mit dem Hintergrund löschen und neu, Richtung neu, Ton bei Ton an |
| 3 / 4 | Kraft − 1 bzw. + 1, begrenzt auf 0..10 · Energie; beim Laser (Waffe 5) zusätzlich ≤ +38h |
| 5 | Feuer: Ende des Zugs; warten, bis beide Maustasten los sind; bei Lenkung (+26h > 0) Ziel wählen `O9:092E` |
| 6 | Richtung r := −r (Rohr spiegeln) |
| 7 / 8 | vorige / nächste Waffe mit Bestand (zyklisch 0..17) |
| 9 / 10 | Herb / Vick an oder aus (nur mit Bestand +2Ah / +2Ch) |

`O7:0EF7` gibt ohne Eingabe 0 zurück; hat die Waffe keinen Bestand, sofort 8, ebenso beim **Laser mit Bestand ≤ 100** (Z5).

### 6.3 Eingaben `O7:0EF7` (Z4, Z5)

Die Tastatur liest die eigene Tastenroutine `0C48` (gehaltene Tasten in `DS:452A` + Scancode, erweiterte + 100h).

- **Tastatur** (Steuerart 1, Z4): ← / → Befehl 1 / 2, ↓ / ↑ 3 / 4, BildAuf / BildAb Kraft + 99 bzw. − 99 und Befehl 4 / 3 (also ± 100),
  Leertaste, Enter, Enter am Ziffernblock 5, I 6, Tab 8, Umschalt + Tab 7, H 9, V 10, Ziffern 1..9, 0 → Panzerinfo `O9:0040(n, 1)`
  (n = 1..9, 0). Ohne Alt wartet jede Taste aufs Loslassen (ein Schritt), **mit Alt wiederholt** sie.
- **Schummeltaste** (Z4): Einfg + Entf gleichzeitig öffnet eine Schleife bis Einfg + Entf oder Leertaste: C → `0431:0594(2, 2, …)`,
  W → alle 18 Waffen Bestand 999, G → Herb und Vick 10, D → Schild, Inertia, Repulser je 1000.
- **Maus** (Steuerart 2, Z5): Klick auf einen Panzer (Farbe 20..29) → Panzerinfo; Leiste y = 18..27: x in `E874..E898` links 7, rechts 8,
  `E898..E89A` Herb (9), `E89A..E89C` Vick (10); Winkelfeld (56..70, 0..7) links 1, rechts 2; Kraftfeld (56..86, 10..17) links 3, rechts
  4; (8..54, 10..17) links − 100, rechts + 100; Richtungsfeld (296..302, 0..7) 6; beide Tasten 5. `0DBD:022A`/`0256` liefern x/y
  (`[APPROX]` nach Verwendung).
- **Computer** (Steuerart 3): virtuelle Methode +4 der Intelligenz (`DS:3BE8 + 4i`, VMT +12h) liefert den Befehl (Teil 3 Rest, `0129`).
- Außer beim Computer danach `Delay(TS)` (`DS:E8C4`).

### 6.4 Offen in Teil 3

Panzerinfo `O9:0040` und Zielwahl `O9:092E`, Zeichnung der Waffenleiste `O8:1268`, Computer `0129` (Zielen, Waffenwahl, Zufall).

## 7. Nicht entscheidbar ohne DOSBox (Stand Teil 1 und 2)

1. Verlauf der Palette 30..59 aus den 9 Stützwerten von `0431:000C` und die genaue Zeichnung von Circles, Spread, Sky, Asterisk:
   je Hintergrund ein Bild **vor dem ersten Schuss** mit notierter Uhrzeit (Sitzung 3); Static und Stars liefern zusätzlich den
   Zufallsstand.
2. Wirkung von `0C79:0428` (Richtung der Rotation): zwei Bilder desselben Hintergrunds im Abstand weniger Takte.
3. Gelände bitgleich: ein Rundenbeginn je Spielerzahl 2 und 10 (Randbreite 60 bzw. 4) mit fester Uhrzeit.
4. Flugbahn Lead Ball: je Wandart ein Schuss mit **Spur an**, notiert Winkel, Kraft, Wind (Kopfzeile) und `SS`; Bild nach dem
   Schuss zeigt die Spur pixelgenau (§5.3). Dazu einmal Wand 0 mit Schuss nach links bei Wind < 0 (Randregel §5.4).
5. Rückstoß (§5.1): Energie des Schützen vor und nach dem Schuss in der Panzerinfo (`O9`), mehrere Schüsse.
6. Krater und Flächenschaden (§5.6): Schuss mit 20 Kiloton Nuke neben einen Panzer, Bild nach dem Einschlag und Energie in der
   Panzerinfo vorher und nachher; dazu ein Bild mit Spread/Static-Hintergrund, um das Freilegen aus dem Puffer zu sehen.
7. Erdrutsch und Fallen (§5.8): Crumble 100 % und „Fall down“ an, ein Schuss unter einen Panzer; Bildfolge (Video) zeigt
   Reihenfolge und Abgleiten; Energie vorher und nachher (`Random(3) + 1` je Zeile).
8. Todesexplosionen O5 und Blitz (§5.9): Video je Todesart; Blitz erst ab Durchgang 11 (lange Runde ohne Treffer).
9. Laser (§5.10): ein Schuss durch Gelände und einer gegen einen Panzer mit und ohne Schild; Bild des Strahls und der Funken.
10. MIRV (§5.11): je Muster ein Schuss mit Spur an (Vorzeichen bei Muster 3 und 4).
11. Gelände bei voller Kraft (§5.12): Schuss mit höchster Kraft in einen Hügel; fliegt er hindurch?
