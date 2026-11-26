# Abdeckungsüberblick `BOMB.EXE` / `BOMB.OVR`

Erstellt nach [docs/vorlagen/abdeckung-grafik.md](../../vorlagen/abdeckung-grafik.md) (übernommen aus
`kaaasi/eishockey-manager-redone`), 26.11.2026, Leitstelle (Claude Opus 5.5). **Stand:** `main` `af4ad17`.

- [abdeckung.html](abdeckung.html) – selbständige Seite (Legende, Gesamtbalken, Karte je Bereich mit Filter, Tabellen).
- [farben.csv](farben.csv) – je Funktion Dateioffset, Adresse, Bereich, Größe, Status, Farbe, Klassen, Grund, Fundstellen.
- Erzeugt mit `python3 tools/reverse-engineering/abdeckung_grafik.py ~/referenz/original/gamedata --out <arbeitsordner>`
  (nutzt `funktionskarte.py` und `markerliste.py`); Klassen aus [../restliste-v1-klassen.csv](../restliste-v1-klassen.csv).

## Ergebnis

514 Funktionen und Datenblöcke, 204.655 Byte.

| Farbe | Bytes | Anteil | Funktionen |
|---|---:|---:|---:|
| grün | 142.843 | 69,8 % | 174 |
| gelb | 1.053 | 0,5 % | 4 |
| blau | 5.802 | 2,8 % | 2 |
| grau | 25.599 | 12,5 % | 258 |
| rot | 29.358 | 14,3 % | 76 |

Größte rote Blöcke: `0129:2200` (BOMB.EXE 0x04B30, 3.405 Byte, Computergegner), `0431:1A63` (0x07413, 2.724 Byte,
Tastenliste „Control List“), `O3:2C8B` (BOMB.OVR 0x0B717, 2.215 Byte, Statistik). Rot ist vor allem `0129`
(Computergegner, Runde 4), Speichern/Laden in O3 und Teile von `0C48`, `0C79`, `0CC8`.

**Einschränkung:** Die Overlays haben wenige, sehr große Prozeduren (z. B. O4: 5 Funktionen auf 20 KB). Schon eine
`[ORIGINAL]`-Fundstelle färbt die ganze Prozedur grün; der Grünanteil der Overlays ist dadurch eine Obergrenze. Dasselbe
gilt in der Vorlage („Dünn belegte große Funktionen“ in der Ausgabe von `funktionskarte.py`).

## Probe

- Summe der Bytes aller Farben = Summe `groesse` (204.655); Anzahl Funktionen gleich (514).
- Stichprobe von Hand (Fundstelle und Marker gelesen): je 10 grün, grau, rot, alle 4 gelb, alle 2 blau – ohne Befund außer
  dem behobenen: Dateioffsets von Segment- und Overlay-Anfängen („file offset 0x5E8“, „0x0E320 + offset“) zählten zuerst
  als Fundstelle der ersten Funktion; jetzt Layoutkonstanten wie `CONSTANTS` der Vorlage. Bekannte Unschärfe: `0B25:00BD`
  ist eine Tabelle, wird aber als Funktion gezählt (rot, 55 Byte).

## Abweichungen von der Vorlage

1. **Zwei Dateien:** Funktionen in `BOMB.EXE` (Segmente) und `BOMB.OVR` (Overlays O1–O10); Dateioffset immer mit Dateiname.
   `funktionskarte.py` bekommt den Ordner `gamedata` statt eines EXE-Pfads.
2. **Aufbau Turbo Pascal 6** statt Borland C++/VROOMM: Layout aus `bombre.Game` (re-grundlagen), Prolog `55 89 E5`, Fernaufrufe
   über Stub-Einsprünge in die Overlays; keine Bildnummern und DGROUP-Globalen als Hinweise, sondern geladene Pascal-Texte.
3. **BGI-Dateien in `0431`:** VGA256-Treiber und Schriften LITT, EURO, TINY als je ein Datenblock (keine Funktionen).
4. **Adressformen:** `On:XXXX`, `On[k]`, `[U]SSSS:XXXX`, `BOMB.EXE 0x…`/`BOMB.OVR 0x…` (blanke Werte im selben Satz gehören zur
   zuletzt genannten Datei). Ein blanker Wert ohne Dateinamen zählt nur als genauer Funktionsanfang in genau einer Datei.
5. **Systembereiche** (`BASE_KIND`) nach re-grundlagen §1; **grau** mit Beleg je Bereich: Borland System, Crt, Dos, Printer,
   Graph (Laufzeitbibliothek, Befunde A2, C1–C6), Overlay (B3), Maus `0E11` (G6), Grafikstart `0DBD` (F6), VGA256-Treiber (F1),
   Speicherübersicht `0059`. **Nicht grau:** Tastatur `0C48` (in der Vorlagenliste nicht genannt, Remake bildet die
   Tastentabelle nach) und Notsicherung `0C02` (Spielfunktion, im Zweifel nicht grau).
6. **Gelb ohne Adresse:** Das Ziel hat keine Lückenliste mit Spalte „Remake“ (`docs/re-abdeckung.md` der Vorlage); keine
   solchen Fälle.
7. **Gelb bei gemischten Klassen:** gelb nur, wenn die `[APPROX]`-Treffer K oder D sind und keiner V oder ohne Klasse ist
   (sonst rot, „im Zweifel rot“).
8. **Zuordnungsskript im Repo** (`tools/reverse-engineering/abdeckung_grafik.py`) statt nur im Arbeitsordner; Ergebnis
   zusätzlich hier abgelegt (Auftrag des Nutzers), nicht nur als Artifact.
9. **Restliste neu:** [../restliste-v1.md](../restliste-v1.md) mit 83 Markern A001–A083, erstmals eingeordnet (Klassen wie
   Vorlage; V = offene Lücke statt v1.0-Blocker). `funktionskarte.py --write` schreibt nach `docs/analyse/re-abdeckung.md`
   (Vorlage: `docs/re-abdeckung.md`); nicht ausgeführt.
10. **Artifact:** Der Typ „Data“ (Dashboards) passt nicht zur Adresskarte mit Rechteck je Funktion; veröffentlicht ist die
    HTML-Seite.
