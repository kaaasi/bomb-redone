# Gesamtreview Runde 2 – Teil 2

Codex, Fortsetzung von [Teil 1](2026-11-15-gesamtreview-runde-2.md). Base
`39fdbcbc35dd8bbc45df843b2487f82ad69cb21f`, geprüfter Head `ce05f0eb3d5d715e6c9c2468b9fd74b0cbfc47ff`.
`git rev-parse HEAD` = Head, `git status --porcelain` vor der Prüfung leer. Referenzdaten separat; Originalprüfsummen
bereits in Teil 1 geprüft. Die dort dokumentierten Gegenbeispiele und Ergebnisse werden nicht wiederholt.

## 1. Merge-Empfehlung

**Ja für diesen Head.** In beiden Teilen kein neuer Produktfehler nachgewiesen. Die gezielten Originalcode-,
Bild- und Randfallvergleiche stützen die umgesetzten Teile der Runde; die verbleibenden, unten benannten Grenzen
sind für den vereinbarten Rundenumfang akzeptabel. Dies ist keine Abnahme der noch nicht umgesetzten Spielfolge.

## 2. Befunde

Keine neuen P0–P3. **Erwartet offen bestätigt:** #39, #71, #102, #140, #141 laut GitHub jeweils `open`;
Sitzung 4, `[APPROX]` in „Restlücken Welle 2“ und Todesexplosion, Zugfolge und Computer in Runde 3 bleiben offen.

## 3. Ausgeführte Prüfungen, akzeptierte Approximationen und Annahmen

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 1095 | 0 | 0 |

`tools/pruefen.sh --schnell`, SDK 10.0.402, Release-Build mit Warnungen als Fehler, Ergebnis bestanden.
Referenzklon über `REFERENZ`, `BOMB_SPIELSTAENDE` und `BOMB_BILDER` eingebunden.

- **Emulator:** `feld.py`/`feld_machine.py`/`feld_schuss.py` und Aufrufübergänge gelesen. EXE und fixierte
  Overlays führen Aufbau, Flug, Einschlag und Grafikcode aus; Traps zählen bzw. fotografieren und geben `False`
  zurück. Ausnahmen: Titel übersprungen, Sätze nach Mischen zurückgesetzt, Seed/Optionen und Schussparameter
  gesetzt; DOS/BIOS/VGA, Uhr, Palette und I/O sind Modelle. Keine Hardware-IRQs, Maus oder Echtzeitrotation.
  Die Probe ersetzt die DOSBox-Bilder nicht und belegt keine Computerzüge; unbekannte Dienste werden protokolliert.
- **Real48:** Randtests für Null mit Mantisse, Unter-/Überlauf, Division durch null, Rundung und negatives Sqrt
  gelesen; der Prüflauf enthält die 20.000-Fälle-Hashes je Trigonometrieoperation und ganze Grad −360..720.
  Unabhängige Original-Laufzeitprobe `python3 -I tools/reverse-engineering/real48.py --gamedata gamedata eval …`:
  `mul 400000000000 420000000000` → `010000000000`, `div 810000000000 000000000000` → Fehler 200,
  `sqrt 830000000080` → Fehler 207, `round 800000000080` → `FFFFFFFF`; entsprechend den Core-Tests.
- **Krater und Erdrutsch:** `python3 -I tools/reverse-engineering/feld_schuss.py --gamedata gamedata --font
  /tmp/bomb-referenz-review/schriften/dosbox-0.74-3-vga-8x8.bin --root-out /tmp/feld-post-review post --only 1 4 7`
  reproduziert die drei `AfterShotProbeTests`-Zeilen einschließlich Feld, Hintergrund, DAC, Panzerzustand und Seed;
  Fall 1/4 schaltet Fallen um, Fall 7 hat Schaden und Fall. Core-Vergleich im Prüflauf grün.
  Gegenprobe in temporärer Kopie: `AfterShot.Run` Bedingung `crumble > 0` → `crumble > 1000`, dann
  `dotnet test tests/BombRedone.UI.Tests --filter FullyQualifiedName~PlayfieldShotSessionTests -v q`:
  4 fehlgeschlagen, 1 bestanden; die Bildtests erfassen damit den Erdrutsch.
- **Startwert:** `dotnet run --project src/BombRedone.Tools -c Release --no-build -- seed --players 2
  --time-from 00:00:00.00 --max-steps 30 --background 8 --wind -92 --tanks 25,281
  --terrain 20:136,100:132,160:161,200:185,250:141,300:141` → ein Kandidat `17:12:39.69`, k=1,
  vor Mischen `4983853D`, danach `D2E3BA6E`. Vorfilter, Endpunkt des Zeitfensters und Rand-Ausschluss gelesen;
  Tests decken die beiden im früheren Review genannten Grenzfälle nun ab. Eindeutigkeit gilt für diesen Suchraum.
- **Bilder:** `dotnet run --project src/BombRedone.Tools -c Release --no-build -- render playfield gamedata
  /tmp/review-pf.png --seed D2E3BA6E`; eigener PIL-Vergleich der Indizes gegen `s3_02`: 268 Unterschiede,
  alle Kopfzeile, davon 248 fehlende Zugwerte und 20 ungemischter Name. Bildtests im Prüflauf vergleichen
  `s3_02` einschließlich relevanter DAC-Werte und `s3_03`–`s3_07` im Feld: erste drei Schüsse pixelgleich;
  beim vierten 371/1371 Abweichungen im Bereich des toten Panzers (Todesexplosion noch offen).
  Die Schussparameter sind aus den Bildern gesucht; der Test bildet den Rohrwechsel näherungsweise nach.
- **Übergänge/Bedienung:** Desktop-Aufruf von Spielerauswahl über `RoundSequence` zur Vorschau und Tasten-/Schuss-
  weiterleitung gelesen; Testlauf umfasst die Bildschirm- und Sitzungsprüfungen. Vorschau ersetzt noch keinen echten Zug;
  Einkauf, vollständiges Laden/Speichern und Computerabläufe sind nicht als spielbare Runde integriert.

**Akzeptierte `[APPROX]`:** die in Teil 1 und der Sitzung-4-Liste genannten Grenzen, insbesondere keine
Palettenrotation, angenäherte VGA-Grundpalette, Schuss-Timing, Speichergrenze bei Spur/Erdrutsch und Vorschauzug;
Satzmischung und Todesexplosion folgen in Runde 3. **Annahmen / Grenzen:** Kein eigener Echtzeit-DOSBox-Lauf,
keine vollständige Prüfung aller Emulator-Instruktionen oder jedes Startwerts; die Bildtests decken nur die
aufgenommenen Szenen ab. Keine erneute vollständige Prüfung von Einkauf und Computer als Runden-3-Umfang.
