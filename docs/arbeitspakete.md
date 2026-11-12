# Arbeitspakete

Die Portierung ist in Pakete (Spuren) aufgeteilt, die parallel in eigenen Chats bearbeitet werden können. Jede Spur hat einen
Branch `paket/<name>` mit einem PR, eigene Dateien und klare Abnahmekriterien. Regeln und Einrichtung: [AGENTS.md](../AGENTS.md).
Den Status pflegt jede Spur nur in ihrem eigenen Abschnitt. Abgeschlossene Runden stehen in [archiv/](archiv/).

**Koordination:** Die Leitstelle verteilt Pakete, prüft und mergt Pull Requests und pflegt Plan und Doku
(Rollen: [prozess/zusammenarbeit.md](prozess/zusammenarbeit.md)). **Spuren starten erst nach dem Go des Nutzers.**

## Aktuelle Runde: Runde 2 – Spielfeld, Gelände, Schuss, Leser `*.TWS` (Plan 12.11.2026, Entscheidungen 12.11.2026, **wartet auf Go**)

Runde 1 abgeschlossen: [archiv/runde-1.md](archiv/runde-1.md). Grundlagen: [analyse/re-grundlagen.md](analyse/re-grundlagen.md)
(Overlays O3–O9, Real48, Zufall), [formate/spielstand.md](formate/spielstand.md), [anforderungen/bildschirme.md](anforderungen/bildschirme.md)
B4. Übernommen: #78 (Leser `*.TWS`), #71 (Satzreihenfolge), #39 (IRQ2), DOSBox-Sitzungen 2 und 3 (#89).

Regeln wie Runde 1: Base aktueller `main` (tatsächlichen SHA in den Draft-PR), reproduzieren → roter Test → grün,
`[ORIGINAL]`/`[APPROX]`, höchstens drei Fortsetzungen je Spur, Originalmaterial nur in Schutzordnern (`AGENTS.md` §2.1).
Erstreview je Spur: Claude Opus 5.5, frische Sitzung; Gesamtreview am Rundenende: ChatGPT (`prozess/anhang.md` C).

### Entscheidungen der Runde (Vorlage Leitstelle, entschieden vom Nutzer 12.11.2026: alle wie empfohlen)

**R2-D1 – Umfang der Runde – entschieden: A (Nutzer 12.11.2026).** Problem: „Spielfeld, Gelände, Schuss“ reicht von einem Standbild bis zur spielbaren Runde.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: Spielfeld (Aufbau, Hintergründe, Gelände, Panzer, Kopfzeile, Waffenleiste) und Schuss der Standardwaffe mit Wind, Wänden, Krater, Erdrutsch; Vorschau per `bombtool`/Desktop, noch keine Zugfolge | klein, jeder Baustein einzeln gegen das Original prüfbar | noch nicht spielbar |
| B: A + spielbare Runde (Zugfolge, Treffer, Schaden, Rundenende, Übergang zur Wertung) | erste spielbare Fassung | Spielfolge O3/O4 hängt an fast allem; großes Erstreview |
| C: B + alle Waffen, Lenksysteme, Abwehr | inhaltlich nah am Ziel | zu groß für eine Runde, Rückkopplung kommt spät |
| D: nur Analyse, Real48 und Leser `*.TWS`; Umsetzung in Runde 3 | sichere Grundlage | kein sichtbarer Fortschritt |

Empfehlung: **A**; B und die Waffen in Runde 3.

**R2-D2 – Zahlentyp der Physik – entschieden: A (Nutzer 12.11.2026).** Problem: Das Original rechnet mit Real48 in Software (`re-grundlagen` §3); `double` weicht in
Rundung und Wertebereich ab, Flugbahnen laufen dann auseinander.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: eigener Typ `Real48`, Operationen bitgenau nach der TP-6-Laufzeit (`12CE:11B6`–`11E8`, `11DC`) | Ziel E3 A erreichbar, Abgleich Schuss für Schuss | Aufwand, langsamer als `double` (unkritisch bei 320 × 200) |
| B: `double`, alles `[APPROX]` | schnell umgesetzt | E3 A ausgeschlossen, Abweichungen schwer zu deuten |
| C: erst `double`, Real48 später austauschbar | früher sichtbar | doppelte Arbeit, Tests müssen neu |
| D: Festkomma | schnell, deterministisch | weder Original noch einfach |

Empfehlung: **A**, eigene Spur `real48` in Welle 1.

**R2-D3 – Startwert des Zufalls für den Abgleich mit DOSBox – entschieden: C mit A, Rückfall D (Nutzer 12.11.2026).** Problem: `Randomize` setzt `RandSeed` aus der DOS-Uhrzeit;
ohne bekannten Startwert lässt sich ein Gelände nicht bitgleich mit einem DOSBox-Bild vergleichen.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: Startwert aus dem Bild zurückrechnen (Suche über alle Uhrzeiten, rund 8,6 Mio. Werte) | keine Änderung an DOSBox | setzt bekannte Aufruffolge von `Random` vor dem Gelände voraus |
| B: DOSBox-Debugger (Debug-Fassung 0.74-3) liest `DS:3AD6` nach dem Start | Wert direkt belegt | anderes Programm, Handarbeit des Nutzers |
| C: Uhrzeit vor dem Start fest setzen (`TIME` in `autoexec`, falls 0.74-3 das erlaubt – in Sitzung 2 prüfen), dann A in kleinem Suchraum | Suche trivial, Aufnahmen wiederholbar | unklar, ob 0.74-3 `TIME` setzen kann |
| D: kein bitgleicher Abgleich in Runde 2, Gelände nach Lage, Form, Statistik (`[APPROX]`) | ohne Zusatzarbeit | E3 A für Gelände offen |

Empfehlung: **C mit A**, Rückfall **D**; die Spur `gelaende` liefert das Suchwerkzeug.

**R2-D4 – Satzreihenfolge nach der Wertung (#71) – entschieden: A (Nutzer 12.11.2026).** Problem: `O3:0000` sortiert die Spielersätze dauerhaft um; das betrifft
Einkauf, Zugfolge und `*.TWS`.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: in `re-runde` beschreiben, Umsetzung mit der Spielfolge (Runde 3) | an der richtigen Stelle, ein Review | bleibt bis Runde 3 offen |
| B: jetzt in `Core/Economy` umsetzen | früh erledigt | ohne Spielfolge kaum prüfbar |
| C: zurückstellen ohne Beschreibung | kein Aufwand | Wissen geht verloren |

Empfehlung: **A**.

**R2-D5 – Listings der Spur `re-runde` – entschieden: A (Nutzer 12.11.2026).** Problem: R1-D4 erlaubte nur `re-grundlagen` den Schreibweg ins Referenz-Repo.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: wie R1-D4: `re-runde` öffnet selbst einen PR im Referenz-Repo (`daten/listings-re-runde`, nur `analyse/`), merged nicht | ein Schritt, Anmerkungen bleiben | zweite Ausnahme von „Spuren lesen nur“ |
| B: nur Skript, die Leitstelle erzeugt und legt ab | Regel ohne Ausnahme | Anmerkungen gehen verloren |
| C: keine Listings, nur Beschreibung mit Offsets | einfach | Nachprüfung aufwendiger |

Empfehlung: **A**.

**R2-D6 – Zeitpunkt des Lesers `*.TWS` (#78) – entschieden: B (Nutzer 12.11.2026).** Problem: Es gibt noch keinen Originalstand (Sitzung 3 offen); GR1-1 sieht den
Leser „nach der DOSBox-Aufnahme mit Beispielständen“ vor.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: sofort (Welle 1) mit synthetischen Daten, Abgleich mit Originalständen als eigener Teil | früh fertig | Teil 2 blockiert bis Sitzung 3, Format ungeprüft |
| B: Stufe 2a, Voraussetzung Sitzung 3 im Referenz-Repo gemergt | gleich gegen Originale prüfbar | wartet auf den Nutzer |
| C: Runde 3 | Runde 2 kleiner | #78 bleibt länger offen |

Empfehlung: **B**.

### DOSBox-Aufnahme `runde-1`, Sitzungen 2 und 3 (Nutzer, `vorlagen/dosbox-aufnahme.md`)

- **Sitzung 2:** Hauptmenü, Sound und Game Options, Spielerauswahl, Teams; Option `PV` (Rotation über IRQ2) unter 0.74-3 – läuft die
  Palette? (#39); `TIME` vor dem Start setzbar? (R2-D3).
- **Sitzung 3:** Runden mit Voreinstellungen und mit jedem Wandmodus und mehreren Hintergründen (je Rundenbeginn ein Bild vor
  dem ersten Schuss, dann nach dem ersten Schuss der Standardwaffe mit notiertem Winkel, Stärke, Wind), Einkauf, Wertung;
  Spielstände nach `formate/spielstand.md` „Benötigte Originalstände“ (Nr. 1–8). Uhrzeit beim Start notieren (R2-D3).

### Welle 1 (parallel) – nach Merge dieses Plans und Go

Integrationsfolge: `real48` → `re-runde` (keine gemeinsamen Dateien).

#### Spur `real48` – Real48-Arithmetik und Zufall im Spielkern (R2-D2 = A)

- **Inhalt:** `Real48` (6 Byte, TP 6) mit Wandlung LongInt → Real (`12CE:11DC`), Add, Sub, Mul, Div, Cmp, Trunc, Round
  (`12CE:11B6`–`11E8`), Wandlung nach `double` nur für die Anzeige; Laufzeitfehler 200/205/207 als eigene Ausnahme;
  Konstanten aus dem Datensegment lesbar (6-Byte-Muster). `TurboPascalRandom` von `Desktop` nach `Core` verschieben und um
  `Random` als Real (`12CE:168B`) ergänzen; Desktop nutzt die Core-Fassung. Fundstellen je Operation, Rundung wie die Laufzeit.
- **Teile:** 1. Darstellung, Wandlung, Add/Sub, Cmp; 2. Mul, Div, Trunc, Round, Fehler; 3. Zufall nach Core, Random Real.
- **Dateien:** `src/BombRedone.Core/Numerics/*` (neu), `src/BombRedone.Desktop/TurboPascalRandom.cs` (entfernen, Aufrufer in
  `src/BombRedone.Desktop/*` anpassen), `tests/BombRedone.Core.Tests/Numerics/*` (neu), `tools/reverse-engineering/real48*` (neu,
  Probe gegen die Originalroutinen, Ausgabe nur in Schutzordner), `docs/analyse/real48.md` (neu).
- **Abnahme:** je Operation Sollwerte mit Beleg – bevorzugt eine Probe, die die Originalroutine aus `BOMB.EXE` ausführt (z. B.
  kleiner 8086-Emulator im Werkzeugordner) und mit dem Remake vergleicht (Zufallseingaben, Randfälle: 0, Vorzeichen,
  Unter-/Überlauf, Rundung bei ,5); ohne Probe je Randfall `[APPROX]`. Vor dem Umzug ein
  Charakterisierungstest der bisherigen Folge (`RandSeed := RandSeed × 08088405h + 1`, `Random(n)`) mit festem Startwert, der nach
  dem Umzug unverändert grün bleibt (bisher gibt es keinen Test von `TurboPascalRandom`).
- **Modell:** Claude Opus 5.5.

#### Spur `re-runde` – Runde und Spielfolge erschließen (Analyse, kein Produktcode)

- **Inhalt:** O3[0] Spielfolge (Zugfolge, Rundenende, Übergänge, Umsortieren der Sätze #71, R2-D4), O4 Runde (Kopfzeile
  `O4:44A7`, Waffenleiste, Zielen, Eingaben), O8 Spielfeldaufbau (Rahmen, Wandbeschriftung `O8:0984`, Gelände, Panzer setzen,
  Crumble, Tank Cliffs), Hintergründe (`re-grundlagen` §8 Nr. 4: welche Prozedur welchen zeichnet), O5–O7 Schuss (Start aus
  Winkel und Stärke, Schrittweite `SS`/`LS`, Schwerkraft, Wind, Wandarten, Einschlag, Krater, Erdrutsch, Fallen `TS`/`FS`), O9
  Panzerinfo. Je Baustein: Prozedur, Ablauf, Konstanten (Real48-Muster), **Reihenfolge der `Random`-Aufrufe** ab `Randomize`
  bis zum ersten Schuss (für R2-D3). #39: §8 Nr. 2 in `re-grundlagen.md` anpassen, sobald Sitzung 2 ein Ergebnis hat.
- **Dateien:** `docs/analyse/runde.md` (neu), `tools/reverse-engineering/runde*` (neu) und Erweiterungen von `bombre.py` (nur neue
  Unterbefehle), `docs/analyse/re-grundlagen.md` (nur §8 und Verweise). Listings nach R2-D5 = A: PR im Referenz-Repo vom Branch `daten/listings-re-runde` (nur `analyse/`), nicht mergen.
- **Abnahme:** jede Aussage mit Offset und ausführbarem Beleg (`befunde` zählt alle grün); Liste „nicht entscheidbar ohne DOSBox“
  mit den nötigen Bildern für Sitzung 3.
- **Modell:** Claude Opus 5.5.

### Welle 2 – nach Merge von Welle 1, eigenes Go

Stufe 2a parallel (`gelaende`, `tws-leser`), Stufe 2b `schuss` nach Merge von `gelaende`. Zuschnitt und Base-SHA ergänzt die
Leitstelle nach Welle 1 aus `analyse/runde.md`; die Abnahmen hier gelten als Rahmen.

- **`gelaende`** (Stufe 2a, Opus): Spielfeld in `Core` (Gelände, Panzerplätze, Wandart, Hintergrund) aus Startwert und
  Optionen, `PlayfieldScreen` in `UI` (Rahmen, Hintergründe, Gelände, Wandbeschriftung, Kopfzeile, Waffenleiste, Panzer),
  Desktop-Vorschau, `bombtool render playfield --seed`, Suchwerkzeug für den Startwert (R2-D3). Abnahme: Pixelvergleich mit
  Bildern aus Sitzung 3 bei bekanntem Startwert, sonst Lage und Form `[APPROX]`.
- **`tws-leser`** (Stufe 2a, Opus, R2-D6): Leser `SavedGame` in `Formats` nach `formate/spielstand.md`, `bombtool tws`,
  unbelegte Felder roh; Abnahme mit den Originalständen aus Sitzung 3, `Fixes #78`.
- **`schuss`** (Stufe 2b, Opus): Flugbahn der Standardwaffe in `Real48`, Wind, alle Wandarten, Einschlag, Krater, Erdrutsch,
  Fallen der Panzer; Spur und Explosion in `UI`. Abnahme: Bahn und Krater gegen Sitzung-3-Bilder mit notiertem Winkel, Stärke,
  Wind und Startwert.

### Zuständigkeit für gemeinsame Dateien (Runde 2)

- `AGENTS.md`, `CLAUDE.md`, `docs/prozess/*`, `docs/vorlagen/*`, `docs/entscheidungen.md`, `tools/pruefen*.sh`,
  `tools/originalschutz-selbsttest.sh`, `.gitignore`, `.github/*`, `tests/**/Support/`: allein die Leitstelle.
- `src/BombRedone.Desktop/*`: Welle 1 nur `real48` (Umzug des Zufalls), Stufe 2a nur `gelaende`, Stufe 2b nur `schuss`.
- `src/BombRedone.Tools/Program.cs`: je Spur eine Registrierungszeile (`gelaende`, `tws-leser`).
- `tools/reverse-engineering/bombre.py`: nur `re-runde` (neue Unterbefehle).
- `docs/arbeitspakete.md`: jede Spur nur ihr eigener Statusabschnitt. Für Welle 1 hat die Leitstelle die Abschnitte unten
  angelegt (vermeidet Konflikte beim Anfügen); spätere Spuren bekommen ihren Abschnitt mit dem Zuschnitt.

### Status real48

- 12.11.2026, Claude Code (Opus 5.5), PR #93: Teile 1–3 fertig, wartet auf Erstreview. Probe
  `tools/reverse-engineering/real48.py` (8086-Emulator der Originalroutinen), Abgleich je Operation 20.000 Fälle;
  Befund: `Randomize` legt CX ins untere Wort (Desktop-Fassung korrigiert). Bericht [analyse/real48.md](analyse/real48.md).

### Status re-runde

- noch nicht begonnen.
