# Review PR #56 – Fortsetzungen nur durch die Leitstelle

Geprüft: Base `3b2a7c67cba5e875907332afa60aa06219f160de`, Head `5607bcde3bb83a78da22203119abbef8e70fb0a9`
(`git rev-parse HEAD` = Head, `git status --porcelain` leer). Reine Doku-Prüfung: `git diff --stat` = 2 Dateien
(`docs/prozess/anhang.md` +2, `docs/vorlagen/spur-start.md` +8/−11), kein Code, kein Build, kein `tools/pruefen.sh`.
Modell: Claude Sonnet 5.5, frische Sitzung ohne Mitarbeit am PR.

## 1. Merge-Empfehlung

**Ja** für diesen Head (nur Codelesen, kein Testnachweis nach `zusammenarbeit.md` §6 Nr. 4, bei reiner Doku entfällt er).
Die geänderten Stellen sind in sich widerspruchsfrei. Die zwei P2-Befunde unten liegen außerhalb des Diffs, ändern aber
den Startaufruf der laufenden Runde; sie sollten vor dem nächsten Start (oder direkt mit diesem PR) bereinigt werden.

## 2. Befunde

**P2 – `docs/arbeitspakete.md` nennt weiter „Selbstfortsetzung“ im Start der Runde**
[Zeile 88](https://github.com/kaaasi/bomb-redone/blob/5607bcde3bb83a78da22203119abbef8e70fb0a9/docs/arbeitspakete.md#L88)
(Welle 1: „Start in wenigen Minuten, Selbstfortsetzung, höchstens 3 je Spur“) und
[Zeile 162](https://github.com/kaaasi/bomb-redone/blob/5607bcde3bb83a78da22203119abbef8e70fb0a9/docs/arbeitspakete.md#L162)
(Stufe 2a: „Start wie Welle 1 (… Selbstfortsetzung, …)“). Beide verweisen auf den Kurzaufruf „Geplanter Lauf mit Teilen“,
der jetzt das Gegenteil sagt; die Leitstelle liest den Rundenstart hier. Beleg:
`grep -n "Selbstfortsetzung" docs/arbeitspakete.md` → Treffer 88 und 162 (die vom Auftrag genannte Suche
`grep -rn "Selbstfortsetzung\|Fortsetzung per" docs AGENTS.md CLAUDE.md` liefert sie; sonst keine Treffer außerhalb
`docs/archiv` und `docs/reviews`). Vorschlag: „Selbstfortsetzung“ → „Fortsetzungen legt die Leitstelle an“.

**P3 – `docs/prozess/zusammenarbeit.md` Zeile 93 (Label `spur-wartet`) nicht nachgezogen**
Die Zeile lautet „Lauf **ohne** Fortsetzung (Zugriff oder Teil blockiert, Kettenende, Freigabe `create_trigger` abgelehnt, …)“.
Neu setzt ein Lauf `spur-wartet` auch dann, wenn Teile offen sind und er die Fortsetzung anfordert (`spur-start.md` Schritt 5);
„Freigabe abgelehnt“ trifft für Spurläufe nicht mehr zu (gilt nur noch für die Nachprüfung des Fixlaufs, Zeile 84, und
bleibt als erwartet offen bestätigt). Beleg: `sed -n 93p docs/prozess/zusammenarbeit.md`. Folge: nur Lesbarkeit, die
Leitstelle arbeitet nach `anhang.md` F („`spur-wartet` durchsehen“) ohnehin richtig.

**P3 – Namensschema nur teilweise ausgeführt**
`Bomb R<runde> <spur> – <Art>` nennt die Arten nicht (Spur, Fortsetzung <n>, Fixlauf, Nachprüfung, Review). Nur
„Fortsetzung <n+1>“ ist in `spur-start.md` Schritt 5 ausgeschrieben. Kein Widerspruch, nur Auslegungsspielraum.

## Geprüft ohne Befund

- Kurzaufruf „Geplanter Lauf mit Teilen“ ([Zeile 11–12](https://github.com/kaaasi/bomb-redone/blob/5607bcde3bb83a78da22203119abbef8e70fb0a9/docs/vorlagen/spur-start.md#L11)):
  Lauf legt nichts an, „so viele Teile wie möglich“ passt zu Schritt 3 („nächsten Teil im selben Lauf, solange Zeit bleibt“).
- „Einzelner Folgeteil“ (Zeile 24) zitiert den neuen Kurzaufruf-Wortlaut korrekt (mit Auslassung) und verweist weiter auf „ohne Schritt 5“.
- „Teile und Fortsetzung“: Einleitung (Verweis statt „erlaubt die Fortsetzung“), Schritt 1 (keine Fortsetzung bei fremdem
  Bearbeiter), Schritt 4 und 6 (keine Fortsetzung bei Blockade/Erschöpfung) sowie neuer Schritt 5 stimmen mit
  `anhang.md` F überein („Fortsetzung anlegen – Leitstelle“, Prompt mit Zähler, Modell durch die Leitstelle).
- Kein Pfad, über den ein Lauf `create_trigger` für eine Fortsetzung darf: `grep -rn "create_trigger" docs AGENTS.md CLAUDE.md`
  → nur Leitstellen-Zugriff (`anhang.md` F), Fixlauf-Nachprüfung (`spur-start.md` 31, `zusammenarbeit.md` 84), beides ausdrücklich
  keine Fortsetzung.
- „Erwartet offen“ bestätigt: Fixläufe legen die Nachprüfung weiterhin selbst an (`zusammenarbeit.md` §5 Punkt 8; `spur-start.md`
  Fixlauf-Absatz, Zeile 31) – bewusst unverändert, nicht beanstandet.

## 3. Ausgeführte Prüfungen und `[APPROX]`

- `git fetch origin`; `git checkout 5607bcd`; `git rev-parse HEAD`; `git status --porcelain` (leer);
  `git diff --stat/--patch 3b2a7c6 5607bcd`; die oben genannten `grep -rn`/`sed -n`-Aufrufe.
- Nicht ausgeführt (reine Doku, CI entfällt): `tools/pruefen.sh`, Build, Tests, Originaldaten.
- Akzeptierte `[APPROX]`: keine betroffen. Annahme: Die Vorgaben des Nutzers vom 11.11.2026 stehen nur im Auftrag und in den
  Vermerken des PR, nicht gesondert belegt.
