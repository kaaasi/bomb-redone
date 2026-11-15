# Review PR #149 – Spur `spielfolge` (Schlacht, Satzreihenfolge, Wertung, Einkauf)

Unabhängiges Erstreview als geplanter Lauf ohne Rückfragen, Claude Code (Claude Opus 5.5), eigene Sitzung ohne
Implementierungskontext (PR aus Sitzung `…01GhACvUgdDfT2P6VJdxBaBK`, dieses Review aus `…01GhACvTgnMya5ELmf3vX6Mk`).
Base `26f0ae1d74df400fac4a10711d00d01bc8208691` · Head `01e835da3f04a1f2d711d9496d5d515183b99b0e`. Geprüft: `git rev-parse HEAD`
= Head, `git status --porcelain` leer; Originaldaten, Spielstände und `bilder/runde-1` aus `kaaasi/bomb-referenz` (Prüfsummen stimmen).

## 1. Merge-Empfehlung

**Ja** für genau diesen Head. Alle drei Läufe von `tools/pruefen.sh` grün, CI `build` auf dem Head success, keine Befunde über P3.

- **Probe selbst ausgeführt:** `folge.py befunde` 6/6 Regionen ok; `folge.py cases` (26 Fälle, 57 s) und `folge.py csharp` erzeugen
  eine **bytegleiche** `BattleProbeData.cs` (`diff` leer). Die C#-Tests vergleichen also gegen den Originalcode im Emulator, nicht
  gegen handgeschriebene Erwartungen.
- **Gegen den Code selbst nachgelesen** (eigene `ndisasm`-Auszüge aus `BOMB.OVR`, O3-Basis `0x08A8C`, nicht über `folge.py`):
  - Mischen `O3:2417`: `Random(20)+10` Durchgänge, `a = Random(N)+1`, `b = Random(N)+1` mit Neuzug bis `b ≠ a`, dann `O3:0000(a, b)`
    – deckt sich mit `BattleRecords.Shuffle`. Mischfälle für 2, 5 und 10 Spieler (je zwei Startwerte, gemischte Steuerarten
    1/2/3, Intelligenz i) prüfen Reihenfolge, Steuerart, Intelligenz und `RandSeed`.
  - Sortierung `O3:5B61–5C6D`: N Durchgänge über `i = 1..N−1`, Tausch bei `Siege[i] < Siege[i+1]` (`+6Ah`, `jnl`, vorzeichenbehaftet),
    dann N Durchgänge bei gleichen Siegen und `Punkte[i] < Punkte[i+1]` (`+13h` LongInt, vorzeichenbehaftet) – deckt sich mit
    `SortForRating`; Fall 12 enthält −32768 und 65536.
  - Bonus `O3:7885–7968`: Mitglieder über alle Sätze (Teamwort `+66h` = `DS:47E0`), `1000·N` (`mul`, dann `cwd`/`idiv`),
    Sieg nur bei `+23h ≠ 0`, Punkte auch für Tote – deckt sich mit `AddTeamBonus`.
  - Ablauf `O3:7847`: `DS:47E0 := 0` nur einmal vor der Schleife; je Spiel Mischen, `O4[0]`, Bonus, (Autosave), `O3:5AEF`,
    (`O3:46DE`), `O3:6696` bei Team Play, Einkauf nur bei `DS:47E2 ≠ DS:E8A6`, Wischbild, `DS:47E2+1`, Schleife solange `≤ DS:E8A6` –
    deckt sich mit `BattleSequence` (Autosave, Save-Dialog, geladener Stand als `[APPROX]` ausgewiesen).
  - `O3:5AFD`: nach dem letzten Spiel `DS:4812 := 0` (Knöpfe auch bei nur Computern) – so in `ScoreBattleFlow`;
    `O3:0FEB`: Textfarbe `13h + [bp-0Ch]` (Schleifenzähler = Satznummer) – passt zum neuen Parameter `record` von `ShopScreen`.
- **Zufallsfolge zwischen den Runden:** `BattleProbeTests` prüfen den `RandSeed` beim Eintritt in jede Runde (Testdouble ohne
  `Random`) gegen die Probe über 3 bzw. 2 ganze Spiele inkl. Wertung, Teamwertung und Einkauf; Wertungs- und Einkaufsfälle der
  Probe zählen 0 `Random`-Aufrufe. Damit belegt: bei Menschen kein `Random` zwischen zwei Mischungen außerhalb der Runde.
- **Wertung gegen `ablauf1-GAME1/2.TWS`:** beide Stände sind bereits in Wertungsreihenfolge (Sortierung ändert nichts),
  „Player 2“ vorn, `+00h` = Position – grün.
- **#71:** `RoundSequence` mischt die Sätze; mit dem alten `SeedSearch.SkipShuffle` an derselben Stelle werden 2 Tests rot (M6).
- **Dateigrenzen eingehalten:** alle 17 Dateien liegen in den Grenzen der Spur (`Core/Battle/*`, `RoundSequence.cs`,
  `UI/Screens/{Score*,Shop*}`, Tests in `Battle/`, `Playfield/RoundSequence*`, `UI.Tests/Screens/Score*`, `folge.py`,
  `runde.md` nur §1, eigener Statusabschnitt). Keine gemeinsame Datei, kein fremder Aufrufer geändert (`ShopScreen`-Parameter optional).
- **Originalschutz:** `originalschutz_pruefen.py` auf dem Head „Keine Originaldateien eingecheckt.“; `folge.py` prüft Regionen nur
  per SHA-256, Ausgaben nach `analyse/folge/` (gesperrt); geändert nur `.cs`/`.md`/`.py`.

## 2. Befunde

Keine P0–P2.

- **P3-1 Testlücke `ShopScreen`-Farbindex nach Satz.** Der neue Parameter `record`
  ([ShopScreen.cs:96](https://github.com/kaaasi/bomb-redone/blob/01e835da3f04a1f2d711d9496d5d515183b99b0e/src/BombRedone.UI/Screens/ShopScreen.cs#L96))
  ist ungetestet: Mutation M10 (`_colorIndex = PlayerColorBase + shop.Account.Number`) lässt alle 34 Shop-/`ScoreBattle`-Tests grün,
  obwohl in [ScoreBattleFlowTests.cs:46](https://github.com/kaaasi/bomb-redone/blob/01e835da3f04a1f2d711d9496d5d515183b99b0e/tests/BombRedone.UI.Tests/Screens/ScoreBattleFlowTests.cs#L46)
  „Player 2“ auf Satz 1 einkauft. Vorschlag: im Flow-Test Textfarbe/DAC-Eintrag `19 + 1` prüfen.
- **P3-2 Testlücke Bonus mit Rest.** Alle Bonusfälle der Probe sind ohne Rest teilbar (3000/1, 6000/3, 10000/4, 5000/2); Mutation
  M12 (Aufrunden statt `idiv`) bleibt grün. Der Code
  ([BattleRecords.cs:116](https://github.com/kaaasi/bomb-redone/blob/01e835da3f04a1f2d711d9496d5d515183b99b0e/src/BombRedone.Core/Battle/BattleRecords.cs#L116))
  ist laut Disassembly richtig; Vorschlag: ein Probefall wie 7 Spieler, 3 Mitglieder (7000/3 = 2333).
- **P3-3 Verdacht, für `runde`/`zug`:** `O3:1005` übergibt `O10[2]` den Satzzeiger; folgt die Spielerfarbe dort `+00h`, wechselt sie
  mit der Position. `ScoreBattleFlow` bekommt die Farbe als Funktion von außen – die Zuordnung ist erst beim Anbinden zu prüfen
  (passt zur Restlücke im PR, nicht belegt).

## 3. Ausgeführte Prüfungen

```
source ~/claude-tools/env.sh && dotnet --version               # 10.0.402
tools/pruefen.sh                                               # alle drei Läufe, Head 01e835da
python3 tools/reverse-engineering/folge.py --gamedata gamedata befunde|cases|csharp; diff … BattleProbeData.cs   # leer
ndisasm -b16 BOMB.OVR ab 0x08A8C+{2417,5B50,5AEF,6696,7847,0F90}
gh api …/commits/01e835da…/check-runs                          # build success
```

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 1132 | 0 | 0 |
| Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 983 | 0 | 72 |
| Pflichtlauf ohne Daten (scheitert wie gewollt) | 983 | 149 | 0 |

(Ein erster Lauf ohne `bilder/runde-1` hatte 6 Fehler in `PlayfieldShot/ScreenSession` – fehlende Bilder, Umgebung, nicht der PR.)

**Gegenproben** (temporärer Worktree, Filter `Battle|RoundSequence`, 38 Tests bzw. UI 34):

| Mutation | Ergebnis |
|---|---|
| M1 Mischen ohne Neuzug `b ≠ a` / M2 `Random(20)+9` | je 8 rot |
| M3 Punktephase `<` → `<=` | 1 rot |
| M4 Sieg auch für Tote / M5 Mitglieder nur lebende | je 2 rot |
| M6 `RoundSequence` mit altem `SkipShuffle` | 2 rot |
| M7 Einkauf auch nach letztem Spiel | 3 rot |
| M8 Bonus nach der Sortierung | 2 rot |
| M9 Zähler +72h nicht zurückgeschrieben | 5 rot |
| M11 keine Teamwertung | 1 rot |
| M10 Shop-Farbe nach Kontonummer / M12 Bonus aufgerundet | grün (P3-1, P3-2) |

**Akzeptierte `[APPROX]`:** Autosave `O3:3B54` und Dialog „SAVE GAME“ `O3:46DE` (Runde 4); geladener Stand `DS:4817` nicht
unterstützt; „# TO ADD:“ `O3:5717` und Statistik nicht in der UI (`AddGames` im Kern vorhanden); Wischbild nicht animiert;
Computer kaufen nie ein (Runde 4); `RoundSequence` ohne `Random` der Computer (Runde 4).

**Annahmen (nicht geprüft):** dass `feld_machine.py` die übrigen Unterprogramme (Grafik, Tastatur) für diese Fälle wirkungsfrei
genug emuliert – gestützt durch die eigene Disassembly der geprüften Kernstellen; Bildschirm-Hashes der Probe nicht gegen die UI
verglichen (im PR als Restlücke genannt).
