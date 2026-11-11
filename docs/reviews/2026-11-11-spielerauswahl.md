# Review PR #65 – `spielerauswahl`

Prüfer: Claude Opus 5.5, frische Sitzung ohne Implementierungskontext (geplanter Lauf, unbeaufsichtigt). Base
`9f64019d207c5f026ed323a0e9aeb997d8ac03da` (im Head enthalten, `git merge-base --is-ancestor`), Head
`5153866a12170f98debc75e74a13f42cfb9d2326` (ausgecheckt, `git rev-parse HEAD` = Head, `git status --porcelain` leer).
Originaldaten und Referenzschrift mit `tools/claude/fetch-reference.sh` (ohne Argumente, Prüfsummen stimmen); Disassembly
selbst mit `ndisasm -b16 -o <O2-Offset>` aus `BOMB.OVR` (Dateioffset = 0x5E8 + O2-Offset).

## 1. Merge-Empfehlung

**Ja** für genau diesen Head. Alle unter „Besonders prüfen“ genannten Stellen stimmen mit der Disassembly überein (Tabelle in
Teil 3). Der Absturz bei 13-Zeichen-Namen (`O2:42E8`) ist als roter Regressionstest belegt. `Core/Setup` ist fensterlos, Zufall
nur als übergebener Generator, „Random“ wird erst in `PlayerRoster.Start(random)` aufgelöst. Architektur und Dateigrenzen sind
eingehalten, keine Originaldaten im Diff, alle drei Läufe grün, CI `build` auf dem Head grün. Offen sind nur zwei P3.

## 2. Befunde

- **P3 – DAC 19 := weiß der Teambildschirme ist durch keinen Test festgelegt**
  ([PlayerSelectTeamScreen.cs#L233](https://github.com/kaaasi/bomb-redone/blob/5153866a12170f98debc75e74a13f42cfb9d2326/src/BombRedone.UI/Screens/PlayerSelectTeamScreen.cs#L233)).
  Der Code stimmt (`O2:29D0`: `call far [E8F6]` mit 13h, 3Fh, 3Fh, 3Fh). Gegenprobe: Zeile gelöscht → alle 25 Tests
  `PlayerSelectTeam*` grün (Mutation M27 unten). Vorschlag: in `PlayerSelectTeamScreenOriginalTests` die Palette nach `Draw` auf
  Eintrag 19 = (63, 63, 63) prüfen.
- **P3 – Fenster: Esc, „?“ und Satzzeichen erreichen die Bildschirme nicht**
  ([DesktopGame.cs#L20](https://github.com/kaaasi/bomb-redone/blob/5153866a12170f98debc75e74a13f42cfb9d2326/src/BombRedone.Desktop/DesktopGame.cs#L20),
  [#L168-L183](https://github.com/kaaasi/bomb-redone/blob/5153866a12170f98debc75e74a13f42cfb9d2326/src/BombRedone.Desktop/DesktopGame.cs#L168-L183)).
  `ToBiosKey` liefert nur Buchstaben, Ziffern, Enter, Leertaste, Pfeile, Rückschritt, Pos1, Ende, Entf; alles andere 0
  (`grep -n "=> " src/BombRedone.Desktop/DesktopGame.cs`: keine Abbildung für `OemPeriod`, `OemMinus`, `OemQuestion` usw.).
  Esc ist Fenstertaste (`WindowKeys`). Im Fenster fehlen daher: Esc = START (`O2:5276`), Esc/„?“ = Random in der Gegnerliste
  (`O2:4C09`, `O2:4C2F`), Esc = Done (`O2:26DF`) und Satzzeichen in Namen (Bereich 32..125, `O2:42A5`). Dokumentiert ist nur Esc
  der Namenseingabe (`spielerauswahl.md` §4). Die Bildschirme selbst behandeln alle Tasten richtig (Tests mit `ScreenInput`);
  Vorschlag: `[APPROX]`-Vermerk ergänzen oder Texteingabe (`Window.TextInput`) später nachziehen.

Keine P0–P2.

## 3. Ausgeführte Prüfungen

**`tools/pruefen.sh` (alle drei Läufe)**, SDK 10.0.402, Commit `5153866a12170f98debc75e74a13f42cfb9d2326`, Ergebnis: bestanden

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 468 | 0 | 0 |
| Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 404 | 0 | 43 |
| Pflichtlauf ohne Daten (scheitert wie gewollt) | 404 | 64 | 0 |

CI auf dem Head: `build` success (Check-Runs von `5153866`).

**Regressionstest 13 Zeichen:** Fix zurückgenommen (`_nameChars` wieder `MaxNameLength + 1`, Zeile `… + 1] = '\0'` entfernt) →
`PlayerSelectScreenTests.ThirteenCharacterNamesCanBeEdited` rot mit `IndexOutOfRangeException` in `DrawCell` (aus `OpenName`);
gleiche Rücknahme in `PlayerSelectTeamNamesScreen` → `PlayerSelectTeamScreenTests.ThirteenCharacterNamesCanBeEdited` rot.

**Abgleich mit der Disassembly** (ndisasm, je Stelle gelesen; Mengen per `od` aus `BOMB.OVR`):

| Stelle | geprüft | Ergebnis |
|---|---|---|
| `O2:551E–5633` Tasten | Mengen 5256 = {13,32}, 5276 = {27,G,P,S}; K/M/C-Merker; links/rechts mit Umlauf, auf/ab ±5 über `DS:3716` (= 48 50 4D 4B) | stimmt |
| `O2:5633–5849` Maus | Feld, Namensfeld (x1+3…y1+12), y1+15/25/35/45, START 71<x<249, 171<y<197; nur Merker setzen | stimmt |
| `O2:585F–5A29` Aktion | Menge 5296 = {0..10}, 52B6 = {1,2}; Reihenfolge Name→Liste→Tastatur→Maus→Computer, Namenseingabe bei früherem Computer | stimmt |
| `O2:5A2C–5AE3` START | `DS:4812`, `Random(7)+1` nur für Computer „Random“, `O2:0078` je Spieler, Warten aufs Loslassen | stimmt |
| `O2:447A–46D8` Namenseingabe | Rückschritt, Enter/Esc, 32..125 anhängen, Pos1 ohne Neuzeichnen, Entf, Ende, links/rechts, Grenze 13, Abschneiden {0,32} | stimmt |
| `O2:4BEF–4F0B` Gegnerliste | Esc/„?“ = 0, Enter/Leertaste, S L R B W T M, auf/ab ohne Umlauf, Mauszeilen 60/69/…/129/<142, sonst Random, Marken löschen 3 × 3 | stimmt |
| `O2:29C4–2CA5` Teams | Start Auswahl 1, DAC 19 weiß, Menge 26DF = {13,27,D}, auf/ab ohne Umlauf, Maus 64+10p ≤ y ≤ 71+10p mit `Delay(40)` je Zeile, Done 130<x<190, 187<y<197, Grenzen 1/Spielerzahl, Fläche (x±5, y−8..y) | stimmt |
| `O2:2CA8–2D63` Prüfung | „ein Spieler nicht im Team von Spieler 1“ (`[4108]` = Team Spieler 1) und größtes Team ≥ 2 | stimmt |
| `O2:35CF–3B95` Teamnamen | Tabelle `DS:4838+53t`, „Team k“ / Einzelname, Eingabe nur bei +0Eh = 0, `Random(11)` bis eindeutig über alle Teams 1..n, Knöpfe/Wiederherstellung (18,50+15k)–(122,64+15k), Spalten 103..250 | stimmt |

**Videobilder B3** (Referenz-Repo `videos/tank-wars-longplay/`): Bildschirm mit 2 Spielern und Gegnerliste aus dem Head gerendert
(Wegwerf-Test in getrenntem Arbeitsbaum, nicht committet) und mit 00027 und 00028 nach Lage und Form verglichen: Felder,
Namensfarben, Steuerart hellblau, Plus-Marken, START mit Glanz, Liste mit gelben Kennbuchstaben stimmen. Teams und Abbruchmeldung
gerendert und mit `spielerauswahl.md` §7 verglichen (kein Videobild vorhanden).

**Rückmutationen** (je Mutation nur betroffenes Testprojekt mit Filter, `BOMB_REQUIRE_GAMEDATA=1`; rot = erkannt):

| Nr. | Mutation | Ergebnis |
|---|---|---|
| M03 | `IsValid`: Vergleich mit Team von Spieler 2 statt 1 | grün – **äquivalent** (∃p: t(p)≠t(1) ⇔ ∃p: t(p)≠t(2)), keine Lücke |
| M04 | `largest >= 2` → `>= 1` | rot (2) |
| M05/M06 | Einzelteam-Name / Zählung `k` nur bei Mitgliedern | rot (4) / rot (1) |
| M07 | Eindeutigkeit der Zufallsnamen aus | rot (1) |
| M08 | „alle Computer“ nie zurückgesetzt | rot (2) |
| M09 | `Names[pick + 1]` → `Names[pick]` | rot (1) |
| M10 | Team-Startwert n → 1 | rot (1) |
| M11 | Reihenfolge Twanger/Wind Master getauscht | rot (1, Original-Test) |
| M12 | Panzerform Zeile y−1 | rot (5) |
| M13 | Obergrenze Team = 10 statt Spielerzahl | rot (2) |
| M14 | `Delay(40)` → 0 bzw. → 20 ms | Endlosschleife (Test hängt) bzw. rot (1) |
| M15 | Abbruch ohne `TeamPlay.Reset` | rot (1) |
| M16 | Mauszeile ab 63+10p | rot (1) |
| M17 | „Team # -->“ um 1 Pixel versetzt | rot (4, Prüfsummen) |
| M18 | Eingabe-Bedingung `!AllComputers` umgekehrt | rot (7) |
| M19 | Spalten 103..250 nicht wiederhergestellt | rot (1) |
| M20 | Mitgliedsname y+1 | rot (1) |
| M21 | ab-Taste Umlauf −5 statt −10 | rot (1) |
| M22 | Mauszeile Gegnerliste 69 → 70 | rot (1) |
| M23 | Namenseingabe nach früherem Computer → nach Tastatur | rot (4) |
| M24 | „COMPUTER“ bei y1+38 | rot (4) |
| M25 | Vorwahl der Liste bis Eintrag 7 | rot (1) |
| M26 | Stauchgrenze 49 → 50 | rot (4) |
| M27 | DAC 19 := weiß entfernt | **grün** → Befund P3 oben |
| M28 | Teambildschirm ohne Team Play | rot (1) |

**Architektur und Grenzen:** `Core/Setup` nutzt nur `System`/`BombRedone.Core`; neue UI-Dateien ohne MonoGame; Uhr nur im
Desktop (`TurboPascalRandom.FromClock`, schon vorher). Diff (`git diff --stat 9f64019 5153866`) nur in `Core/Setup`,
`Screens/PlayerSelect*`, `Desktop/DesktopGame.cs`, Tests der Spur, `anforderungen/spielerauswahl.md` und eigenem Statusabschnitt
in `arbeitspakete.md`; `ScreenInput.cs`, `Menu*`, `Surface.cs`, `Tools/*` unverändert; keine gesperrten Dateitypen.

**Akzeptierte `[APPROX]`** (erwartet offen, bestätigt): Start auf dem Menüpuffer statt Kreisübergang `UCC8:0687`; Übergänge
`UCC8:0AC0`/`O2:3F9C` nicht als Bewegung; volle GetImage-Wiederherstellung trotz `GetMem(1033/2215)`; `DS:E8B6`/`DS:E94E` als
eine Einstellung; Stelle 14 der Namenseingaben als #0 (im Original Byte +0Fh des Spielersatzes bzw. +0Eh „alle Computer“ des
Teams – dort könnte Getipptes das Merkmal verändern, im Remake nicht); Rohrwinkel als `double` (Winkel 90, identisch); Teambildschirme
ohne Video-/DOSBox-Bild, Prüfsummen nur Regressionsschutz; Spielfeld nach START als Platzhalter.

**Annahmen (nicht geprüft):** `0xDBD:0x22A` = Trunc(CX/2) und `0xDBD:0x256` = DX (Mausabfrage) aus der Benennung der Spur
übernommen; Inhalt des Puffers `DS:E5BC` vor den Teamnamen (`O2:3F2C–3F88`) nicht nachgelesen.
