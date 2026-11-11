# Bomb Redone

Portierung von **Tank Wars** (Kenneth Morse, MS-DOS, Release 3.2 von 1992; Arbeitstitel BOMB) nach C#/.NET 10 – zuerst
originalgetreu (Pixel, Regeln, Abläufe), danach Erweiterungen.

> **Wichtig:** Dieses Repository enthält **keine** Dateien des Originalspiels. Sie werden zur Laufzeit und in Tests aus einer
> eigenen Kopie im Ordner `gamedata/` gelesen.

## Projektstruktur

| Ordner | Inhalt |
|---|---|
| `src/BombRedone.Desktop` | Das Spiel (MonoGame-Fenster; F11 Vollbild, F10 4:3-Korrektur, Esc Ende) |
| `src/BombRedone.UI` | Bildschirme – zeichnen in einen 320×200-Palettenpuffer, ohne Fensterabhängigkeit |
| `src/BombRedone.Core` | Spielzustand und Regeln |
| `src/BombRedone.Formats` | Lesen der Originaldateien |
| `src/BombRedone.Audio` | Tonfolgen (vorerst Gerüst) |
| `src/BombRedone.Tools` | `bombtool` – Untersuchen und Exportieren |
| `tests/` | xUnit, je Schicht ein Projekt |
| `docs/` | Entscheidungen, Prozess, Arbeitspakete, Formate, Analysen, Reviews |

## Voraussetzungen und Start

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (Version in `global.json`)
- Originaldateien nach `gamedata/` ([docs/originaldateien.md](docs/originaldateien.md))

```bash
tools/pruefen.sh --schnell                                    # Build und Tests
dotnet run --project src/BombRedone.Tools -- info gamedata    # Originaldateien prüfen
dotnet run --project src/BombRedone.Desktop                   # Fenster (noch ohne Inhalt)
```

## Stand

Runde 0 (Einrichtung) – Gerüst, Prüfung, Prozess. Plan: [docs/arbeitspakete.md](docs/arbeitspakete.md) ·
Entscheidungen: [docs/entscheidungen.md](docs/entscheidungen.md) · Regeln für alle Agenten: [AGENTS.md](AGENTS.md)
(Claude Code zusätzlich: [CLAUDE.md](CLAUDE.md))
