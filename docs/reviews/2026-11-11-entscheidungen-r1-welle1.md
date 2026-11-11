# Review PR #35 – Entscheidungen R1-D1, R1-D5, R1-D6 (Welle 1)

Base `9ef7234`, Head `d355b1f` (geprüft: `git rev-parse` = Head, Arbeitsbaum sauber). Reviewer: Claude (frische Sitzung, keine Mitarbeit am PR).

## 1. Merge-Empfehlung

**Ja** für Head `d355b1f`. Die drei Entscheidungen sind wie vom Nutzer am 11.11.2026 genannt eingetragen, es gibt keinen weiteren
Inhalt. Prüfumfang: Diff und Skripte, Testnachweis nur für den Originalschutz-Selbsttest; kein Build (laut Auftrag nicht nötig).

## 2. Befunde

Keine P0–P2. 

- **P3 (Hinweis, kein Beleg nötig):** `AGENTS.md` Z. 21 ist nun deutlich länger als die übrigen Zeilen (~140 Zeichen); nur Formatierung.
- **P3 (Hinweis):** `export/` stand schon vorher (Abschnitt „Assets exported by the tools“) in der `.gitignore`; der PR verschiebt
  es in den Schutzblock und dokumentiert es. Funktional ändert sich nichts außer der Einordnung und dem Test – das Gegenbeispiel
  war also schon vor dem PR ignoriert; der neue Selbsttest-Eintrag sichert es jetzt ab.

## 3. Ausgeführte Prüfungen

- `git diff --stat origin/main pr35`: 6 Dateien, +12/−10: `.gitignore`, `AGENTS.md`, `docs/arbeitspakete.md`, `docs/entscheidungen.md`,
  `docs/formate/konfiguration.md`, `tools/originalschutz-selbsttest.sh`. Kein Quelltext, keine Binärdateien, nichts Originales.
- **R1-D1 = A:** `docs/entscheidungen.md` Zeile R1-D1 nennt nur `cycles=3000` und sagt ausdrücklich „DOSBox-Fassung … nicht entschieden“;
  `arbeitspakete.md` ergänzt „Zyklen entschieden … Fassung offen“; „Offen:“-Zeile nennt nur die Fassung. Beleg `re-grundlagen` (~3.580 Befehle/ms)
  deckt sich mit `docs/analyse/re-grundlagen.md` Z. 123. **Offen bestätigt:** DOSBox-Fassung (0.74-3 / Staging / DOSBox-X).
- **R1-D5 = C:** Eintrag nennt Werte aus `BOMBCFG.EXE` (Crumble 75 / Walls None), Abweichung von `BOMB.DOC` (50 % / Random);
  `docs/formate/konfiguration.md` ersetzt „offen“ durch die Entscheidung. Die Tabelle dort (`0x6FFC`, `0x7030`) blieb unverändert.
- **R1-D6 = A:** `export/` in `AGENTS.md` §2.1 und im Schutzblock der `.gitignore`.
- `tools/originalschutz-selbsttest.sh`: alle Fälle „ok“, inkl. neuem `export/BOMB1.png` → ignoriert.
- `git check-ignore -v export/x.png src/export/x.png`: beide Treffer (`.gitignore:16:export/`); `exportfoo/x.png` nicht ignoriert (rc 1).
- CI auf dem Head: `build` = success, `publish` = skipped.

**Akzeptierte `[APPROX]`:** Annahme „1 Zyklus je Befehl“ (R1-D1), wie im Eintrag vermerkt. Annahmen: keine eigene Prüfung des Disassembly-Belegs
aus `re-grundlagen` (bereits im dortigen Review bestätigt).
