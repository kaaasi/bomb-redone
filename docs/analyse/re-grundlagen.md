# Analyse `re-grundlagen` – Programmaufbau von `BOMB.EXE` / `BOMB.OVR`

Spur `re-grundlagen`, Runde 1 (Stand 11.11.2026, Claude Opus 5.5). Eigene Beschreibung mit Dateioffsets; Listings und
Texttabelle liegen nur im Referenz-Repo (`analyse/re-grundlagen/`, R1-D2 = B, R1-D4 = A).

**Beleg für jede Aussage:** `python3 tools/reverse-engineering/bombre.py befunde` prüft die Befunde A1–H6 gegen die
Originaldateien in `gamedata/` (Ergebnis zum Head: **40 von 40 bestätigt**), `… karte` die Bildschirmkarte (26 von 26),
`… segments`, `… overlays` und `… fonts` erzeugen die Tabellen unten. Die Befundnummer steht hinter jeder Aussage.
Werkzeuge: Python 3 und `ndisasm` (Paket `nasm`, nur für `listings`).

**Adressen:** `Seg:Ofs` bezieht sich auf das Ladebild von `BOMB.EXE`: Dateioffset = `0x16A0 + Seg × 16 + Ofs`. Overlay-Code:
`On:Ofs` = Overlay n, Offset im Overlay; Dateioffset = Anfang des Overlays in `BOMB.OVR` + Ofs. Variablen `DS:xxxx` liegen im
Datensegment `14D5`.

## 1. Aufbau von `BOMB.EXE`

- DOS-MZ, nicht gepackt: Kopf 5.792 Byte, Ladebild 100.400 Byte bis zum Dateiende, 1.439 Relokationen, Einsprung `0000:00FA`,
  Stapel `23C0:2800` (A1).
- Turbo Pascal 6.0: Copyright-Text der Laufzeit im System-Segment (`BOMB.EXE 0x14599`, A2); System-Init setzt `DS = 14D5` (A3).
- 28 Segmente (aus Relokationen und Overlay-Fixups), davon 10 Overlay-Stubs (A4). Zuordnung:

| Segment | Datei | Byte | Inhalt | Beleg |
|---|---|---:|---|---|
| `0000` | 0x016A0 | 1.344 | Hauptprogramm (Startschritte als Texte, Ablauf §6) | F5 |
| `0054` | 0x01BE0 | 80 | Stub O1 (Titel) | B2 |
| `0059` | 0x01C30 | 3.328 | Spieleinheit: Speicherübersicht, EMS-/Overlay-Meldungen | Texte |
| `0129` | 0x02930 | 12.416 | Spieleinheit ohne Texte, viel Real48 und `Random` (Kandidat: Computergegner) | `[APPROX]` |
| `0431` | 0x059B0 | 27.920 | Spieleinheit: Dialoge, Statusfenster, Tastenliste; eingebunden VGA256-Treiber und 3 Schriften | F1–F4 |
| `0B02`–`0B20` | 0x0C6C0 | je 48–96 | Stubs O2–O10 | B1 |
| `0B25` | 0x0C8F0 | 3.536 | Einheit mit Namenssätzen der Graph-Prozeduren (Zweck offen) | Texte |
| `0C02` | 0x0D6C0 | 1.120 | Notsicherung „BOSS“ bei Speichermangel | Texte |
| `0C48` | 0x0DB20 | 784 | Tastatur-Handler INT 09h („XKEY“) | G5 |
| `0C79` | 0x0DE30 | 1.264 | Palettenrotation per Interrupt („CYCLER“) | G3, G4 |
| `0CC8` | 0x0E320 | 3.920 | eigener VGA-Zugriff: Modus 13h, A000h, DAC, Strahlrücklauf | G1, G2 |
| `0DBD` | 0x0F270 | 1.344 | Grafikstart: InstallUserDriver/InstallUserFont, Fehlermeldung, PIC | F6 |
| `0E11` | 0x0F7B0 | 272 | Maus (INT 33h) | G6 |
| `0E22` | 0x0F8C0 | 64 | Borland `Printer` | C6 |
| `0E26` | 0x0F900 | 15.360 | Borland `Graph` (BGI-Kern) | C5 |
| `11E6` | 0x13500 | 288 | Borland `Dos` | C4 |
| `11F8` | 0x13620 | 1.568 | Borland `Crt` | C1–C3 |
| `125A` | 0x13C40 | 1.856 | Borland `Overlay` (EMS) | B3 |
| `12CE` | 0x14380 | 8.304 | Borland `System` (TP-6-Laufzeit, Real48, Zufall) | A2, D, E |
| `14D5` | 0x163F0 | 15.072 | Datensegment (Waffennamen, Computernamen, typisierte Konstanten) | A3 |

Initialisierung am Einsprung (`BOMB.EXE 0x179A`): System, Crt, Graph, Printer, dann die Spieleinheiten `0E11`, `0DBD`, `0CC8`,
`0C79`, `0C48`, `0C02`, `0B25`, `0129`, `0059` (`bombre.py segments`). **Spielcode** ist damit alles außer den sechs
Borland-Einheiten `0E22`, `0E26`, `11E6`, `11F8`, `125A`, `12CE` und den Treiber-/Schriftdaten in `0431`.

## 2. Aufbau von `BOMB.OVR`

Kopf `FBOV` + Länge (Dateilänge − 8), danach 10 Overlay-Einheiten lückenlos hintereinander, je Code und Fixup-Tabelle
(Wortliste der Offsets, an denen ein Segmentwort relativ zum Ladebild steht) (B1). Der Stub im EXE beginnt mit `INT 3Fh`,
enthält Dateioffset, Code- und Fixup-Größe und je Einsprung 5 Byte `INT 3Fh` + Zieloffset (ab Stub-Offset `0x20`).

| Nr | Stub | OVR-Offset | Code | Einsprünge | Inhalt (aus Texten und Aufrufen, §6) |
|---|---|---|---:|---:|---|
| O1 | `0054` | 0x00008 | 1.394 | 1 | Titelfolge (`BOMB1.RAW`/`BOMB2.RAW`) |
| O2 | `0B02` | 0x005E8 | 31.788 | 10 | Prozessortest, Maustest, `BOMB.CFG` und Befehlszeile, Hauptmenü, Game Options, Spieler, Teams |
| O3 | `0B08` | 0x08A8C | 31.357 | 4 | Spielfolge (Runden), Einkauf, Wertung, Statistik, Sichern/Laden, Endergebnis |
| O4 | `0B0C` | 0x10D05 | 20.211 | 2 | Runde: Kopfzeile, Zielen, MIRV-Einstellung; ruft O5–O9 |
| O5 | `0B0F` | 0x1613C | 11.104 | 6 | ohne Texte; viel `Random`, Ton, Real48 (Kandidat: Schuss und Explosion) `[APPROX]` |
| O6 | `0B13` | 0x18F2C | 6.348 | 2 | ohne Texte; Ton, `Random` `[APPROX]` |
| O7 | `0B16` | 0x1A996 | 5.576 | 4 | ohne Texte; Verzögerungen `TS`/`FS` (Kandidat: Turm, Fallen) `[APPROX]` |
| O8 | `0B1A` | 0x1C0C0 | 7.965 | 2 | Spielfeldaufbau mit senkrechter Wandbeschriftung links |
| O9 | `0B1D` | 0x1E21D | 3.548 | 2 | Panzerinfo, Lenkwaffen-Ziel |
| O10 | `0B20` | 0x1F123 | 676 | 7 | kleine Hilfsroutinen, von fast allen Overlays gerufen |

Einsprünge und Aufrufer: `bombre.py overlays`; Listings im Referenz-Repo vermerken an jedem Stub-Aufruf das Ziel
(`O2[3] -> Overlay-Offset 6611`).

## 3. Laufzeit, Zufall, Zahlentyp

- **Bibliothekseinheiten** (C1–C6): `Crt.Sound` `11F8:02C7` (Teiler `1234DDh / Hz`, Port 43h ← B6h, Port 42h, Port 61h |= 3),
  `Crt.NoSound` `11F8:02F4`, `Crt.Delay` `11F8:029C`, `KeyPressed` `11F8:02FB`, `ReadKey` `11F8:030D`;
  `Dos.GetIntVec`/`SetIntVec` `11E6:00E7`/`00FF`, `Dos.Intr` `11E6:000B`.
- **Zufall** (D1–D5): TP-Generator, `RandSeed` = `DS:3AD6` (LongInt). Schritt `RandSeed := RandSeed × 08088405h + 1`
  (`12CE:16C4`, `BOMB.EXE 0x15A44`; Faktor als `8405h` im Code, Rest als Schiebe-Additionen). `Random(n)` (`12CE:1675`) =
  oberes Wort des neuen Werts `mod n`; `Random` als Real (`12CE:168B`). `Randomize` (`12CE:16FC`) setzt `RandSeed` aus der
  DOS-Uhrzeit (INT 21h AH=2Ch, CX:DX) – das Hauptprogramm ruft es beim Start. `Random(n)` hat 140 Aufrufstellen.
  **Folge für das Remake:** gleiche Abläufe wie im Original nur mit gleichem Startwert *und* gleicher Aufruffolge; in DOSBox
  muss dafür die Uhrzeit festgelegt oder `RandSeed` nach dem Start gesetzt werden.
- **Zahlentyp der Physik** (E1, E2): **Real48** in Software (`$N-`). 238 Aufrufe der Wandlung LongInt → Real (`12CE:11DC`),
  dazu Add/Sub/Mul/Div/Cmp/Trunc/Round (`12CE:11B6`–`11E8`, Register-Real CX:SI:DI bzw. DX:BX:AX); kein 8087-Code und keine
  Emulator-Interrupts INT 34h–3Dh im Spielcode. Laufzeitfehler bei Überlauf 205, ungültiger Operation 207, Division 200.
  Die Nachbildung braucht eine bitgenaue Real48-Arithmetik (Rundung wie TP 6), nicht `double`.

## 4. Grafik, Schrift, Palette

- **BGI:** eingebunden in `0431` der Treiber VGA256 2.00 (`BOMB.EXE 0x07EB7`) und die Strichschriften LITT (`0x08B90`),
  EURO (`0x09F9B`) und TINY (`0x0C0AE`) (F1–F4). Die Offsets sind die Dateianfänge (`PK` + 08h 08h, BGI-Kennung); `befunde`
  prüft zusätzlich den Schriftkopf hinter dem Copyright-Text (1Ah 80h 00h + Schriftname: LITT `0x08BE8`, EURO `0x09FF4`,
  TINY `0x0C0F9`) und meldet beide Offsets. `0DBD` meldet den Treiber `vga256` und die Schriften EURO, LCOM, SCRI, SIMP,
  TSCR, TINY an (Nummern in `DS:E910`–`E91A`) (F6); LCOM, SCRI, SIMP, TSCR sind nicht eingebunden und werden nie benutzt.
- **Schriften im Gebrauch** (F7, `bombre.py fonts`): `SetTextStyle` (`0E26:1496`) nur mit DefaultFont 8 × 8, SmallFont
  (LITT), EURO und TINY. Kopfzeile „TANK WARS …“ des Hauptmenüs und Knopf START (`O2:06BD`, `O2:07BA`): **EURO, Größe 1**,
  mehrfach versetzt gezeichnet (Schatten): Kopfzeile `O2:06BD` mit 4, START `O2:07BA` mit 5 Aufrufen von `OutTextXY` (F8);
  `[APPROX]` wozu der fünfte Aufruf bei START dient, ist nicht untersucht. Menüeinträge (`O2:1E7F`) und Wertung: SmallFont Größe 1; Überschrift „Main Menu“
  und Spielfeld-Kopfzeile: DefaultFont; Einkauf: EURO und TINY Größe 4; Wandbeschriftung (`O8:0984`): SmallFont senkrecht.
  `[APPROX]` Die Namen `SetTextStyle`, `SetTextJustify` (`0E26:1454`), `OutTextXY` (`0E26:1F12`), `SetColor`
  (`0E26:1DC2`) folgen aus den Argumentmustern, nicht aus einem Symbolbeleg.
- **Eigener VGA-Zugriff** (G1, G2): `0CC8` setzt Modus 13h per INT 10h, Fenster 0..319 × 0..199, Bildsegment A000h, schreibt
  die DAC-Palette über 3C8h/3C9h und wartet auf den Strahlrücklauf (3DAh).
- **Palettenrotation** (G3, G4): `0C79` hängt den Handler `0C79:00E3` entweder an INT 0Ah (IRQ2, Rücklauf-Interrupt über
  CRTC-Register 11h; Option `PV`) oder an INT 1Ch (Zeitgeber 18,2 Hz; `PT`). Alle `DS:E8DD` Takte (Palette Speed / Cycle Delay)
  schreibt er ab Farbindex `DS:E8CE` `DS:E8D2`+1 Farben aus einem Palettenpuffer, dessen Fenster je Schritt um eine Farbe
  (3 Byte) weiterrückt. Die Rotation läuft also in Echtzeit, unabhängig vom Prozessortakt.

## 5. Zeit, Geschwindigkeit, Ton

- **Delay** (C3, H6): `Crt.Delay(ms)` zählt je ms `DS:EC44` Schleifen. Die Init kalibriert über einen BIOS-Takt (0040:006C) mit
  einem 16-Bit-Zähler und teilt durch 55; der Zähler sättigt bei FFFFh, `DelayCnt` dann höchstens 1.191. Auf schnellen
  Rechnern wird `Delay` damit zu kurz.
- **Geschwindigkeitsoptionen** (H1, H5): Die Optionstabelle in O2 (`BOMB.OVR 0x06277`) kennt `TS`, `FS`, `ES`, `SS`, `LS` –
  **kein `PS`**, anders als die Liste in `BOMB.DOC` (dort `PSnn Projectile Speed`; der Fließtext nennt `TS`). Werte per `Val`
  nach `DS:E8C4` (TS), `E8C8` (FS), `E8C6` (ES), `E8CA` (SS), `E8CC` (LS). TS, FS, ES und LS gehen unverändert an `Crt.Delay`
  – es sind **Millisekunden**, nicht Mikrosekunden wie in `BOMB.DOC`. SS ist eine Schrittzahl (O4). `[APPROX]` LS ist
  vermutlich das `PS` der Anleitung (Einsatz nur in O4).
- **Ton** (H2, H4): nur über `Crt.Sound`/`NoSound` (PIT-Kanal 2, Lautsprecher); keine anderen Zugriffe auf Port 42h/43h.
  105 Aufrufe von `Sound`, fast alle in O4–O7.
- **Weitere Zeitquellen:** Tastatur über eigenen INT-09h-Handler, Palettenrotation über INT 1Ch/0Ah; alles übrige (Zeichnen,
  Real48-Rechnung, Schussbahn) läuft so schnell wie der Prozessor.

**Vorschlag R1-D1 (Zyklen):** feste Zyklen, **`cycles=3000`** beibehalten. Begründung: Die Kalibrierschleife hat 3 Befehle; bei
`cycles=N` (≈ N Befehle je ms) zählt sie ≈ N × 55 / 3 Durchläufe je Takt und sättigt ab N ≈ 3.580 – darüber laufen alle
Delays (TS/FS/ES/LS, Titel) zu kurz. 3000 liegt knapp darunter und entspricht grob dem Entwicklungsrechner (386SX-16 laut
`BOMB.TXT`). `[APPROX]` Die Grenze nimmt 1 Zyklus je Befehl an; in DOSBox prüfen: `DS:EC44` nach dem Start muss unter 1.191
liegen. Für Aufnahmen Palettenrotation über Zeitgeber (`PT`), nicht `PV`.

## 6. Ablauf und Karte „Bildschirm → Prozeduren“

Hauptprogramm `0000:013B`: Startschritte (u. a. Exit-Proc, Treiber/Schriften anmelden, Tastatur, Maus, InitGraph, Cycler,
Titel), `Randomize`, dann O2[0] Prozessortest, O2[3] `BOMB.CFG` + Befehlszeile, O2[1] Maustest, O1 Titel, O2[2] Hauptmenü und
Spieleinstellung, O3[0] Spielfolge → O4[0] Runde (ruft O5–O9), O3[1] Endergebnis. Aufrufer je Einsprung: Ausgabe von
`bombre.py xref` und Listings.

| Bildschirm | Prozedur | Datei |
|---|---|---|
| Titelfolge | `O1:0014` | `BOMB.OVR 0x0001C` |
| Hauptmenü: Kopfzeile / START / Einträge / „Main Menu“ | `O2:06BD` / `O2:07BA` / `O2:1E7F` / `O2:0909` | `0x00CA5` / `0x00DA2` / `0x02467` / `0x00EF1` |
| Game Options: Beschriftungen, Namen für Hintergrund, Wand, Wind | `O2:11CC`, `O2:0E35`, `O2:0F81`, `O2:1042` | `0x017B4`, `0x0141D`, `0x01569`, `0x0162A` |
| Spielerauswahl: Steuerart, Name, Computergegner; Teams | `O2:509C`, `O2:42E8`, `O2:481B`; `O2:2749` | `0x05684`, `0x048D0`, `0x04E03`; `0x02D31` |
| Spielfeld: Kopfzeile, MIRV, Wandbeschriftung, Panzerinfo, Lenkziel | `O4:44A7`, `O4:13D4`, `O8:0984`, `O9:0040`, `O9:092E` | `0x151AC`, `0x120D9`, `0x1CA44`, `0x1E25D`, `0x1EB4B` |
| Statusfenster (Game Status) | `0431:0F1B` | `BOMB.EXE 0x068CB` |
| Wertung (einzeln / Teams), Endergebnis | `O3:5AEF`, `O3:6696`, `O3:7566` | `0x0E57B`, `0x0F122`, `0x0FFF2` |
| Einkauf, Statistik, Sichern, Laden | `O3:0E1F`, `O3:2508`, `O3:46DE`, `O3:4D97` | `0x098AB`, `0x0AF94`, `0x0D16A`, `0x0D823` |
| Hintergründe (Zeichnen) | `O8:0000(n)`, gerufen aus `O8:0984` ([runde.md](runde.md) §3.3) | `0x1C0C0` |

Zuordnung über die Texte, die eine Prozedur lädt (`mov di,<Text>; push cs`); `bombre.py karte` prüft jede Zeile.

## 7. Texte

`bombre.py strings` schreibt alle Pascal-Texte (Länge ≥ 3) mit Datei, Dateioffset, Block und Offset nach
`analyse/re-grundlagen/texte.tsv` (592 Einträge: Hauptprogramm, Einheiten, Overlays, Datensegment mit Waffen- und
Computernamen). Die Tabelle liegt im Referenz-Repo, nicht hier.

## 8. Nicht entscheidbar ohne DOSBox

1. Sättigung von `Crt.Delay`: tatsächlicher Wert von `DS:EC44` bei `cycles=3000` und die Zyklengrenze (§5).
2. Ob DOSBox Staging den Rücklauf-Interrupt IRQ2 liefert (Option `PV`); sonst bleibt die Rotation stehen.
3. Wirkung von `LS` (vermutlich Schussgeschwindigkeit) und die Voreinstellungen von TS/FS/ES/SS/LS aus `BOMB.CFG`.
4. ~~Welche Prozedur welchen Hintergrund zeichnet~~ – beantwortet: `O8:0000(n)` ([runde.md](runde.md) §3.3); offen bleiben die
   Verläufe der Palette 30..59 (dort §7).
5. Bildgenaue Schrift: Rasterung der Strichschriften EURO/LITT/TINY durch den BGI-Kern bei Größe 1 und 4 (Referenzbild nötig).
6. ~~Ob `0B25` (Namenssätze der Graph-Prozeduren) zur Laufzeit gebraucht wird~~ – beantwortet: ja, `0B25:0000`/`005A` sind
   Sin/Cos-Tabellen für ganze Grad, benutzt von Repulser und Computern ([runde.md](runde.md) §5.2, §6.5).

## 9. Listings (Referenz-Repo)

`bombre.py listings` erzeugt 27 Listings (`ndisasm`, Texte übersprungen, bekannte Ziele als Kommentar) nach
`analyse/re-grundlagen/listings/`. Abgelegt per PR im Referenz-Repo (Branch `daten/listings-re-grundlagen`, nur `analyse/`).
