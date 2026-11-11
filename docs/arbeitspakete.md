# Arbeitspakete

Die Portierung ist in Pakete (Spuren) aufgeteilt, die parallel in eigenen Chats bearbeitet werden können. Jede Spur hat einen
Branch `paket/<name>` mit einem PR, eigene Dateien und klare Abnahmekriterien. Regeln und Einrichtung: [AGENTS.md](../AGENTS.md).
Den Status pflegt jede Spur nur in ihrem eigenen Abschnitt. Abgeschlossene Runden stehen in [archiv/](archiv/).

**Koordination:** Die Leitstelle verteilt Pakete, prüft und mergt Pull Requests und pflegt Plan und Doku
(Rollen: [prozess/zusammenarbeit.md](prozess/zusammenarbeit.md)). **Spuren starten erst nach dem Go des Nutzers.**

## Aktuelle Runde: Runde 1 – Dateiformate, Reverse Engineering, erste Bildschirme (Plan 11.11.2026, **Go für Welle 1 und 2: 11.11.2026**)

Runde 0 abgeschlossen: [archiv/runde-0.md](archiv/runde-0.md). Grundlagen: Erstbefund [originaldateien.md](originaldateien.md),
Entscheidungen [entscheidungen.md](entscheidungen.md) (E1–E7, R1-D1 = A, R1-D2 = B, R1-D3 = A, R1-D4 = A, R1-D5 = C, R1-D6 = A, Vorlage Release 3.2).

**Kernaussage:** Außer zwei Titelbildern gibt es keine Grafikdateien; fast alles Sichtbare zeichnet das Programm selbst.

**Base für alle Spuren:** aktueller `main` (Planstand nach Merge dieses Plans); tatsächlichen Base-SHA in den Draft-PR. Regeln:
Befund reproduzieren, roter Test, dann grün; `[ORIGINAL]` mit `BOMB.EXE 0x…`/`BOMB.OVR 0x…`/Anleitung/Referenzbild, sonst
`[APPROX]`; Quelltext und Kommentare englisch; höchstens drei Fortsetzungen je Spur. Bei Unklarheit „wie im Original“; bleibt
es unklar, Teil blockiert lassen, die Leitstelle bündelt die Fragen. Originalmaterial im Arbeitsverzeichnis nur in den
Schutzordnern (`AGENTS.md` §2.1).

### Vorlagen im Referenz-Repo (Stand 11.11.2026)

- `original/gamedata/` – Release 3.2; `original/spielstaende/nutzer-2026-11-11.CFG` – `BOMB.CFG` mit **Voreinstellungen** (45 Byte).
- `videos/tank-wars-longplay/` (439 Bilder, 576 × 360, 1 Bild/s) und `videos/tank-wars/` (152 Bilder, Spielbild 570 × 356).
  Sichtung der Leitstelle (Übersichtsbilder, nicht jedes Bild): Das Longplay zeigt **„TANK WARS 3.2“** in der Kopfzeile des
  Menüs (Sekunde 10 ff.) – dieselbe Fassung wie die Vorlage. Abgedeckt sind: Titelfolge (zwei Bilder, Sekunden 0–9 und am
  Ende), Hauptmenü (Sound Options, Game Options, Players, Games, START), Fenster „Game Options“, Spielerauswahl (10 Felder
  Keyboard/Mouse/Computer, Namenseingabe, START), Spielfeld mit Kopfzeile und Waffenleiste, mehrere erzeugte Hintergründe
  (Quadrate, Strahlen, Ringe, Rauschen, einfarbig, Sterne), Wandmodi als Beschriftung links (WARP, ACCELERATE, ELASTIC, STICKY),
  Schussbahnen, Explosionen, Wertungstafel („n Games Played out of 10“, Add/Continue|Exit/Save), Einkauf (Purchase/Done,
  ein- und zweispaltige Waffenliste), Überblendungen. **Nicht abgedeckt:** `BOMBCFG.EXE`, Inhalt von „Sound Options“,
  Menüs für Laden und Sichern, Teamspiel, Tastaturbedienung.
- Grenze: Beide Videos sind auf nicht ganzzahlige Faktoren skaliert (576/320 = 1,8) und komprimiert. Sie belegen Aufbau,
  Abläufe und ungefähre Lage, nicht einzelne Pixel und keine exakten Farben. Abnahmen gegen Videobilder sind vorläufig
  (`[APPROX]`), bis ein DOSBox-Bild sie belegt.

### Entscheidungen der Runde

**R1-D1 – DOSBox-Referenz (folgt aus E4) – entschieden: A, DOSBox 0.74-3 mit `cycles=3000` (Nutzer 11.11.2026; Zyklen als Vorschlag aus `re-grundlagen` belegt).** Problem: Aufnahmen und Takt brauchen genau eine Einstellung.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: DOSBox 0.74-3, `cycles=3000` (etwa 286/386SX) | überall verfügbar, stabil | alte Fassung, wenig Aufnahmekomfort |
| B: DOSBox Staging (aktuell), `cycles=3000` | gepflegte Fassung, saubere PNG-Aufnahme in Originalgröße | Verhalten kann sich zwischen Versionen ändern → Version festschreiben |
| C: DOSBox-X, `cycles=3000` | Debugger eingebaut, hilft beim Reverse Engineering | mehr Einstellungen, mehr Fehlerquellen |
| D: Zyklen erst nach der Analyse festlegen (Zeitschleifen im Code suchen), Aufnahme bis dahin mit B | Wert ist belegt statt geschätzt | Titel- und Menübilder sind vom Takt unabhängig, Animationen müssten ggf. neu aufgenommen werden |

Entschieden: **A** (Nutzer 11.11.2026), DOSBox 0.74-3 mit `cycles=3000`; der Zyklenwert ist durch `re-grundlagen` belegt
(Sättigung der Delay-Kalibrierung bei etwa 3.580). Die frühere Empfehlung D mit B (Staging) wurde nicht übernommen.

**R1-D2 – Ablage von Disassembly-Listings – entschieden: B (Nutzer 11.11.2026).** Problem: Listings sind abgeleiteter Originalcode.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: nur lokal erzeugen (Skript im Code-Repo), nirgends ablegen | nichts Originales in Git | jeder Lauf erzeugt neu; Anmerkungen gehen verloren |
| B: annotierte Listings im privaten Referenz-Repo (`analyse/`), im Code-Repo nur eigene Beschreibungen mit Offsets | Anmerkungen bleiben, Code-Repo bleibt sauber | Spuren bräuchten Schreibweg ins Referenz-Repo (heute nur Leitstelle/Nutzer) |
| C: Listings im Code-Repo | ein Ort | widerspricht `AGENTS.md` §2.1 |

Empfehlung: **B**, Ablage durch die Leitstelle per PR aus dem, was die Spur als Datei liefert.

**R1-D3 – Ziel für `BOMB.CFG` und `*.TWS` – entschieden: A (Nutzer 11.11.2026).** Problem: Umfang der Formatarbeit.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: nur lesen (Import von Originalständen) | reicht für Abgleich und Tests | Remake-Stände laufen nicht im Original |
| B: lesen und bytegleich schreiben (Rundreise) | stärkster Beleg für das Format, Abgleich in beide Richtungen | deutlich mehr Aufwand, erst mit vollständigem Spielzustand möglich |
| C: zurückstellen bis nach den ersten Bildschirmen | Runde 1 bleibt klein | Spielzustand wird ohne Formatwissen entworfen |

Empfehlung: **A in Runde 1** (Felder benennen, soweit belegbar), B als späteres Ziel.

**R1-D4 – Ablageweg der Listings – entschieden: A (Nutzer 11.11.2026; geschrieben „R1-D2 = A“, als R1-D4 verstanden, weil
R1-D2 = B am selben Tag ausdrücklich entschieden wurde).** R1-D2 legt den Ort fest (Referenz-Repo). Problem: Spuren
dürfen dort bisher nicht schreiben, und eine Spur kann der Leitstelle keine Dateien übergeben.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: Die Spur `re-grundlagen` öffnet selbst einen PR im Referenz-Repo (Branch `daten/listings-…`), merged nicht | ein Schritt, Listings sofort prüfbar | Ausnahme von „Spuren lesen nur“; die Sitzung braucht Schreibzugriff auf das Referenz-Repo |
| B: Die Spur liefert nur das Skript, die Leitstelle erzeugt und legt ab | Regel bleibt ohne Ausnahme | Anmerkungen der Spur im Listing gehen nicht mit; zweiter Arbeitsgang |
| C: Der Nutzer legt ab (Datei aus dem Chat der Spur) | keine Rechte für Agenten nötig | Handarbeit, fehleranfällig bei großen Dateien |

Empfehlung: **A**, beschränkt auf `analyse/` und auf diese Spur.

### Welle 1 (parallel) – Go des Nutzers am 11.11.2026, 11:58

**Start (Nutzer 11.11.2026):** Eine neue Leitstellen-Sitzung (Claude Opus 5.5) legt die vier Spuren als unbeaufsichtigte
geplante Läufe an (Start in wenigen Minuten, Selbstfortsetzung, höchstens 3 je Spur) und wartet auf die Labels. Startaufruf
je Spur: Kurzaufruf „Geplanter Lauf mit Teilen“ aus `vorlagen/spur-start.md` mit dem Modell der Spur und diesen Besonderheiten:

| Spur | Modell | Besonderheiten im Startaufruf |
|---|---|---|
| `formate` | Claude Sonnet 5.5 | – |
| `konfiguration` | Claude Opus 5.5 | Beispieldatei im Referenz-Repo: `original/spielstaende/nutzer-2026-11-11.CFG` (Voreinstellungen). |
| `re-grundlagen` | Claude Opus 5.5 | R1-D4 = A: für die Listings im Referenz-Repo einen PR vom Branch `daten/listings-re-grundlagen` öffnen (nur Ordner `analyse/`), nicht mergen; diese Schreibaktionen sind autorisiert. Sonst schreibt die Spur dort nichts. |
| `bildschirme-inventar` | Claude Sonnet 5.5 | Videobilder mit `tools/claude/fetch-reference.sh videos` laden; keine Bilder oder Ausschnitte ins Code-Repo. |

Voraussetzung nach `prozess/anhang.md` F: beide Repos in der anlegenden Sitzung mit access `push` eingebunden. Die Spuren haben
keine Teilliste; jede ist ein Teil, Fortsetzungen nur bei Abbruch. Erstreviews (Modell je Spur unten) legt die Leitstelle nach
„Ready for review“ an; **ChatGPT nur für das Gesamtreview am Rundenende**. Welle 2 braucht ein eigenes Go.


Integrationsfolge: `bildschirme-inventar` → `formate` → `konfiguration` → `re-grundlagen` (keine gemeinsamen Dateien außer
einer Zeile in `src/BombRedone.Tools/Program.cs` bei `formate` und `konfiguration`).

#### Spur `formate` – Titelbilder lesen und exportieren

- **Inhalt:** `RawImage` (320 × 200 Indizes + Palette, 6 Bit → 8 Bit) in `src/BombRedone.Formats/`, `bombtool export` (PNG nach
  `export/`), `docs/formate/dateiformate.md`. Abgleich mit den Titelbildern des Longplays (Sekunden 0–9): Motiv, Lage und
  Farbeindruck; die 6-Bit-Dehnung (×4 oder ×255/63) bleibt `[APPROX]`, bis ein DOSBox-Bild sie belegt.
- **Dateien:** `src/BombRedone.Formats/RawImage*.cs`, `Palette*.cs`, `src/BombRedone.Tools/Export*.cs` (+ eine Zeile in
  `Program.cs`), `tests/BombRedone.Formats.Tests/RawImage*`, `docs/formate/dateiformate.md`.
- **Abnahme:** synthetische Tests (Rundreise, Fehlerfälle: zu kurz, zu lang, Palettenwert > 63), Originaldaten-Tests (Maße,
  Farbanzahl, Prüfsumme des dekodierten Bildes), Export läuft.
- **Modell:** Claude Sonnet 5.5. **Review:** Claude Opus 5.5 (Erstreview, frische Sitzung).

#### Spur `konfiguration` – `BOMB.CFG` lesen (R1-D3 = A)

- **Inhalt:** Format von `BOMB.CFG` aus der Lese- und Schreibroutine in `BOMBCFG.EXE`/`BOMB.EXE` (Meldungen „BOMB.CFG
  Version“, „A valid BOMB.CFG …“ als Einstieg), der Beispieldatei mit Voreinstellungen und der Optionsliste in `BOMB.DOC`
  (Sound, Flysound, Games, Background, Wind, Wall, Crumble, Invisible, Trace, Tanks Fall, Computers Buy, Team, Mouse, Video,
  Palette, Speed). Leser `GameConfig` in `Formats` (nur lesen), `bombtool cfg <datei>`, `docs/formate/konfiguration.md`.
- **Dateien:** `src/BombRedone.Formats/GameConfig*.cs`, `src/BombRedone.Tools/Config*.cs` (+ eine Zeile in `Program.cs`),
  `tests/BombRedone.Formats.Tests/GameConfig*`, `docs/formate/konfiguration.md`.
- **Abnahme:** jedes Feld mit Fundstelle im Code **oder** als „unbelegt“ markiert (eine Beispieldatei allein belegt keine
  Feldzuordnung); synthetische Tests je Feld und für Fehlerfälle (falsche Version, zu kurz); Originaldaten-Test liest die
  Beispieldatei aus `~/referenz/original/spielstaende/` und vergleicht mit den Voreinstellungen der Anleitung. Liste der
  Einstellungen, für die eine zweite Beispieldatei nötig wäre.
- **Modell:** Claude Opus 5.5. **Review:** Claude Opus 5.5 (Erstreview, frische Sitzung).

#### Spur `re-grundlagen` – Programmaufbau erschließen (Analyse, kein Produktcode)

- **Inhalt:** MZ-Kopf und Segmente von `BOMB.EXE`; Aufbau von `BOMB.OVR` (`FBOV`, Overlay-Einheiten und ihre Zuordnung);
  Erkennen der Turbo-Pascal-6-Laufzeit und der Bibliothekseinheiten (System, Crt, Graph/BGI, Overlay), damit nur Spielcode
  übrig bleibt; eingebundene BGI-Treiber und Schriften (die Schrift der Kopfzeile „TANK WARS 3.2“ und der Knöpfe); Zufallsgenerator
  (`Random`/`Randomize`, `RandSeed`) und Zahlentyp der Physik (Real48, 8087 oder Festkomma); Zeitschleifen, die
  Geschwindigkeitsoptionen `ES/FS/PS/SS` und Tonausgabe (Ports 0x42/0x43/0x61); Palettenrotation; Tabelle aller Texte mit
  Offsets; Karte „Bildschirm → Prozeduren“ für die Bildschirme aus `docs/anforderungen/bildschirme.md` (Titel, Hauptmenü,
  Game Options, Spielerauswahl, Spielfeld, Hintergründe, Wertung, Einkauf). Vorschlag für R1-D1 (Zyklen).
- **Dateien:** `docs/analyse/re-grundlagen.md`, `tools/reverse-engineering/*` (Skripte lesen aus `gamedata/`, schreiben nach
  `analyse/` im Arbeitsverzeichnis – Schutzordner). Listings: eigener PR der Spur im Referenz-Repo (Branch
  `daten/listings-re-grundlagen`, nur `analyse/`, nicht mergen; R1-D4 = A).
- **Abnahme:** jede Aussage mit Offset und ausführbarem Beleg (Skriptaufruf, Ergebnis); Liste „nicht entscheidbar ohne DOSBox“.
- **Modell:** Claude Opus 5.5. **Review:** Claude Opus 5.5 (frische Sitzung).

#### Spur `bildschirme-inventar` – Bildschirme und Abläufe aus den Videos (Analyse, kein Produktcode)

- **Inhalt:** `docs/anforderungen/bildschirme.md`: je Bildschirm ein Abschnitt mit Zweck, Elementen (Texte wörtlich,
  Knöpfe, Felder), Übergängen und Fundstellen als Bildnummern (`videos/<kurzname>/<nnnnn>.png`), dazu der Ablauf einer Partie
  als Zustandsfolge und die Liste der Waffen mit Preisen, soweit lesbar. Abgleich mit `BOMB.DOC` (was die Anleitung nennt, die
  Videos aber nicht zeigen). Nur Beschreibung, **keine Bilder oder Ausschnitte** ins Code-Repo.
- **Dateien:** `docs/anforderungen/bildschirme.md`.
- **Abnahme:** jedes Bild beider Videos ist genau einem Bildschirm oder Übergang zugeordnet (Tabelle Bildbereiche →
  Bildschirm); jede Aussage mit Bildnummer oder Abschnitt der Anleitung; Lesefehler wegen der Videoqualität als „unsicher“
  markiert; Liste der fehlenden Bildschirme für die DOSBox-Aufnahme.
- **Modell:** Claude Sonnet 5.5. **Review:** Claude Sonnet 5.5 (reine Doku-Prüfung, frische Sitzung).

### Welle 2 – Go des Nutzers am 11.11.2026, 14:02 (Zuschnitt Leitstelle 11.11.2026)

Base aller Spuren der Stufe 2a: `2f51fa0deb84793e999c3b14653ec5edd42e6e70` (`main` nach Welle 1 und R1-D1). Gegenüber dem
Stichpunktplan ist `schrift-menue` geteilt: Die Schrift braucht kein Bildschirm-Grundgerüst und läuft parallel zu `titel`, das
Menü braucht beides. Start wie Welle 1 (unbeaufsichtigte geplante Läufe, Selbstfortsetzung, höchstens 3 je Spur, Kurzaufruf
„Geplanter Lauf mit Teilen“). Erstreview je Spur: Claude Opus 5.5, frische Sitzung. Stufen nacheinander; die nächste Stufe legt die
Leitstelle nach dem Merge der vorigen an (Base dann der neue `main`, im Startaufruf genannt).

| Stufe | Spur | Modell | braucht |
|---|---|---|---|
| 2a | `titel` | Claude Opus 5.5 | – |
| 2a | `schrift` | Claude Opus 5.5 | – |
| 2a | DOSBox-Aufnahme `runde-1` (Nutzer mit Agent, `vorlagen/dosbox-aufnahme.md`) | Claude Sonnet 5.5 | R1-D1 |
| 2b | `menue` | Claude Opus 5.5 | `titel`, `schrift` |
| 2c | `spielerauswahl`, `wertung-einkauf` (parallel) | Claude Opus 5.5 | `menue` |

Integrationsfolge 2a: `titel` → `schrift`. Bilder der DOSBox-Aufnahme sind, sobald im Referenz-Repo gemergt, Abnahmevorlage
(E3: pixelgenau); bis dahin gelten die dekodierten Originaldateien und die Disassembly, Videobilder nur für Abläufe (`[APPROX]`).

#### Spur `titel` – Bildschirm-Grundgerüst und Titelfolge

- **Inhalt:** Grundgerüst in `UI`: Bildschirm mit `Update(Takt, Eingabe)` und `Draw(Surface)` (Zeit und Eingabe von außen,
  `AGENTS.md` §4), Bildschirmwechsel; Titelfolge nach `O1:0014` (`BOMB.OVR 0x0001C`, `docs/analyse/re-grundlagen.md` §6):
  `BOMB1.RAW`/`BOMB2.RAW` (Überlagerung, Paletten, Wartezeiten, Abbruch per Taste) und Verhalten bei fehlenden Dateien
  (`BOMB.TXT` „GRAPHICS FILES“, Disassembly). Anzeige im Fenster (`Desktop`): `Surface` → Textur über `Presentation`
  (E6), Eingabe an den Bildschirm. Ende der Titelfolge führt vorerst zu einem leeren Platzhalter-Bildschirm (Hauptmenü folgt in `menue`).
- **Dateien:** `src/BombRedone.UI/Screens/*` (Grundgerüst, `TitleScreen*`), `src/BombRedone.Desktop/*`,
  `tests/BombRedone.UI.Tests/Screens/*`, `docs/anforderungen/titel.md`.
- **Abnahme:** synthetische Tests für Ablauf und Zeiten mit vorgegebenem Takt (kein Systemtakt), fehlende Dateien je Fall;
  Originaldaten-Test: gezeichnete `Surface` je Zustand pixelgleich zu den dekodierten Bildern (Prüfsummen wie in `formate`);
  Zeiten und Übergänge mit Fundstelle `[ORIGINAL]` oder `[APPROX]`; Desktop startet und zeigt den Titel (Beleg im PR:
  Bildschirmfoto nur als Beschreibung, kein Bild einchecken).

#### Spur `schrift` – Schriften des Originals lesen und zeichnen

- **Inhalt:** Leser für die in `BOMB.EXE` eingebundenen BGI-Strichschriften LITT, EURO, TINY (Offsets aus
  `re-grundlagen` §4, F1–F4) und die 8 × 8-Standardschrift der Graph-Einheit (Fundstelle suchen; ist sie nicht in `BOMB.EXE`,
  `[APPROX]` mit Begründung); Textausgabe in `UI` mit der Semantik von `SetTextStyle`/`SetTextJustify`/`OutTextXY`/`SetColor`
  (Größe, Richtung waagrecht/senkrecht, Ausrichtung). Formatbeschreibung in `docs/formate/schriften.md`. Keine Schriftdaten
  ins Code-Repo (Tests mit Originaldaten lesen aus `gamedata/`, synthetische Tests mit selbst erzeugten Schriften).
- **Dateien:** `src/BombRedone.Formats/BgiFont*.cs`, `src/BombRedone.Formats/BitmapFont*.cs`, `src/BombRedone.UI/Text/*`,
  `tests/BombRedone.Formats.Tests/*Font*`, `tests/BombRedone.UI.Tests/Text/*`, `docs/formate/schriften.md`.
- **Abnahme:** synthetische Tests (Kopf, Zeichentabelle, Strichbefehle, Fehlerfälle); Originaldaten-Tests (drei Schriften
  gelesen, Zeichenanzahl, Breiten, Prüfsumme gerenderter Probetexte); Abgleich „TANK WARS 3.2“ (EURO, Größe 1) mit
  Videobild LP 00009 nach Lage und Form (`[APPROX]`, bis ein DOSBox-Bild vorliegt); Skalierungsregel der BGI-Größen mit Beleg.

#### Spur `menue` – Stufe 2b: Standardschrift, Hauptmenü, Game Options

- **Base:** `main` nach Merge von `titel` und `schrift` (SHA im Startaufruf). **Teile** (geordnet, je ein Teil je Lauf möglich):
  1. **Standardschrift nach R1-schrift-1 = D:** VGA-8 × 8-Schrift aus dem Quelltext von DOSBox 0.74-3 (GitHub-Spiegel des
     Quelltexts, Herkunft und Lizenz GPL im PR nennen) als Datei im Referenz-Repo: eigener PR vom Branch
     `daten/vga-schrift-dosbox` (nur `schriften/`, mit README zu Herkunft, Lizenz, SHA-256), nicht mergen – diese Schreibaktion
     ist für diese Spur autorisiert (wie R1-D4). Eingebauter Ersatz: gemeinfreie 8 × 8-Schrift mit belegter Herkunft im
     Code-Repo; Laden der Referenzdatei wie Originaldaten (`TestGameData`/`GameData`-Weg), sonst Ersatz.
  2. **Hauptmenü:** Hintergrund, Kopfzeile „TANK WARS 3.2“ (EURO, Schatten), START, Einträge, „Main Menu“ nach `O2:06BD`,
     `O2:07BA`, `O2:1E7F`, `O2:0909` (`re-grundlagen` §6); Übergang vom Titel (ersetzt den Platzhalter von `titel`).
  3. **Game Options und Bedienung:** `O2:11CC`, `O2:0E35`, `O2:0F81`, `O2:1042`; Maus und Tastatur nach der Disassembly
     (Eingabe über `ScreenInput`); Werte aus `GameConfig` (R1-D5); Ziele ohne Umsetzung (Spielerauswahl, Laden) als Platzhalter.
- **Dateien:** `src/BombRedone.Formats/BitmapFont*.cs`, `src/BombRedone.UI/Text/*` (Ergänzungen), `src/BombRedone.UI/Screens/Menu*`,
  `src/BombRedone.UI/Screens/Options*`, `src/BombRedone.UI/Screens/ScreenInput.cs` (nur additiv), `src/BombRedone.Desktop/*`,
  `tests/BombRedone.Formats.Tests/BitmapFont*`, `tests/BombRedone.UI.Tests/Screens/Menu*`, `…/Options*`, `…/Text/*`,
  `docs/anforderungen/menue.md`, `docs/formate/schriften.md` (§4). Gemeinfreie Schrift: Dateityp in
  `.github/originalschutz-erlaubt.txt` nur über die Leitstelle (im PR melden).
- **Abnahme:** synthetische Tests je Element und Bedienschritt (Takt und Eingabe von außen); Originaldaten-Tests: Prüfsummen der
  gezeichneten Bildschirme; mit Referenzschrift pixelgleich zu DOSBox-Bildern der Aufnahme `runde-1`, sobald sie gemergt sind,
  sonst Abgleich mit Videobildern nach Lage und Form (`[APPROX]`); jede Lage, Farbe und Taste mit `[ORIGINAL]`-Fundstelle oder
  `[APPROX]`. Fortsetzungen nur, wenn ein Teil nicht in einen Lauf passt.
- **Modell:** Claude Opus 5.5. **Review:** Claude Opus 5.5 (Erstreview, frische Sitzung).

#### Spätere Stufe 2c (Zuschnitt nach Merge von `menue`)

- **`spielerauswahl`**, **`wertung-einkauf`** – weitere statische Bildschirme (Karte in `re-grundlagen` §6).

**Rundenende:** Gesamtreview durch ChatGPT (`prozess/anhang.md` C), danach Plan Runde 2 (Spielfeld, Gelände, Schuss).

### Zuständigkeit für gemeinsame Dateien (Runde 1)

- `AGENTS.md`, `CLAUDE.md`, `docs/prozess/*`, `docs/vorlagen/*`, `docs/entscheidungen.md`, `tools/pruefen*.sh`,
  `tools/originalschutz-selbsttest.sh`, `.gitignore`, `.github/*`, `tests/**/Support/`: allein die Leitstelle.
- `src/BombRedone.Tools/Program.cs`: je Spur eine Registrierungszeile (`formate`, `konfiguration`).
- `src/BombRedone.Desktop/*`: in Welle 1 niemand, in Stufe 2a nur `titel`, in Stufe 2b nur `menue`.
- `docs/arbeitspakete.md`: jede Spur nur ihr eigener Statusabschnitt (unten anfügen: `### Status <spur>`).

### Status formate

- Teil 1 (einziger Teil): umgesetzt in PR #12 – `RawImage`, `Palette`, `bombtool export`, Tests, `docs/formate/dateiformate.md`.
  `[APPROX]`: 6→8-Bit-Dehnung (`Palette.DefaultScaling`), bis ein DOSBox-Bild sie belegt. Offen für `re-grundlagen`: Überlagerung von `BOMB2` über `BOMB1` (durchsichtiger Index, zwei Paletten).
  Fixlauf 11.11.2026: R12-B1 (#22), R12-B2 (#23), R12-B3 (#24) behoben; R12-B4 (#25) wartet auf Entscheidung.

### Status bildschirme-inventar

- Teil 1 (einziger Teil): `docs/anforderungen/bildschirme.md` fertig, PR #11 (Claude Sonnet 5.5). Zuordnung aller 591 Videobilder
  geprüft; Lesefehler als „unsicher“ markiert; Liste fehlender Bildschirme für die DOSBox-Aufnahme in Abschnitt 6. Review: Claude
  Sonnet 5.5, frische Sitzung (Leitstelle).

### Status re-grundlagen

- 11.11.2026, Claude Opus 5.5 (geplanter Lauf): Analyse fertig, PR #9; Befunde `docs/analyse/re-grundlagen.md`, Werkzeug
  `tools/reverse-engineering/bombre.py` (39/39 Befunde, 26/26 Bildschirme bestätigt). Listings: PR im Referenz-Repo,
  Branch `daten/listings-re-grundlagen`. Offen: Liste „Nicht entscheidbar ohne DOSBox“ (§8).
- 11.11.2026, Claude Opus 5.5 (Fixlauf): Review-Befunde R9-P2-1, R9-P3-1..3 behoben (Byte-Vergleiche → Offset, Länge, SHA-256;
  Schrift-Offsets erklärt; neuer Befund F8), `befunde` 40/40, `karte` 26/26. Wartet auf Delta-Prüfung der Leitstelle.

### Status konfiguration

- 11.11.2026: begonnen (Claude Code, geplanter Lauf), Base `ca689e9`.
- 11.11.2026: fertig – Review. Alle 28 Felder aus der Leseroutine `BOMB.OVR 0x6BF9` belegt, keins unbelegt; zwei Voreinstellungen weichen von `BOMB.DOC` ab (Crumble 75, Walls None), siehe `formate/konfiguration.md`. PR #10.
- 11.11.2026: Fixlauf – `main` gemergt (#11, #12), R10-01 (#32) und R10-02 (#33) behoben; R10-03 (#34, `bombtool cfg` ohne Test) bleibt Restlücke. Voreinstellungen Crumble 75 / Walls None unverändert (Entscheidung Nutzer).

### Status schrift

- 11.11.2026, Claude Opus 5.5 (geplanter Lauf), Base `1fc3d68`: fertig – Review, PR #43. Strichschriften LITT/EURO/TINY lesen,
  Textausgabe mit Kern-Semantik (Fundstellen im Graph-Kern), `docs/formate/schriften.md`. Kopfzeile und Menüeintrag stimmen mit
  Longplay 00009 auf etwa 1 Bildpunkt. Offen: Quelle der 8 × 8-Schrift (ROM, nicht im Original) – Entscheidung Nutzer.
- 11.11.2026: Fixlauf – `main` gemergt (#44, #45), R43-01 (#50), R43-02 (#51), R43-03 (#52) behoben. R1-schrift-1 = D
  (Nutzer 11.11.2026), Umsetzung in Spur `menue`.

### Status titel

- 11.11.2026: begonnen (Claude Code, Claude Opus 5.5, geplanter Lauf), Base `1fc3d68`.
- 11.11.2026: fertig – Review. Grundgerüst (`IScreen`, `ScreenHost`, `ScreenInput`), Titelfolge nach O1:0014 mit Fundstellen,
  Desktop zeigt den Titel; PR #44, Anforderungen `anforderungen/titel.md`. `[APPROX]`: Dauer eines Kopierdurchgangs (85 ms),
  DAC-Maske 6 Bit, Palette vor dem Titel.

### Status menue

- 11.11.2026: begonnen (Claude Code, Claude Opus 5.5, geplanter Lauf), Base `3b2a7c6`.
- 11.11.2026: Teil 1 fertig – `BitmapFontSource` (Referenzschrift DOSBox 0.74-3, sonst gemeinfreier Ersatz `BitmapFontSubstitute`),
  Referenzdatei per kaaasi/bomb-referenz#8 (nicht gemergt). PR #55 (Draft), Teile 2–3 offen.
