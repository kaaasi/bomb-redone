# Anforderungen: Hauptmenü und Game Options

Spur `menue` (Runde 1, Welle 2, Stufe 2b). Quelle: Disassembly von O2 (`BOMB.OVR`, Overlay ab 0x005E8: Dateioffset = 0x5E8 +
Offset `O2:xxxx`), `BOMB.EXE` (Segment `0431` ab 0x059B0, `0CC8` ab 0x0E320, `0C79` ab 0x0DE30, `0E11` ab 0x0F7B0, `0DBD` ab
0x0F270); Listings im Referenz-Repo (`analyse/re-grundlagen/listings/`), `O2:1E7F` dort falsch ausgerichtet – mit `ndisasm -e
0x2467 -o 0x1E7F` neu erzeugt. Bildschirm B2 in `bildschirme.md`, Vergleichsbild Longplay 00010 (nur Lage und Form).
Umsetzung: `src/BombRedone.UI/Screens/Menu*`. Standardschrift: `docs/formate/schriften.md` §4 (Teil 1).

## 1. Ablauf `O2:3D49` (O2[2])

1. Schleife `O2:0216(i)` für i = 1..10, dann bei `DS:E8BF` ≠ 0 Sonderweg ohne Menü (nicht umgesetzt, Bedeutung offen).
2. `U431:01D8`: DAC 17/18/19 = (21,21,21), (42,42,42), (63,63,63); `SetViewPort(0, 0, 319, 199, ClipOn)`.
3. Hintergrund `O2:3B96(0)` (§2), Kopfzeile `O2:06BD`, **Kopie des Bildschirms** (64.000 Byte) in den Puffer `DS:E5BC`,
   Überschrift `O2:0909`, Menü und Schleife `O2:1E7F` (§3, §4).
4. Danach Übergänge (`UCC8:0687`, `UCC8:0AC0`, `UCC8:09E4`) und je nach Wahl Laden (`O3[2]`) oder Spielerauswahl `O2:52DD`.
   **Nicht umgesetzt:** Übergänge; START und Load Game führen vorerst auf einen Platzhalter (Stufe 2c).

## 2. Hintergrund `O2:3B96(0)` (`BOMB.OVR 0x0417E`)

- DAC 30..59 = (i, 0, i) über die Palettenroutine `DS:E8F6`.
- `Random(2)`: 0 → Rahmen k = 0..100, 1 → k = 0..160 (bei k > 99 liegen die Ecken vertauscht, die Linien treffen sich in der Mitte).
  Rahmen k = `UCC8:0031(k, k, 319−k, 199−k, k mod 30 + 30)`: vier Linien rechts, links, unten, oben.
- Linien `UCC8:09E4`: senkrecht/waagrecht als Lauf, sonst Bresenham (Fehlerterm 2·minor − major) vom linken Ende; kein Clipping.
- **Palettenrotation** 30..59: Puffer `DS:DFA6` mit den 30 Einträgen zweimal hintereinander (`UCC8:0C20`), Rotor
  `UC79:003D` mit Anfang 30, Anzahl 29 (+1), Geschwindigkeit `DS:E8C2` = „Pspeed“ aus `BOMB.CFG`. Handler `UC79:00E3`: je Tick
  Zähler + 1; erreicht er die Geschwindigkeit, Zähler 0, DAC 30..59 aus dem Puffer ab Position p (0..29, über 29 → 0), p + 1.
  Tick: INT 1Ch (18,2 Hz) oder bei „PV“ der Strahlrücklauf; installiert nur bei „Palette“ = ein (`0000:0435`, `DS:E8B1`).
  `[APPROX]` Tickdauer aus der PC-Hardware (54,925 ms bzw. 70 Hz), Zähler beginnt bei 0.

## 3. Bild (`[ORIGINAL]`)

| Element | Prozedur | Lage | Farben, Schrift |
|---|---|---|---|
| Knopf (`U431:02A7`) | – | Fläche (x1,y1)–(x2,y2) per `Bar`, je 2 Pixel Kante: dunkel rechts/unten, hell links/oben (zuletzt, gewinnt an den Ecken) | |
| Kopfzeile | `O2:06BD` | Knopf (10,6)–(310,42) | Fläche 18, hell 15, dunkel 17; EURO Größe 1, zentriert; „TANK WARS  3.2“ (Text + `DS:0056`) bei (163,23), (162,22), (161,21) Farbe 14, (160,20) Farbe 12 |
| Überschrift | `O2:0909` | Knopf (118,63)–(202,77) | DefaultFont, Farbe 1, zentriert (160,70) „Main Menu“ |
| Tafel | `O2:1E7F` | Knopf (118,85)–(202,187) | |
| Einträge n = 0..4 | `O2:1E7F` | vertiefter Knopf (122, 91+20n)–(198, 101+20n), Fläche 2, hell 17, dunkel 15; Innenfläche danach aus `DS:E5BC` (Hintergrund scheint durch) | SmallFont, `SetUserCharSize(1,1,1,1)`, zentriert (160, 95+20n); Farbe 10, Zahlen 14, START 13 |
| Zahlen | `O2:1E7F` | x = 160 + TextWidth(„Players: “ bzw. „Games: “) / 2, zentriert | `DBD:02FE` = `Str` |
| Load Game | `O2:1E7F` | Knopf (20,145)–(80,155) | „Load Game“ Farbe 10, darüber „L        “ Farbe 12 bei (50,149) |
| Marken | `U431:03C3` | Plus aus 5 Pixeln Farbe 19 bei (112, 95+20·Wahl) und (208, …), nur bei 0 < x < 320, 0 < y < 199; Löschen `U431:040F` aus `DS:E5BC` | |

`[APPROX]` Farben 0..15 = VGA-Standardpalette (Titel stellt die vorherige Palette wieder her); `Bar` schließt die Ecken ein
(BGI-Treiber nicht verfolgt). Großes START mit Glanz `O2:07BA` wird nur von `O2:52E4` (Spielerauswahl) gerufen, nicht vom Hauptmenü (Stufe 2c).

## 4. Bedienung `O2:21DC–26B7` (`[ORIGINAL]`)

- **Tasten** (je Durchlauf ein `ReadKey`): Enter/Leertaste wählt den markierten Eintrag; L/l lädt; erweiterte Tasten aus der
  Tabelle `DS:3716` (`BOMB.EXE 0x19B06`: 48h, 50h, 4Dh, 4Bh): auf/ab bewegt die Marke (0..4, kein Umlauf), rechts/links wählt den
  markierten Eintrag mit Richtung.
- **Maus** (nur mit Treiber, `DS:E94E`; x = Trunc(CX / 2), y = DX): bei gedrückter Taste 1 oder 2 Merker links/rechts setzen; bei
  122 < x < 198 wählt y in [91+20n, 101+20n] den Eintrag n, dann **warten bis beide Tasten los sind**; 20 < x < 80 und
  145 < y < 155 lädt (ohne Warten). Merker werden nur nach einer Aktion gelöscht (`O2:21EF`) – ein Klick neben das Menü wirkt
  bei der nächsten Aktion nach.
- **Aktionen:** Sound/Game Options (n = 0/1) nur mit Enter oder Maustaste (nicht rechts/links); Players: rechts/rechte Taste + 1
  bis 10, links/linke Taste − 1 bis 2, Enter + 1 mit Umlauf über 10 → 2; Games: rechts + 1, rechte Taste + 5 (nur unter 99),
  links − 1, linke Taste − 5 (nur über 1), dann auf 1..99 begrenzt, Enter + 5 mit Umlauf (über 99 → − 99). Neue Zahl: Fläche
  (x−4, 133)–(x+10, 139) bzw. (x−5, 153)–(x+15, 159) aus `DS:E5BC`, Zahl Farbe 14. START beendet das Menü.
- Startwerte: Players 2, Games 10 (`O2:6647/6652`), Games aus `BOMB.CFG`.
- **Nachbildung:** ein `Update` = ein Durchlauf je getippter Taste (mindestens einer für die Maus); das Warten auf das Loslassen
  ist ein Zustand, Tasten aus dieser Zeit bleiben im Puffer. `ScreenInput` hat dafür Tastenwörter (`Keys`) und die Mauslage in
  Treiberkoordinaten (`MouseX` 0..639, `MouseY` 0..199) bekommen.

## 5. Abnahme und Restlücken

- Tests: `MenuGraphicsTests` (Linien, Rahmen, Knopf, Kopie, Marken), `MenuScreenTests` (Muster je `Random(2)`, Palette, Puffer,
  Tasten, Maus, Rotation; synthetische Schriften), `MenuScreenOriginalTests` (Prüfsummen beider Muster mit EURO/LITT aus
  `BOMB.EXE` und Ersatzschrift; Lage der Texte).
- `[APPROX]` Kein DOSBox-Bild des Menüs: Abgleich mit Longplay 00010 nach Lage und Form (Kopfzeile, Tafel, Einträge, Load Game,
  Marken an „Sound Options“, Hintergrund mit Mittelbalken = Muster 1) stimmt; pixelgleich erst mit der Aufnahme `runde-1`.
- Offen: Mauszeiger (zeichnet der Treiber, nicht nachgebildet), Übergänge nach dem Menü, Sonderweg `DS:E8BF`.
