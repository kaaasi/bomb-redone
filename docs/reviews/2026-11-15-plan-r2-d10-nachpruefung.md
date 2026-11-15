# Nachprüfung: Plan R2-D10 (PR #122), Review `2026-11-15-plan-r2-d10.md`

Reviewer: Claude Opus 5.5, geplanter Lauf, frische Sitzung ohne Mitarbeit an #122. Geprüft: Head
`5ca54dfc741930fd8c50770552c7fe436b166794` (`git rev-parse pr122` = Head), Base `076137d` (= `main`), nur Delta
`c72eb94..5ca54df` (ein Commit, nur `docs/arbeitspakete.md`, +18/−11) und die Befunde des Vorreviews. Referenz-Repo lesbar
(`git ls-remote … HEAD` → `e42feec`). Nur Doku: kein Build, kein `tools/pruefen.sh`. `H = https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794`.

## 1. Merge-Empfehlung

**Ja** für diesen Head. P2-1 und P3-1 bis P3-4 sowie der PNG-Hinweis sind im Plan umgesetzt; die erneut ausgeführten Proben
bestätigen die eingetragenen Werte. Übrig sind zwei P3 ohne Merge-Hindernis (Präzisierung beim Spurstart genügt).

**Erwartet offen (bestätigt):** Aufnahmen für Sitzung 4 (Runde 3) stehen unverändert in den Restlücken (Delta berührt sie nicht).

## 2. Befunde

**Vorbefunde – erledigt:**

- **P2-1** erledigt: Zeitfenstersuche `--time-from`/`--time-to` mit Rückfall ganzer Tag, Vorfilter in Aufrufreihenfolge, k klein
  ([H/docs/arbeitspakete.md#L270-L279](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L270-L279)); Rückfall „kein Startwert“ → R2-D3 D mit Suchraum und
  Laufzeit in `feld.md` ([#L284](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L284)), ebenso für `schuss-bilder` ([#L295-L296](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L295-L296)).
  R2-D3 nennt D als Rückfall (`docs/entscheidungen.md` Z. 40) → keine neue Nutzerentscheidung. Probe erneut: PNG-Chunks `s3_01`/`s3_02`
  = `IHDR PLTE IDAT IEND` (kein `tIME`), `grep -n -i 'uhrzeit\|[0-9]:[0-9][0-9]' bilder/runde-1/README.md` → kein Treffer;
  `bombtool seed` weiter nur mit fester Zeit (`Program.cs` Z. 19 → `RenderCommand.RunSeedSearch`) – die Erweiterung ist richtig als Teil 1 eingeplant.
- **P3-1** erledigt ([#L271-L272](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L271-L272)). Probe erneut (`ablauf1-GAME1.TWS`, LE): Spieler 2, Wind −92,
  Crumble 75, Spiele 10, Wind-Option 2, Wände 0, Hintergrund 9, Byte 70 (Team Play, `spielstand.md` Z. 72) = 0 – passt zum Zuschnitt.
- **P3-2** erledigt ([#L235-L236](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L235-L236), Abnahme [#L244-L245](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L244-L245)).
  Probe erneut: alle vier Stände 890 Byte, letztes Byte ≠ 0 vor 307 bei 305; `ablauf1-*` erstes Byte ≠ 0 ab 307 = **307**,
  `frisch-*` ab 307 null.
- **P3-3** erledigt ([#L290](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L290)).
- **P3-4** erledigt ([#L318](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L318)); `VgaDraw.cs` liegt in `src/BombRedone.Core/Graphics/` (`git ls-tree`).
- **Hinweis PNG-Leser** übernommen ([#L278-L279](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L278-L279)).

**Neu (aus dem Delta):**

**P3-5 – Dateigrenze für Teil 1 nur „bei belegter Abweichung“.** Teil 1 verlangt jetzt in jedem Fall Änderungen an
`RenderCommand.cs` (Optionen) und `SeedSearch.cs` (Zeitfenster, Vorfilter), die Dateiliste führt beide aber nur unter „bei belegter
Abweichung … (Begründung im PR)“ ([#L280-L282](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L280-L282)); `SeedSearch.cs` ist nur über
`Core/Playfield/*` erfasst. Beleg: `git show 5ca54df:docs/arbeitspakete.md | sed -n 280,282p`. Vorschlag: `RenderCommand.cs` und
`src/BombRedone.Core/Playfield/SeedSearch.cs` als feste Dateien von Teil 1 nennen, samt Tests unter `tests/BombRedone.Core.Tests/Playfield/*`.

**P3-6 – Dateizeiten von Sitzung 3 nur passiv.** „Liefert der Nutzer die Dateizeiten …“ ([#L274-L275](https://github.com/kaaasi/bomb-redone/blob/5ca54dfc741930fd8c50770552c7fe436b166794/docs/arbeitspakete.md#L274-L275))
sagt nicht, wer fragt; ohne Frage läuft die Spur sicher in die Ganztagssuche. Vorschlag: Leitstelle stellt die Frage mit dem
Spurstart (gebündelt nach `AGENTS.md` §2.9) – kein Plan-Blocker, da der Rückfall greift.

## 3. Ausgeführte Prüfungen

- `git ls-remote https://github.com/kaaasi/bomb-referenz HEAD` → `e42feec…`; Referenz-Klon auf `origin/main`.
- `git fetch origin pull/122/head`; `git rev-parse` = `5ca54df…`; `git log c72eb94..5ca54df` → 1 Commit; `git diff --stat` → nur
  `docs/arbeitspakete.md`; Diff vollständig gelesen.
- Python-Probe (`python3 -I`, nur lesend) über die vier `.TWS` (Länge, letztes/erstes Byte ≠ 0 um 307, Werte Offset 32–70) und
  PNG-Chunks von `s3_01`/`s3_02` – Ergebnisse oben.
- `git ls-tree` `Core/Graphics`, `Core/Playfield`; `git grep '"seed"'` in `src/BombRedone.Tools`.
- Originalschutz: Delta und diese Datei enthalten keine Bilder oder Pixel, nur Dateinamen und Bytewerte.
- Nicht ausgeführt: `tools/pruefen.sh` (nur Doku, laut Auftrag).

**Akzeptierte `[APPROX]`:** keine neuen. **Annahme, getrennt:** DOSBox übernimmt die Rechneruhrzeit (für P3-6).
