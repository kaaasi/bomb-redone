# Vorlage: DOSBox-Aufnahme mit dem Nutzer (nur Ablage, keine Analyse)

**Kurzaufruf** (der Nutzer schreibt ihn in einen neuen Chat; empfohlenes Modell: **Claude Sonnet 5.5**):

> Repo kaaasi/bomb-redone. Arbeite nach `docs/vorlagen/dosbox-aufnahme.md`. Aufnahme: `<runde-1 | …>`.

Zweck: Der Agent führt den Nutzer Schritt für Schritt durch Aufnahmen des Originals unter DOSBox, prüft jede Datei und legt
Spielstände, Konfigurationen und Bilder im Referenz-Repo ab. **Keine Auswertung, keine Codeänderung**; die Auswertung macht
später eine Analysespur. Die Ablage im Referenz-Repo erfolgt im Auftrag des Nutzers per PR (`AGENTS.md` §3: ablegen nur
Leitstelle und Nutzer per PR); gemergt wird vom Nutzer.

## Ablauf für den Agenten

1. **Zugriff:** `add_repo` für `kaaasi/bomb-redone` (access `read`) und `kaaasi/bomb-referenz` (access `push`; ggf. erst per
   `ToolSearch` mit `select:mcp__claude-code-remote__add_repo` laden). Scheitert das, dem Nutzer melden.
2. **Vorhandenes lesen:** im Referenz-Repo `original/spielstaende/README.md` und `bilder/README.md` (Tabellen, SHA-256).
3. **DOSBox-Einstellungen zuerst:** Variante, Version, `machine`, `cycles`, Skalierung und Aufnahmeweg (Bildschirmfoto als PNG
   in 320 × 200, unskaliert) erfragen bzw. die in `docs/entscheidungen.md` festgelegte Referenz nennen; in die README der
   Aufnahme schreiben. Ohne feste Einstellungen keine Aufnahme.
4. **Je Sitzung:** dem Nutzer nur den nächsten Schritt nennen (kurz, nummeriert), auf Dateien und Bilder warten. Jede
   hochgeladene Datei in einem Ordner **außerhalb des Code-Repos** ablegen und sofort prüfen: Größe, SHA-256, bei Bildern Format und Maße (320 × 200 bzw. das ungeschnittene Fenster);
   passt etwas nicht zum Schritt, den Nutzer bitten, ihn zu wiederholen. Dateien und Bilder unverändert übernehmen.
5. **Ablage:** Branch `daten/<aufnahme>-<sitzung>` vom aktuellen `main` des Referenz-Repos. Spielstände und Konfigurationen
   nach `original/spielstaende/` (`BOMB.CFG` umbenannt nach `<zweck>.CFG`), Bilder nach `bilder/<aufnahme>/`
   (Namen `<sitzung>_<nn>_<inhalt>.png`). README: je Datei eine Tabellenzeile (Herkunft, Stand im Spiel, Beobachtungen des
   Nutzers wörtlich knapp, Bilder), SHA-256 in den Block. Commit mit Agenten-Trailer, PR gegen `main` des Referenz-Repos;
   **nicht mergen**. Ein PR je Sitzung (oder einer am Ende, wenn der Nutzer es so will).
6. **Abschluss:** kurze Liste an den Nutzer (PR-Nummern, Dateien) und ein Issue im Code-Repo „DOSBox-Aufnahme <aufnahme>:
   abgelegt“ mit der Liste der Dateien. Keine Änderungen am Code-Repo.

## Aufnahmen

Die Schrittlisten je Aufnahme schreibt die Leitstelle mit dem Plan der Runde hierher (eigener Abschnitt `## Aufnahme <name>`).

## Aufnahme `runde-1` (Plan Leitstelle 11.11.2026, startet erst nach Go und Entscheidung R1-D1)

Allgemein: Original aus `original/gamedata/` in einen leeren Ordner kopieren und in DOSBox einbinden. Nach jedem Schritt das
genannte Bild; Beobachtungen (Ton, Geschwindigkeit, Animationen) als Notiz.

### Sitzung 1 – Konfiguration und Titel

1. `BOMBCFG.EXE` starten: jeden Menüpunkt und jede Hilfeseite fotografieren. Mit den Voreinstellungen „Done | Save & Exit“;
   die erzeugte `BOMB.CFG` hochladen (**standard.CFG**).
2. Eine zweite Konfiguration mit **genau einer** geänderten Einstellung (Ton aus) speichern und hochladen (**ton-aus.CFG**),
   danach je eine weitere mit genau einer anderen Änderung, soweit es Einstellungen gibt (Namen `<einstellung>.CFG`).
3. `BOMB.EXE` mit **standard.CFG** starten: jedes Bild der Titelfolge fotografieren (auch Zwischenstände von Überblendungen,
   so gut es geht) und notieren, wie lange jedes Bild steht und wie es wechselt.
4. Einmal `BOMB1.RAW` umbenennen und starten, einmal zusätzlich `BOMB2.RAW`: Unterschiede fotografieren und notieren.

### Sitzung 2 – Hauptmenü und Optionen

1. Hauptmenü fotografieren; jeden Knopf und jede Option einmal mit der Maus und einmal mit der Tastatur bedienen, je Zustand
   ein Bild (auch gedrückte Knöpfe und Auswahlmarken).
2. Spielerauswahl: 2 Spieler (Mensch gegen Mensch), Namen eingeben, Bilder der Eingabe. Danach eine Partie mit je einem der
   7 Computergegner anlegen und die Auswahl fotografieren.

### Sitzung 3 – Eine Runde, Einkauf, Spielstand

1. Partie Mensch gegen Mensch starten: Spielfeld vor dem ersten Schuss fotografieren, dann nach einem Schuss ohne Treffer,
   nach einem Treffer und am Rundenende. Statusbildschirm fotografieren.
2. Einkauf: Liste fotografieren, einen Gegenstand kaufen, erneut fotografieren.
3. Im Statusbildschirm „SAVE“: Menü fotografieren, als **GAME1** sichern, Datei `GAME1.TWS` hochladen. Eine Runde
   weiterspielen, als **GAME2** sichern und hochladen.
4. Neu starten, „LOAD“: Menü fotografieren, GAME1 laden, erstes Bild danach fotografieren.
