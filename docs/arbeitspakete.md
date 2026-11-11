# Arbeitspakete

Die Portierung ist in Pakete (Spuren) aufgeteilt, die parallel in eigenen Chats bearbeitet werden können. Jede Spur hat einen
Branch `paket/<name>` mit einem PR, eigene Dateien und klare Abnahmekriterien. Regeln und Einrichtung: [AGENTS.md](../AGENTS.md).
Den Status pflegt jede Spur nur in ihrem eigenen Abschnitt. Abgeschlossene Runden stehen in [archiv/](archiv/).

**Koordination:** Die Leitstelle verteilt Pakete, prüft und mergt Pull Requests und pflegt Plan und Doku
(Rollen: [prozess/zusammenarbeit.md](prozess/zusammenarbeit.md)). **Spuren starten erst nach dem Go des Nutzers.**

## Aktuelle Runde: Runde 0 – Einrichtung (11.11.2026)

| Ergebnis | Stand |
|---|---|
| Referenz-Repo: Struktur und Originaldateien | [bomb-referenz #1](https://github.com/kaaasi/bomb-referenz/pull/1) gemergt (Nutzer, 11.11.2026) |
| Einrichtungs-PR | [#1](https://github.com/kaaasi/bomb-redone/pull/1) gemergt (Nutzer, 11.11.2026), CI auf `main` grün |
| Unabhängiges Review | [reviews/2026-11-11-einrichtung.md](reviews/2026-11-11-einrichtung.md) (ChatGPT, PR #2): **Nein**, zwei P2 zu `.gitignore`; #1 war da schon gemergt. Fixlauf: [#4](https://github.com/kaaasi/bomb-redone/pull/4), Nachprüfung bzw. Entscheidung des Nutzers offen |
| Werkzeug-Release `claude-werkzeuge` | vorhanden (Workflow nach dem Merge gelaufen); `fetch-reference.sh original` gegen `main` geprüft, Prüfsummen stimmen |
| Ablagen `analyse/` und `videos/` im Referenz-Repo | [bomb-referenz #2](https://github.com/kaaasi/bomb-referenz/pull/2) offen |

## Plan Runde 1 – Dateiformate, Reverse Engineering, erste Bildschirme (Entwurf 11.11.2026, **kein Go**)

Grundlage: Erstbefund in [originaldateien.md](originaldateien.md). Kernaussage: Außer zwei Titelbildern gibt es keine
Grafikdateien; fast alles Sichtbare zeichnet das Programm selbst. Runde 1 braucht deshalb früh Bildschirmfotos aus DOSBox und
einen Überblick über den Programmcode.

**Base für alle Spuren:** aktueller `main` nach Merge von Runde 0. Regeln: Befund reproduzieren, roter Test, dann grün;
`[ORIGINAL]` mit `BOMB.EXE 0x…`/`BOMB.OVR 0x…`/Anleitung/Referenzbild, sonst `[APPROX]`; Quelltext und Kommentare englisch;
höchstens drei Fortsetzungen je Spur. Bei Unklarheit „wie im Original“; bleibt es unklar, Teil blockiert lassen, die Leitstelle
bündelt die Fragen.

### Nachträge vom 11.11.2026 (nach Rückmeldung des Nutzers)

- **R1-D2 = B** (Listings im Referenz-Repo, `analyse/`).
- **Videos zuerst:** Der Nutzer hat zwei Gameplay-Videos; sie ersetzen am Anfang die DOSBox-Aufnahme. Dazu braucht die
  Leitstelle die Adressen und die Einzelbilder (YouTube ist aus der Arbeitsumgebung nicht ladbar): der Nutzer entnimmt sie
  lokal (z. B. `ffmpeg -i <video> -vf fps=1 %05d.png`) und lädt sie als ZIP hoch, Ablage in `videos/<kurzname>/` des
  Referenz-Repos. Grenze: Videobilder belegen Abläufe und Aufbau, nicht die Pixel; Abnahmen der Bildschirm-Spuren gegen
  Videobilder gelten als vorläufig (`[APPROX]`), die DOSBox-Aufnahme `runde-1` folgt gezielt für die Bildschirme, die
  umgesetzt sind. Damit wird R1-D1 erst vor dieser Aufnahme gebraucht, nicht vor dem Go.
- **`BOMB.CFG` vom Nutzer (für R1-D3):** Die hochgeladene Datei ist **beim Hochladen beschädigt** worden (30 Byte; zwei
  Stellen enthalten das UTF-8-Ersatzzeichen `EF BF BD`, das letzte Byte ist `3F` – Bytes ab 0x80 wurden ersetzt). Sie ist
  deshalb nicht abgelegt. Erkennbar bleibt: Die Datei beginnt mit der Versionskennung „3.2“ und ist danach eine kurze Folge
  einzelner Bytes mit kleinen Werten (vermutlich etwa 26 Byte im Original). Bitte als ZIP erneut hochladen.
- **Befund zur Geschwindigkeit (E4, R1-D1):** `BOMB.DOC` nennt eigene Geschwindigkeitsoptionen des Spiels (`ESnn`
  Explosion, `FSnn` Fall, `PSnn` Projectile, `SSnn` Shot) und Einstellungen wie Wind, Wandverhalten und Hintergrund. Der Takt
  hängt also nicht nur von DOSBox ab, sondern auch von `BOMB.CFG`; die Referenz für Aufnahmen ist DOSBox-Einstellung **und**
  Konfigurationsdatei.

### Offene Entscheidungen

**R1-D1 – DOSBox-Referenz (folgt aus E4).** Problem: Aufnahmen und Takt brauchen genau eine Einstellung.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: DOSBox 0.74-3, `cycles=3000` (etwa 286/386SX) | überall verfügbar, stabil | alte Fassung, wenig Aufnahmekomfort |
| B: DOSBox Staging (aktuell), `cycles=3000` | gepflegte Fassung, saubere PNG-Aufnahme in Originalgröße | Verhalten kann sich zwischen Versionen ändern → Version festschreiben |
| C: DOSBox-X, `cycles=3000` | Debugger eingebaut, hilft beim Reverse Engineering | mehr Einstellungen, mehr Fehlerquellen |
| D: Zyklen erst nach der Analyse festlegen (Zeitschleifen im Code suchen), Aufnahme bis dahin mit B | Wert ist belegt statt geschätzt | Titel- und Menübilder sind vom Takt unabhängig, Animationen müssten ggf. neu aufgenommen werden |

Empfehlung: **D mit B** – statische Bilder jetzt mit DOSBox Staging (Version in der README), Zyklenwert als Vorschlag aus
`re-grundlagen`. Der Wert 3000 ist eine Schätzung ohne Beleg.

**R1-D2 – Ablage von Disassembly-Listings – entschieden: B (Nutzer 11.11.2026).** Problem: Listings sind abgeleiteter Originalcode.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: nur lokal erzeugen (Skript im Code-Repo), nirgends ablegen | nichts Originales in Git | jeder Lauf erzeugt neu; Anmerkungen gehen verloren |
| B: annotierte Listings im privaten Referenz-Repo (`analyse/`), im Code-Repo nur eigene Beschreibungen mit Offsets | Anmerkungen bleiben, Code-Repo bleibt sauber | Spuren bräuchten Schreibweg ins Referenz-Repo (heute nur Leitstelle/Nutzer) |
| C: Listings im Code-Repo | ein Ort | widerspricht `AGENTS.md` §2.1 |

Empfehlung: **B**, Ablage durch die Leitstelle per PR aus dem, was die Spur als Datei liefert.

**R1-D3 – Ziel für `BOMB.CFG` und `*.TWS`.** Problem: Umfang der Formatarbeit.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: nur lesen (Import von Originalständen) | reicht für Abgleich und Tests | Remake-Stände laufen nicht im Original |
| B: lesen und bytegleich schreiben (Rundreise) | stärkster Beleg für das Format, Abgleich in beide Richtungen | deutlich mehr Aufwand, erst mit vollständigem Spielzustand möglich |
| C: zurückstellen bis nach den ersten Bildschirmen | Runde 1 bleibt klein | Spielzustand wird ohne Formatwissen entworfen |

Empfehlung: **A in Runde 1** (Felder benennen, soweit belegbar), B als späteres Ziel.

### Welle 1 (parallel, nach Go)

#### Spur `formate` – Titelbilder lesen und exportieren

- **Inhalt:** `RawImage` (320 × 200 Indizes + Palette, 6 Bit → 8 Bit) in `src/BombRedone.Formats/`, `bombtool export` (PNG nach
  `export/`), `docs/formate/dateiformate.md`. Prüfen, ob die Palette am Dateiende wirklich so verwendet wird (Abgleich mit
  Bildschirmfoto aus der Aufnahme, sobald vorhanden; bis dahin `[APPROX]`).
- **Dateien:** `src/BombRedone.Formats/RawImage*.cs`, `Palette*.cs`, `src/BombRedone.Tools/Export*.cs` (+ eine Zeile in
  `Program.cs`), `tests/BombRedone.Formats.Tests/RawImage*`, `docs/formate/*`.
- **Abnahme:** synthetische Tests (Rundreise, Fehlerfälle: zu kurz, zu lang), Originaldaten-Tests (Maße, Farbanzahl,
  Prüfsumme des dekodierten Bildes), Export läuft. **Modell:** Claude Sonnet 5.5. **Review:** Claude Opus 5.5 (Erstreview).

#### Spur `re-grundlagen` – Programmaufbau erschließen (Analyse, kein Produktcode)

- **Inhalt:** MZ-Kopf und Segmente von `BOMB.EXE`; Aufbau von `BOMB.OVR` (`FBOV`, Overlay-Einheiten und ihre Zuordnung);
  Erkennen der Turbo-Pascal-6-Laufzeit und der Bibliothekseinheiten (System, Crt, Graph/BGI, Overlay), damit nur Spielcode übrig
  bleibt; eingebundene BGI-Treiber und Schriften; Zufallsgenerator (`Random`/`Randomize`, `RandSeed`) und Zahlentyp der Physik
  (Real48, 8087 oder Festkomma); Zeitschleifen und Tonausgabe (Ports 0x42/0x43/0x61); Tabelle aller Texte mit Offsets;
  Karte „Bildschirm → Prozeduren“ als Arbeitsgrundlage für die Bildschirm-Spuren. Vorschlag für R1-D1 (Zyklen).
- **Dateien:** `docs/analyse/re-grundlagen.md`, `tools/reverse-engineering/*` (Skripte, die aus `gamedata/` lesen und nach
  `export/` schreiben).
- **Abnahme:** jede Aussage mit Offset und ausführbarem Beleg (Skriptaufruf, Ergebnis); Liste „nicht entscheidbar ohne DOSBox“.
  **Modell:** Claude Opus 5.5. **Review:** Claude Opus 5.5 (frische Sitzung).

#### Aufnahme `runde-1` – Bildschirmfotos, `BOMB.CFG`, Spielstände (mit dem Nutzer)

- Ablauf und Schritte: [vorlagen/dosbox-aufnahme.md](vorlagen/dosbox-aufnahme.md), Abschnitt „Aufnahme `runde-1`“.
  **Modell:** Claude Sonnet 5.5. Braucht R1-D1. Ergebnis im Referenz-Repo (`bilder/runde-1/`, `original/spielstaende/`).

### Welle 2 (nach Merge von Welle 1 und abgelegter Aufnahme)

#### Spur `titel` – erste Bildschirme: Titelfolge

- **Inhalt:** `Screen`-Grundgerüst in `UI` (ein Bildschirm, Eingabe, Takt als Eingabe), Titelfolge mit `BOMB1.RAW`/`BOMB2.RAW`
  einschließlich des Verhaltens bei fehlenden Dateien (`BOMB.TXT` „GRAPHICS FILES“), Anzeige im Fenster.
- **Abnahme:** Pixelvergleich gegen die Bilder der Aufnahme (Sitzung 1), Test je Bild. **Modell:** Claude Opus 5.5.

#### Spur `schrift-menue` – Schrift und Hauptmenü

- **Inhalt:** Schriftdarstellung des Originals (aus `re-grundlagen`: BGI-Schrift oder eigene Zeichenroutine), Hauptmenü als
  statischer Bildschirm, danach Bedienung mit Maus und Tastatur.
- **Abnahme:** Pixelvergleich gegen die Aufnahme (Sitzung 2), jeder Zustand ein Test. **Modell:** Claude Opus 5.5.

#### Spur `formate-2` – `BOMB.CFG` und `*.TWS` (nach R1-D3)

- **Inhalt:** Formatbeschreibung aus den Beispieldateien (Differenzen zwischen Dateien mit genau einer Änderung) und den
  Lese-/Schreibroutinen im Programm; Leser in `Formats`. **Modell:** Claude Opus 5.5.

**Rundenende:** Gesamtreview durch ChatGPT (`prozess/anhang.md` C), danach Plan Runde 2 (Spielfeld, Gelände, Schuss).

### Zuständigkeit für gemeinsame Dateien (Runde 1)

- `AGENTS.md`, `CLAUDE.md`, `docs/prozess/*`, `docs/vorlagen/*`, `docs/entscheidungen.md`, `tools/pruefen*.sh`, `.github/*`,
  `tests/**/Support/`: allein die Leitstelle.
- `src/BombRedone.Tools/Program.cs`: je Spur eine Registrierungszeile.
- `src/BombRedone.Desktop/*`: allein `titel`.
