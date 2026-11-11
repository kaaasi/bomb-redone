# Nachprüfung PR #37 – Entscheidung R1-D1 = A (DOSBox 0.74-3)

Nachprüfung von `docs/reviews/2026-11-11-r1-d1-dosbox.md`. Base `fa4f3dd`, bisher geprüft `4a0ecee`, jetzt Head `064766a` (geprüft: `git rev-parse HEAD` = Head, `git status --porcelain` leer). Reviewer: Claude (frische, unbeaufsichtigte Sitzung, keine Mitarbeit am PR). Reine Doku: kein Build, kein `tools/pruefen.sh`, CI entfällt.

## 1. Merge-Empfehlung

**Ja** für Head `064766a`. Beide Befunde des Erstreviews (P2, P3) sind behoben, das Delta enthält nur diese beiden Korrekturen, nichts Weiteres. Nur Codelesen und Greps, kein Testnachweis (bei reiner Doku nicht vorgesehen).

## 2. Befunde

- **P2 (Widerspruch im Planabschnitt R1-D1) – behoben.** [`docs/arbeitspakete.md` Z. 50–51](https://github.com/kaaasi/bomb-redone/blob/064766a3c0ed7aedee11f4e0a3f10af4101c5de0/docs/arbeitspakete.md#L50-L51) lautet jetzt „Entschieden: **A** (Nutzer 11.11.2026), DOSBox 0.74-3 mit `cycles=3000`; der Zyklenwert ist durch `re-grundlagen` belegt … Die frühere Empfehlung D mit B (Staging) wurde nicht übernommen.“ Das deckt sich mit Überschrift und `entscheidungen.md`.
  Gegenbeispiel: `grep -n "Staging\|ohne Beleg" docs/arbeitspakete.md` → Z. 46 (Optionstabelle) **und Z. 51** (der neue Satz, der die verworfene Empfehlung benennt). „Ohne Beleg“ ist verschwunden. Z. 51 ist kein Widerspruch, sondern die gewollte Abgrenzung; die Vorgabe „nur Optionstabelle“ ist wörtlich nicht erfüllt, inhaltlich schon. Kein Mangel, nur Hinweis zur Formulierung der Vorgabe.
- **P3 (doppelte Leerzeile in `docs/entscheidungen.md`) – behoben.** Probe: `awk 'prev=="" && $0=="" {print NR} {prev=$0}' docs/entscheidungen.md` → keine Ausgabe. Das Delta entfernt genau diese eine Leerzeile.
- **Erwartet offen, bestätigt: [#39](https://github.com/kaaasi/bomb-redone/issues/39) (P3, IRQ2-Frage für DOSBox 0.74-3).** Issue ist offen. `docs/analyse/re-grundlagen.md` Z. 159 fragt weiterhin nach „DOSBox Staging“; Klärung bei der Aufnahme, nicht Gegenstand dieses PR.
- Neue Befunde: keine.

## 3. Ausgeführte Prüfungen

- `git diff --stat 4a0ecee 064766a`: 2 Dateien, +2/−3 (`docs/arbeitspakete.md`, `docs/entscheidungen.md`); voller Diff gelesen. Nur Doku, keine Binärdateien, nichts Originales.
- `gh api repos/kaaasi/bomb-redone/pulls/37`: Head `064766a`, Base `fa4f3dd`, Dateien des PR = diese zwei; Commits: `4a0ecee`, `064766a`.
- `grep -rn "Staging" docs README.md`: übrige Treffer sind Optionstabelle, der Abgrenzungssatz, ein Review-Zitat und #39 – kein Widerspruch zur Entscheidung.
- Kein Build, keine Tests, keine CI-Auswertung (laut Auftrag). `[APPROX]`: keine neuen; das bestehende (1 Zyklus je Befehl) bleibt unverändert und akzeptiert.
