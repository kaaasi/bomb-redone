# Gesamtreview Runde 1 – Teil 2

Unabhängige Fortsetzung von [Teil 1](2026-11-11-gesamtreview-runde-1.md). Base `ca689e9590c93292c59aae9e8083eb659b9cfa50`, Head `1ee630ce2d0286ac2abbfd6dbe37731cf4293e56`; Reviewer Codex.

## Merge-Empfehlung

**Nein für diesen Rundenstand:** Der in R1-D3 für Runde 1 beschlossene Import von Originalspielständen fehlt (P2-1). Ohne BOMB.CFG verwendet der Desktop zudem andere Voreinstellungen als R1-D5 (P2-2). Beide wirken auf die tatsächlich erreichbaren Nutzungswege. Teil 1 und die Prüfungen unten fanden darüber hinaus keine neuen nachgewiesenen Produktfehler. Der ausstehende DOSBox-Pixelabgleich allein ist kein Ablehnungsgrund.

## Befunde

- **P2-1 – Originalspielstand kann nicht importiert werden (R1-D3).** [Entscheidung R1-D3](https://github.com/kaaasi/bomb-redone/blob/1ee630ce2d0286ac2abbfd6dbe37731cf4293e56/docs/entscheidungen.md#L24) und [Plan](https://github.com/kaaasi/bomb-redone/blob/1ee630ce2d0286ac2abbfd6dbe37731cf4293e56/docs/arbeitspakete.md#L63) nennen Lesen/Import von `*.TWS` in Runde 1. `rg -n -i 'tws|spielstand|savegame|save.game' src` liefert keinen Treffer; [DesktopGame.cs](https://github.com/kaaasi/bomb-redone/blob/1ee630ce2d0286ac2abbfd6dbe37731cf4293e56/src/BombRedone.Desktop/DesktopGame.cs#L131) leitet `LoadGame` über den Standardzweig an einen Platzhalter. Damit kann ein Originalstand nicht eingelesen werden. Ein Original-`.TWS` für einen Formatvergleich lag hier nicht vor; die Entscheidung muss umgesetzt oder ausdrücklich für später geändert werden.
- **P2-2 – Fallback bei fehlender CFG widerspricht R1-D5.** [DesktopGame.cs](https://github.com/kaaasi/bomb-redone/blob/1ee630ce2d0286ac2abbfd6dbe37731cf4293e56/src/BombRedone.Desktop/DesktopGame.cs#L124) erzeugt ohne `BOMB.CFG` `new OptionsSettings()`, dessen [Crumble-Vorgabe](https://github.com/kaaasi/bomb-redone/blob/1ee630ce2d0286ac2abbfd6dbe37731cf4293e56/src/BombRedone.UI/Screens/OptionsSettings.cs#L17) 10 statt der [beschlossenen 75](https://github.com/kaaasi/bomb-redone/blob/1ee630ce2d0286ac2abbfd6dbe37731cf4293e56/docs/entscheidungen.md#L27) ist; weitere Felder weichen ebenfalls von der BOMBCFG-Beispielkonfiguration ab. Beleg: `gamedata/BOMB.CFG` fehlt; Fenster → Game Options zeigt `10%`. `dotnet test tests/BombRedone.UI.Tests -c Release --no-build --filter FullyQualifiedName~DefaultsAreTheValuesBeforeBombCfgIsRead` besteht mit Sollwert 10; `BOMB_REQUIRE_GAMEDATA=1 dotnet test tests/BombRedone.Formats.Tests -c Release --no-build --filter FullyQualifiedName~ExampleMatchesDocumentedDefaults` besteht mit 75. Mit gültiger CFG werden deren Werte übernommen. Das Original beendet sich ohne CFG; der Befund betrifft den vom Remake angebotenen Fallback und die Rundenentscheidung, nicht eine behauptete Original-Ausgabe ohne CFG.

**Testlücken:** Kein neuer eigenständig nachgewiesener Testlückenbefund. Bekannte #47 und #75 bleiben offen; insbesondere decken Bildschirmtests BIOS-Eingaben ab, nicht alle Desktop-Tasten. Erwartet offene P3 #15–#21, #34, #39, #46–#48, #61, #68, #71, #74, #75 auf GitHub als offen/P3 bestätigt; nicht erneut gemeldet.

## Ausgeführte Prüfungen

- Checkout sauber, Head exakt wie angegeben; `git diff --stat a49340d 1ee630c` zeigt ausschließlich Teil 1. SDK `10.0.402`; `tools/pruefen.sh --schnell` mit Originaldaten, Beispiel-CFG und hashgeprüfter Referenzschrift:

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 621 | 0 | 0 |

- `objdump -D -b binary -m i8086 -M intel` auf lokalen, aus `BOMB.OVR` geschnittenen Bereichen: O3:5B61–5C6D (Siege, bei Gleichstand Punkte), O3:1CF1–1DC2 (Mengenlimit 9, verfügbare Mittel), O3:2080–2282 (Kauf/Bestand/Neuaufbau), O3:6A86–6CD2 (Teamgrößen, durchschnittliche Siege, Punkte) mit `Standings`, `Shop`, `TeamStandings` und deren Tests abgeglichen; keine Abweichung in diesen Zweigen. Kein vollständiger Disassemblierungsnachweis aller Nebenpfade.
- O2-Leser `BOMB.OVR` 0x6D36–0x7014 per `objdump` stichprobenartig auf Blockgrößen und Reihenfolge, Version, 1-/2-Byte-Felder sowie 16-Bit-Speicherumrechnung gegen `GameConfig` und synthetische Feld-/Grenztests geprüft; Originalkonfiguration-Test grün. Kurze Datei und Nicht-0/1-Boolean bleiben bekannte `[APPROX]`.
- Fenster mit Referenzschrift und Originaldaten: Titel → Menü → Optionen, Team Play ein; Spielerzahl 3 → Spielerauswahl → `S` → Team Setup, Spieler 2 per Down/Left zu Team 1, Enter → Teamnamen für Teams 1 und 3 → Enter → Spielplatzhalter. Tests decken auch ungültige Teamaufteilung, Namensbearbeitung und Rückgaben ab. Bekannte Einschränkung Esc/Satzzeichen siehe #75; kein vollständiger Tasten- oder Pixelvergleich.
- Entscheidungen R1-D1 (DOSBox 0.74-3/3000), D2 (Listings im Referenz-Repo), D4 (Referenz-PR [#7](https://github.com/kaaasi/bomb-referenz/pull/7)), D6 (`git check-ignore export/x.png foo/export/x.png`) und Schrift-D (Teil 1) geprüft; keine weitere konkrete Abweichung festgestellt. D3/D5 siehe Befunde. Die tatsächliche DOSBox-Aufnahme und ein zweiter CFG-Satz wurden nicht geprüft.
- Bereits in Teil 1 ausgeführte Gegenbeispiele nicht wiederholt. Alle vorhandenen `[APPROX]` bis zur Aufnahme `runde-1` akzeptiert; Schussphysik und Rundenablauf sind noch nicht umgesetzt. Annahme: die über den Connector bezogene Beispiel-CFG enthält BOMBCFG-Defaults, wie im Referenzmaterial dokumentiert.
