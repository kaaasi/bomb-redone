# Arbeitspakete

Die Portierung ist in Pakete (Spuren) aufgeteilt, die parallel in eigenen Chats bearbeitet werden können. Jede Spur hat einen
Branch `paket/<name>` mit einem PR, eigene Dateien und klare Abnahmekriterien. Regeln und Einrichtung: [AGENTS.md](../AGENTS.md).
Den Status pflegt jede Spur nur in ihrem eigenen Abschnitt. Abgeschlossene Runden stehen in [archiv/](archiv/).

**Koordination:** Die Leitstelle verteilt Pakete, prüft und mergt Pull Requests und pflegt Plan und Doku
(Rollen: [prozess/zusammenarbeit.md](prozess/zusammenarbeit.md)). **Spuren starten erst nach dem Go des Nutzers.**

## Aktuelle Runde: Runde 3 – spielbare Partie (Plan 15.11.2026, R3-D1/D2 entschieden 15.11.2026, noch kein Go)

Runde 2 abgeschlossen: [archiv/runde-2.md](archiv/runde-2.md). Grundlagen: [analyse/runde.md](analyse/runde.md) (Spielfolge §1,
Rundenbeginn §2, Waffen §5, Zug und Eingaben §6), [analyse/feld.md](analyse/feld.md) (Probe, Bildvergleich),
[anforderungen/wertung-einkauf.md](anforderungen/wertung-einkauf.md). Übernommen: #71 (R2-D4 = A), #39, #102 (Hinweis), #140, #141,
Sitzung 4 und Restlücken (unten).

Regeln wie Runde 2: Base aktueller `main` (bei Planung `aec025a`; tatsächlichen SHA in den Draft-PR), reproduzieren → roter Test →
grün, `[ORIGINAL]`/`[APPROX]`, höchstens drei Fortsetzungen je Spur, Originalmaterial nur in Schutzordnern (`AGENTS.md` §2.1).
Pascal-`=` auf Real48 nur über `Real48.PascalEquals` (#96); Abnahme ist die Probe gegen den Originalcode bzw. das Bild, nicht
`runde.py befunde` allein (#102). Zufall nur über die eine `TurboPascalRandom`-Instanz, Aufrufreihenfolge wie im Original.
Erstreview je Spur: Claude Opus 5.5, frische Sitzung; Gesamtreview am Rundenende: ChatGPT/Codex (`prozess/anhang.md` C).

### Entscheidungen der Runde (Vorlage Leitstelle; R3-D1, R3-D2 entschieden vom Nutzer 15.11.2026: wie empfohlen)

**R3-D1 – Umfang der Runde – entschieden: A (Nutzer 15.11.2026).** Problem: R2-D1 hat „spielbare Runde und Waffen“ nach Runde 3 gelegt; dazu gehören Zug, 18 Waffen samt
Lenkung, Tod und Rundenende, die Spielfolge mit Wertung und Einkauf, sieben Computerarten (`runde.md` §6.5) und Speichern/Laden.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: Partie Mensch gegen Mensch vom Titel bis zum Endergebnis: Zug mit Tastatur und Maus, alle Waffen samt Laser, MIRV, Lenkung und Abwehr, Tod, Blitz, Rundenende, Spielfolge mit Wertung, Einkauf und #71; Computer und Speichern/Laden in Runde 4 | erste vollständig spielbare Fassung, jeder Baustein per Probe prüfbar | Computer fehlen noch; vier Spuren |
| B: A + alle Computerarten | Partie wie im Original | Computer hängen an Schussprotokoll und Sichtlinien (§6.6 offen); zu groß für eine Runde |
| C: A ohne Laser, MIRV und Lenkung (nur ballistische Waffen) | kleinere Spur `waffen` | Zug und Zielwahl müssten in Runde 4 erneut angefasst werden |
| D: A + Speichern (Autosave, Dialog `O3:46DE`) und Laden | Spielstände im Kreis prüfbar (R1-D3: Schreiben „später“) | Stände Nr. 2–8 fehlen noch (Sitzung 4); größerer Spielfolge-Teil |

Empfehlung: **A**; Computer, Speichern und Laden in Runde 4.

**R3-D2 – Abnahme, solange Sitzung 4 fehlt – entschieden: A (Nutzer 15.11.2026).** Problem: Für Hintergründe, Wandarten, Waffen, Tod, Laser, MIRV und die Stände Nr. 2–8
gibt es noch keine Bilder; Sitzung 4 macht der Nutzer.

| Option | Vorteil | Nachteil |
|---|---|---|
| A: wie R2-D7/D9/D10 – Spuren sofort nach Go, Abnahme bitgleich gegen die Probe (8086-Emulator mit Graph und Treiber); Bildvergleich und Abgleich der Stände als Folgespuren nach Sitzung 4 | Runde läuft unabhängig von der Aufnahme, in Runde 2 bewährt | Bilder bestätigen erst nachträglich; Folgespuren |
| B: erst Sitzung 4, dann alle Spuren | gleich gegen Bilder prüfbar | Runde wartet auf den Nutzer |
| C: Spuren ohne Probe nach `runde.md`, Bildbezogenes `[APPROX]` | schnell | Lesefehler der Analyse bleiben unentdeckt (#102) |

Empfehlung: **A**.

### DOSBox-Aufnahme `runde-1`, Sitzung 4 (Nutzer, parallel zu Welle 1)

Schrittliste in [vorlagen/dosbox-aufnahme.md](vorlagen/dosbox-aufnahme.md) „Sitzung 4“ – übernommen aus „Restlücken Welle 2“ (Archiv
Runde 2), `analyse/runde.md` §7 Nr. 1–11 (Nr. 8–10 jetzt fällig) und `formate/spielstand.md` „Benötigte Originalstände“ Nr. 2–8,
ergänzt um Zug, Panzerinfo, Zielwahl, MIRV-Dialog, Wertung nach Toten. Kurzaufruf (Modell Claude Sonnet 5.5):

> Repo kaaasi/bomb-redone. Arbeite nach `docs/vorlagen/dosbox-aufnahme.md`. Aufnahme: `runde-1`, Sitzung 4.

### Welle 1 (parallel, eigenes Go)

Integrationsfolge: `waffen` → `zug` → `spielfolge`. Gemeinsame Dateien: Abschnitt „Zuständigkeit“ unten.

#### Spur `waffen` – alle Waffen, Lenkung, Abwehr (R3-D1 = A)

- **Inhalt:** `Core/Shot` nach `runde.md` §5: Abschuss §5.1 (Bestand − 1, Herb/Vick − 1), Sprengwaffen {1, 2, 3, 4, 7, 11} mit
  Radien §5.5 (Baby Missile vorhanden), CRI, Sonic Blaster, Dirt, Xplosive Dirt §5.7 (dafür BGI-`Circle` `0E26:11EA` vollständig
  statt nur Radius 1), Laser §5.10 samt Aufladebild, MIRV §5.11 (Muster und Detonation als Eingabe; Dialog in `zug`, Vorzeichen bei
  Muster 3/4 per Probe klären, §5.13), Lenkung Herb/Vick §5.12 G1 (Ziel als Eingabe), Abwehr Schild/Inertia/Repulser in `O10:0000`
  (Q11) vollständig. **Bestand** (Review #145 P2-1): `waffen` legt Bestand, Lenk- und Abwehrwerte im Panzer (`PlayfieldTank`) an, samt
  Übertrag aus dem Konto beim Rundenbeginn und zurück beim Rundenende über die vorhandene öffentliche API von `PlayerAccount`
  (unverändert) und Lesezugriff; `zug` und `spielfolge` nutzen nur diese API. `bombtool render playfield --shot` mit Waffe, Muster, Detonation, Lenkung, Ziel.
- **Probe:** `feld_schuss.py` um Waffe, Muster, Lenkung, Ziel und Abwehr erweitern (Originalcode `O4:01B7`, `O4:3426`, `O4:13D4`,
  `O6`, `O10:0000`); `0E26`-Graph mit emuliert wie in Runde 2.
- **Teile:** 1. Probe für alle Waffen; 2. Sprengwaffen, Dirt, Xplosive Dirt, `Circle`; 3. CRI, Sonic, Abwehr; 4. Laser; 5. MIRV,
  Lenkung; 6. Darstellung (`PlayfieldShotScreen`) und `bombtool`.
- **Dateien:** `src/BombRedone.Core/Shot/*`, `src/BombRedone.Core/Playfield/*` (außer `RoundSequence.cs`), `src/BombRedone.Core/Graphics/*`,
  `src/BombRedone.Core/Numerics/*` (nur neue Einsprünge), `tests/BombRedone.Core.Tests/{Shot,Graphics,Numerics}/*`,
  `tests/BombRedone.Core.Tests/Playfield/*` (außer `RoundSequence*`), `src/BombRedone.UI/Screens/PlayfieldShot*`,
  `tests/BombRedone.UI.Tests/Screens/PlayfieldShot*`, `src/BombRedone.Tools/RenderCommand.cs`, `tests/BombRedone.UI.Tests/Tools/Render*`,
  `tools/reverse-engineering/feld*`, `tools/reverse-engineering/real48.py` (nur neue Einsprünge), `docs/analyse/feld.md`,
  `docs/analyse/runde.md` (nur §5, Korrekturen mit Beleg).
- **Abnahme:** je Waffe 0..17 bitgleich zur Probe (Feld, Hintergrund, DAC, Sätze, `RandSeed`) in festen Fällen je Wandart, mit und ohne
  Ton; Laser durch Gelände und gegen Panzer mit und ohne Schild; MIRV je Muster und Detonation; Herb, Vick, beide; Abwehr bis zum
  Verbrauch; alle bisherigen Probe-Hashes unverändert; ohne Beleg je Punkt `[APPROX]`.
- **Modell:** Claude Opus 5.5.

#### Spur `zug` – Zug, Eingaben, Panzerinfo, Zielwahl

- **Inhalt:** `Core/Turn` (neu) nach `runde.md` §6: Zugbeginn Z1 (Panzer neu, +5Eh/+70h, Waffenwechsel ohne Bestand, Kraft ≤ 10 ·
  Energie), Waffenleistenfarben `O4:0000` Z2, Zug `O7:040C` Z3 (Befehle 1–10, Laser-Grenze +38h, Lenkbits), Eingaben `O7:0EF7` Z4/Z5
  als abstrakte Eingabe (gehaltene Tasten, Alt-Wiederholung, BildAuf/BildAb, Maus x/y/Tasten, `Delay(TS)` als Takt) samt
  Schummeltaste, Zugfolge `O4:4770` R5 (Durchgang `DS:47EA`, Tote überspringen), Panzerinfo `O9:0040` I1, Zielwahl `O9:092E` I2
  (Tastatur, Maus), MIRV-Setup-Dialog (`O4:13D4`, liefert Muster und Detonation). `UI`: Felder Winkel/Kraft/Richtung/Energie, Rohr
  löschen und neu, Panzerinfo-Fenster, Zielwahlleiste, MIRV-Dialog. Desktop: Vorschau → echter Zug mit Tastatur und Maus (Schuss über
  `Core/Shot`), noch ohne Tod und Rundenende (→ `runde`). Restlücke R2 „Desktop-Zug ohne Kopfzeile“ damit erledigt.
- **Probe:** `tools/reverse-engineering/zug*` (neu) auf `feld_machine` (nur Import; Änderungen an `feld*` gehören `waffen`, sonst im PR
  anfordern) führt `O7:040C`, `O9:0040`, `O9:092E` und den MIRV-Dialog mit eingespeisten Tasten- und Mauszuständen aus.
- **Teile:** 1. Probe; 2. `Core/Turn` (Befehle, Eingaben, Zugfolge); 3. `UI` (Kopfzeile, Panzerinfo, Zielwahl, MIRV-Dialog); 4. Desktop.
- **Dateien:** `src/BombRedone.Core/Turn/*` (neu), `tests/BombRedone.Core.Tests/Turn/*` (neu), `src/BombRedone.UI/Screens/Playfield*`
  (außer `PlayfieldShot*`), `src/BombRedone.UI/Screens/{TankInfo,TargetSelect,MirvSetup}*` (neu),
  `tests/BombRedone.UI.Tests/Screens/` zu diesen Namen, `src/BombRedone.Desktop/*`, `tools/reverse-engineering/zug*` (neu),
  `docs/analyse/zug.md` (neu), `docs/analyse/runde.md` (nur §6, Korrekturen mit Beleg).
- **Abnahme:** je Befehl und Eingabeart bitgleich zur Probe (feste Eingabefolgen: Pfeile mit und ohne Alt, BildAuf/BildAb, jedes
  Mausfeld, Schummeltaste C/W/G/D, Winkel über 90°, Waffenwechsel ohne Bestand); Zugfolge mit Toten und Durchgangswechsel; Fenster,
  Leiste und Dialog pixelgleich zur Probe; Bildvergleich in `bilder-s4`.
- **Modell:** Claude Opus 5.5.

#### Spur `spielfolge` – Schlacht, Satzreihenfolge, Wertung, Einkauf (`Fixes #71`)

- **Inhalt:** `Core/Battle` (neu) nach `runde.md` §1: Schlacht über die Spiele 1..`DS:E8A6` (S1), Mischen `O3:2417` mit Tausch `O3:0000`
  über Satz, Steuerart, Intelligenz und Nummern (S3, S4; #71, R2-D4 = A; `RoundSequence` darauf umstellen, Zähler +72h wandern mit
  dem Satz), Teamsieg-Bonus S2, Wertung `O3:5AEF` mit Sortierung S5, Teamwertung `O3:6696`, Einkauf `O3:0E1F` je Spieler in
  Satzreihenfolge (vorhandener `ShopScreen`), Wischbild, Endergebnis (Add, Exit; Save ohne Wirkung `[APPROX]` bis Runde 4); alle
  `Random`-Aufrufe zwischen den Runden. Die Runde ist eine Schnittstelle (Eingabe: Sätze, Ausgabe: Sätze nach der Runde, Teamsieger
  `DS:47E0`) mit Testdouble; Desktop bindet erst `runde` an.
- **Probe:** `tools/reverse-engineering/folge*` (neu) führt `O3:2417`, `O3:0000`, `O3:5AEF`, `O3:6696` und den Bonus auf gesetzten
  Sätzen aus (Einkauf bis zur Eingabe).
- **Teile:** 1. Probe; 2. Tausch, Mischen, Wertung, Teamwertung, Bonus; 3. Ablauf der Schlacht mit Bildschirmen gegen das Testdouble.
- **Dateien:** `src/BombRedone.Core/Battle/*` (neu), `src/BombRedone.Core/Economy/*`, `src/BombRedone.Core/Setup/*`,
  `src/BombRedone.Core/Playfield/RoundSequence.cs`, `tests/BombRedone.Core.Tests/{Battle,Economy,Setup}/*`,
  `tests/BombRedone.Core.Tests/Playfield/RoundSequence*`, `src/BombRedone.UI/Screens/{Score*,Shop*,StatusWindow.cs}`,
  `tests/BombRedone.UI.Tests/Screens/{Score*,Shop*,Status*}`, `tools/reverse-engineering/folge*` (neu),
  `docs/anforderungen/wertung-einkauf.md`, `docs/analyse/runde.md` (nur §1, Korrekturen mit Beleg).
- **Abnahme:** Mischen und Tausch bitgleich zur Probe (Sätze, Steuerarten, Intelligenz-Zuordnung, `RandSeed`) für 2, 5 und 10 Spieler;
  Wertungsreihenfolge gegen Probe und gegen `ablauf1-GAME1/2.TWS` („Player 2“ vorn, `spielstand.md` „Beobachtungen“); Teambonus
  und Teamwertung mit drei Teams; Einkaufsreihenfolge; bestehende Score-/Shop-Tests grün.
- **Modell:** Claude Opus 5.5.

### Welle 2 (nach Merge von Welle 1, eigenes Go)

#### Spur `runde` – Rundenablauf, Tod, Blitz, Rundenende, ganze Partie

- **Inhalt:** `Core/Round` (neu) nach `runde.md` §5.9: Rundenschleife `O4:44A7` ab dem Startspieler – Zug (`zug`) → Waffe (`waffen`)
  → nach dem Schuss K11 (Panzer neu, Crumble, Fallen, Schussprotokoll `DS:4A80`, `O4:0000`) → Todesschleife K12 (Punkte +1500/−1000,
  +6Eh) → Todesexplosionen `O5` K15/K16 (alle sieben, Bild per Probe) → Blitz K13 ab Durchgang 11 → Rundenende K14 (+68h, `DS:47E0`);
  Anbindung an `Core/Battle`. Desktop: ganze Partie Mensch gegen Mensch vom Titel bis zum Endergebnis.
- **Teile:** 1. Probe (`O5`, `O6:14C5`, Todesschleife; ganze Runden mit festen Eingaben); 2. nach dem Schuss, Todesschleife,
  Rundenende; 3. Todesexplosionen, Blitz; 4. Desktop.
- **Dateien:** `src/BombRedone.Core/Round/*` (neu), `tests/BombRedone.Core.Tests/Round/*` (neu), Erweiterungen in `Core/Shot/*`,
  `Core/Turn/*`, `Core/Battle/*`, `UI/Screens/Playfield*` und deren Tests, `src/BombRedone.Desktop/*`, `tools/reverse-engineering/tod*`
  (neu) und `feld*`, `docs/analyse/feld.md`, `docs/analyse/runde.md` (nur §5.9).
- **Abnahme:** ganze Runden mit festem Startwert und festen Eingaben bitgleich zur Probe bis zum Rundenende samt `RandSeed`; je
  Todesart; Blitz; `s3_06`/`s3_07` jetzt auch am toten Panzer pixelgleich (Restlücke `schuss-bilder`) bzw. Abweichung erklärt;
  Wertung nach Spiel 1 gegen `s3_08`.
- **Modell:** Claude Opus 5.5.

### Folgespuren nach Sitzung 4 (R3-D2 = A; Zuschnitt durch die Leitstelle nach Ablage im Referenz-Repo)

- **`tws-abgleich`:** Stände Nr. 2–8 lesen und abgleichen (`formate/spielstand.md`), Autosave gegen Dialog (#71), Laden nur beschreiben
  (Runde 4).
- **`bilder-s4`:** Bildvergleich je Thema von `runde.md` §7 (Hintergründe und Palette 30..59, Rotation – dann Palettenrotation
  umsetzen statt `[APPROX]` –, 10 Spieler, Wandarten, Rückstoß, Krater, Erdrutsch, Tod und Blitz, Laser, MIRV, volle Kraft) und der
  Zugbilder (Panzerinfo, Zielwahl, MIRV-Dialog); `TIME` (R2-D3) und `PV`/IRQ2 (#39, `re-grundlagen.md` §8 Nr. 2) nachtragen. Bei
  vielen Themen mehrere Spuren.
- **`p3-runde-3`** (letzter Lauf): #140, #141 und die P3 der Runde; Verweis in `Core/Graphics/IndexBuffer.cs` Z. 6 auf das Archiv
  Runde 2 richten (Review #145 P3-3).

### Restlücken aus Runde 2 (übernommen)

- **Erledigt durch Spuren dieser Runde:** Lenkung, Repulser, übrige Einschläge, Bestand (`waffen`); Desktop-Zug ohne Kopfzeile
  (`zug`); Satzmischung #71 (`spielfolge`); Todesexplosion in `s3_06`/`s3_07` (`runde`); BGI-`Circle` nur Radius 1 (`waffen`).
- **Nach Sitzung 4:** Palettenrotation, Stände Nr. 2–8, `TIME`, `PV`/#39 (Folgespuren).
- **Bleiben `[APPROX]`:** Spurpunkt/Erdrutsch jenseits 64000 Byte, 15 ms je Flugtick, DAC 0..15 Grundpalette.
- **Runde 4:** Computer (`runde.md` §6.5, offen §6.6), Speichern und Laden, Ton (E5).

### Zuständigkeit für gemeinsame Dateien (Runde 3)

- `AGENTS.md`, `CLAUDE.md`, `docs/prozess/*`, `docs/vorlagen/*`, `docs/entscheidungen.md`, `tools/pruefen*.sh`,
  `tools/originalschutz-selbsttest.sh`, `.gitignore`, `.github/*`, `tests/**/Support/`: allein die Leitstelle.
- `src/BombRedone.Desktop/*`, `src/BombRedone.UI/Screens/Playfield*` (außer `PlayfieldShot*`): Welle 1 nur `zug`, Welle 2 nur `runde`.
- `Core/Shot/*`, `Core/Playfield/*` (außer `RoundSequence.cs`), `Core/Graphics/*`, `Core/Numerics/*`, `PlayfieldShot*`, `RenderCommand.cs`,
  `tools/reverse-engineering/feld*`, `real48.py`, `docs/analyse/feld.md`: Welle 1 nur `waffen`, Welle 2 nur `runde`.
- `Core/Economy/*`, `Core/Setup/*`, `RoundSequence.cs`, Score-/Shop-Bildschirme: Welle 1 nur `spielfolge`.
- `src/BombRedone.Tools/Program.cs`: je Spur höchstens eine Registrierungszeile.
- **Aufrufer in fremden Dateien** (Review #145 P2-2): Ändert eine Spur eine Signatur, zieht sie die Aufrufer in fremden Dateien
  minimal mit (`AGENTS.md` §2.5, Begründung im PR) – bekannt: `RenderCommand.cs` (Score-/Shop-/Status-Bildschirme, `PlayfieldScreen`),
  `Desktop/DesktopGame.cs` (`RoundSequence`), `Desktop/RoundPreviewScreen.cs` (`PlayfieldShotScreen.Fire`). Kollidiert das beim
  Integrieren, passt die spätere Spur der Integrationsfolge nach dem Rebase an (Fixlauf ohne Befund).
- `tests/BombRedone.UI.Tests/Screens/PlayfieldSession*` (gemeinsamer Testhelfer): Welle 1 nur `waffen`; `zug` legt eigene Helfer an.
- `docs/analyse/runde.md`: je Spur nur ihr Abschnitt (§5 `waffen`, §6 `zug`, §1 `spielfolge`, §5.9 `runde`), nur Korrekturen mit Beleg.
- `docs/arbeitspakete.md`: jede Spur nur ihr eigener Statusabschnitt (unten angelegt).

### Status waffen

- 15.11.2026: Teile 1–6 fertig (Claude Code, ein geplanter Lauf, PR #147): Probe `feld_schuss.py waffen` (182 Schüsse), alle Waffen samt Laser, MIRV, Lenkung, Abwehr in `Core/Shot` bitgleich zur Probe, `BgiDraw.Circle`, `Real48.ArcTan`, Bestand im Panzer mit `TakeStock`/`ReturnStock`, Darstellung und `bombtool`. Wartet auf Erstreview.

### Status zug

- 15.11.2026: Teil 1 (Probe) fertig: `tools/reverse-engineering/zug.py` (40 Züge, 5 Zugfolgen), [analyse/zug.md](analyse/zug.md);
  Korrektur `runde.md` §6.3 (Alt umgekehrt, Schummeltaste nur Ziffernblock).
- 15.11.2026: Teil 2 (`Core/Turn`) fertig: 40 Züge und 5 Zugfolgen bitgleich zur Probe.
- 15.11.2026: Teil 3 (UI) fertig: Bildschirm an jeder Abfragestelle aller 40 Züge gleich der Probe.
- 15.11.2026: Teil 4 (Desktop) fertig: `PlayfieldTurnScreen` statt Vorschau. Spur fertig – Review.

### Status spielfolge

### Status runde
