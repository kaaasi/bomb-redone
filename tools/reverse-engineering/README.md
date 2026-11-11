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
