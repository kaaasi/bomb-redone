# `*.TWS` – Spielstände (Release 3.2)

Spur `tws-format`, Runde 1 (GR1-1 = C: nur Analyse und Doku, R1-D3 = A: Ziel ist Lesen). Stand 11.11.2026, Claude Opus 5.5.
Kein Leser in `src/` (folgt in Runde 2); kein Originalspielstand im Referenz-Repo – alles hier stammt aus dem Maschinencode.

**Beleg für jede Aussage:** `python3 tools/reverse-engineering/tws.py befunde` leitet den Aufbau aus der Schreib- und der
Leseroutine ab und prüft die Befunde T1–T7 gegen `gamedata/` (Ergebnis zum Head: **29 von 29 bestätigt**). Die Nummer steht hinter
jeder Aussage; „unbelegt“ heißt: nicht aus dem Code bestimmt. Weitere Aufrufe: `… aufbau --spieler N --computer 2,5` (Tabelle mit
Dateioffsets), `… satz` (Zugriffe auf die Spielersätze), `… xref <adresse>` (Zugriffe auf eine Variable), `… lesen <datei>`
(Spielstand zerlegen, Ausgabe nur auf dem Bildschirm), `… selbsttest` (synthetische Dateien, ohne Originaldaten). Werkzeuge:
Python 3, `ndisasm` (Paket `nasm`); Zwischendateien nur in `analyse/tws/`.

**Adressen** wie in [`../analyse/re-grundlagen.md`](../analyse/re-grundlagen.md): `On:Ofs` = Overlay n, `Seg:Ofs` = Segment des
Ladebilds von `BOMB.EXE`, `DS:xxxx` = Datensegment `14D5`, `U129` = Spieleinheit `0129`. Die Befundliste nennt die Dateioffsets.

## Quellen

| Was | Prozedur | Datei | Befund |
|---|---|---|---|
| Sichern | `O3:3B54` (O3[3]) | `BOMB.OVR 0x0C5E0` | T2 |
| Laden | `O3:3FE4` | `BOMB.OVR 0x0CA70` | T3 |
| Dialog „SAVE GAME“ | `O3:46DE` | `BOMB.OVR 0x0D16A` | T6b |
| Dialog „Select Game to Load:“ | `O3:4D97` (O3[2]) | `BOMB.OVR 0x0D823` | T6d |
| Intelligenz sichern / laden | `U129:00C6` / `U129:0135` | `BOMB.EXE 0x029F6` / `0x02A65` | T5a |
| Laufzeit: Assign, Reset, Rewrite, BlockRead, BlockWrite, Close, Halt | `12CE:1C74`, `1CA2`, `1CAB`, `1D8D`, `1D94`, `1D23`, `00E9` | `BOMB.EXE 0x15FF4` ff. | T1 |

## Aufbau

Ungetypte Datei mit Satzgröße 1 (`Rewrite(f, 1)` / `Reset(f, 1)`), geschrieben als Folge von `BlockWrite` direkt aus den globalen
Variablen, gelesen mit `BlockRead` in derselben Reihenfolge und Größe (T2a, T2b, T3a, T3b). Keine Prüfsumme, kein Längenfeld;
Zahlen Little Endian; 1-Byte-Felder sind Turbo-Pascal-`Boolean` oder `Byte`, 2-Byte-Felder `Integer`/`Word` (Vorzeichen nur wo
genannt belegt).

**Länge** = 654 + 118 × Spielerzahl + 7 × Computerspieler Byte (30 Kopf + 41 global + je Spieler 118 [+ 7] + 583 Teams;
`… aufbau`, `… selbsttest`). Beispiel: 2 Spieler ohne Computer 890 Byte, 10 Computer 1.904 Byte.

### Kopf (Offset 0, 30 Byte)

Text ohne Längenbyte: `Tank Wars Saved Game`, CR LF, `V`, die Versionskennung `DS:0056`, CR LF, ^Z (1Ah) (T4). Die Kennung ist
derselbe `string[4]` „3.2“ + NUL wie in `BOMB.CFG` ([konfiguration.md](konfiguration.md)), wird zur Laufzeit nur gelesen (T4b) und
steht hier **ohne** ihr Längenbyte, also als 4 Zeichen `3.2` + NUL.

### Globale Werte (Offset 30, 41 Byte)

| Offset | Byte | Variable | Bedeutung | Beleg |
|---:|---:|---|---|---|
| 30 | 2 | `DS:47D8` | unbelegt; nur in O6 benutzt (gesetzt auf 2, 4, 10, 15, 30, 60, als Teiler) | `xref 47D8` |
| 32 | 2 | `DS:47DA` | Spielerzahl, Obergrenze der Spielerschleife | T2c |
| 34 | 2 | `DS:47E2` | laufendes Spiel der Schlacht; Einkauf nur, solange ≠ `DS:E8A6` | T6a (`O3:79A5`), `wertung-einkauf.md` |
| 36 | 2 | `DS:47E4` | Wind dieser Runde: Option `DS:E8A8`, bei 5 (Random) `Random(5)` | T7a |
| 38 | 2 | `DS:47E6` | Spielernummer, Hilfswert der Runde (Satz +28h eines Spielers, danach Satzindex) | T7f |
| 40 | 2 | `DS:47E8` | unbelegt; im Code kein direkter Zugriff | `xref 47E8` (0 Stellen) |
| 42 | 2 | `DS:47EA` | Durchgangszähler der Runde: 1, +1 bei jedem Umlauf über alle Spieler; Vergleich mit 10 (`O4:4D73`) | T7c |
| 44 | 2 | `DS:47EC` | Wände dieser Runde: Option `DS:E8AA`, bei 5 (Random) `Random(5)` | T7b |
| 46 | 1 | `DS:47EE` | Spieler am Zug (nächster Satz mit +23h ≠ 0) | T7c |
| 47 | 1 | `DS:4812` | alle Spieler Computer (START der Spielerauswahl) | `spielerauswahl.md` §3 |
| 48 | 1 | `DS:4814` | Wände dieser Runde in {1, 2, 3} (Sticky, Elastic, Accel) | T7b |
| 49 | 1 | `DS:4815` | Wände dieser Runde = 4 (Warp) | T7b |
| 50 | 2 | `DS:E8A2` | Windwert der Runde, `Integer` mit Vorzeichen, begrenzt auf −30000..30000; nicht in `BOMB.CFG` | T7a, T7d |
| 52 | 2 | `DS:E8A4` | Crumble Percent | T7d |
| 54 | 2 | `DS:E8A6` | Spiele je Schlacht (GamesToPlay) | T7d |
| 56 | 2 | `DS:E8A8` | Wind (Option) 0..5 | T7d |
| 58 | 2 | `DS:E8AA` | Wände (Option) 0..5 | T7d |
| 60 | 2 | `DS:E8AC` | Hintergrund (Option) 0..10 | T7d |
| 62 | 2 | `DS:E8AE` | Hintergrundmaske (Bit n = Hintergrund n) | T7d |
| 64 | 1 | `DS:E8B2` | Fluggeräusch | T7d |
| 65 | 1 | `DS:E8B3` | Toneffekte | T7d |
| 66 | 1 | `DS:E8B4` | Schuss unsichtbar (Inviso) | T7d |
| 67 | 1 | `DS:E8B5` | Spur sichtbar (Trail) | T7d |
| 68 | 1 | `DS:E8B8` | Panzer fallen (FallDown) | T7d |
| 69 | 1 | `DS:E8BA` | Computer kaufen (CompuBuy) | T7d |
| 70 | 1 | `DS:E8BC` | Team Play | T7d |

T7d: Die Variablen ab `DS:E8A4` sind genau die, die der Leser von `BOMB.CFG` (`O2:6611`) füllt; Werte und Namen stehen in
[konfiguration.md](konfiguration.md). Ein geladener Spielstand überschreibt also diese Optionen aus `BOMB.CFG`.

### Je Spieler i = 1..Spielerzahl (ab Offset 71)

| Byte | Variable | Inhalt | Beleg |
|---:|---|---|---|
| 116 | `DS:402E + 74h·i` | Spielersatz i (unten) | T2b, T2c |
| 2 | `DS:E5A6 + 2i` | Steuerart: 1 Tastatur, 2 Maus, 3 Computer (`Core/Setup/PlayerControl`) | T2c, `spielerauswahl.md` |
| 7 | Intelligenz | **nur bei Steuerart 3**: Objekt der Tabelle `DS:3BE8 + 4i` (unten) | T2c, T5a |

Satz 0 (Tauschpuffer der Wertung) und die Sätze über der Spielerzahl werden nicht gespeichert. Höchstens 10 Spieler: Laden leert
11 Zeiger `DS:3BE8` (Spieler 0..10, T3c).

**Intelligenz (7 Byte).** Turbo-Pascal-Objekt aus `U129` (VMT-Feld bei Self+12h, T5b); gespeichert werden vier Felder (T5a):

| Byte | Feld | Inhalt | Beleg |
|---:|---|---|---|
| 2 | Self+00h | Art 1..7: Mr. Stupid, Lobber, Rifleman, Lob & Shoot, Windless Wit, Twanger, Wind Master | T5b, T5d |
| 1 | Self+0Bh | unbelegt (Init setzt 0) | T5a |
| 2 | Self+0Eh | beim Anlegen `Random(4)+1`, wiederholt bis in der Menge der Art (Art 3 und 6: {1..4}, sonst {1, 2, 4}); Zweck unbelegt | T5c |
| 2 | Self+10h | Spielernummer (Index des Satzes, den die Intelligenz liest) | T5b |

Laden liest zuerst die Art in eine lokale Variable, ruft den Konstruktor der Art (dieser ruft `Init` und verbraucht dabei
`Random`) und dann `U129:0135`, das die übrigen drei Felder überschreibt (T3b, T3e, T5c). Andere Felder (+02h..+0Ah, +0Ch) setzt
`Init` neu, sie stehen nicht in der Datei. **Folge:** Laden ändert `RandSeed` (je Computer mindestens ein `Random(4)`); `RandSeed`
selbst wird nicht gespeichert. Eine Art außerhalb 1..7 lässt den Zeiger nil, `Load` wird trotzdem gerufen (T3e).

### Teamtabelle (am Ende, 583 Byte)

`DS:4838`, 11 Sätze zu 53 Byte (Team 0..10; T7e): Name `String[13]`, +0Eh „alle Computer“, +0Fh Mitgliederzahl
(`spielerauswahl.md` §8). Wird immer gespeichert, auch ohne Team Play.

## Spielersatz (0x74 Byte)

Belegte Felder aus anderen Spuren und Zugriffe nach `… satz` (Offset im Satz; Breite ist die des Zugriffs, ein LongInt erscheint
als zwei Wörter). Alles andere: unbelegt.

| Satz+ | Typ | Inhalt | Beleg |
|---|---|---|---|
| 00h | Byte | Spielernummer | `spielerauswahl.md` §1 (`O2:0216`) |
| 01h | `String[13]` | Name; bei Computern zugleich Name der Intelligenz (`O2:0078`) | `spielerauswahl.md` §1, T5d |
| 0Fh, 11h | Word | unbelegt (über 100 Zugriffe, meist `U129`) | `satz` |
| 13h | LongInt | Punkte | `wertung-einkauf.md` §2 |
| 17h | LongInt | ausgegebenes Geld (Geld = Punkte − ausgegeben) | `wertung-einkauf.md` §2 |
| 1Bh | Word | unbelegt | `satz` |
| 1Dh, 1Fh | Word | Rohrwinkel und Richtungsfaktor (Zeichnen des Panzers) | `spielerauswahl.md` §7 |
| 21h | Word | unbelegt | `satz` |
| 23h | Byte | ≠ 0: Spieler kommt an die Reihe (im Spiel) | T7c |
| 24h, 26h | Byte/Word | unbelegt | `satz` |
| 28h | Word | Nummer eines anderen Spielers | T7f |
| 2Ah | Word[2] | Lenksysteme (Reihenfolge `Core/Economy/ItemCatalog.GuidanceTable`) | `wertung-einkauf.md` §2 |
| 2Eh | Word[18] | Waffen (`ItemCatalog.WeaponTable`) | `wertung-einkauf.md` §2 |
| 52h | LongInt[3] | Abwehr (`ItemCatalog.DefenseTable`) | `wertung-einkauf.md` §2 |
| 5Eh | Word | unbelegt | `satz` |
| 60h, 62h, 64h | Word | Farbteiler der Spielerfarbe | `spielerauswahl.md` §1–2 |
| 66h | Word | Team | `spielerauswahl.md` §6 |
| 68h | Word | unbelegt | `satz` |
| 6Ah | Word | gewonnene Spiele | `Core/Economy/PlayerAccount` |
| 6Ch, 6Eh, 70h | Word | unbelegt (u. a. `O3:68E4`, `O4`) | `satz` |
| 72h | Word | unbelegt; +1 in `O4:4766` | `satz` |

Bezug zu #71: Die Wertung sortiert die Sätze dauerhaft um (`O3:0000`). Das Autosave schreibt **vor** der Wertung, der Dialog
**danach** (T6a) – die Satzreihenfolge der beiden Stände kann sich also unterscheiden. Ob `O3:0000` auch `DS:E5A6` und
`DS:3BE8` mittauscht (sonst passen Steuerart und Intelligenz nicht mehr zum Satz), ist unbelegt und gehört zu #71.

## Verhalten

- **Wann gesichert wird** (T6a–T6c): nach jedem Spiel einer Schlacht (`O3:796B`): mit Autosave (`DS:4818`) unter dem Namen
  `DS:4822`, danach Wertung; ist „Save“ in der Wertung gewählt (`DS:4816`), Dialog `O3:46DE` nach der Einzel- und nach der
  Teamwertung. Dialog: **S** Sichern, **N** neuer Name, **C** Abbruch, **A** Autosave umschalten, **Q** Sichern und Programmende;
  Vorschlag `GAMEn` mit dem ersten n ohne Datei. Boss-Taste (`UC02`): Sichern unter einem Pfad + `\BOSS` (also `…\BOSS.TWS`; Herkunft des Pfads nicht untersucht), dann `Halt(0)`.
  Ein Stand liegt damit immer **zwischen zwei Spielen**; Gelände und Panzerlage der Runde stehen nicht in der Datei.
- **Laden** (T6d): nur über „Load Game“ im Hauptmenü (`DS:4817`); Auswahl aus `*.TWS` im aktuellen Verzeichnis (Namen bis 10
  Zeichen), danach ohne Spielerauswahl und Teams weiter in der Spielfolge. Keine Datei: „No Save Files Found“, Taste, `Halt(0)`.
  Wie die Spielfolge nach dem Laden fortsetzt (ab `DS:47E2`), ist in `O3:7847` nicht untersucht – unbelegt.
- **Prüfung** (T3d): nur der Kopf (Pascal-Vergleich über die 30 Byte); ungleich → „Save File is not valid!“, Taste, `Halt(0)`.
  Sonst keine Prüfung: kein E/A-Fehlertest (`{$I-}`, T2d); eine zu kurze Datei lässt die übrigen Variablen teilweise unverändert
  (`BlockRead` ohne Ergebnisvariable setzt nur `InOutRes`, T1c), zusätzliche Byte werden nicht gelesen.
- **Nicht gespeichert:** `RandSeed`, die Geschwindigkeits- und Anzeigeoptionen aus `BOMB.CFG` (TS, FS, ES, SS, LS, Maus,
  Palette, Video, Boss-Speicher), Sätze und Steuerarten über der Spielerzahl, Laufwerte der Runde außer den globalen Werten oben.

## Für den Leser in Runde 2

Folge aus T2/T3 (Vorschlag, `[APPROX]` bis ein Originalstand vorliegt): Kopf exakt vergleichen, dann die 26 globalen Werte, je
Spieler Satz + Steuerart und bei Steuerart 3 die 7 Byte der Intelligenz; Länge nach der Formel prüfen. Satzfelder nur übernehmen,
soweit oben belegt; den Rest roh behalten (R1-D3 B später: bytegleiches Schreiben).

## Benötigte Originalstände (DOSBox-Aufnahme)

Noch kein `*.TWS` im Referenz-Repo (`original/spielstaende/`). Für den Abgleich (`tws.py lesen`) und Tests des Lesers – je Stand
die Einstellungen und den Spielverlauf notieren (`docs/vorlagen/dosbox-aufnahme.md`):

1. **Zwei Menschen, kein Team Play**, Voreinstellungen, nach Spiel 1 mit dem Dialog gesichert (Name merken) – Grundfall, Länge 890.
2. **Derselbe Spielverlauf mit Autosave** – Vergleich Autosave (vor der Wertung) und Dialog (danach): Satzreihenfolge (#71).
3. **Zehn Spieler, davon Computer jeder Art** (Mr. Stupid … Wind Master, dazu Random) – Intelligenzblöcke, Länge, Arten 1..7.
4. **Team Play mit drei Teams**, gemischt Mensch/Computer – Teamtabelle, Satz +66h.
5. **Nach einem Einkauf** mit Waffen, Lenksystem und Abwehr – Satz +2Ah..+5Dh, +13h/+17h.
6. **Optionen abweichend** von `BOMB.CFG` (Wind Random, Wände Random, Hintergrund Auswahl, Crumble ≠ 10, Inviso, Trail aus) –
   globale Werte, `DS:47E4`/`DS:47EC` gegen die Optionen.
7. **Boss-Taste** während einer Runde – `BOSS.TWS`: welche Rundenwerte (`DS:47EA`, `DS:47EE`, `DS:E8A2`) mitten im Spiel stehen.
8. Danach je Stand **Laden** und ein Bildschirmfoto der ersten Wertung bzw. Runde – Verhalten nach dem Laden (`O3:7847`).
