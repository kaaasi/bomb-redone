# Originaldateien: Ablage, Laden, Erstbefund

Die vom Nutzer bereitgestellte Fassung **Tank Wars Release 3.2** liegt im privaten Repository
[`kaaasi/bomb-referenz`](https://github.com/kaaasi/bomb-referenz). Zugriff braucht Leserechte auf dieses Repository.

| Inhalt | Pfad im Referenz-Repository |
|---|---|
| Unverändertes hochgeladenes Archiv | `original/bomb32.zip` |
| Vollständig entpackte Dateien | `original/gamedata/` (13 Dateien) |
| SHA-256-Prüfsummen für ZIP und entpackte Dateien | `original/SHA256SUMS` |
| Herkunft, Vollständigkeit, Formate | `original/README.md` |
| Spielstände `*.TWS` und `BOMB.CFG` aus DOSBox | `original/spielstaende/` (noch leer) |
| Referenzbilder aus DOSBox | `bilder/<aufnahme>/` (noch leer) |

SHA-256 der unveränderten ZIP: `8c38643090097d1601fb404163dc91d41890ed955e82b03673f2b6705e7b7e8c`.
Nachweis des Inhalts sind die Prüfsummen, nicht ein fester Commit.

## Laden in einer neuen Umgebung

```bash
tools/claude/fetch-reference.sh original     # Sparse-Klon nach ~/referenz (REFERENZ=<pfad> ändert das Ziel), prüft SHA256SUMS
ln -s ~/referenz/original/gamedata gamedata  # oder: tools/claude/setup-workspace.sh ~/referenz/original/gamedata
```

Erwartet wird `gamedata/BOMB.EXE` im Stamm des Code-Repositories (Groß-/Kleinschreibung der Dateinamen ist egal). Der Ordner
ist per `.gitignore` geschützt. Ohne Zugriff auf das Referenz-Repository: beim Nutzer anfragen.

## Erstbefund (11.11.2026, Leitstelle; noch kein Reverse Engineering)

**Vollständig:** `BOMB.TXT` nennt 13 Dateien, alle sind vorhanden; das ZIP ist fehlerfrei. `BOMB.CFG` fehlt absichtlich (wird von
`BOMBCFG.EXE` erzeugt; ohne sie startet `BOMB.EXE` nicht), Spielstände sind nicht enthalten.

| Datei | Byte | Format (Beleg) |
|---|---:|---|
| `BOMB.EXE` | 106.192 | DOS-MZ, Turbo Pascal 6.0 (Laufzeittext „1983,90 Borland“), 1.439 Relokationen, nicht gepackt, BGI eingebunden (u. a. „VGA256“) |
| `BOMB.OVR` | 127.951 | Turbo-Pascal-Overlay, Kennung `FBOV`; Code und Texte |
| `BOMB1.RAW`, `BOMB2.RAW` | je 64.768 | 320 × 200 Palettenindizes (64.000 Byte), danach 256 × 3 Byte Palette mit Werten ≤ 63; Titelbilder |
| `BOMBCFG.EXE` | 109.952 | DOS-MZ, Turbo Pascal 6.0; Konfigurationsprogramm |
| `BOMBCFG.HLP` | 24.315 | Hilfedatei, Kennung `FBHF` |
| `BOMB.DOC`, `BOMB.TXT`, `REVISION.HST` | 36.240 / 16.568 / 22.081 | Text (ASCII, CR LF): Anleitung, Kurzbeschreibung, Änderungsliste |
| `BOMB.ICO`, `BOMBCFG.ICO` | je 766 | Windows-Symbol 32 × 32, 16 Farben |
| `BOMB.ICP`, `BOMBCFG.ICP` | je 888 | OS/2-Symbol 32 × 32 |

Aus der Anleitung: nur 320 × 200 mit 256 Farben (VGA/MCGA), Maus optional, 1 bis 10 Spieler, 7 Computergegner, Toneffekte
abschaltbar (Ausgabeweg nicht genannt), Spielstände `<name>.TWS`, Einstellungen in `BOMB.CFG`.

**Folgen:** Außer den beiden Titelbildern gibt es keine Grafikdateien – Schrift, Panzer, Waffen, Gelände und Menüs zeichnet das
Programm selbst. Sie sind nur über Disassembly und Bildschirmfotos zu erschließen. Die Formate von `BOMB.CFG` und `*.TWS`
brauchen Beispieldateien aus DOSBox (`docs/vorlagen/dosbox-aufnahme.md`).

Die Originaldateien bleiben im Referenz-Repository bzw. lokal im ignorierten `gamedata/`. In dieses Code-Repository kommen nur
Beschreibungen und Links.
