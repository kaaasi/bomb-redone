# Bildschirme und Abläufe aus den Videos

Spur `bildschirme-inventar` (Runde 1, Welle 1). Reine Beschreibung; **keine Bilder oder Ausschnitte** in diesem Repo.

**Quellen.** Einzelbilder aus zwei Videos im Referenz-Repo `kaaasi/bomb-referenz` (`videos/README.md`), Dateinummer = Sekunde:

- `tank-wars` (Kürzel **TW**): 640 × 356, Spiel links x 0–570; 152 Bilder (0–151); Fassung nicht erkennbar; 4 Panzer.
- `tank-wars-longplay` (Kürzel **LP**): 576 × 360; 439 Bilder (0–439, **359 fehlt**); Release 3.2 (Menükopf „TANK WARS 3.2“).

Bildnummern werden als `LP 00017` bzw. `TW 00100` angegeben (= `videos/<kurzname>/<nnnnn>.png`). `BOMB.DOC` / `BOMB.TXT` werden als
**DOC** bzw. **TXT** mit Abschnittsname zitiert (Release 3.2, aus `original/bomb32.zip`).

**Grenzen.** Die Videobilder sind skaliert und verlustbehaftet. Sie belegen keine Pixelmaße, Farben oder Schriftgrößen (dafür
gelten DOSBox-Bilder). Texte wurden aus vergrößerten Einzelbildern gelesen; was dabei nicht sicher lesbar war, steht als
**„unsicher“**. Aufgenommen wurde ein Spielstand mit den Voreinstellungen des Spielers, nicht die Voreinstellungen des Programms
(z. B. zeigt die Optionsliste 75 % Crumble, DOC nennt 50 % als Standard).

## 1. Zuordnung der Bildbereiche zu Bildschirmen

Jedes vorhandene Bild ist genau einer Zeile zugeordnet. „Übergang“ = Bildaufbau oder Wischeffekt zwischen zwei Bildschirmen.

### 1.1 Longplay (`LP`, 0–439 ohne 359)

| Bilder | Bildschirm / Übergang | Anmerkung |
|---|---|---|
| 00000–00008 | **B1 Titelbild** | Schriftzug „TANK WARS“ baut sich auf (00000–00003: nur Teile sichtbar), ab 00004/00005 „Copyright 1990, 1991, 1992 Kenneth Morse“ |
| 00009–00010, 00015, 00023–00024 | **B2 Hauptmenü** (ohne Popup) | Kopf „TANK WARS 3.2“, Kasten „Main Menu“ |
| 00011–00014 | B2 + Popup **B2a Sound Options** | Mauszeiger auf „Flying“ |
| 00016–00022 | B2 + Popup **B2b Game Options** | „Walls“ wechselt dabei den Wert (00017–00021) |
| 00025 | Übergang B2 → B3 | Menü verschwindet kreisförmig (unsicher: Irisblende) |
| 00026–00027, 00029, 00034–00036 | **B3 Spielerauswahl** | 10 Felder, großer Knopf START |
| 00028 | B3 + Popup **B3a Computerwahl** | Liste der Computergegner |
| 00030–00033 | B3 + Dialog **B3b Namenseingabe** | „NAME:“ mit Eingabecursor, Name „Javier“ entsteht (00032–00033) |
| 00037 | Übergang B3 → Spiel | schräger Wisch von links, Menü wird von Magenta verdrängt |
| 00038 | Übergang (schwarz) | Bildschirm leer |
| 00039–00063 | **B4 Spielfeld**, Spiel 1 | Hintergrund „konzentrische Quadrate“; 00039 Aufbau, Spielername oben ab 00040 |
| 00064–00066 | **B5 Wertung** „1 Game Played out of 10“ | |
| 00067 | Übergang B5 → B6 | Wertungstafel kippt schräg weg |
| 00068–00072 | **B6 Einkauf** (Javier) | 00072: nach dem Kauf nur noch ein Eintrag sichtbar (unsicher) |
| 00073–00105 | B4 Spielfeld, Spiel 2 | Hintergrund „Rechteck-Ringe“ (rot/grün); 00073 Aufbau |
| 00106–00108 | B5 Wertung „2 Games Played out of 10“ | |
| 00109–00114 | B6 Einkauf | |
| 00115 | Übergang (Geländeaufbau) | Rechteck mit Rauschen, noch ohne Spielfeld |
| 00116–00139 | B4 Spielfeld, Spiel 3 | Hintergrund Rauschen (violett/blau) |
| 00140–00141 | B5 Wertung „3 Games Played out of 10“ | danach **kein** Einkauf (00142 → neues Spiel) |
| 00142 | Übergang | Sanduhr-Form des neuen Hintergrunds |
| 00143–00166 | B4 Spielfeld, Spiel 4 | Hintergrund Strahlen (rot/rosa); 00160–00166 Erdrutsch durch „Crumble“ |
| 00167–00168 | B5 Wertung „4 Games Played out of 10“ | |
| 00169–00174 | B6 Einkauf | |
| 00175 | Übergang (Aufbau, ellipsenförmig) | |
| 00176–00211 | B4 Spielfeld, Spiel 5 | Hintergrund blaue Ringe |
| 00212–00213 | B5 Wertung „5 Games Played out of 10“ | |
| 00214 | B6 Einkauf des zweiten Spielers (Computer „Lob & Shoot“) | nur ein Bild, Liste leer |
| 00215–00236 | B4 Spielfeld, Spiel 6 | Hintergrund dunkel, ohne Muster |
| 00237–00238 | B5 Wertung „6 Games Played out of 10“ | |
| 00239–00244 | B6 Einkauf | |
| 00245 | Übergang (Rauschen-Rechteck) | |
| 00246–00279 | B4 Spielfeld, Spiel 7 | Hintergrund buntes Rauschen |
| 00280–00281 | B5 Wertung „7 Games Played out of 10“ | |
| 00282–00286 | B6 Einkauf | |
| 00287 | Übergang | Treppenform (Block-Wisch) |
| 00288–00328 | B4 Spielfeld, Spiel 8 | Hintergrund blauer Verlauf; 00302: hellblaue Fläche über dem Berg (unsicher, Blitz oder Effekt) |
| 00329–00330 | B5 Wertung „8 Games Played out of 10“ | |
| 00331–00337 | B6 Einkauf (Javier) | |
| 00338 | B6 Einkauf des Computers („Lob & Shoot“) | ein Bild, sechs Einträge (Incinerator bis Mark II Dirt) |
| 00339 | Übergang (Aufbau, einfarbig gelb) | |
| 00340–00383 | B4 Spielfeld, Spiel 9 | Hintergrund gelbes Muster |
| 00384 | Übergang B4 → B5 | leere Wertungstafel („9 Games out o…“) |
| 00385–00386 | B5 Wertung „9 Games Played out of 10“ | |
| 00387–00393 | B6 Einkauf | |
| 00394–00426 | B4 Spielfeld, Spiel 10 | Hintergrund Strahlen (blau/grün); 00394 Aufbau |
| 00427–00430 | B5 Wertung „10 Games Played out of 10“ | mittlerer Knopf „Exit“ (LP 00428, siehe B5) |
| 00431 | Übergang (schwarz) | |
| 00432–00439 | B1 Titelbild (Ende) | wie Anfang, Aufbau wie 00000–00005 |

Prüfung (Skript über die Tabelle): alle Nummern 00000–00439 außer 00359 sind je genau einer Zeile zugeordnet; 00359 liegt
in der Zeile 00340–00383, die Datei fehlt im Referenz-Repo (Lücke in der Bildfolge).

### 1.2 Kurzes Video (`TW`, 0–151)

Das Video beginnt im laufenden Spiel (Spiel 8 von 10, Spielnummer aus den Tafeln TW 00031/00094) und endet auf der Schlusswertung.

| Bilder | Bildschirm / Übergang | Anmerkung |
|---|---|---|
| 00000–00030 | **B4 Spielfeld** | 4 Panzer (rot „ZZZ“, gelb, blau, grün); Feldbeschriftung links „WARP“; Hintergrund dunkel mit Sternpunkten; 00009–00011 Einschlag, Erde stürzt nach |
| 00031 | **B5 Wertung** | „8 Games Played out of 10“ |
| 00032–00037 | **B6 Einkauf** (ZZZ) | 00032 Liste noch im Aufbau |
| 00038–00093 | B4 Spielfeld | ein Panzer wird getroffen (00059–00062), Gelände erodiert, 00066: Name in blauer Schrift (Spielerwechsel) |
| 00094 | B5 Wertung | „9 Games Played out of 10“ |
| 00095–00100 | B6 Einkauf (ZZZ) | zwei Spalten, 16 Einträge (siehe Abschnitt 4) |
| 00101 | Übergang (leer, dunkel) | |
| 00102–00149 | B4 Spielfeld | |
| 00150 | B5 Wertung „10 Games Played out of 10“ | Knöpfe **Add / Exit / Save** |
| 00151 | Übergang | Tafel kippt schräg weg |

Zählprobe: 31 + 1 + 6 + 56 + 1 + 6 + 1 + 48 + 1 + 1 = 152 Bilder.

## 2. Bildschirme

### B1 Titelbild

- **Zweck:** Start (und Ende) des Programms. Fundstellen: LP 00000–00008, 00432–00439.
- **Elemente:** Motiv: Panzer links unten vor grüner Hügelkette, orange-gelber Himmel (Rauschmuster), roter Mündungsblitz;
  Schriftzug „TANK WARS“ in zwei Zeilen; unten „Copyright 1990, 1991, 1992 Kenneth Morse“ (LP 00005–00008, wörtlich). Der
  Schriftzug erscheint von rechts nach links über mehrere Sekunden (LP 00000–00003).
- **Übergänge:** → Hauptmenü ohne sichtbaren Wisch (LP 00008 → 00009). Am Programmende wieder Titelbild nach schwarzem Bild
  (LP 00430–00432), also Rückkehr dorthin statt direktem Sprung in DOS (unsicher: kein DOS-Bild im Video).
- **Anleitung:** TXT „FILE NAMES“/„FILES“ nennt `BOMB1.RAW`/`BOMB2.RAW` als Grafikdateien (Zuordnung zu den Titelbildern ist Sache
  der Spur `formate`).

### B2 Hauptmenü

- **Zweck:** Optionen wählen, Spiel starten. Fundstellen: LP 00009–00024.
- **Elemente:** Kopfleiste „TANK WARS 3.2“ (gelb auf grau, wörtlich LP 00010); Hintergrund magentafarbene Rechteckringe;
  Kasten „Main Menu“ (blau auf grau) mit fünf Knöpfen in grüner Schrift: „Sound Options“, „Game Options“, „Players: 2“,
  „Games:10“, „START“; links ein einzelner schmaler Knopf **„Load Game“** (abgeblendet, LP 00012/00016; Text `BOMB.OVR 0x23F3`;
  vgl. DOC „LOAD MENU“). Mauszeiger vorhanden.
- **Bedienung:** Mit der Maus; laut DOC auch Tastatur (hoch/runter, links/rechts) – im Video nur Maus zu sehen.
- **Übergänge:** „Sound Options“ → B2a, „Game Options“ → B2b (Klick schließt das Popup wieder, LP 00023), „START“ → B3 (LP 00024–00025).

#### B2a Popup Sound Options (LP 00011–00014)

Kleines graues Feld links neben dem Menü, zwei Zeilen: „Effect:“ mit Wert „ON“, „Flying:“ mit Wert „OFF“ (Werte gelb; LP
00012 wörtlich). Entspricht DOC „Sound Effects (Default Yes)“ und „Fly Sound Effects (Default No)“ – **Beschriftungen weichen ab**
(Bildschirm: „Effect“, „Flying“).

#### B2b Popup Game Options (LP 00016–00022)

Graues Feld mit zehn Zeilen, Bezeichnung links, Wert gelb rechts (wörtlich, LP 00017):

| Zeile | Wert im Video | Anleitung (DOC „GAME OPTIONS“) |
|---|---|---|
| Crumble Percent: | 75% | „Crumbling Hills“, Standard 50 % |
| Walls: | NO → … → RND (Zwischenwerte unsicher, LP 00017–00021) | „Walls“, sechs Werte, Standard Random |
| Trail Visible: | YES | „Trail Visible“ |
| Ball Visible: | YES | „Shot Visible“ |
| Tanks Fall: | YES | „Tanks Fall“ |
| Computer Can Buy: (LP 00017, Zeiger überdeckt; Text `BOMB.OVR 0x1710`) | YES | „Computers Can Buy“ |
| Background: | RND | „Background“ |
| Cycle Delay: | 0 | „Palette Speed“ |
| Wind Force: | Norm | „Wind Force“ |
| Team Play: | NO | „Team Play“ |

Die Zeilen „Number of Players“ und „Games“ stehen im Hauptmenü („Players: 2“, „Games:10“), nicht im Popup.

### B3 Spielerauswahl

- **Zweck:** je Spieler festlegen, wer steuert; Namen vergeben. Fundstellen: LP 00026–00036.
- **Elemente:** gleiche Kopfleiste „TANK WARS 3.2“; zehn Felder in 2 × 5 („Player 1“ … „Player 10“, LP 00028). Jedes Feld: Kopf „Player n“ (Spielerfarbe: 1 rot, 2 grün, weitere ohne Farbe/grau
  solange nicht belegt), darunter drei Zeilen **KEYBOARD / MOUSE / COMPUTER**; die gewählte Zeile ist blau
  hervorgehoben (LP 00029: Player 1 KEYBOARD, Player 2 COMPUTER); darunter ein kleines „+“. Unten großer Knopf „START“ (gelbe
  Schmuckschrift, LP 00027).
- **Anzahl:** sichtbar sind immer zehn Felder; belegte hell, nicht belegte dunkelgrau (LP 00026). Bei „Players: 2“ sind zwei
  belegt.
- **Übergänge:** Klick auf eine Steuerungsart; bei COMPUTER → B3a; Klick auf den Namenskopf → B3b (DOC „PLAYER SELECTION“:
  Leertaste oder Klick auf das Namensfenster); „START“ → Spiel (LP 00036–00037).

#### B3a Popup Computerwahl (LP 00028)

Orange/gelbe Liste mit acht Einträgen (wörtlich): „Random (?)“, „Mr. Stupid“, „Lobber“, „Rifleman“, „Lob & Shoot (B)“, „Windless Wit“,
„Twanger“, „Wind Master“. DOC nennt sieben Computer (ohne „Random“). „(?)“ und „(B)“ sind Tasten: „?“ (und Esc) wählt „Random“,
B „Lob & Shoot“ (Text „Random (“ `BOMB.OVR 0x4CC3`; Tasten `O2:4C09`/`O2:4C2F`, `anforderungen/spielerauswahl.md`).

#### B3b Namenseingabe (LP 00030–00033)

Graues Feld „NAME:“ mit Eingabezeile; Vorgabe „Player 1_“ (LP 00030), Text wird beim Tippen ersetzt („J_“, „Javie_“, LP 00032–00033). Eingabe
mit Cursor-Unterstrich. Länge der Namen unbekannt.

### B4 Spielfeld (Runde)

- **Zweck:** Kampf. Fundstellen: siehe Tabellen 1.1/1.2 (zehn Spiele LP, drei Abschnitte TW).
- **Kopfzeile** (zwei Zeilen, LP 00041 wörtlich): links „Angle:“ (Wert gelb) und „Power:“ (Wert gelb); Mitte der Name des
  Spielers am Zug in dessen Farbe (z. B. „Javier“ rot, „Lob & Shoot“ grün; TW „ZZZ“ rot) und darunter „Wind:“ mit Zahl und Pfeil
  (z. B. „26 ←“); rechts „Direction(←)“ bzw. „(→)“ (Pfeil gelb) und „Men:“ mit Zahl (100 am Anfang; sinkt nach Treffern, z. B.
  37 bei LP 00057).
- **Waffenleiste:** unter der Kopfzeile ein Streifen mit **20 Kästchen** (LP 00041, TW 00005): gewählte Waffe weiß umrandet, geladene
  rot (TW 00005: rot „3.“ Kästchen), nicht vorhandene schwarz/unscharf; die beiden letzten tragen „H“ und „V“ (Zielhilfen, DOC
  „Guidance Systems“). Zuordnung der Symbole zu Waffen ist aus dem Video nicht lesbar (unsicher).
- **Spielfeld:** Rahmen in Violett, Hintergrund wechselt je Spiel (zehn verschiedene Muster, siehe 1.1), Gelände grün, Himmel
  Muster; Hintergrundfarben „kreisen“ (Palettenanimation, DOC „Palette Speed“, nicht aus Standbildern belegbar). Links am Rand
  senkrechte Beschriftung der Wandart (LP: „ACCELERATE“, TW: „WARP“; DOC „Walls … written down the left side“; Schreibweise
  nur halb lesbar, unsicher).
- **Elemente im Feld:** Panzer (klein, in Spielerfarbe), Flugbahnen als Spur (rot/dunkel), Explosionen als runde Bälle (rot, schwarz,
  dunkelrot, grün: LP 00063, 00097, 00136–00138, 00155, 00229, 00271–00272; Farbe je nach Waffe, Zuordnung unsicher), Erdrutsch
  als Streu aus grünen Punkten (LP 00160–00166, 00209–00211, 00234–00236) und Staub aus weißen Punkten (TW 00060–00062).
- **Ablauf in der Runde:** Aufbau des Geländes (Übergangsbild, 1.1), Spielername oben (LP 00040), Zug der Spieler abwechselnd
  (Namenswechsel in der Kopfzeile: LP 00040–00051), Schuss, Spuren bleiben mehrere Züge stehen (mehrere Bögen sichtbar, LP
  00060–00063), Rundenende bei letzter Zerstörung (Ball + Wertung).
- **Übergänge:** nach dem Rundenende direkt B5 (LP 00063 → 00064; 00105 → 00106); Aufbau des nächsten Spiels über
  Übergangsbilder (1.1).

### B5 Wertung (Status)

- **Zweck:** Zwischenstand nach jeder Runde. Fundstellen: LP 00064–00066, 00106–00108, 00140–00141, 00167–00168, 00212–00213,
  00237–00238, 00280–00281, 00329–00330, 00385–00386, 00427–00430; TW 00031, 00094, 00150–00151.
- **Elemente** (wörtlich, LP 00064): Hintergrund magentafarbene Waagerechtstreifen; Titelkasten „1 Game Played out of 10“ (später
  „2 Games …“, „10 Games Played out of 10“); Tabelle mit Spaltenkopf „Player“ / „Wins“ / „Score“ und je Spieler einer Zeile „1. Javier“,
  Siege, Punkte (Beispiel „1. Javier 1 4260“, „2. Lob & Shoot 0 0“; Name in Spielerfarbe, Punkte in Spielerfarbe); darunter drei
  Knöpfe **Add**, **Continue**, **Save**. Bei vier Spielern vier Zeilen (TW 00031/00150).
- **Letzte Wertung:** TW 00150 und LP 00428 zeigen statt „Continue“ den Knopf **Exit** („Add / Exit / Save“; Texte
  `BOMB.OVR 0xE52D`/`0xE536`).
- **Abweichung von DOC** („STATUS SCREENS“): dort „OK, ADD, and SAVE“ und zwei Tafeln bei Team Play; im Video heißt der erste Knopf
  „Continue“ und nur eine Tafel ist zu sehen (kein Team Play).
- **Übergänge:** „Continue“ → Einkauf oder nächste Runde (LP 00067–00068); „Add“ und „Save“ nicht im Video angeklickt.

### B6 Einkauf (Equipment)

- **Zweck:** Waffen kaufen. Fundstellen: LP 00068–00072, 00109–00114, 00169–00174, 00214, 00239–00244, 00282–00286, 00331–00338,
  00387–00393; TW 00032–00037, 00095–00100.
- **Elemente** (wörtlich, LP 00172, TW 00100): Kopfbalken links „YOU HAVE UNALLOCATED:“ mit Knopf „Purchase“ und darunter
  „2930 DOLLARS“ (TW 00100) bzw. „5260 DOLLARS“ (LP 00172); Mitte Spielername in dünner roter Schrift („ZZZ“, „Javier“); rechts „YOU
  CURRENTLY HAVE:“ mit Knopf „Done“ und „12930 DOLLARS“ (TW 00100). Darunter Waffenkacheln (ein oder zwei Spalten), je Kachel:
  Name, „<Anzahl> FOR <Preis>“, ein kleines „+“, rechts ein Kästchen zum Anwählen; kleine gelbe Zahl rechts = Bestand (z. B.
  „8“ bei Incinerator, „1000“ bei Laser, TW 00100); angewähltes Kästchen mit Menge „1“.
- **Anzeige hängt vom Geld ab:** es erscheinen nur bezahlbare Waffen (LP 00069: drei Einträge nach Spiel 1; TW 00100: 16
  Einträge); die Liste baut sich von oben nach unten auf (TW 00032, LP 00109).
- **Bedienung laut DOC „BUYING EQUIPMENT“:** `+`/`-`, Maus rechts/links, `P` = Purchase, `D` = Done; im Video nur Maus.
- **Übergänge:** „Done“ → nächstes Spiel (LP 00072 → 00073; TW 00100 → 00101).
- **Computer:** Der Einkauf des Computergegners wird nur als sehr kurzes Bild gezeigt (LP 00214, 00338, Name „Lob & Shoot“ in
  Grün; LP 00214 Liste leer, LP 00338 sechs Einträge) – DOC „Computers Can Buy“.
- **Fehlender Einkauf:** Nach Spiel 3 (LP 00140–00142) folgt kein Einkaufsbild (Grund unklar; DOC: nur wer genug Punkte hat).

## 3. Ablauf einer Partie als Zustandsfolge

Aus beiden Videos (Belege je Schritt in Klammern):

1. **Titel** (LP 00000–00008) → Hauptmenü.
2. **Hauptmenü** (LP 00009–00024): Optionen über zwei Popups (B2a, B2b) und die Felder „Players“/„Games“ einstellen → „START“.
3. **Spielerauswahl** (LP 00026–00036): je Spieler Steuerung wählen (KEYBOARD/MOUSE/COMPUTER), Computergegner aus Liste, Name
   eintippen → „START“. Zwischen Auswahl und Spiel ein Wischeffekt (LP 00037) und ein schwarzes Bild (LP 00038).
4. **Schleife je Spiel (1 bis N, hier N = 10):**
   a. **Spielfeld** (B4): Aufbau, Panzer setzen, Züge bis nur noch ein Panzer übrig ist (LP 00039–00063).
   b. **Wertung** (B5) (LP 00064–00066), „Continue“.
   c. **Einkauf** (B6), je Spieler, falls genug Geld (LP 00068–00072); sonst direkt zu d (LP 00140 → 00142).
   d. **Übergangsbild** mit neuem Hintergrund (LP 00073, 00115, 00142, 00175, 00245, 00287, 00339), dann a.
5. **Nach dem letzten Spiel:** Wertung „10 Games Played out of 10“ (LP 00427–00430, TW 00150) mit Knöpfen Add/Exit/Save, schwarzes Bild
   (LP 00431), **Titelbild** (LP 00432–00439).

Nicht im Video: Wahl „Add“ (weitere Spiele), „Save“ (Speichermenü), „Load“, Team Play, Statusabfrage mit Zifferntasten, Pause, Boss-Taste.

## 4. Waffen und Preise

Alle Angaben aus der Einkaufstafel TW 00100 (vollständigste Liste, links nach rechts, oben nach unten); Form „Anzahl FOR Preis“
(Preis in Dollar). Bildqualität: Ziffern bei 3000/8000/10000 leicht verwechselbar (unsicher), Rest gut lesbar.

| Waffe (wörtlich) | Menge FOR Preis | Fundstelle | Anmerkung |
|---|---|---|---|
| Incinerator | 100 FOR 3000 | TW 00100, LP 00172 | DOC „Standard Incinerator“ |
| Mark II Incin. | 10 FOR 4000 | TW 00100, LP 00172 | DOC „Mark II Incinerator“ |
| 20 Kilo Nuke | 2 FOR 7000 | TW 00100 | DOC „Nukes“ |
| 5 Meg Nuke | 1 FOR 10000 | TW 00100 | DOC „Nukes“ |
| Laser | 1000 FOR 10000 | TW 00100 | DOC „Lasers“: 1000 Einheiten Energie |
| CRI 4/D | 5 FOR 5000 | TW 00100, LP 00172 | DOC „Chain Reaction Inducers“ |
| MIRV | 3 FOR 8500 | TW 00100 | DOC „MIRVs“ |
| CRI 8/D | 3 FOR 10000 | TW 00100 | |
| CRI 20/ND | 3 FOR 10000 | TW 00100 | |
| Sonic Blaster | 10 FOR 4500 | TW 00100, LP 00172 | |
| Ball 'O' Dirt | 10 FOR 4000 | TW 00100, LP 00172 | DOC „Balls 'o' Dirt“ |
| Mark II Dirt | 7 FOR 6000 | TW 00100 | |
| Pressured Dirt | 3 FOR 8000 | TW 00100 | |
| High Pres. Dirt | 1 FOR 10000 | TW 00100 | |
| Xplosive Dirt | 10 FOR 4500 | TW 00100, LP 00172 | DOC „Explosive Dirt“ |
| Inertia Damp. | 100% FOR 10000 | TW 00100 | blau beschriftet (Abwehrsystem, DOC „Defense Systems“) |

Nicht in der Tafel TW 00100: **CRI 20/D** (1 FOR 15000; `BOMB.EXE 0x1886C`, bei 12930 Dollar zu teuer) sowie Lead Ball und
Hand Grenade (DOC „WEAPONS“: „The first three are available when the game begins“; in `BOMB.EXE` Menge 0 und Preis −1, der
Einkauf überspringt Menge 0, `O3:020B`/`BOMB.OVR 0x08C97` – nicht käuflich, Beleg `anforderungen/wertung-einkauf.md` §1). Die
15 Waffen der Tafel plus diese drei ergeben die 18 Waffennamen von `BOMB.EXE` (`0x16C6C` ff.) und DOC. Ebenso fehlen die
Zielhilfen („Guidance Systems“, Tasten H/V, je 20000) und zwei Abwehrsysteme (Energy Shield 20000, Repulser 15000) – zu teuer
(`Core/Economy/ItemCatalog`). Ob die Tafel unten weitere Zeilen hat, ist am Bildrand nicht sicher (TW 00100 schneidet unten ab).

## 5. Abgleich mit der Anleitung (`BOMB.DOC`)

| Thema | DOC | Video | Befund |
|---|---|---|---|
| Hauptmenü | Sound Options, Game Options, Players, Games, Start | alle fünf (LP 00010) | stimmt; **Start** heißt „START“ |
| Optionsnamen | „Sound Effects“, „Fly Sound Effects“, „Shot Visible“, „Palette Speed“, „Crumbling Hills“ | „Effect“, „Flying“, „Ball Visible“, „Cycle Delay“, „Crumble Percent“ | Namen weichen ab (DOC beschreibt BOMBCFG-Namen) |
| Standardwerte | Crumble 50 %, Walls Random | 75 %, wechselnd | Werte stammen aus Spielerkonfiguration |
| Wind-Stufen | fünf von None bis Gale, plus RND | „Norm“ | nur ein Wert sichtbar |
| Spielerauswahl | K/M/C wählen, Name per Leertaste | KEYBOARD/MOUSE/COMPUTER, Namensdialog | stimmt |
| Computergegner | sieben | acht Zeilen mit „Random (?)“ | zusätzlich „Random“; „(?)“ und „(B)“ sind Tasten (B3a) |
| Kopfzeile Spiel | Power, Angle, Direction, Men, Wind | stimmt (LP 00041) | |
| Waffenleiste | 20 Kästchen, schwarz/weiß/rot | stimmt (TW 00005) | |
| Wertung | Knöpfe OK, ADD, SAVE; zwei Tafeln bei Team Play | Add, Continue, Save; letzte Tafel mit Exit | Beschriftung weicht ab (Fassung?) |
| Einkauf | Liste kaufbarer Waffen, `+`/`-`, P, D | stimmt; „Purchase“, „Done“ | stimmt |
| Waffenanzahl | 18 Waffen, 2 Zielhilfen, 3 Abwehr | 15 + 1 in der Tafel | siehe Abschnitt 4 |
| Spiele | Standard 10 | „Games:10“, „out of 10“ | stimmt |
| Titelbild | Copyright 1990, 1991, 1992 | wörtlich so (LP 00005) | stimmt |

## 6. Fehlende Bildschirme für die DOSBox-Aufnahme

Diese Zustände kommen in den Videos nicht oder nicht lesbar vor und sollten bei der Aufnahme `runde-1` (DOSBox, 320 × 200,
verlustfrei, `docs/vorlagen/dosbox-aufnahme.md`) enthalten sein:

1. **Hauptmenü vollständig:** Knopf „Load Game“ nicht abgeblendet (mit Spielständen) und der Ladedialog, alle Werte der Game-Options-Zeilen (Walls sechs Werte, Background-Namen,
   Wind None…Gale, Cycle Delay, Team Play).
2. **Spielerauswahl:** alle zehn Spieler belegt, Zustand „MOUSE“ und der Hinweis „+“, Computerliste, Namenseingabe leer.
3. **Team-Play-Bildschirm** und Teamnamen-Eingabe (DOC „TEAM PLAY“) sowie zweite Wertungstafel.
4. **Speichermenü** (Cancel, New Name, Save, Save & Quit, AutoSave), **Ladedialog** (DOC „LOAD MENU“).
5. **Wertung** komplett: „Add“ (Spiele hinzufügen), Statistik pro Spieler (Klick auf den Namen: Treffer, Tötungen), letzte Tafel (Exit).
6. **Einkauf:** volle Tafel mit allen 21 Einträgen inklusive Zielhilfen H/V und drei Abwehrsystemen; Anzeige mit sehr wenig Geld.
7. **Spielfeld:** Statusfenster bei Zifferntasten 1–0, Pause (Strg-P), Zielwahl der Zielhilfen, Wandart-Beschriftungen
   (alle sechs), Aufbau der Panzer („fallen vom Himmel“, DOC „PLAYING THE GAME“), Laser, Blitz („lightning bolts“, DOC „Mass Kill“).
8. **Boss-Taste F10:** vorgetäuschte DOS-Eingabeaufforderung.
9. **Fehlerfälle:** fehlende Konfiguration, fehlende Grafikdateien (TXT „GRAPHICS FILES“ – Wortlaut prüfen), keine Maus.
10. **BOMBCFG.EXE** (eigenes Programm, nicht Teil der Videos); Aufruf `BOMB /NEW` (DOC „QUICK START“).
11. **Vergleichsbilder 320 × 200** für jeden oben beschriebenen Bildschirm (B1–B6) zur pixelgenauen Prüfung; die Videobilder
    reichen dafür nicht.

## 7. Offene Unsicherheiten (Kurzliste)

- Werte der Walls-Zeile (LP 00017–00021).
- Zuordnung der Explosionsfarben und der Symbole in der Waffenleiste zu Waffen.
- Grund für den fehlenden Einkauf nach Spiel 3.
- Ob die Einkaufstafel unten weitere Zeilen hat (Bildrand).
