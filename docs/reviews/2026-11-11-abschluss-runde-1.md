# Review: PR #83 – Abschluss Runde 1 (Fix #79 und gesammelte P3)

Reviewer: Claude Code (Claude Opus 5.5), geplanter Lauf, frische Sitzung ohne Implementierungskontext · 11.11.2026
Base `35d8b420a4d9d4e17785f1a9b010ef9c5ca85e9b` · Head `99f5650e667f661372f003250c29b072df605aa3`
(Checkout nachgewiesen: `git rev-parse HEAD` = Head, `git status --porcelain` leer; SDK 10.0.402)

## 1. Merge-Empfehlung

**Ja** für genau diesen Head. Alle drei Läufe von `tools/pruefen.sh` selbst ausgeführt und grün (Zahlen identisch mit dem PR),
das Gegenbeispiel #79 ist behoben, jede Rückmutation der neuen bzw. aus früheren Reviews geforderten Tests wird rot, die
Deutung von `UCC8:0006` (#61) ist in der Disassembly belegt, die Doku-Stichproben (#15–#21) stimmen mit den Videobildern und den
Texten in `BOMB.OVR`/`BOMB.EXE` überein. Keine Dateien von `tws-format`, keine Originaldaten im Diff. Offen für die Merge-Prüfung
(nicht Teil dieses Urteils): CI `Build` auf dem Head stand beim Review noch auf „queued“ (§6 Nr. 3).

## 2. Befunde

Keine P0–P2. Kein P3 mit Beleg.

- **Hinweis (kein Befund):** Der Weg `Window.TextInput` → `_typed` → `ReadInput` in `DesktopGame` ist ohne Test (kein
  Desktop-Testprojekt) und laut PR nicht interaktiv geprüft. Codelesen: Leertaste, Enter, Esc, Backspace kommen nur aus der
  Taste (`ToBiosKey`), `FromTypedCharacter` schließt 0x20, 0x0D, 0x1B, 0x08, 0x7F aus → keine Doppelung; F10/F11 erzeugen kein
  Textzeichen; `_typed` wird je Bild geleert. Kein verschluckter Fall gefunden. Tab, F1–F9, Bild auf/ab setzen wie vor dem PR nur
  `KeyPressed` ohne Wort (kein Bildschirm nutzt sie).

### Besonders geprüft

1. **#75 Esc / Beenden:** `WindowKeys` = {F10, F11}; Esc → `BiosKey.Escape` (0x011B). Beenden bleibt möglich über
   Fenster schließen/Alt+F4 (MonoGame beendet `Run`) und `!_screens.IsRunning → Exit()` (`DesktopGame.cs` Update). Esc wirkt
   wie im Original: Optionsbild verlässt (`O2:118C` = {13, 27}), Spielerauswahl wählt START (`PlayerSelectScreen.cs:372`),
   Namenseingabe beendet (`:700`), Statusfenster schließt (`StatusWindow.cs:85`); Hauptmenü und Platzhalter ignorieren Esc
   (Original hat dort keinen Ausstieg). Doku `titel.md` §1, `menue.md`, `spielerauswahl.md` passt dazu.
2. **#61 `UCC8:0006` = `GetViewSettings`:** bestätigt, nicht `SetAllPalette`.
   `ndisasm -b16 -e 0x0E326 BOMB.EXE`: `mov di,0E8FAh; push ds; push di; call 0E26:1028; retf`.
   `ndisasm -e 0x10928`: `mov di,0EBE2h; push ds; push di; les di,[bp+6]; push es; push di; mov ax,9; push ax; call 12CE:0A81`.
   `ndisasm -e 0x14E01` (12CE:0A81): `lds si,[bx+0Ah]; les di,[bx+6]; mov cx,[bx+4]; rep movsb` = `Move(DS:EBE2, DS:E8FA, 9)`.
   Direkt davor (`BOMB.EXE 0x108EB`ff.) speichert die Routine `SetViewPort` x1/y1/x2/y2/Clip nach `EBE2..EBEA` (9 Byte
   `ViewPortType`). Rein lesend – Dokumentation in `menue.md` §1 korrekt.
3. **#34/#68 Test-Infrastruktur:** `UI.Tests` → `bombtool` (nur Formats/UI, `InternalsVisibleTo`); `ConfigCommandTests` in
   eigener Collection ohne Parallelisierung (Console-Umleitung; sonst schreibt kein UI-/Formats-Code auf die Konsole).
   `fetch-reference.sh`: Probe mit Kopie von `~/referenz` ohne `schriften/SHA256SUMS`:
   `REFERENZ=<kopie> tools/claude/fetch-reference.sh schriften` → „schriften: SHA256SUMS fehlt …“, Rückgabe 1 (Stand `main`:
   Rückgabe 0); ohne Argument ebenfalls 1. `pruefen.sh`: Meldungstexte des Selbsttests entsprechen
   `TestGameData`/`BitmapFontReferenceFact` (`"Original data missing (folder …"` bzw. `"…: Reference font missing"`).
4. **Doku #15–#21 (Stichproben):** LP 00012 „Load Game“ abgeblendet; LP 00428 „Add / Exit / Save“; TW 00031 „8 Games Played out
   of 10“; LP 00359 fehlt, 00358 ≠ 00357 und ≠ 00360 (`cmp`). Texte: `BOMB.OVR 0x1710` „Computer Can Buy:“, `0x23F3` „Load
   Game“, `0x4CC3` „Random (“, `0xE52D` „Continue“/`0xE536` „Exit“; `BOMB.EXE 0x1886C` „CRI 20/D“, `0x16C6C` „Lead Ball“.
5. **#46/#47:** Schleife `BOMB.EXE 0x05E34` = 5 Befehle je Byte ohne Treffer (`mov bx,cx; mov al,[bx]; cmp al,dl; jnz; loop`),
   64.000 × 5 / 3000 ≈ 107 ms – Herleitung stimmt. `BOMB.OVR 0x004B2` = `B8 3C 00 50 9A` (Delay(60)).

## 3. Ausgeführte Prüfungen

`tools/claude/fetch-reference.sh` (ohne Argumente, Rückgabe 0, Prüfsummen stimmen), `tools/claude/setup-workspace.sh`,
`tools/pruefen.sh` (alle drei Läufe, Commit `99f5650`, „Ergebnis: bestanden“):

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 649 | 0 | 0 |
| Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 564 | 0 | 56 |
| Pflichtlauf ohne Daten (scheitert wie gewollt) | 564 | 85 | 0 |

`tools/pruefen-selbsttest.sh` 7/7 ok, `tools/originalschutz-selbsttest.sh` ok, `originalschutz_pruefen.py` ok,
`versionen_pruefen.py` ok. Diff: keine Datei unter `docs/formate/spielstand.md`, `tools/reverse-engineering/tws*`, kein `.png/.cfg/.tws/.raw/.exe/.ovr`.

**Gegenbeispiel #79:** Fensterpfad `CreateMenu` nutzt `OptionsSettings.ForConfig(null)`/`MenuSettings.ForConfig(null, …)` →
`GameConfig.BombCfgDefaults` (Crumble 75, Walls None, Trail an, Background 9, PV an); `ExampleEqualsBombCfgDefaults` vergleicht
mit der von `BOMBCFG.EXE` mit Voreinstellungen geschriebenen Datei (`original/spielstaende/nutzer-2026-11-11.CFG`) – grün.

**Rückmutationen** (Arbeitskopie per `git worktree`, je Mutation gefilterter `dotnet test` mit `BOMB_REQUIRE_GAMEDATA=1`):

| Mutation | Test | Ergebnis |
|---|---|---|
| `OptionsSettings.ForConfig`: `null` → `new OptionsSettings()` (altes Verhalten) | `OptionsScreenTests.WithoutBombCfg…` | rot |
| `MenuSettings.ForConfig`: `null` → `new MenuSettings()` | `MenuScreenTests.WithoutBombCfg…` | rot |
| `BombCfgDefaults.CrumblePercent` 75 → 10 | `BombCfgDefaultsHoldTheValues…`, `ExampleEquals…` | rot (2) |
| `BombCfgDefaults.TurretSpeed` 8 → 7 | `ExampleEqualsBombCfgDefaults` | rot |
| #47 `CycleStepDuration` 60 → 50 ms | `CyclingStepsEverySixty…`, `CyclingDelayIsSixty…` | rot (2) |
| #48 Lesefehler `return []` → `return null` | `UnreadableFileIsAZeroRecord…` | rot |
| #74 Zeile DAC 19 := (63,63,63) gelöscht | `TeamPaletteSetsDacNineteenToWhite` | rot |
| #34 Offset CrumblePercent 9 → 10 | `ConfigCommandTests` | rot |
| #34 Exitcode Lesefehler 1 → 2 | `ConfigCommandTests` | rot (2) |
| #75 `FromTypedCharacter` Bereich 0x20..0x7F | `BiosKeyTests` | rot (2) |
| #68 `pruefen.sh`: Bedingung `gamedata == 0` entfernt | `pruefen-selbsttest.sh` Fall `nur-schrift` | „FEHLER … bestanden, erwartet abgelehnt“ |
| #68 `fetch-reference.sh` Stand `main` | Probe ohne `schriften/SHA256SUMS` | Rückgabe 0 statt 1 |

**Erwartet offen (bestätigt):** #39, #71, #78 offen und im PR unberührt (#78 Spur `tws-format`). Alle übrigen genannten Issues
(#15–#21, #34, #46–#48, #61, #68, #74, #75, #79) stehen als `Fixes #…` in den Commits.

**Akzeptierte `[APPROX]`:** #75 Reihenfolge Sondertasten vor Textzeichen innerhalb eines Bildes; Zeichen über 0x7E entfallen
(Unicode vs. Codepage 437). #46 85 ms je Durchgang bleibt bis zur Aufnahme `runde-1` (zwischen 73 ms Longplay und 107 ms
Herleitung). **Annahmen:** Verhalten von `Window.TextInput` unter MonoGame DesktopGL 3.8.6 nicht interaktiv geprüft (kein
Bildschirm); `BombCfgDefaults` gegen `BOMBCFG.EXE 0x7008` nur über die Beispieldatei geprüft, nicht Feld für Feld in der Disassembly.
