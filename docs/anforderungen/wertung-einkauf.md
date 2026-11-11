# Wertung, Endergebnis und Einkauf (Spur `wertung-einkauf`)

Stand 11.11.2026, Claude Opus 5.5. Fundstellen: `Seg:Ofs`/`On:Ofs` wie in `docs/analyse/re-grundlagen.md`; Datensegment
`DS:0000` = `BOMB.EXE 0x163F0`.

## 1. Waffen- und Ausrüstungstabelle (`Core/Economy/ItemCatalog`)

Je Gruppe vier parallele Felder im Datensegment (typisierte Konstanten): Langname und Kurzname (`string[255]`, je 256 Byte),
Preis (LongInt), Bündelmenge (Word). Der Einkauf `O3:0E1F` liest genau diese Felder.

| Gruppe | Anzahl | Langname | Kurzname | Preis | Menge |
|---|---:|---|---|---|---|
| Waffen | 18 | `DS:087C` (`0x16C6C`) | `DS:1A7C` (`0x17E6C`) | `DS:2C7C` (`0x1906C`) | `DS:2CC4` (`0x190B4`) |
| Lenksysteme | 2 | `DS:2CE8` (`0x190D8`) | `DS:2EE8` (`0x192D8`) | `DS:30E8` (`0x194D8`) | `DS:30F0` (`0x194E0`) |
| Abwehr | 3 | `DS:30F4` (`0x194E4`) | `DS:33F4` (`0x197E4`) | `DS:36F4` (`0x19AE4`) | `DS:3700` (`0x19AF0`) |

Werte (Test `ItemCatalogOriginalTests` vergleicht die eingebaute Tabelle mit `BOMB.EXE`): 18 + 2 + 3 Einträge wie in `BOMB.DOC`
„WEAPONS“. Alle 16 Einträge der Videotafel (`bildschirme.md` §4, TW 00100) stimmen in Menge und Preis überein.

**Bestätigung zu #19 (R11-05):** CRI 20/D steht in der Tabelle (`DS:247C`, 1 für 15000). In TW 00100 fehlt er, weil der
Spieler 12930 Dollar hatte und der Einkauf nur Bezahlbares oder Vorhandenes zeigt (`O3:01CB`). Lead Ball und Hand Grenade
haben Menge 0 und Preis −1; der Einkauf überspringt jeden Eintrag mit Menge 0 (`O3:020B`, `BOMB.OVR 0x08C97`) – „nicht
käuflich“ ist damit belegt.

## 2. Spielerdaten (`Core/Economy/PlayerAccount`)

Spielersätze zu 0x74 Byte ab `DS:402E` (Satz 0 = Tauschpuffer, `O3:0000`). Benutzt: `+13h` Punkte (LongInt), `+17h`
ausgegebenes Geld (LongInt), `+2Ah` Lenksysteme (Word[2]), `+2Eh` Waffen (Word[18]), `+52h` Abwehr (LongInt[3]).
**Geld = Punkte − ausgegeben** (`O3:0F65`; `BOMB.DOC` „Money is directly related to score“).

## 3. Einkauf – Regeln (`Core/Economy/Shop`)

- **Wer kommt in den Einkauf** (`O3:0E3D..0F90`): Geld ≥ billigster käuflicher Preis (3000). Computer zusätzlich nur mit
  „Computers Can Buy“ und Entscheidung `U129:018D` – nicht Teil dieser Spur.
- **Angebot** (`O3:015B`): je Gruppe in Tabellenreihenfolge jeder Eintrag mit Menge ≠ 0, der vorrätig (> 0) oder mit
  Punkte − ausgegeben bezahlbar ist.
- **+** (`+`, `=`, Leertaste, rechte Maustaste; `O3:1CF1`): höchstens 9 Bündel, nur wenn der Preis ≤ nicht zugeteiltes Geld.
  **−** (`-`, linke Maustaste; `O3:1B33`): nur abwählen, kein Verkauf.
- **Purchase** (`P`, Enter; `O3:2080`): ausgegeben += Preis × Anzahl, Bestand += Menge × Anzahl (Waffen/Lenksysteme 16 Bit,
  Abwehr 32 Bit), danach neues Angebot. **Done** (`D`, Esc): kauft ebenso und schreibt erst jetzt `+17h` zurück (`O3:23C5`).

## 4. Einkauf – Bildschirm (`UI/Screens/ShopScreen`)

Zeichnet über dem vorher gezeigten Bild (im Original die Wertungstafel; unterhalb der Kopfzeile wird `(0, 38)–(319, 199)` aus dem
Puffer `DS:E5BC` erneuert, `O3:11D1`). Je Spieler ein Besuch; Tastaturspieler bedienen nur mit Tasten, Mausspieler nur mit der
Maus (Spielertyp 1/2, `O3:13A3`, `O3:14AA`).

| Element | Lage, Schrift, Farbe | Fundstelle |
|---|---|---|
| Kopfzeile | Rahmen `(5,5)–(314,35)` (U431:02A7, Fläche 18, hell 15, dunkel 17) | `O3:0FCA` |
| Spielername | EURO 4 mit Nutzergröße 1/2, mittig oben bei (160, 3), Farbe 19 + Spielernummer | `O3:0F98`, `O3:0FEB` |
| Purchase / Done | Knöpfe `(10,16)–(59,24)` und `(284,16)–(310,24)`, SmallFont 4, Farbe 17, Text bei (11,14)/(285,14) | `O3:1025–10C8` |
| Geld | TINY 4; links (11,0)/(11,21) Farbe 6, rechts bündig (309,0)/(309,21) Farbe 14 | `O3:10CD–11CC` |
| Kacheln | ab (5, 40), 99 × 13, Zeilenabstand 18, neue Spalte (+104) unterhalb y2 = 197 → 9 je Spalte | `O3:0199–032D` |
| Name | SmallFont 1 (Nutzergröße 8/8), mittig bei ((x1+x2)/2 − 7, y1 + 3); Farbe Waffen 4, Lenksysteme 10, Abwehr 9 | `O3:016D–0312` |
| „+“ | bei (x2 − 40, y2 − 2), Farbe 100 + n: (63,0,0) wenn bezahlbar, sonst schwarz | `O3:0692–085E` |
| Bestand | TINY 4 rechtsbündig bei (x2 − 13, y2), Farbe 14, nur > 0; Energy Shield Bestand/10 + „%“, Inertia Damp. + „%“, je höchstens „100%“ | `O3:0869–0A17` |
| „Menge FOR Preis“ | TINY 4 links unten bei (x1 + 2, y2), Farbe 0; Energy Shield und Inertia Damp. „100%“ | `O3:0A22–0C8B` |
| Auswahlkästchen | vertiefter Rahmen `(x2−9, y1+3)–(x2−3, y2−3)`; Menge TINY 4 mittig, Farbe 6 | `O3:0C96`, `O3:1DC7` |
| Cursor | Rechteck auf dem Kästchen, Farbe 11, bei nicht bezahlbarem Eintrag 0 | `O3:121C–1382` |

Tasten (`O3:13AD–14A1`): Leertaste, `+`, `=` wählen, `-` abwählen; `P`/Enter Purchase, `D`/Esc Done; Pfeile hoch/runter ±1,
rechts/links ±9 (eine Spalte). Maus (`O3:14B4–15F6`): Treffer beim Drücken, wirksam nach dem Loslassen; rechts wählt, links
wählt ab. Nach einem Kauf mit Inhalt werden die Kacheln neu aufgebaut, der Cursor bleibt im Bereich (`O3:2247–2280`).

`[APPROX]`: Das „+“ zeichnet `UCC8:017D` als Ellipse mit Radien 1/1 (nicht im Einzelnen verfolgt; hier die vier Nachbarn des
Mittelpunkts). Die Schlussblende nach dem letzten Spieler (`O3:23E2`, `UCC8:011A`) fehlt noch. Vergleich mit TW 00100: Reihenfolge,
Texte, Spaltenaufbau und Farben stimmen nach Lage und Form; Prüfsumme bis zu einem DOSBox-Bild vorläufig.

## 5. Wertung einzeln (`Core/Economy/Standings`, `UI/Screens/ScoreScreen`)

`O3:5AEF` (`BOMB.OVR 0x0E57B`). Reihenfolge (`O3:5B61–5C6D`): Blasensortierung nach Siegen absteigend, danach bei gleichen
Siegen nach Punkten; gleiche Siege und Punkte teilen sich den Rang (`O3:5E44–5F62`). Hintergrund: `O2:3B96` mit Argument 1
(waagerechte Linien in Farbe y mod 30 + 30, DAC 30..59 = (i, 0, i)), danach als Puffer `DS:E5BC` gesichert – darüber zeichnet
der Einkauf.

| Element | Lage, Schrift, Farbe | Fundstelle |
|---|---|---|
| Kasten | `(45, top)–(275, bottom)`, top = 100 − (5n + 15), bottom = 5n + 115 | `O3:5B0B`, `O3:5C96` |
| Titelkasten | `(65, top−20)–(255, top−5)`; SmallFont 1, Farbe 6, mittig (160, top − 13): „n Game(s) Played out of m“ | `O3:5CB5–5D8F` |
| Kopf | Farbe 17, „Player“ (50, top+5), Linie y = top + 16 (50..270), „Wins“ rechtsbündig 185, „Score“ rechtsbündig 270 | `O3:5DCC–6035` |
| Zeilen | y = top + 10 + 10i in Farbe 19 + i: „ r.“ bei 50, Name bei 72, Siege 185, Punkte 270 | `O3:5E44–609E` |
| Knöpfe | Add (60..110), Continue bzw. Exit nach dem letzten Spiel (135..185), Save (210..260), y bottom+5..bottom+15, Text Farbe 0 | `O3:60AF–61CF` |

Bedienung (`O3:61DE–65B2`): Ziffern 1..9, 0 = 10 → Statistik der Zeile; `A` → Spiele hinzufügen; `S` → Sichern; jede andere
Taste → weiter. Maus: Zeile (45 < x < 275) → Statistik nach dem Loslassen; Knöpfe wie oben. Ohne Knöpfe (`DS:4812` ≠ 0, nach dem
letzten Spiel immer mit Knöpfen) geht es nach 2000 ms von selbst weiter. Statistik (`O3:2508`), Dialog „# TO ADD:“ (`O3:5717`)
und Sichern (`O3:46DE`) sind nicht Teil der Spur: der Bildschirm meldet nur die Wahl.

`[APPROX]`: Palettenrotation des Hintergrunds (`UC79:003D`) und die Abschlussblende (`O3:65D1`, `UCC8:0AC0`) fehlen. Vergleich mit
LP 00064: Lage, Texte und Farben stimmen nach Lage und Form.

## 6. Endergebnis (`UI/Screens/ScoreResults`)

`O3:7566` (`BOMB.OVR 0x0FFF2`) schreibt nach dem Grafikende im Textmodus (ClrScr, weiß auf schwarz): in Farbe 6
„n Game(s) Played“ (n = `DS:47E2` − 1), „   And just in case you missed them:“, Leerzeile; in Farbe 15 „             Results “,
„   Name            Score   Wins“, ein Leerzeichen und 32 × `C4h`; in Farbe 10 je Spieler „ r.“ + „ “ + Name, `GotoXY(17)`
Punkte in 8 Stellen, `GotoXY(28)` Siege in 4 Stellen (spätere Ausgabe überschreibt lange Namen); zwei Leerzeilen. Ausgabe:
`bombtool render results`.

## 7. Wertung Teams (`Core/Economy/TeamStandings`, `UI/Screens/ScoreTeamScreen`)

`O3:6696` (`BOMB.OVR 0x0F122`), aufgerufen nach der Einzelwertung, wenn Team Play an ist (`DS:E8BC`, `O3:798F`); danach
ggf. Sichern und Einkauf. Zeichnet **keinen eigenen Hintergrund**, sondern über das, was die Einzelwertung hinterlassen hat
(`ScoreTeamScreen` bekommt Bild und DAC, z. B. `ScoreScreen.Background`/`Dac`).

- **Teamsätze** zu 0x35 Byte ab `DS:4838` + 0x35 × Nummer (1..10): `+00h` Name (`string[14]`), `+0Fh` Mitgliederzahl (Word,
  aus der Teamwahl), ab `+11h` sechs Real48-Summen, die die Routine neu füllt: Punkte (`+13h`), Spielerworte `+68h`..`+70h`
  aller Spieler mit Team `+66h` = Nummer (`O3:6713–69BD`); `+68h` und Siege (`+6Ah`) danach durch die Mitgliederzahl geteilt
  (`O3:69C9–6A79`). Angezeigt werden nur Punktsumme und mittlere Siege; die übrigen Summen gehören zur Statistik.
- **Reihenfolge:** Index 1..10; 10 Durchläufe Blasensortierung nach Mitgliederzahl absteigend (`O3:6A86`), dann über die
  ersten n Plätze (n = Teams 1..Spielerzahl mit Mitgliedern, `O3:66B8`) n Durchläufe nach mittleren Siegen absteigend
  (`O3:6B03`) und n Durchläufe bei gleichen Siegen nach Punkten (`O3:6BCA`). Rang wie bei der Einzelwertung: wächst nach einem
  Team, dessen Punkte oder mittlere Siege vom nächsten Platz abweichen (`O3:6F72–7003`). Mittelwerte werden hier exakt
  verglichen (bei ≤ 10 Mitgliedern gleichwertig zum Real48-Vergleich).

| Element | Lage, Schrift, Farbe | Fundstelle |
|---|---|---|
| Kasten | `(45, top)–(275, bottom)`, top = 100 − (5n + 15), bottom = 5n + 115 | `O3:669D`, `O3:6CD5` |
| Titelkasten | `(69, top−20)–(251, top−5)`; Titel wie Einzelwertung, SmallFont 1, Farbe 6, mittig (160, top − 13) | `O3:6CFA–6DD4` |
| Kopf | Farbe 17: „Team“ (50, top+5), Linie y = top + 16 (50..270), „Score“ rechts 270, „Wins“ rechts 185 | `O3:6E11–6E84`, `O3:701F`, `O3:70E7` |
| Zeilen | y = top + 10 + 10·Platz, Farbe 14: „ r.“ bei 50, Name bei 72, `Str(Punkte:10:0)` rechts 270, `Str(Mittel:5:2)` rechts 185 | `O3:6EC1–7195` |
| Knöpfe | Continue bzw. Exit nach dem letzten Spiel (85..135, Text 110), Save (185..235, Text 210); y bottom+5..bottom+15, Text Farbe 0 | `O3:71B2–7283` |

Bedienung (`O3:7288–7467`): Ziffern 1..9, 0 = 10 → Statistik des sortierten Platzes (`O3:2C8B`), wenn die Ziffer ≤ Spielerzahl
ist (auch ein leerer Platz); `S`/`s` → Sichern und weiter; jede andere Taste → weiter; kein „Add“. Maus: Zeile 1..n
(45 < x < 275) → Statistik nach dem Loslassen; Knöpfe wie oben. Ohne Knöpfe (`DS:4812` ≠ 0) nach **5000 ms** weiter
(`O3:7469`); anders als die Einzelwertung erzwingt die Teamwertung nach dem letzten Spiel keine Knöpfe (die Einzelwertung stellt
`DS:4812` beim Verlassen wieder her, `O3:6574`). `O10[1]` setzt für jeden Spieler DAC 19 + n (`O3:6DD9`), ungenutzt; die DAC des
vorigen Bildes bleibt.

`[APPROX]`: Die Schlussblende (`O3:7472–74B6`, `UCC8:0687` mit dem Puffer `DS:E5BC`) fehlt; `Str(…:5:2)` rundet exakte Hälften
(z. B. 1/8) hier von null weg, die Real48-Umwandlung `12CE:1BDE` ist nicht verfolgt. Kein Video mit Team Play: Prüfsumme
(`ScoreTeamScreenOriginalTests`) hält den Stand der Disassembly fest. Ausgabe: `bombtool render team`.

## 8. Statusfenster „Game Status“ (`UI/Screens/StatusWindow`)

`U431:0F1B` (`BOMB.EXE 0x068CB`), im Spiel mit Strg-P (`UC02:041F`: Tastentabelle `DS:452A` + Scancode, P = 19h → `DS:4543`;
die Basis folgt aus Strg-S = `DS:4549`, Strg-M = `DS:455C` und den Abfragen unten). Sichert den Bildschirm nach `DS:E5BC`,
zeichnet darüber, wartet und kopiert ihn zurück (`U431:1725`); Text-, Füll- und Fenstereinstellungen werden gesichert und
wiederhergestellt.

- **Rahmen:** Bar `(10,25)–(310,175)` Farbe 0, Rechtecke `(10,25)–(310,175)` und `(12,27)–(308,173)` Farbe 13 (`UCC8:0031`).
- **Titel** „Game Status“ Farbe 15, mittig bei (160, 35). **Zeilen** linksbündig/mittig ab x = 20, je Zeile `MoveTo` und
  `OutText`: Bezeichnung Farbe 1, Text Farbe 5, Werte Farbe 14 (Texte `U431:0E30–0F11`, „Backgound“ wie im Original):

  | y | Zeile | Werte |
  |---:|---|---|
  | 45 | Sound: Effect (…)   Fly (…) | `E8B3`, `E8B2` (YES/NO, `O2:0007`) |
  | 55 | Ground: Crumbles (…%) | `E8A4` |
  | 65 / 75 | The Walls: Rebound (…) / This Round (…) | `E8AA` / `47EC` (`O2:0F81`) |
  | 85 / 95 | The Shot: Invisible (…) / Trail (…) | `E8B4` / `E8B5` |
  | 105 | Game: Number (…) of (…) | `47E2`, `E8A6` |
  | 115 | Tanks: Fall (…) | `E8B8` |
  | 135 | Computers: Buy Weapons (…) | `E8BA` (y = 125 bleibt leer) |
  | 145 | Screen: Backgound (…) | `E8AC` (`O2:0E35`) |
  | 155 / 165 | The Wind: Control (…) / This Round (…) | `E8A8` / `47E4` (`O2:1042`) |

  Die zweite Zeile einer Gruppe beginnt bei x = `GetX` nach der Bezeichnung (`U431:11A8`, `128B`, `15B0`).
- **Schließen** (`U431:1660–16F0`): warten, bis eine Maustaste oder Esc, Enter oder Leertaste gedrückt ist (`DS:452B`, `4546`,
  `4563`), dann, bis alle los sind.

`[APPROX]`: Das Fenster setzt keine Schrift; angenommen ist die DefaultFont 8 × 8 des Spielfelds (`O4:44A7`), mit der
Ersatzschrift gezeichnet. Tasten kommen als getippte Tastenwörter, nicht als gehaltene Tasten: eine getippte Esc/Enter/Leertaste
gilt als gedrückt und im nächsten `Update` ohne sie als losgelassen. Kein Bild des Fensters: Prüfsumme (`StatusWindowTests`)
hält den Stand fest. Ausgabe: `bombtool render status` (über schwarzem Spielfeld, Menüfarben).
