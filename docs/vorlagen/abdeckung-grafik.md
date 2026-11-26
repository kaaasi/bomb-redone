# Vorlage: Grafischer Überblick der Abdeckung von BOMB.EXE / BOMB.OVR

Übernommen aus `kaaasi/eishockey-manager-redone` (`docs/vorlagen/abdeckung-grafik.md`) am 26.11.2026; Farbregeln unverändert,
Abweichungen in [../analyse/abdeckung-grafik/README.md](../analyse/abdeckung-grafik/README.md). Erzeugt eine Grafik, wie weit die
Funktionen von `BOMB.EXE`/`BOMB.OVR` im Remake umgesetzt sind, in fünf Farben. Nur lesen: kein `--write` von `funktionskarte.py`
oder `markerliste.py` (Ausnahme: Ablage auf Wunsch des Nutzers per Branch `prozess/…` und PR).

**Kurzaufruf** (der Nutzer schreibt ihn in einen neuen Chat):

> Repo kaaasi/bomb-redone. Erstelle den Abdeckungsüberblick nach `docs/vorlagen/abdeckung-grafik.md`.
> Stand: `<main | Branch/SHA>`.

**Modell:** Claude Sonnet 5.5 reicht (Skripte und feste Regeln, Unklares wird rot). Claude Opus 5.5 nur mit Einordnung der roten
Funktionen oder wenn neue `[APPROX]`-Marker einzuordnen sind.

## Farben (je Funktion, gewichtet nach Bytes)

| Farbe | Bedeutung | Regel |
|---|---|---|
| **grün** | belegt umgesetzt | Status `umgesetzt` der Funktionskarte, und keine Fundstelle der Funktion gehört zu einem `[APPROX]` der Klasse E |
| **gelb** | umgesetzt ohne Beleg | Status `genähert` mit `[APPROX]` der Klasse K oder D; ebenso `dokumentiert` oder `unbekannt`, wenn das Remake die Funktion nachweislich ohne Adresse nachbaut, mit Nachweis (Datei im Remake) |
| **blau** | anders umgesetzt | eine Fundstelle der Funktion gehört zu einem `[APPROX]` der Klasse E (Nutzerentscheidung, korrigierter Fehler des Originals, Plattformersatz, Remake-Erweiterung) – blau hat Vorrang vor grün und gelb |
| **grau** | weggefallen | nur mit Beleg: Doku oder Code nennt die Funktion als entfallen bzw. Plattformersatz ohne Gegenstück (Speicherprüfung, DOS-Treiber für Grafik, Ton, Maus, Overlay-Manager, Borland-Laufzeitbibliothek mit erkennbarem Kennzeichen); Liste `GREY` in `abdeckung_grafik.py` |
| **rot** | noch unbekannt | alles Übrige, besonders `unbekannt` und `dokumentiert` ohne Remake-Nachweis und ohne Entfall-Beleg; im Zweifel rot |

„Gehört zu einem `[APPROX]`“ heißt wie in `funktionskarte.py` Schritt 3: der nächste Marker vor der Fundstelle im selben Kommentar
(sonst danach). Die Klasse steht in der Markerliste (`markerliste.py`, Spalte Klasse; Gründe in
`docs/analyse/restliste-v1-klassen.csv`).

## Ablauf

1. Lies `AGENTS.md` §3 und `docs/analyse/re-grundlagen.md` §1–§2. Originaldaten: `tools/claude/fetch-reference.sh` (Claude:
   Referenz-Repo per `add_repo`, access read). `ndisasm` (Paket `nasm`).
2. Stand auschecken; `git rev-parse --short HEAD` notieren.
3. `python3 tools/reverse-engineering/markerliste.py` muss mit Rückgabe 0 enden (alle Marker eingeordnet); sonst neue Marker in
   `restliste-v1-klassen.csv` einordnen (Opus).
4. `python3 tools/reverse-engineering/abdeckung_grafik.py ~/referenz/original/gamedata --out /tmp/abdeckung/` (Arbeitsordner
   außerhalb des Repos): Funktionskarte, Zuordnung, Farben, `abdeckung.html`, `farben.csv`, Summen und Stichprobe auf stdout.
   Rohdaten der Karte bei Bedarf: `funktionskarte.py <gamedata> --csv /tmp/abdeckung/funktionen.csv` (enthält Textauszüge, nicht
   ins Repo).
5. Probe: Summe der Bytes aller Farben = Summe der Größen; Anzahl Funktionen gleich (Ausgabe „Probe“); Stichprobe 10 Funktionen
   je Farbe von Hand gegen Fundstelle und Marker prüfen (im Ergebnis nennen).
6. Grafik: wie erzeugt (Kopf mit SHA und Datum, Legende, Gesamtbalken nach Bytes und Anzahl, Karte je Bereich mit Filter und
   Hover, Tabellen der 20 größten roten und gelben, graue mit Beleg). Aus dem Original nur Adressen, Größen und kurze Texte.
7. Ausgabe: Claude als Artifact (zuerst die Artefakttypen auflisten; passt ein Dashboard-Typ, diesen nehmen, sonst die HTML-Seite
   veröffentlichen). Andere Agenten: HTML-Datei an den Nutzer. Ins Repo nur auf Wunsch (`docs/analyse/abdeckung-grafik/`).
8. Antwort an den Nutzer **knapp** (`AGENTS.md` §7): Link bzw. Datei, Anteil je Farbe in Bytes (Prozent), die drei größten roten
   Blöcke, Ergebnis der Probe.
