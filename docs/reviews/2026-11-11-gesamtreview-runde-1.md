# Gesamtreview Runde 1 – Zwischenstand

Base `ca689e9590c93292c59aae9e8083eb659b9cfa50` · Head `a49340d3f3d7da4b188f87f4564d826c8e02b7af` · Reviewer: Codex, unabhängige Sitzung.

## Merge-Empfehlung

**Noch keine abschließende Empfehlung.** Der überprüfte Ausschnitt und der Prüflauf ergaben keinen neuen nachgewiesenen Produktfehler; der risikoorientierte Gesamtabgleich ist aber noch nicht abgeschlossen. Fortsetzung in einer neuen Datei `2026-11-11-gesamtreview-runde-1-teil2.md` auf neuem Branch gemäß Anhang D; insbesondere O3:5AEF und die weiteren Zweige von O3:0E1F/O3:6696 sowie Lade-/Speicher-, Runden- und Spielübergänge prüfen. Nicht Geprüftes ist für sich allein keine Ablehnung.

## Befunde

Keine neuen nachgewiesenen Produktfehler in den bisher geprüften Bereichen. Erwartet offene Issues #15–#21, #34, #39, #46–#48, #61, #68, #71, #74, #75: auf GitHub alle offen und P3; keine erneute Meldung. Insbesondere #47 (R44-02): ein fester 60-ms-Sollwerttest ist nicht vorhanden; `TitleScreenTests` verwendet `CycleStepDuration`. Die vorhandenen Zeitablauftests sind keine Bestätigung des festen Sollwerts. R64-01: Tests `EqualScoreButDifferentWinsGetDifferentRanks` und `EqualScoreButDifferentAverageWinsGetDifferentRanks` vorhanden.

## Ausgeführte Prüfungen

- Checkout sauber, Head exakt wie oben; SDK 10.0.402; `dotnet build BombRedone.slnx -c Release -warnaserror`: erfolgreich, 0 Warnungen/Fehler.
- `BOMB_REQUIRE_GAMEDATA=1 dotnet test tests/BombRedone.Formats.Tests --filter FullyQualifiedName~RawImageTests`: 17 bestanden; R12-B1 umfasst die dekodierten Pixel mit Präfix `e54ecb608342`.
- `dotnet test tests/BombRedone.UI.Tests --filter 'FullyQualifiedName~NulEndsTheDefaultFontOutput|FullyQualifiedName~NulEndsTheVerticalDefaultFontOutputFromTheLastCharacter'`: 2 bestanden (R43-01).
- `tools/pruefen.sh --schnell` mit Originaldaten, Originalkonfiguration und Referenzschrift:

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 621 | 0 | 0 |

- Erstlauf ohne die im älteren lokalen Referenzcheckout fehlende Beispiel-CFG: 618/3/0; drei fehlende-Datei-Fehler, nach Bezug der CFG über den Connector obiger Wiederholungslauf grün. Referenzschrift über Connector bezogen, SHA-256 `75c79a7e…f319` geprüft. `BitmapFontSource` sucht externe Schrift, prüft Hash und verwendet sonst den dokumentierten gemeinfreien Ersatz (R1-schrift-1 = D).
- `python3 .github/scripts/originalschutz_pruefen.py`: keine Originaldateien eingecheckt; Dateitypsuche nach Bildern, Binärschrift und Originalformaten ohne Treffer. Kein vollständiger Herkunftsnachweis aller Quelltextdaten.
- `python3 tools/reverse-engineering/bombre.py befunde --gamedata gamedata --out /tmp/bomb-review-analysis`: 40/40 dokumentierte Grundbefunde bestätigt. Waffentabelle durch Originaldaten-Test gegen `BOMB.EXE` verglichen; Einkaufs-Minimum O3:0E3D–0E83 und Teamzählung O3:66A2–66E3 stichprobenartig disassembliert und mit Core verglichen, kein Widerspruch im geprüften Ausschnitt.
- Desktop mit Originaldaten und Referenzschrift gestartet: Enter Titel → Menü; Down/Enter Optionen; Right ändert Crumble 10 → 11; Enter zurück, dreimal Down/Enter → Spielerauswahl. Zufall/Uhr im Desktop eingespeist; Suche in `src` fand keine Systemuhr bzw. `Random.Shared` im Core. Kein pixelgenauer DOSBox-Vergleich.
- Akzeptiert: vorhandene `[APPROX]` bis DOSBox-Aufnahme `runde-1`; keine davon als neuer Befund. Annahme: die bezogene Beispiel-CFG ist die dokumentierte Originalkonfiguration. Nicht abgeschlossen: gezielter Originalabgleich weiterer Economy-Zweige, Originalstände/Bildschirmfotos, übrige Bedienpfade und Entscheidungen; Schussphysik und vollständiger Rundenablauf sind in diesem Rundenstand noch nicht implementiert.
