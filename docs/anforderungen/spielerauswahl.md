# Anforderungen: Spielerauswahl

Spur `spielerauswahl` (Runde 1, Welle 2, Stufe 2c). Quelle: Disassembly von O2 (`BOMB.OVR`, Overlay ab 0x005E8: Dateioffset =
0x5E8 + Offset `O2:xxxx`) und `BOMB.EXE` (Datensegment `DS` = Segment 14D5 ab 0x163F0, `UCC8` ab 0x0E320). Das Listing im
Referenz-Repo ist bei `O2:4153–4F0D` an Texten falsch ausgerichtet; neu disassembliert mit `ndisasm -b16 -e <start> -o <start>`
(Textkonstanten übersprungen). Bildschirm B3, B3a, B3b in `bildschirme.md`. Umsetzung: `src/BombRedone.UI/Screens/PlayerSelectScreen.cs`,
Spielaufstellung `src/BombRedone.Core/Setup/*`.

## 1. Ablauf in `O2:3D49`

1. Vor dem Hauptmenü `O2:0216(n)` für n = 1..10: Spielersatz bei `DS:402E + 116·n` (Byte 0 = n, ab Byte 1 Name `String[13]`
   „Player n“), Steuerart `DS:E5A6 + 2n` := 1 (Tastatur), Farbteiler (Wörter +60h/+62h/+64h, §3).
2. Hauptmenü, dann Übergang `UCC8:0687` (Kreise aus dem Puffer `DS:E5BC` um (50,150) und (160,100)), dann **`O2:52DD`**.
3. Danach bei „Team Play“ (`DS:E8BC`) `O2:2749` (Teams), Wischen `UCC8:0AC0`, bei Teams `O2:35CF` (Teamnamen), schwarzes
   Wischen `UCC8:09E4`, Palettenrotation aus.
- **Umgesetzt:** `O2:52DD` mit `O2:07BA`, `O2:4F0E`, `O2:509C`, `O2:42E8`, `O2:481B` und Spielaufstellung. **Nicht umgesetzt:**
  alle Übergänge (`[APPROX]`: die Auswahl beginnt auf dem Puffer `DS:E5BC` – Hintergrund und Kopfzeile; die Kreise decken Tafel,
  Überschrift und Load Game ab, ob lückenlos, ist ungeprüft), Teams `O2:2749`/`O2:35CF`, Wischen am Ende; nach START folgt ein
  Platzhalter.

## 2. Bild `O2:52DD` (`BOMB.OVR 0x058C5`, `[ORIGINAL]`)

| Element | Prozedur | Lage | Farben, Schrift |
|---|---|---|---|
| Knopf START | `O2:07BA` | Knopf (71,171)–(248,197) | EURO, `SetUserCharSize(1,1,3,10)`, Mitte/Mitte; „START“ gelb (14) bei (163±i, 182±i), i = 1..5, zuletzt hellrot (12) bei (163,182) |
| Spielerfeld n | `O2:52E7–54B3` | Knopf (x1,y1)–(x1+55,y1+55), x1 = 10 + 61·((n−1) mod 5), y1 = 50 (n ≤ 5) bzw. 111 | Fläche 18, über der Spielerzahl 17; hell 15, dunkel 17; SmallFont, `SetUserCharSize(1,1,1,1)` |
| Namensfeld | `O2:4F0E` | vertiefter Knopf (x1+3,y1+3)–(x2−3,y1+12) | Name Farbe 19+n (über der Spielerzahl 0), Mitte/oben bei ((x1+x2)/2, y1+1); `SetUserCharSize(m,100,1,1)`, m = 100, 99, … bis `TextWidth` ≤ 49 |
| Steuerart | `O2:509C` | Mitte/Mitte bei ((x1+x2)/2, y1+19/29/39) | „KEYBOARD“, „MOUSE“, „COMPUTER“; gewählt hellblau (9), sonst schwarz; ohne Maus (`DS:E8B6` = 0) „MOUSE“ mit `SetUserCharSize(1,1,1,3)` |
| Markenplatz | `O2:5444` | ((x1+x2)/2, y2−4) | Plus aus dem Puffer (`U431:040F`) – zeigt den Hintergrund; über der Spielerzahl schwarzes Plus (`UCC8:017D` Ellipse r = 1,1 + `UCC8:0BB6` Punkt) |
| Marke | `O2:54E0` | ebenda | `U431:03C3`, Farbe 19 |

- **Farben** (`O10:01AC` mit 100 %, `BOMB.OVR 0x1F2CF`): DAC 19+n := (63 div r, 63 div g, 63 div b) mit den Teilern aus `O2:0216`
  (7FFFh → 0): rot, grün, blau, gelb, türkis, magenta, weiß, (63,31,0), (63,0,31), (63,31,31).
- Steuerart über der Spielerzahl wird 0 (`O2:50A2`), sonst bleibt sie zwischen den Aufrufen erhalten.

## 3. Bedienung `O2:551E–5A29` (`[ORIGINAL]`)

- **Tasten** (je Durchlauf ein `UpCase(ReadKey)`): Enter/Leertaste (`O2:5256`) = Name; K, M, C = Tastatur, Maus, Computer (setzen
  den jeweils anderen Merker zurück); Esc, G, P, S (`O2:5276`) = START; erweiterte Tasten (`DS:3716`): links/rechts innerhalb der
  Spieler mit Umlauf, auf/ab ± 5 (Umlauf zwischen den Zeilen, liegt das Ziel über der Spielerzahl, wieder − 5).
- **Maus** (gedrückte Taste 1 oder 2, x = Trunc(CX/2)) für n = 1..Spielerzahl: im Feld (x1<x<x2, y1<y<y2) nur Auswahl;
  Namensfeld (x1+3<x<x2−3, y1+3<y<y1+12) = Name; zwischen x1 und x2: y1+15<y<y1+25 Tastatur, y1+25..35 Maus, y1+35..45 beide
  Merker (Computer) – die Mausbereiche setzen nur ihren Merker. 71<x<249, 171<y<197 = START. **Kein** Warten auf das Loslassen:
  gehaltene Taste wiederholt die Aktion.
- **Aktion** für Spieler p (`O2:58DB–5A13`, in dieser Reihenfolge): Name bei Tastatur/Maus → Namenseingabe; Name bei Computer →
  Gegnerliste; Tastatur bzw. Maus: Steuerart setzen, Wörter neu, war es ein Computer → Namenseingabe; Computer: setzen, Wörter neu,
  Gegnerliste. Nach Eingabe/Liste wird das Namensfeld neu gezeichnet. Dann Merker löschen, Marke neu.
- **START** (`O2:5A2C`): `DS:4812` := alle Spieler Computer; Tabelle der Intelligenzen `DS:3BE8` leeren; je Computer mit Namen
  „Random“ (`O2:52D6`) Name := Name `Random(7)+1`; `O2:0078` setzt je Name die Intelligenz (für jede Steuerart); Warten auf das
  Loslassen der Maustasten.

## 4. Namenseingabe `O2:42E8` (`BOMB.OVR 0x048D0`, `[ORIGINAL]`)

- Name bis Stelle 13 mit Leerzeichen auffüllen; `GetImage(108,83,212,117)`; Knopf (110,85)–(210,115); „NAME:“ magenta (5)
  links/oben bei (113,85); vertieftes Feld (115,100)–(205,110); Zeichen schwarz, links/Mitte bei (118 + 6(k−1), 104) einzeln.
  Länge + 1 (das aufgefüllte Leerzeichen), Schreibmarke dort – **Tippen hängt an**.
- Zelle (`O2:4153`): drei Rechtecke Farbe 18 (x−1,100)–(x+4,110) usw., Zeichen neu; mit Schreibmarke (`O2:4006`) zusätzlich
  (x−1,109)–(x+4,110) aus dem Puffer `DS:E5BC` (Hintergrundpixel als Unterstrich).
- Tasten (`ReadKey` ohne `UpCase`): Rückschritt (`O2:4265`) löscht vor der Marke; Enter/Esc (`O2:4285`) beenden; Zeichen 32..125
  (`O2:42A5`) an der Marke, Marke + 1, Länge wächst; Pos1 löscht von hinten alles und setzt den Namen auf zwei Leerzeichen
  (ohne Neuzeichnen der Marke); Entf löscht an der Marke; Ende springt ans Ende; links/rechts bewegen. Marke und Länge höchstens 13.
- Ende: Leerzeichen und #0 am Ende entfernen (`O2:42C8`), `PutImage`.
- `[APPROX]` `GetMem(1033)` ist kleiner als das Bild (105 × 35); nachgebildet ist die vollständige Wiederherstellung. Esc
  beendet im Fenster das Programm (Fenstertaste aus `titel`) und erreicht die Eingabe dort nicht.

## 5. Gegnerliste `O2:481B` (`BOMB.OVR 0x04E03`, `[ORIGINAL]`)

- Warten auf das Loslassen; `GetImage(114,56,206,144)`; Knopf (116,58)–(204,142); Auswahl = Name 1..6 der Liste, sonst 0 (Wind
  Master wird nie vorgewählt). Einträge Mitte/Mitte bei (160, 63+10k) in Teilen übereinander: braun (6), Kennbuchstabe gelb (14).
- Namen `DS:007C + 256·k` (`BOMB.EXE 0x1646C + 256·k`): Random, Mr. Stupid, Lobber, Rifleman, Lob & Shoot, Windless Wit, Twanger,
  Wind Master – gleiche Reihenfolge wie `BOMB.DOC` „PLAYER SELECTION“ (ohne Random).
- Tasten (`UpCase`): Esc und „?“ = Random, Enter/Leertaste = Auswahl, S L R B W T M = 1..7, auf/ab 0..7 ohne Umlauf.
- Maus: 116<x<204 und 60<y≤69, 69<y≤79, …, 119<y≤129, 129<y<142 = 0..7, sonst – auch außerhalb – Random; danach Warten auf das
  Loslassen. Marken (`U431:03C3`) bei x = 119 und 201, gelöscht mit Flächenquadraten 3 × 3 (Farbe 18).
- Gewählt: Name := Listenname, `PutImage`. `[APPROX]` Puffer `GetMem(2215)` zu klein, wie oben.

## 6. Spielaufstellung (`Core/Setup`)

`PlayerRoster` hält die zehn Sätze (Name, Steuerart) und die Spielerzahl; `Start(random)` liefert `GameSetup` (Spieler in Reihenfolge
mit Name, Steuerart, Intelligenz 1..7 laut Name, Team 0) und „alle Computer“. Zufall nur über den übergebenen Generator; Random
wird erst beim Start aufgelöst. `[APPROX]` Team immer 0, bis `O2:2749` umgesetzt ist.

## 7. Abnahme und Restlücken

- Tests: `PlayerRosterTests`, `ComputerOpponentsOriginalTests` (Namen aus `BOMB.EXE`, `BOMB.DOC`), `PlayerSelectScreenTests`
  (Lage, Farben, Tasten, Maus, Namenseingabe, Gegnerliste, START; synthetische Schriften), `PlayerSelectScreenOriginalTests`
  (Texte mit Dateioffset aus `BOMB.OVR`, Prüfsummen von vier Zuständen, Farben, Stauchen langer Namen).
- **Abgleich** (`[APPROX]`, kein DOSBox-Bild): Longplay 00027 (Felder, START mit Glanz, Plus im Feld), 00028 (Gegnerliste),
  00031 (Namenseingabe) nach Lage und Form – stimmt. Das Video zeigt beim Tippen „J_“ statt angehängt: der Spieler hat vorher
  gelöscht (Rückschritt/Pos1), der Code hängt an.
- `[APPROX]` `DS:E8B6` (Maus gefunden) und `DS:E94E` (Maus benutzt) sind hier eine Einstellung (`MenuSettings.MouseAvailable`).
- Offen: Übergänge (`UCC8:0687`, `UCC8:0AC0`, `UCC8:09E4`), Teams (`O2:2749`, `O2:2EBD`, `O2:300A`, `O2:31A8`, `O2:35CF`),
  Rückweg ins Hauptmenü (gibt es im Original nicht: Esc startet).
