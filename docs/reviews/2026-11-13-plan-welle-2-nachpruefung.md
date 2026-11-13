# Nachprüfung: Plan Runde 2, Welle 2 (PR #103)

Reviewer: Claude Opus 5.5, geplanter Lauf, frische Sitzung ohne Mitarbeit an #103. Nachprüfung von
`docs/reviews/2026-11-13-plan-welle-2.md` (Head `009eb30`), nur Delta `009eb30..fece1f8` und die dort genannten Befunde.
Geprüft: Head `fece1f863bb0dea0b6aeb9a179f476666a81ed2d`, Base `401b15cd604eb3dacfb22af80fe97e3ab2666357`; Checkout verifiziert
(`git rev-parse HEAD` = Head, `git status --porcelain` leer). Links: `H = https://github.com/kaaasi/bomb-redone/blob/fece1f863bb0dea0b6aeb9a179f476666a81ed2d`.

## 1. Merge-Empfehlung

**Nein** für diesen Head. P2-1, P2-3 und P3-1…4 sind behoben. P2-2 ist nur zur Hälfte behoben: der Pixelzustand liegt jetzt in
`Core/Playfield`, aber die Zeichenroutinen, die `Core` dafür braucht, gibt es nur in `UI`, und keine Spur darf sie verschieben (N2-1).
Neu durch R2-D8: „wählbar in `bombtool`“ ohne Tools-Datei in den Grenzen von `schuss` (N2-2). Beides sind kleine Planänderungen in
`docs/arbeitspakete.md`; danach genügt die Delta-Prüfung der Leitstelle (`zusammenarbeit.md` §5 Nr. 4, nur P2/P3).

## 2. Befunde

### Gegenbeispiele des Erstreviews

| ID | Ergebnis | Beleg |
|---|---|---|
| P2-1 Krater | **behoben** | R2-D8 = A in [H/docs/arbeitspakete.md#L102-L113](https://github.com/kaaasi/bomb-redone/blob/fece1f863bb0dea0b6aeb9a179f476666a81ed2d/docs/arbeitspakete.md#L102-L113) und `entscheidungen.md` Z. 45; Inhalt Z. 219, Abnahme Z. 230 „Krater … für Baby Missile“. Radius 10 für Waffe 1 stimmt mit `runde.md` Z. 257 (`DS:47D8`), `0CC8:0687` mit Z. 269, `12CE:128F` mit Z. 273. Teil 6 „Nr. 6 mit Baby Missile statt Nuke“ – siehe N3-2. |
| P2-2 Pixelzustand | **teilweise** | Z. 178–183: Puffer und Zeichnen in `Core/Playfield`, `PlayfieldScreen` kopiert nur. Offen: N2-1. |
| P2-3 `Numerics` | **behoben** | Z. 225–227 und Z. 240: `Real48.Trig*.cs`, Tests, `real48.py` (nur neue Einsprünge) für `schuss`; Abnahme Z. 230 „bitgleich gegen die Laufzeit“. Zuordnung `Sin`/`Cos` vertauscht: N3-1. |
| P3-1 Probe | **behoben** | Z. 185–189: Graph `0E26` und VGA256 mit emuliert, `0CC8` emuliert, DAC-Modell `DS:E8F6`/`0CC8:0C20`. Restpunkt N3-3. |
| P3-2 Wind/Startspieler | **behoben** | Z. 188–189: `O4:44A7` bis vor die Kopfzeile in der Probe, Wind-Überlauf #98 immer als Test. |
| P3-3 Mischen | **behoben** | Z. 192: Suchwerkzeug zählt `runde.md` §4 Nr. 1 ohne Tausch (R2-D4); passt zu §4 „Vorschlag“ (Schritte 1–5). |
| P3-4 Verweise | **behoben** | Z. 182 `runde.md` §6.1 Z1, Z2 (`O4:0000`, Farben 100..119; geprüft `runde.md` Z. 378–390); Z. 217 Q2 (Rückstoß, §5.1) und Q11 (Schaden). |

### Neue Befunde im Delta

**N2-1 (P2) – Zeichenroutinen für `Core/Playfield` nur in `UI`, nicht in den Dateigrenzen.**
[H/docs/arbeitspakete.md#L178-L183](https://github.com/kaaasi/bomb-redone/blob/fece1f863bb0dea0b6aeb9a179f476666a81ed2d/docs/arbeitspakete.md#L178-L183)
lässt `Core/Playfield` Rahmen (`Rectangle`), Wandbeschriftung (SmallFont senkrecht, `runde.md` §3.1), Hintergründe (Line/Circle/Bar),
Gelände (`Line`, `O8:0C60`) und Panzerbild (`0431:05ED`) selbst zeichnen. Diese Routinen gibt es schon, aber nur in `UI` auf `Surface`:
`MenuGraphics.Line/Rectangle/Bar` (`src/BombRedone.UI/Screens/MenuGraphics.cs` Z. 32, 109, 122), `LineRasterizer`
(`UI/Text/LineRasterizer.cs`), BGI-Text `GraphText` (`UI/Text/GraphText.cs`), Panzer `PlayerSelectTeamScreen.DrawTank`
(`UI/Screens/PlayerSelectTeamScreen.cs` Z. 123–132, `[ORIGINAL] U431:05ED`). `Core` darf `UI` nicht kennen (`AGENTS.md` §4), die
Dateien stehen in keiner Dateigrenze von `gelaende` (Z. 194–197). Die Spur muss also entweder dieselben `[ORIGINAL]`-Routinen in `Core`
ein zweites Mal schreiben (genau die Doppelung, die P2-2 vermeiden sollte; Rasterung kann auseinanderlaufen) oder `UI`-Dateien außerhalb
ihrer Grenze ändern (§2.5).
Beleg: `grep -rln --include=*.cs -E 'Line|Rectangle|Circle|DrawTank|GraphText' src/BombRedone.Core` → nur `Economy/TeamStandings.cs`
(Treffer in Doku-Kommentaren „Line on the screen“), keine Zeichenroutine; `grep -n 'UI/Text\|MenuGraphics\|PlayerSelectTeam\|Surface.cs\|Core/Graphics' docs/arbeitspakete.md` → kein Treffer.
Vorschlag (Leitstelle wählt, ggf. Nutzer nach §2.9): A `gelaende` erhält `src/BombRedone.Core/Graphics/*` (neu: Indexpuffer, Line,
Rectangle, Bar, Circle/Ellipse, Panzer) und darf `MenuGraphics.cs`, `LineRasterizer.cs`, `PlayerSelectTeamScreen.DrawTank` auf diese
Routinen umstellen (gemeinsame Dateien, Begründung im PR, bestehende UI-Tests als Gegenprobe); B Doppelung ausdrücklich zulassen mit
Test „Core-Rasterung = UI-Rasterung“; C Wandbeschriftung (liegt links bei x ≈ 0, außerhalb des Rahmens 8..312, von keiner Regel
gelesen) bleibt in `PlayfieldScreen`, damit braucht `Core` keinen Text. Empfehlung: A, zusammen mit C.

**N2-2 (P2) – Baby Missile „wählbar in `bombtool`“ ohne Tools-Datei.** R2-D8 A und Inhalt `schuss`
([H/docs/arbeitspakete.md#L219](https://github.com/kaaasi/bomb-redone/blob/fece1f863bb0dea0b6aeb9a179f476666a81ed2d/docs/arbeitspakete.md#L219))
verlangen die Wahl der Waffe in `bombtool`; die Dateien von `schuss` (Z. 225–229) enthalten nichts unter `src/BombRedone.Tools/`, und
Z. 239 legt `RenderCommand.cs` „nur `gelaende`“ zu.
Beleg: `grep -n 'Tools/' docs/arbeitspakete.md` → nur Z. 195 (`gelaende`), 211 (`tws-leser`), 239 (Zuständigkeit).
Vorschlag: `schuss` erhält `src/BombRedone.Tools/RenderCommand.cs` (nach `gelaende`, nacheinander) und `tests/BombRedone.UI.Tests/Tools/Render*`;
Z. 239 entsprechend ergänzen. Alternativ „wählbar nur in der Desktop-Vorschau“.

**N3-1 (P3) – `Sin`/`Cos` vertauscht.** Z. 225 „`Sin`, `Cos` `12CE:12EE`/`1301`“; laut `runde.md` Z. 207 ist `cos` = `12CE:12EE`
(Sin(π/2 − x)) und `sin` = `12CE:1301` (der Fehler stand schon im Erstreview P2-3). Reihenfolge tauschen.

**N3-2 (P3) – Sitzung 3 ohne Baby Missile.** Teil 6 vergleicht `runde.md` §7 Nr. 6 „mit Baby Missile statt Nuke“; die Liste der
Sitzung 3 (Z. 125 „Schuss neben einen Panzer (Krater, …)“) nennt keine Waffe, `runde.md` §7 Nr. 6 nennt die Nuke. Für die Aufnahme
„Baby Missile (Einkauf)“ in Z. 125 ergänzen, sonst fehlt das Referenzbild für die Abnahme.

**N3-3 (P3) – Probe des Erstreviews nicht im Zugriff der Spur; R2-D7 wörtlich überholt.** Z. 185 „Ansatz wie die Probe im Review
#104“: `emu_o8.py` liegt weder in diesem Repo noch im Referenz-Repo (`git ls-tree -r origin/main` in `bomb-referenz` → kein `emu*`).
Entweder die Leitstelle legt es per `daten/…`-PR unter `analyse/` ab, oder Z. 185 sagt „Spur baut die Probe neu“. Außerdem stehen in
R2-D7 (`arbeitspakete.md` Z. 95: „Grafikaufrufe (`0E26`, `0CC8`) abgefangen“, „Graph-Routinen selbst nur `[APPROX]`“;
`entscheidungen.md` Z. 44 „Grafikaufrufe abgefangen“) noch die alte Fassung; ein Satz „präzisiert durch Zuschnitt `gelaende`, Probe“
genügt.

## 3. Ausgeführte Prüfungen

| Prüfung | Ergebnis |
|---|---|
| `git ls-remote https://github.com/kaaasi/bomb-referenz HEAD` | erreichbar (`0abf1d3`) |
| `git diff --stat 009eb30 fece1f8` | nur `docs/arbeitspakete.md` (+42/−13), `docs/entscheidungen.md` (+1); `git diff --check` sauber |
| Verweise gegen `docs/analyse/runde.md` | §3.1, §3.6, §4, §5.2, §5.5, §5.6, §6.1, §7 Nr. 6 wie oben |
| Dateigrenzen gegen `src/` | `MenuGraphics`, `LineRasterizer`, `GraphText`, `DrawTank` nur in `UI`; `Core.csproj` referenziert nur `Formats` |
| `tools/pruefen.sh --schnell` (Head, SDK 10.0.402) | bestanden |

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 750 | 0 | 0 |

**Akzeptierte `[APPROX]`:** keine (Plan-PR ohne Code). **Annahmen:** Hintergründe brauchen Circle/Ellipse aus Graph (nach `runde.md`
§3.3 und §7 Nr. 1, nicht einzeln nachgeprüft); die Wandbeschriftung wird von keiner Regel gelesen (Lage x ≈ 0 nach §3.1, Farbe nicht 2
bzw. 20..29 nicht nachgeprüft).
