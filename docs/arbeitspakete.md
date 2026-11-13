# Arbeitspakete

Die Portierung ist in Pakete (Spuren) aufgeteilt, die parallel in eigenen Chats bearbeitet werden können. Jede Spur hat einen
Branch `paket/<name>` mit einem PR, eigene Dateien und klare Abnahmekriterien. Regeln und Einrichtung: [AGENTS.md](../AGENTS.md).
Den Status pflegt jede Spur nur in ihrem eigenen Abschnitt. Abgeschlossene Runden stehen in [archiv/](archiv/).

**Koordination:** Die Leitstelle verteilt Pakete, prüft und mergt Pull Requests und pflegt Plan und Doku
(Rollen: [prozess/zusammenarbeit.md](prozess/zusammenarbeit.md)). **Spuren starten erst nach dem Go des Nutzers.**

## Aktuelle Runde: Runde 2 – Spielfeld, Gelände, Schuss, Leser `*.TWS` (Plan 12.11.2026, Welle 1 gemergt 12.11.2026, Welle 2: Go 13.11.2026, `schuss` wartet auf R2-D8)

Runde 1 abgeschlossen: [archiv/runde-1.md](archiv/runde-1.md). Grundlagen: [analyse/re-grundlagen.md](analyse/re-grundlagen.md)
(Overlays O3–O9, Real48, Zufall), [formate/spielstand.md](formate/spielstand.md), [anforderungen/bildschirme.md](anforderungen/bildschirme.md)
B4. Übernommen: #78 (Leser `*.TWS`), #71 (Satzreihenfolge), #39 (IRQ2), DOSBox-Sitzungen 2 und 3 (#89).

Regeln wie Runde 1: Base aktueller `main` (tatsächlichen SHA in den Draft-PR), reproduzieren → roter Test → grün,
`[ORIGINAL]`/`[APPROX]`, höchstens drei Fortsetzungen je Spur, Originalmaterial nur in Schutzordnern (`AGENTS.md` §2.1).
Erstreview je Spur: Claude Opus 5.5, frische Sitzung; Gesamtreview am Rundenende: ChatGPT (`prozess/anhang.md` C).

### Entscheidungen der Runde (Vorlage Leitstelle; R2-D1…D6 entschieden vom Nutzer 12.11.2026, R2-D7 am 13.11.2026: alle wie empfohlen; R2-D8 offen)

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

**R2-D7 – Abnahme von `gelaende` und `schuss`, solange Sitzung 3 fehlt – entschieden: A (Nutzer 13.11.2026).** Problem: Die
Abnahme verlangt den Pixelvergleich mit Bildern aus Sitzung 3 (R2-D3); Sitzungen 2 und 3 sind noch nicht aufgenommen, `TIME` in
0.74-3 ist ungeprüft. `runde.md` beschreibt Gelände und Schuss Befehl für Befehl; offen sind Bilddetails (§5.13, §7).

| Option | Vorteil | Nachteil |
|---|---|---|
| A: jetzt starten; Probe führt die Originalroutinen (`O8:0984` mit Hintergrund und Gelände, später `O4:01B7`/`O6`) im 8086-Emulator von `real48.py` aus, Grafikaufrufe (`0E26`, `0CC8`) abgefangen in einen 320 × 200-Puffer; Vergleich Pixel und `RandSeed` für viele Startwerte und Optionen; Bildvergleich mit Sitzung 3 als letzter Teil | bitgleich gegen das Original ohne DOSBox, Startwert direkt vorgegeben (R2-D3 entschärft), Bilder bestätigen nur noch | Probe ist eigene Arbeit (Overlay laden, Graph-Aufrufe nachbilden); Graph-Routinen selbst nur `[APPROX]` |
| B: `gelaende` und `schuss` erst nach Sitzung 3 (wie R2-D6 = B) | gleich gegen Bilder prüfbar | Welle 2 wartet auf den Nutzer, nur `tws-leser` kann danach starten |
| C: jetzt starten ohne Probe, Umsetzung nur nach `runde.md`, alles Bildbezogene `[APPROX]` bis Sitzung 3 | schnell sichtbar | Lesefehler der Analyse bleiben bis Sitzung 3 unentdeckt (#102), Nacharbeit wahrscheinlich |
| D: zuerst eigene Spur `feld-probe` (nur Probe), danach `gelaende` | Probe einzeln geprüft | eine Welle mehr, Probe ohne Verbraucher schwer abzunehmen |

Empfehlung: **A**. Gelingt die Probe für einen Baustein nicht, bleibt dieser Teil blockiert (`spur-wartet`, `AGENTS.md` §2.10).

**R2-D8 – Krater in `schuss`, obwohl Lead Ball keinen hat – offen (Vorlage Leitstelle 13.11.2026, Review #104 P2-1).** Problem: R2-D1 A
nennt „Schuss der Standardwaffe mit … Krater“; laut `runde.md` §5.5 (Q10) macht Lead Ball (Waffe 0) aber weder Explosion noch Krater,
nur 5 Schaden bei Volltreffer. Krater, Explosion und Flächenschaden (§5.6 K1–K4) gibt es nur für die Sprengwaffen {1, 2, 3, 4, 7, 11}.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: Lead Ball wie geplant, dazu Explosion, Krater und Flächenschaden §5.6 für **eine** Sprengwaffe (Baby Missile, Waffe 1, Radius 10), wählbar in Vorschau und `bombtool`; Abnahme Krater nur für diese Waffe | Krater-Mechanik (Puffer, `0CC8:0687`, `Sqrt`) in Runde 2 geprüft, Rest der Waffen ist danach nur Radius/Sonderfall | Waffe ohne Einkauf und Bestand; eine Waffe mehr im Erstreview |
| B: wie A, aber alle Sprengwaffen mit gleichem Flug (1–4, Radien 10/15/30/60) | Radius-Tabelle gleich mit erledigt | etwas mehr Prüfaufwand; 7 und 11 bleiben trotzdem für Runde 3 |
| C: Krater streichen, `schuss` nur Lead Ball ohne Krater; §5.6 und `runde.md` §7 Nr. 6 in Runde 3 | kleinste Spur, R2-D1 wörtlich auf die Standardwaffe beschränkt | Erdrutsch K9 ohne Krater kaum sichtbar zu prüfen; sichtbarer Fortschritt kleiner |
| D: Standardwaffe der Runde ist Baby Missile statt Lead Ball | ein Schuss mit Krater | weicht vom Original ab (Startbestand ist Lead Ball) |

Empfehlung: **A**.

### DOSBox-Aufnahme `runde-1`, Sitzungen 2 und 3 (Nutzer, `vorlagen/dosbox-aufnahme.md`)

- **Sitzung 2:** Hauptmenü, Sound und Game Options, Spielerauswahl, Teams; Option `PV` (Rotation über IRQ2) unter 0.74-3 – läuft die
  Palette? (#39); `TIME` vor dem Start setzbar? (R2-D3).
- **Sitzung 3:** Runden mit Voreinstellungen und mit jedem Wandmodus und mehreren Hintergründen (je Rundenbeginn ein Bild vor
  dem ersten Schuss, dann nach dem ersten Schuss der Standardwaffe mit notiertem Winkel, Stärke, Wind), Einkauf, Wertung;
  Spielstände nach `formate/spielstand.md` „Benötigte Originalstände“ (Nr. 1–8). Uhrzeit beim Start notieren (R2-D3).
  Ergänzt nach Welle 1 (`analyse/runde.md` §7): je Hintergrund ein Bild vor dem ersten Schuss, zwei Bilder desselben Hintergrunds
  kurz nacheinander (Rotation); Rundenbeginn mit 2 und 10 Spielern; je Wandart ein Schuss mit Spur an (Winkel, Kraft, Wind, `SS`
  notieren), dazu Wand 0 nach links bei Wind < 0; Energie in der Panzerinfo vor und nach Schüssen (Rückstoß); Schuss neben einen
  Panzer (Krater, Energie vorher/nachher); Crumble 100 % mit „Fall down“, Schuss unter einen Panzer (Video); Schuss mit höchster Kraft
  in einen Hügel. Nr. 8–10 (Tod, Laser, MIRV) erst für Runde 3.

### Welle 1 (parallel) – gemergt 12.11.2026 (#93, #94)

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

### Welle 2 – Zuschnitt 13.11.2026 (Leitstelle), eigenes Go

Welle 1 gemergt (12.11.2026): `real48` #93, `re-runde` #94. Grundlage: [analyse/runde.md](analyse/runde.md), [analyse/real48.md](analyse/real48.md).
Base: aktueller `main` beim Start (bei Zuschnitt `401b15c`). Stufe 2a `gelaende` (sofort nach Go), `tws-leser` (nach Sitzung 3,
R2-D6 = B); Stufe 2b `schuss` nach Merge von `gelaende`. Integrationsfolge: `gelaende` → `tws-leser` → `schuss`.

**Für alle Spuren der Welle:** Pascal-`=`/`<>` auf Real48 ist `Real48.Compare(a, b) == 0`, nicht `==` (#96). Die Befunde von
`runde.py` prüfen Befehlsmuster, teils ohne Sprungbedingung (#102): Abnahme ist die Probe gegen den Originalcode bzw. das Bild, nicht
`befunde` allein. Zufall nur über `TurboPascalRandom` aus `Core` (eine Instanz, Aufrufreihenfolge wie `runde.md` §4).

#### Spur `gelaende` – Spielfeld aus Startwert und Optionen (Stufe 2a)

- **Inhalt:** `Core/Playfield`: Wandart (RND `Random(5)`), Hintergrundwahl (RND/USER), Gelände nach `runde.md` §3.5 (Real48-Vergleiche,
  Schranken als Real48-Muster), Panzerplätze und Anfangswerte der Sätze §3.6 (P11, P12), Fallen auf das Gelände (P13), Wind §2
  (16-Bit-Überlauf, #98), Startspieler; Eingabe ist `RandSeed` **nach dem Mischen** (§4 Nr. 2–7), Ausgabe Zustand plus Stand von
  `RandSeed`. **Pixelzustand im Spielkern** (Review #104 P2-2): Die Regeln des Originals lesen den Bildschirm (Fallen P13 über
  `0CC8:0BFF`, später Treffer Q9, Erdrutsch K9, Krater K2 aus dem Hintergrundpuffer `DS:E5C0`). Deshalb hält `Core/Playfield` einen
  Bildschirm- und einen Hintergrundpuffer (320 × 200 Palettenindizes) samt Palette und zeichnet dort selbst: Rahmen Farbe 13,
  Wandbeschriftung `O8:0984`, die neun Hintergründe §3.3 samt Palette 30..59, Gelände Farbe 2, Panzerbild (`0431:05ED`/`0685`,
  `[ORIGINAL]`); Regeln lesen nur diese Puffer. `UI`: `PlayfieldScreen` kopiert die Puffer in `Surface` und ergänzt Kopfzeile und
  Waffenleiste (`runde.md` §6.1 Z1, Z2: `O4:0000` setzt die Farben 100..119, `O4:44A7`, `O8:1268`); Desktop-Vorschau;
  `bombtool render playfield --seed <n> [--cfg …]`; Suchwerkzeug für den Startwert (R2-D3, `runde.md` §4 „Vorschlag“).
- **Probe (R2-D7 = A, Review #104 P3-1, P3-2):** Ansatz wie die Probe im Review #104 (`Cpu` aus `real48.py` mit Fernaufrufen,
  Overlays mit Fixups). Graph `0E26` und der gelinkte Treiber VGA256 2.00 (`BOMB.EXE 0x7EB7`) werden **mit emuliert** (nur INT 10h
  und Ports abgefangen), ebenso `0CC8` (schreibt direkt nach A000h); DAC-Modell für `SetRGB` über `DS:E8F6` und das Lesen der DAC
  (Stars, `0CC8:0C20`). Umfang: `O8:0984` vollständig und `O4:44A7` bis vor die Kopfzeile (Wind, Startspieler). Was die Probe nicht
  abdeckt, bekommt `[ORIGINAL]` mit Test nach `runde.md` (Wind-Überlauf #98 immer als Test) oder bleibt nach `AGENTS.md` §2.10 blockiert.
- **Teile:** 1. Probe (`tools/reverse-engineering/feld*`); 2. `Core`: Puffer, Wand, Hintergrundwahl und Hintergründe, Gelände, Rahmen,
  Panzerbild, Panzer, Fallen, Wind, Startspieler; 3. `PlayfieldScreen` (Kopfzeile, Waffenleiste); 4. `bombtool`, Desktop-Vorschau,
  Suchwerkzeug (zählt die Schritte des Mischens `runde.md` §4 Nr. 1, ohne Tausch der Sätze, R2-D4); 5. Bildvergleich mit
  Sitzung 3 (blockiert, bis die Bilder im Referenz-Repo gemergt sind: `spur-wartet`, kein erneuter Versuch).
- **Dateien:** `src/BombRedone.Core/Playfield/*` (neu), `src/BombRedone.UI/Screens/Playfield*` (neu), `src/BombRedone.Desktop/*`,
  `src/BombRedone.Tools/RenderCommand.cs`, `src/BombRedone.Tools/Program.cs` (eine Zeile, falls nötig),
  `tests/BombRedone.Core.Tests/Playfield/*`, `tests/BombRedone.UI.Tests/Screens/Playfield*`, `tests/BombRedone.UI.Tests/Tools/Render*`,
  `tools/reverse-engineering/feld*` (neu), `docs/analyse/feld.md` (neu), `docs/anforderungen/bildschirme.md` (nur Abschnitt Spielfeld).
- **Abnahme:** nach R2-D7; je Baustein `[ORIGINAL]` mit Offset aus `runde.md`; Folge der `Random`-Aufrufe als Test (Zahl der
  Schritte von `RandSeed` je Baustein); Wind-Überlauf (#98) als Test; Rückfall ohne Beleg je Punkt `[APPROX]`.
- **Modell:** Claude Opus 5.5.

#### Spur `tws-leser` – Leser `*.TWS` (Stufe 2a, R2-D6 = B)

- **Voraussetzung:** Sitzung 3 mit den Originalständen Nr. 1–8 (`formate/spielstand.md` „Benötigte Originalstände“) im Referenz-Repo
  gemergt; vorher kein Start.
- **Inhalt:** `SavedGame` in `Formats` nach `formate/spielstand.md` (Kopf, Sätze `74h`, Intelligenzblöcke, Teamtabelle), unbelegte
  Felder roh; Satzreihenfolge wie gespeichert (#71 nur beschreiben, R2-D4); `bombtool tws <datei>`. `Fixes #78`.
- **Teile:** 1. Leser mit synthetischen Ständen (aus `tools/reverse-engineering/tws.py` erzeugt); 2. Abgleich aller Originalstände
  (`[GameDataTheory]`), Abweichungen zur Formatbeschreibung in `spielstand.md`; 3. `bombtool tws`.
- **Dateien:** `src/BombRedone.Formats/SavedGame*.cs` (neu), `tests/BombRedone.Formats.Tests/SavedGame*` (neu),
  `src/BombRedone.Tools/TwsCommand.cs` (neu), `src/BombRedone.Tools/Program.cs` (eine Zeile), `docs/formate/spielstand.md`.
- **Abnahme:** jeder Originalstand gelesen, Rundreise der Rohbytes unverändert; Werte gegen die notierten Einstellungen der Sitzung.
- **Modell:** Claude Opus 5.5.

#### Spur `schuss` – Schuss der Standardwaffe (Stufe 2b, nach Merge von `gelaende`)

- **Inhalt (R2-D1 = A, nur Lead Ball):** `Core`: Abschuss und Startwerte §5.1–5.2 (Rückstoß-`Random:Real`, Q2; Schaden Q11), Flug §5.3 in Real48
  (`SS`/`LS`, Schwerkraft, Wind/5000/SS), Wände §5.4 (alle Wandarten, Randregel), Treffer und Ende §5.4, Einschlag und Schaden §5.5,
  Explosion und Krater §5.6 nach **R2-D8** (`Random` nur bei Ton), Erdrutsch und Fallen §5.8 (Crumble, `Random(3)` je Zeile), Gelände bei voller
  Kraft §5.12. `UI`: Spur (Trail, Inviso), Explosion, Krater, Erdrutsch im `PlayfieldScreen`. Nicht in Runde 2: übrige Waffen,
  Laser, MIRV, Lenkung, Tod/Blitz/Rundenende §5.9, Zugfolge, Computer.
- **Teile:** 1. Probe nach R2-D7 (Flug `O4:01B7`, Explosion `O6`); 2. Flug, Wände, Ende; 3. Einschlag, Schaden, Krater; 4. Erdrutsch,
  Fallen; 5. Darstellung; 6. Bildvergleich mit Sitzung 3 (`runde.md` §7 Nr. 4, 5, 7, 11, Nr. 6 nach R2-D8; blockiert bis die Bilder da sind).
- **Dateien:** `src/BombRedone.Core/Shot/*` (neu), `src/BombRedone.Core/Numerics/Real48.Trig*.cs` (neu: `Sin`, `Cos` `12CE:12EE`/`1301`,
  Grad→Bogen `0DBD:00D9`, π; bei R2-D8 A auch `Sqrt` `12CE:128F`), `tests/BombRedone.Core.Tests/Numerics/Real48Trig*` (neu),
  `tools/reverse-engineering/real48.py` (nur neue Einsprünge, Hash-Probe wie Welle 1; Review #104 P2-3), `src/BombRedone.Core/Playfield/*` (nur Erweiterungen für Krater/Erdrutsch, nach
  `gelaende`), `src/BombRedone.UI/Screens/Playfield*`, `src/BombRedone.Desktop/*`, `tests/BombRedone.Core.Tests/Shot/*`,
  `tests/BombRedone.UI.Tests/Screens/Playfield*`, `tools/reverse-engineering/feld*` (Erweiterung der Probe), `docs/analyse/feld.md`.
- **Abnahme:** nach R2-D7; Bahn und Endpunkt je Wandart, Krater nach R2-D8; `Sin`/`Cos`/`Sqrt` bitgleich gegen die Laufzeit (Probe);
  `RandSeed` nach dem Schuss gleich.
- **Modell:** Claude Opus 5.5.

### Zuständigkeit für gemeinsame Dateien (Runde 2)

- `AGENTS.md`, `CLAUDE.md`, `docs/prozess/*`, `docs/vorlagen/*`, `docs/entscheidungen.md`, `tools/pruefen*.sh`,
  `tools/originalschutz-selbsttest.sh`, `.gitignore`, `.github/*`, `tests/**/Support/`: allein die Leitstelle.
- `src/BombRedone.Desktop/*`: Welle 1 nur `real48` (Umzug des Zufalls), Stufe 2a nur `gelaende`, Stufe 2b nur `schuss`.
- `src/BombRedone.Tools/Program.cs`: je Spur eine Registrierungszeile (`gelaende`, `tws-leser`); `RenderCommand.cs`: nur `gelaende`.
- `src/BombRedone.Core/Numerics/*` und `tools/reverse-engineering/real48.py`: in Welle 2 nur `schuss` (neue Dateien bzw. Einsprünge).
- `src/BombRedone.Core/Playfield/*`, `src/BombRedone.UI/Screens/Playfield*`, `tools/reverse-engineering/feld*`, `docs/analyse/feld.md`:
  `gelaende`, danach `schuss` (nacheinander, nie gleichzeitig).
- `tools/reverse-engineering/bombre.py`: nur `re-runde` (neue Unterbefehle).
- `docs/arbeitspakete.md`: jede Spur nur ihr eigener Statusabschnitt. Für Welle 1 und 2 hat die Leitstelle die Abschnitte unten
  angelegt (vermeidet Konflikte beim Anfügen).

### Status real48

- 12.11.2026, Claude Code (Opus 5.5), PR #93: Teile 1–3 fertig, wartet auf Erstreview. Probe
  `tools/reverse-engineering/real48.py` (8086-Emulator der Originalroutinen), Abgleich je Operation 20.000 Fälle;
  Befund: `Randomize` legt CX ins untere Wort (Desktop-Fassung korrigiert). Bericht [analyse/real48.md](analyse/real48.md).

### Status re-runde

- 12.11.2026, Claude Opus 5.5, Draft-PR `paket/re-runde`, Base `5a3e384`. Teile (vom Lauf festgelegt, der Spurabschnitt hat
  keine Teilliste): 1. O8 Spielfeldaufbau, Hintergründe, `Random`-Folge bis zum ersten Schuss; 2. O5–O7 Schuss; 3. O4 Runde,
  O9 Panzerinfo, O3[0] Spielfolge mit #71; 4. Listings (Referenz-Repo, R2-D5 = A), Liste „nicht entscheidbar ohne DOSBox“.
- 12.11.2026, Lauf 1: Teil 1 fertig (Spielfolge und #71 aus Teil 3 vorgezogen), Teil 2 bis auf `runde.md` §5.6 (Krater,
  Erdrutsch, Fallen, Todesexplosionen, Laser, MIRV), Teil 4 Stand Lauf 1 (bomb-referenz#10, nicht gemergt). Offen: Rest Teil 2,
  Teil 3 (Zug `O7:040C`, Kopfzeile `O4:44A7`, Eingaben, O9, Computer `0129`), §7 ergänzen, Listings neu erzeugen. 41 Befunde.
- 12.11.2026, Fortsetzung 1: Teil 2 fertig (offen nur Bilddetails, `runde.md` §5.13): Explosion,
  Krater, Flächenschaden, CRI, Sonic, Dirt, Erdrutsch, Fallen, Tod, Blitz, Rundenende, Laser, MIRV, Lenkung; §7 Nr. 6–11.
  Teil 3: Zugbeginn, Kopfzeile, Waffenleiste, Zug `O7:040C`, Tasten, Maus, Schummeltaste, Panzerinfo, Zielwahl, Computer-Rahmen
  `0129:01D0`, alle sieben Computer-Arten (§6); offen nur Sichtlinien von Wind Master (§6.6). Teil 4: Listings unverändert,
  README in bomb-referenz#10 nachgetragen. 81 Befunde. Alle Teile fertig, Ready for review (Merge-Basis `1128839`).
- 12.11.2026, Fixlauf zum Review `docs/reviews/2026-11-12-re-runde.md`: P2-1 (#98, Wind in 16 Bit, R3 verschärft), P3-1 (#99),
  P3-2 (#100), P3-3 (#101, Begründung im PR) behoben; P3-4 (#102) Hinweis für Welle 2. 81 Befunde.

### Status gelaende

- noch nicht begonnen.

### Status tws-leser

- noch nicht begonnen (Voraussetzung Sitzung 3).

### Status schuss

- noch nicht begonnen (nach Merge von `gelaende`).
