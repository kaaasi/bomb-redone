# Reverse Engineering (`re-grundlagen`)

`bombre.py` liest `gamedata/BOMB.EXE` und `gamedata/BOMB.OVR` und schreibt Abgeleitetes nur nach `analyse/re-grundlagen/`
(Schutzordner, nie committen; Ablage im Referenz-Repo per PR). Voraussetzung: Python 3, für `listings` zusätzlich `ndisasm`
(Paket `nasm`).

| Aufruf | Ergebnis |
|---|---|
| `python3 tools/reverse-engineering/bombre.py befunde` | prüft alle Befunde aus `docs/analyse/re-grundlagen.md`, Rückgabe ≠ 0 bei Abweichung |
| `… karte` | prüft die Karte „Bildschirm → Prozeduren“ |
| `… segments` / `… overlays` | Segmenttabelle, Init-Reihenfolge / Overlay-Einheiten und Einsprünge |
| `… fonts` | alle `SetTextStyle`-Aufrufe mit Schrift, Richtung, Größe |
| `… strings` / `… xref` | `texte.tsv` (alle Pascal-Texte mit Offsets) / `prozeduren.tsv` (Prozeduren mit geladenen Texten) |
| `… listings` | annotierte Disassembly je Segment und Overlay |
| `… all` | alles nacheinander |

Optionen: `--gamedata <dir>`, `--out <dir>`.

**Keine Bytes der Originale im Skript** (`AGENTS.md` §2.1, R1-D2): `befunde` vergleicht feste Stellen über `REGIONS`
(Segment bzw. Dateioffset, Länge, SHA-256 des Bereichs); was dort steht, beschreibt `docs/analyse/re-grundlagen.md` in Worten.
Suchen nach Aufrufen bekannter Routinen bauen die Befehlskodierung aus Ziel und Operand (`far_call`, `mov_dx`). Übrig bleiben
nur allgemeine Befehlsmuster (Prolog `push bp; mov bp,sp`, Overlay-Stub `INT 3Fh`, Opcode-Muster mit Platzhaltern) und
kurze Texte (Menütexte, Dateinamen, Optionskürzel). Neue Befunde: Hash mit dem Skript aus den Originalen berechnen, nie die Bytes
eintragen.

## `runde.py` (Spur `re-runde`)

Schreibt nur nach `analyse/re-runde/` (Ablage im Referenz-Repo per PR, R2-D5 = A). Nutzt `bombre.py` (Ladebild, Overlays).

| Aufruf | Ergebnis |
|---|---|
| `python3 tools/reverse-engineering/runde.py befunde` | prüft alle Befunde aus `docs/analyse/runde.md` (Befehlsfolgen in `runde_befunde.py`), Rückgabe ≠ 0 bei Abweichung |
| `… procs` / `… randoms` / `… reals` | `prozeduren.tsv` (Aufrufer, Aufrufe) / `random.tsv` (alle `Random`-Aufrufe mit Argument) / `real48.tsv` (Real48-Konstanten) |
| `… listings` | annotierte Listings: Ziele, Real48-Werte, DS-Variablen, Prozeduranfänge (`ndisasm -s` je Prozedur) |
| `… all` | alles nacheinander |

Die Befunde vergleichen reguläre Ausdrücke über die eigene Disassembly (aus Ziel und Operand gebaut), keine Bytes der Originale;
wie bei `bombre.py` stehen nur kurze Bildschirmtexte (z. B. „Wind: “, „Maximum Power:“) als Klartext im Skript.
