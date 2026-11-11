# Grundsatzentscheidungen

Entscheidungen des Nutzers, je mit Datum. Vorlage durch die Leitstelle nach `AGENTS.md` §2.9 (Problem, 3–5 Optionen mit Vor-
und Nachteilen, Empfehlung); hier steht nur das Ergebnis mit der gewählten Option. Änderungen nur per PR mit neuem Datum.

## Runde 0 (11.11.2026)

| Nr. | Frage | Entscheidung |
|---|---|---|
| E1 | Zielplattform, Grafikbibliothek | **A:** MonoGame DesktopGL (Windows, Linux, macOS). |
| E2 | Welcher Grafikmodus gilt als Original? | **D:** höchster Modus zuerst, Puffer modusneutral (Breite, Höhe, Palette als Parameter). **Befund aus den Originaldateien:** Release 3.2 kennt nur 320 × 200 mit 256 Farben (VGA/MCGA, `BOMB.TXT` „REQUIREMENTS“) – das ist der Maßstab; weitere Modi gibt es im Original nicht. |
| E3 | Maßstab der Originaltreue | **B als Pflicht:** statische Bildschirme pixelgenau, Regeln und Formeln aus der Disassembly belegt. **A als Ziel je Baustein:** bitgleich (gleicher Zufallsgenerator, gleiche Arithmetik), wo belegbar. Markierung `[ORIGINAL]`/`[APPROX]`. |
| E4 | Zeitverhalten | **A:** eine feste DOSBox-Einstellung ist die Referenz für Aufnahmen und Takt. Den Wert schlägt die Leitstelle in Runde 1 nach der Analyse vor (Entscheidung R1-D1). |
| E5 | Audio | **C:** Runde 1 stumm, Projekt `Audio` nur als Gerüst; danach eigene Synthese aus belegten Tonfolgen. Keine Originalaufnahmen im Code-Repo. |
| E6 | Darstellung im Fenster | **B:** ganzzahlige Skalierung, dazu optional die 4:3-Korrektur (320 × 200 hat nichtquadratische Pixel). |
| E7 | Sichtbarkeit von `bomb-redone` | **B:** privat. Folge: GitHub Free ohne Branch-Schutz und mit begrenzten Actions-Minuten (`docs/prozess/anhang.md` B). |

## Runde 1 (Vorbereitung)

| Nr. | Frage | Entscheidung |
|---|---|---|
| R1-D2 | Ablage von Disassembly-Listings | **B** (11.11.2026): annotierte Listings im privaten Referenz-Repo (`analyse/`), im Code-Repo nur eigene Beschreibungen mit Offsets. Ablage durch Leitstelle oder Nutzer per PR. |
| – | Frühe Bildvorlage | Vorschlag des Nutzers (11.11.2026): zwei Gameplay-Videos zuerst statt der DOSBox-Aufnahme. Übernommen mit Grenze: Videobilder belegen Abläufe und Aufbau, keine Pixelgenauigkeit (E3); was nur aus Videos stammt, bleibt `[APPROX]`, bis ein DOSBox-Bild es belegt. |
| R1-D3 | Ziel für `BOMB.CFG` und `*.TWS` (`*.TWS`: siehe GR1-1) | **A** (11.11.2026): in Runde 1 nur lesen. Schreiben und Rundreise sind späteres Ziel. |
| – | Maßgebliche Fassung | **Release 3.2** bleibt die Vorlage (Nutzer 11.11.2026). |
| R1-D1 | DOSBox-Referenz (Zyklen) | **A** (11.11.2026): `cycles=3000`. Beleg aus `re-grundlagen` (`docs/analyse/re-grundlagen.md`): die Delay-Kalibrierung sättigt bei etwa 3.580 Befehlen/ms; 3000 liegt darunter. Annahme 1 Zyklus je Befehl bleibt `[APPROX]`. Fassung: **DOSBox 0.74-3** (Nutzer 11.11.2026, Option A der Vorlage in `docs/arbeitspakete.md`). |
| R1-D5 | Voreinstellungen Crumble/Walls (`BOMBCFG.EXE` 75 / None gegen `BOMB.DOC` 50 % / Random) | **C** (11.11.2026): Werte aus `BOMBCFG.EXE` übernehmen (tatsächliches Verhalten), Abweichung von der Anleitung in `docs/formate/konfiguration.md` dokumentiert. |
| R1-D6 | Ordner für exportierte Bilder (Issue #25) | **A** (11.11.2026): `export/` gehört zu den Schutzordnern (`AGENTS.md` §2.1, `.gitignore`). |
| R1-schrift-1 | Quelle der 8 × 8-Standardschrift (DefaultFont; Glyphen aus dem ROM, nicht in den Originaldateien) | **D** (11.11.2026): VGA-Schrift von DOSBox 0.74-3 (GPL) als Datei im Referenz-Repo für Abgleich und Tests (pixelgleich zur Referenz R1-D1); eine gemeinfreie 8 × 8-Schrift mit geprüfter Herkunft als eingebauter Ersatz, wenn die Referenzdatei fehlt. Umsetzung: Spur `menue`. |
| GR1-1 | Lesen von `*.TWS` (R1-D3 = A, im Gesamtreview Runde 1 als fehlend gemeldet, #78) | **C** (11.11.2026): in Runde 1 nur die Formatbeschreibung aus der Disassembly (Speichern `O3:46DE`, Laden `O3:4D97`; Analyse und Doku, kein Code); der Leser folgt in Runde 2 nach der DOSBox-Aufnahme mit Beispielständen. R1-D3 gilt für `BOMB.CFG` unverändert. |
| R1-D4 | Ablageweg der Listings | **A** (11.11.2026): Die Spur `re-grundlagen` öffnet selbst einen PR im Referenz-Repo (nur `analyse/`), merged nicht. Ausnahme von „Spuren lesen nur“, auf diese Spur beschränkt. |

Arbeitsweise (Nutzer 11.11.2026): `main` nur über Branch und PR; Deutsch in Doku und Commits, Quelltext und Kommentare englisch;
knappe Antworten; je Aufgabe ein Claude-Modell; ChatGPT nur für Reviews; Spuren erst nach Go; Entscheidungen gesammelt.
