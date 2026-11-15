# Nachprüfung Review PR #135 – Plan `p3-runde-2`

Geprüfter Head: `39fd40e16bb3338bd8ad6b394f504120e20f23ff`, Base `189031fab8feeb539b7ed1751358088e8ae9efde`, Delta zu
`0d0d95e4b5d104c3e2252190fbf708833b55a4f6` (Review `2026-11-15-plan-p3.md`). Checkout nachgewiesen (`git rev-parse HEAD` = Head,
`git status --porcelain` leer). Reviewer: Claude Opus 5.5, geplanter Lauf, ohne Mitarbeit am PR. Nur Doku-PR; `tools/pruefen.sh`
nicht ausgeführt (laut Auftrag nicht nötig).

## 1. Merge-Empfehlung

**Ja für diesen Head.** Das Delta (`docs/arbeitspakete.md`, +10/−5) behebt P2-1 und die drei P3; keine neuen Befunde. #102 bleibt
wie erwartet Hinweis für Runde 3 ([Z. 309](https://github.com/kaaasi/bomb-redone/blob/39fd40e16bb3338bd8ad6b394f504120e20f23ff/docs/arbeitspakete.md#L309)).

## 2. Befunde

**P2-1 – behoben.** Die Dateigrenzen enthalten jetzt `tests/BombRedone.UI.Tests/Screens/Playfield*` mit beiden Dateien namentlich
([Z. 314–315](https://github.com/kaaasi/bomb-redone/blob/39fd40e16bb3338bd8ad6b394f504120e20f23ff/docs/arbeitspakete.md#L314-L315)).
Gegenprobe: `ls tests/BombRedone.UI.Tests/Screens | grep Playfield` → u. a. `PlayfieldSeedCommandTests.cs` (#129, #130) und
`PlayfieldScreenSessionTests.cs` (#131), beide vom Muster erfasst. #131 steht in Teil 1 („#131 Testteil“) und Teil 3 (Z. 310).
Das Muster deckt auch weitere `Playfield*`-Tests ab; die früheren Eigentümer (`gelaende`, `schuss`, `gelaende-bilder`,
`schuss-bilder`) sind gemergt, offen sind nur #135 und #136 → kein Konflikt.

**P3-1 – behoben.** #111 nennt jetzt Mischen und ausgelassenen Startspieler und legt den Ort fest: Ablauflogik nach `Core/Playfield`,
roter Test in `tests/BombRedone.Core.Tests/Playfield/*`, kein neues Testprojekt (Z. 304–306). Gegenprobe: `RoundPreview` ist weiter
`private static` in `src/BombRedone.Desktop/DesktopGame.cs` Z. 157 (ruft `SeedSearch.SkipShuffle` Z. 161 je Runde); `SeedSearch.cs`
liegt in `src/BombRedone.Core/Playfield/` – beide Ziele in den Grenzen (Z. 313), `BombRedone.slnx` bleibt unberührt.

**P3-2 – behoben.** `src/BombRedone.Desktop/*` hat „danach `p3-runde-2`“ (Z. 336); eine Sammelzeile (Z. 337–338) weist nach Merge
aller Welle-2-Spuren alle Plan-Dateien (`Core/Numerics/*`, `Core/Graphics/*`, `Core/Playfield/*`, `RenderCommand.cs`, `feld*`,
`feld.md`) allein `p3-runde-2` zu. Die älteren Zeilen (z. B. Z. 340 „in Welle 2 nur `schuss`“) bleiben stehen, widersprechen aber
nicht, da sie Welle 2 betreffen und die Sammelzeile ausdrücklich „nach Merge“ gilt. Kein Befund.

**P3-3 – behoben.** #112: „der alte Statuseintrag ‚Status gelaende‘ bleibt historisch“ (Z. 307) – damit ist #112 ohne Änderung
fremder Statusabschnitte abschließbar.

## 3. Ausgeführte Prüfungen

- `git fetch origin pull/135/head`, `git checkout` Head, `git diff --stat 0d0d95e..39fd40e` → nur `docs/arbeitspakete.md`; Delta
  vollständig gelesen.
- `ls tests/BombRedone.UI.Tests/Screens`, `ls src/BombRedone.Core/Playfield`, `grep -n "SkipShuffle\|RoundPreview" -r src`,
  `grep -n "Playfield\|Welle 2" docs/arbeitspakete.md`; offene PRs über die GitHub-Schnittstelle (nur #135, #136).
- Kein Build, keine Tests (Doku-PR). Keine `[APPROX]` betroffen.
