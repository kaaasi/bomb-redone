# Nachprüfung PR #144 – Plan Runde 3 (Delta `87c7083..82d5cae`)

Geprüft: Base `aec025a`, Head `82d5cae7392feb088fca2f4531c72e182b5b7786` (`git rev-parse HEAD` = Head, `git status --porcelain` leer).
Nachprüfung des Reviews `docs/reviews/2026-11-15-plan-runde-3.md` (PR #145, Head `87c7083`); nur Delta und die Befunde P2-1, P2-2, P3-1–P3-3.
Reviewer: Claude Sonnet 5.5, neue Sitzung ohne Beteiligung an PR #144 oder #145 (reine Doku-Prüfung, `zusammenarbeit.md` §5 Modellwahl).
Gegenbeispiele erneut ausgeführt: – (keine genannt).

## 1. Merge-Empfehlung

**Ja** für Head `82d5cae`. Alle fünf Befunde sind im Delta behoben, das Delta enthält nur Doku (2 Dateien, +12/−4 Zeilen) und
keine neue Lücke. Es bleibt keine offene P0/P1/P2 (`zusammenarbeit.md` §6 Nr. 5). Einordnung: **nur Codelesen** – kein Code im Diff,
kein Testnachweis nach §6 Nr. 4 nötig; CI entfällt wegen `paths-ignore` (§6 Nr. 3).

## 2. Befunde (Stand der Nachprüfung)

| Befund | Ergebnis | Beleg |
|---|---|---|
| P2-1 Waffenbestand ohne Eigner | **behoben** | `arbeitspakete.md` Z. 66–68: `waffen` legt Bestand, Lenk- und Abwehrwerte in `PlayfieldTank` an, samt Übertrag Konto → Panzer (Rundenbeginn) und zurück (Rundenende); `zug` und `spielfolge` nutzen nur diese API. |
| P2-2 Aufrufer fremder Dateien | **behoben** | Z. 172–176 (neue Regel in „Zuständigkeit“) nennt die drei Aufrufer-Dateien und den Testhelfer. |
| P3-1 Rundenbeginn nur mit 10 Spielern | **behoben** | `dosbox-aufnahme.md` Nr. 3: „mit 2 und mit 10 Spielern … je Bild … Uhrzeit notieren“. |
| P3-2 Todesarten abgeschwächt | **behoben** | `dosbox-aufnahme.md` Nr. 12: „je Todesart … ein Tod als Video – so viele Tode, bis jede vorkam“. |
| P3-3 PR-Text veraltet / `IndexBuffer.cs` | **behoben** (PR-Text), **zurückgestellt mit Eintrag** (Kommentar) | Siehe unten. |

**P2-1 – Prüfung des Fixes (kein neuer Befund).**
- Die genutzte API existiert und bleibt unverändert: `src/BombRedone.Core/Economy/PlayerAccount.cs` hat öffentlich `Stock(ItemKind,int)`
  (Z. 51), `SetStock` (Z. 63), `AddStock` (Z. 78).
- Die Dateigrenzen tragen den Auftrag: `PlayfieldTank.cs` und `Playfield.cs` liegen in `src/BombRedone.Core/Playfield/*`
  (Spur `waffen`, Z. 71–76); die Panzer entstehen in `Playfield.cs:230` (`new PlayfieldTank(i)`).
  Beleg, dass der Übertrag noch fehlt (also wirklich neue Arbeit ist): `grep -n -i "stock\|account" src/BombRedone.Core/Playfield/PlayfieldTank.cs`
  und `grep -n "PlayerAccount\|PlayfieldTank" src/BombRedone.Core/Playfield/RoundSequence.cs src/BombRedone.Core/Playfield/RoundStart.cs` → keine Treffer.
- Integrationsfolge `waffen → zug → spielfolge` bleibt stimmig: `spielfolge` ruft die API von `waffen` aus `RoundSequence.cs` auf (seine Datei).

**P2-2 – Prüfung des Fixes.** Alle genannten Aufrufer bestehen im Head, `grep -n` ergibt:
`RenderCommand.cs:86` (`StatusWindow`), `:93` (`ScoreScreen`, Ausdruck `=> new(...)`, deshalb kein Treffer für `new ScoreScreen`), `:102` (`ScoreTeamScreen`),
`:114` (`ShopScreen`), `:214` (`PlayfieldScreen`); `Desktop/DesktopGame.cs:145` (`new RoundSequence`);
`Desktop/RoundPreviewScreen.cs:88` (`PlayfieldShotScreen.Fire`). Die Glob-Prüfung `PlayfieldSession*` trifft nur `tests/BombRedone.UI.Tests/Screens/PlayfieldSession.cs`;
die zug-eigenen Tests `PlayfieldScreenSessionTests.cs` u. a. beginnen mit `PlayfieldScreen…` und bleiben bei `zug`. Die Rückfallregel
(„passt die spätere Spur nach dem Rebase an“) ist ausführbar.

**P3-3 – Details.** `gh api repos/kaaasi/bomb-redone/pulls/144` → `head.sha` = `82d5cae…`, Text nennt Head `82d5cae`, R3-D1/D2 als entschieden, Befundverweis;
„Draft bis zur Entscheidung“ ist entfernt (`draft:false`). Der PR-Text sagt, der Code-Kommentar in `IndexBuffer.cs` sei behoben; das stimmt **nicht**
(`git diff --name-only aec025a 82d5cae` → nur `docs/**`, `sed -n 6p src/BombRedone.Core/Graphics/IndexBuffer.cs` → Verweis „spur gelaende“ unverändert).
Gedeckt ist das durch den neuen Eintrag in `p3-runde-3` (`arbeitspakete.md` Z. 152–153); der PR-Text ist hier ungenau, Wirkung keine.
Als **P3-Hinweis** (kein Merge-Hindernis): im PR-Text „(Code-Kommentar … → `p3-runde-3`)“ als „vorgemerkt“ lesen.

**Hinweis zu P3-2 (kein Befund).** „(sieben)“ zählt die Ausgänge von `Random(7)`; `runde.md` Z. 308 ordnet ihnen `O5[0]`, `O5[1]`, `O5[2]`, `O5[1]`, `O5[3]`,
`O5[4]`, `O5[5]` zu, also sechs verschiedene Routinen. Das Abbruchkriterium „bis jede vorkam“ bleibt brauchbar; bei der Aufnahme genügen sechs verschiedene Bilder.

## 3. Ausgeführte Prüfungen

| Prüfung | Befehl | Ergebnis |
|---|---|---|
| Head, Arbeitsbaum | `git rev-parse HEAD; git status --porcelain` | `82d5cae…`, leer |
| Delta nur Doku | `git diff --name-only aec025a 82d5cae` | `docs/arbeitspakete.md`, `docs/archiv/runde-2.md`, `docs/entscheidungen.md`, `docs/vorlagen/dosbox-aufnahme.md` (gesamter PR); das Delta `87c7083..82d5cae` berührt nur `arbeitspakete.md` und `dosbox-aufnahme.md` (`--stat`: 2 Dateien, +12/−4) |
| Delta gelesen | `git diff 87c7083 82d5cae` | 4 Hunks in `arbeitspakete.md`, 2 in `dosbox-aufnahme.md`; keine weitere Änderung |
| `main` unverändert | `git fetch --depth=1 origin main; git rev-parse FETCH_HEAD` | `aec025a…` = Base |
| CI entfällt | `.github/workflows/build.yml` Z. 9, 12 `paths-ignore: ['docs/**', '**/*.md']` | „entfällt, nur Doku“ (§6 Nr. 3) |
| Originalschutz | `python3 -I .github/scripts/originalschutz_pruefen.py` | „Keine Originaldateien eingecheckt.“ |
| Erwartet offen | `gh api repos/kaaasi/bomb-redone/issues/{39,71,102,140,141} --jq .state` | alle fünf `open` (bestätigt) |
| PR #144 | `gh api repos/kaaasi/bomb-redone/pulls/144` | `open`, `draft:false`, Head `82d5cae`, Labels vor diesem Review leer |

`tools/pruefen.sh --schnell`: nicht ausgeführt – das Delta enthält keinen Code, das Ergebnis wäre das von `main`. Akzeptierte `[APPROX]`: keine neuen.
Annahmen: Dateinamen und Aufrufer am Stand Head gelesen (Klon `--depth 50`); die neuen Spurdateien (`Core/Turn`, `Core/Battle`) existieren noch nicht.
