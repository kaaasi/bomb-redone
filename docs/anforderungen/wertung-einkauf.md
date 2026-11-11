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
