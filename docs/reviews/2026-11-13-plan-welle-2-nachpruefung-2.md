# Nachprüfung 2: Plan Runde 2, Welle 2 (PR #103)

Reviewer: Claude Opus 5.5, geplanter Lauf, frische Sitzung ohne Mitarbeit an #103. Nachprüfung von
`docs/reviews/2026-11-13-plan-welle-2-nachpruefung.md` (Head `fece1f8`), nur Delta `fece1f8..fc003a5` und die Befunde N2-1, N2-2,
N3-1…3. Geprüft: Head `fc003a59b80ab1dae365dfba1e0156231a0e2272`, Base `401b15cd604eb3dacfb22af80fe97e3ab2666357`; Checkout
verifiziert (`git rev-parse HEAD` = Head, `git status --porcelain` leer). Links: `H = https://github.com/kaaasi/bomb-redone/blob/fc003a59b80ab1dae365dfba1e0156231a0e2272`.

## 1. Merge-Empfehlung

**Ja** für genau diesen Head. Alle fünf Befunde der Nachprüfung sind behoben; das Delta (ein Commit, nur `docs/arbeitspakete.md`
+20/−13 und `docs/entscheidungen.md` ±1) bringt keine neue Unstimmigkeit über P3 hinaus. Der Hinweis N4-1 kann beim Spurstart
oder in der Spur selbst erledigt werden und blockiert nicht.

## 2. Befunde

### Gegenbeispiele der Nachprüfung

| ID | Ergebnis | Beleg |
|---|---|---|
| N2-1 Zeichenroutinen | **behoben** | [H/docs/arbeitspakete.md#L182-L186](https://github.com/kaaasi/bomb-redone/blob/fc003a59b80ab1dae365dfba1e0156231a0e2272/docs/arbeitspakete.md#L182-L186): `gelaende` legt `Core/Graphics` an (Indexpuffer, Line, Rectangle, Bar, Circle/Ellipse, Panzer) und stellt `MenuGraphics.cs`, `LineRasterizer.cs`, `DrawTank` darauf um; Dateien Z. 197–199 (gemeinsame Dateien mit Begründung im PR, `tests/BombRedone.Core.Tests/Graphics/*`), Zuständigkeit Z. 247 „nur `gelaende`“. Umstellung ist möglich: `BombRedone.UI.csproj` Z. 7 referenziert `Core`; die drei Dateien stehen in keiner anderen Spur (`grep -n 'MenuGraphics\|LineRasterizer\|PlayerSelectTeamScreen' docs/arbeitspakete.md` → nur Z. 183–184, 198–199, 247). Gegenprobe vorhanden: `tests/BombRedone.UI.Tests/Screens/MenuGraphicsTests.cs`, `Text/LineRasterizerTests.cs`, `Screens/PlayerSelectTeamScreenTests.cs`. Wandbeschriftung in `PlayfieldScreen` (Z. 185–186, Option C) – `runde.md` §3.1: Punkt (0, 100), senkrecht, links des Rahmens (8, 28)–(312, 192) aus §3.2. Rest: N4-1. |
| N2-2 `RenderCommand.cs` | **behoben** | Z. 233 `RenderCommand.cs` (Waffenwahl, nach `gelaende`) und `tests/BombRedone.UI.Tests/Tools/Render*` in `schuss`; Z. 245 „`gelaende`, danach `schuss`“. Desktop-Vorschau war schon in den Grenzen von `schuss`. |
| N3-1 Sin/Cos | **behoben** | Z. 230 „`Cos` `12CE:12EE`, `Sin` `12CE:1301`“ = `runde.md` Z. 207. |
| N3-2 Baby Missile in Sitzung 3 | **behoben** | Z. 124–125 „Schuss mit Baby Missile (vorher einkaufen) neben einen Panzer (Krater, Energie vorher/nachher)“; passt zu Teil 6 Z. 229. |
| N3-3 Probe / R2-D7 | **behoben** | Z. 188–189: „Die Spur baut die Probe selbst … das Skript des Reviews liegt nicht im Zugriff der Spur“, Befehlsumfang genannt. R2-D7: Z. 100 und `entscheidungen.md` Z. 44 „präzisiert im Zuschnitt `gelaende` (Graph und Treiber mit emuliert)“. Die Optionstabelle Z. 95 behält die alte Fassung – als Vorlage des Nutzers richtig so. |

### Neue Befunde im Delta

**N4-1 (P3, Hinweis) – Pixelvergleich der Probe für `O8:0984` „vollständig“, Wandbeschriftung aber nur in `UI`.**
[H/docs/arbeitspakete.md#L185-L191](https://github.com/kaaasi/bomb-redone/blob/fc003a59b80ab1dae365dfba1e0156231a0e2272/docs/arbeitspakete.md#L185-L191): Die Probe vergleicht `O8:0984` vollständig, das Original
schreibt die Beschriftung in den Bildschirm; der Bildschirmpuffer von `Core/Playfield` enthält sie nach N2-1/C nicht. Ohne Festlegung
vergleicht die Spur entweder gegen die Ausgabe von `PlayfieldScreen` (bis vor die Kopfzeile) oder maskiert die Spalten links des
Rahmens – beides ist vertretbar, sollte aber im Abschnitt „Probe“ oder in `docs/analyse/feld.md` stehen. Beleg: `grep -n
'Wandbeschriftung\|maskier' docs/arbeitspakete.md` → nur Z. 186 (Zuordnung zu `PlayfieldScreen`), keine Aussage zum Vergleich.
Vorschlag: ein Halbsatz „Vergleich: Puffer aus `Core` plus Beschriftung aus `PlayfieldScreen`“; alternativ der Spur überlassen.

Keine weiteren Befunde. Geprüft und unauffällig: `schuss` braucht für die Explosion (K1) die Ellipse `0CC8:0351` (`runde.md`
Z. 265), die nach N2-1 in `Core/Graphics` von `gelaende` liegt – nutzbar ohne Änderung; der Krater `0CC8:0687` (Z. 269) gehört zu
den erlaubten Erweiterungen von `Core/Playfield` (Z. 233).

## 3. Ausgeführte Prüfungen

| Prüfung | Ergebnis |
|---|---|
| `git ls-remote https://github.com/kaaasi/bomb-referenz HEAD` | erreichbar (`0abf1d3`) |
| `git diff --stat fece1f8 fc003a5`, `git log fece1f8..fc003a5` | ein Commit, nur `docs/arbeitspakete.md` (+20/−13), `docs/entscheidungen.md` (±1); `git diff --check` sauber |
| Verweise gegen `docs/analyse/runde.md` | §3.1 (Beschriftung bei (0, 100)), §3.2 (Rahmen), Z. 207 (Sin/Cos), Z. 265, 269 (`0CC8:0351`, `0CC8:0687`) |
| Dateigrenzen gegen `src/` | `UI.csproj` → `Core`; `Core.csproj` → nur `Formats`; Aufrufer von `MenuGraphics`/`LineRasterizer`/`DrawTank` nur in `UI` und `Tools/RenderCommand.cs` |
| `tools/pruefen.sh --schnell` (Head, SDK 10.0.402) | bestanden |

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 750 | 0 | 0 |

**Akzeptierte `[APPROX]`:** keine (Plan-PR ohne Code). **Annahmen:** Die senkrechte Beschriftung um x = 0 bleibt links von x = 8
(Breite der SmallFont-Zeichen nicht nachgemessen); keine Regel liest Pixel links des Rahmens (aus §3.1–§5 gelesen, nicht emuliert).
