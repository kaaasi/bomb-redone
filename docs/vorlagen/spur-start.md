# Vorlage: Start einer Spur

**Kurzaufruf** (das ist alles, was der Nutzer in den neuen Chat schreibt):

> Repo kaaasi/bomb-redone. Arbeite nach `docs/vorlagen/spur-start.md`. Spur: `<name>`. Agent: `<Claude Code | ChatGPT | …>`.
> Besonderheiten: `<nur falls nötig, sonst weglassen>`

**Geplanter Lauf mit Teilen** (Startaufruf der Leitstelle; die Fortsetzung hängt `Fortsetzung <n> von 3, Übergabe in PR #<nr>.` an):

> Repo kaaasi/bomb-redone. Arbeite nach `docs/vorlagen/spur-start.md`. Spur: `<name>`. Agent: Claude Code.
> Unbeaufsichtigt, keine Rückfragen. Teile nach Abschnitt „Teile und Fortsetzung“, so viele Teile wie in diesem Lauf
> möglich; keine geplanten Aufgaben anlegen (Fortsetzungen legt nur die Leitstelle an, höchstens 3 je Spur). Modell der Spur: `<modell>`.
> Zugriff zuerst prüfen: `git ls-remote https://github.com/kaaasi/bomb-referenz HEAD`. Scheitert das,
> `add_repo` (ggf. erst per `ToolSearch` mit `select:mcp__claude-code-remote__add_repo` laden; meist nicht vorhanden) für
> `kaaasi/bomb-referenz` (access read) und erneut prüfen; bleibt es unerreichbar, sofort blockiert melden (genaue
> Fehlermeldungen von `ls-remote` und `ToolSearch`), keine Code-Arbeit. Meldung: Benachrichtigung an den Nutzer; gibt es einen
> PR der Spur, dort Kommentar und Label `spur-wartet`; sonst keinen PR anlegen.

Der Zugriffsabsatz steht in **jedem** Startaufruf eines geplanten Spurlaufs (Spur, Fortsetzung) wörtlich und unverändert; Reviews nach
`review-anfrage.md` nur, wenn Daten nötig sind („keine Code-Arbeit“ heißt dort „kein Review“). Voraussetzung der Leitstelle:
`docs/prozess/anhang.md` F. Geplante Läufe erben Schreibrecht auf das Referenz-Repo, Spuren und Reviews schreiben dort trotzdem
nie; fehlen Daten oder Bilder, im PR melden – ablegen nur Leitstelle oder Nutzer.

**Einzelner Folgeteil ohne Fortsetzung:** wie oben, aber statt „Teile nach … (Fortsetzungen legt nur die Leitstelle an, höchstens 3 je Spur).“ der Satz
„Teile nach Abschnitt ‚Teile und Fortsetzung‘; nur Teil <n>, keine Fortsetzung anlegen.“ – es gilt „Teile und Fortsetzung“ ohne
Schritt 5.

**Fixlauf** (geplanter Lauf): Startaufruf wie oben mit Auftrag „Befunde `<IDs>` aus `<review-datei>` zu PR
#`<nr>` beheben, Head vorher `<sha>`“ – ohne Befund (nur Rebase/Planpflicht): „PR #`<nr>` auf `main` rebasen, `<Anpassung laut
Plan>`, Head vorher `<sha>`“. Am Ende (`zusammenarbeit.md` §5 Punkt 8): bei behobenem P0/P1 **keine** geplante Aufgabe anlegen, sondern
`spur-wartet` und in der Übergabe „Nachprüfung anlegen – Leitstelle“ mit fertigem Kurzaufruf aus `review-anfrage.md` (Delta
alter..neuer Head, nur diese Befunde) und Modell nach „Modellwahl“; sonst – nur P2/P3 oder ohne Befund – Label `leitstelle-delta`. Mit dem Push des neuen
Heads `review-nein` entfernen. Widerspricht der Lauf einem Befund mit eigenem Beleg: `beleg-strittig`, beide Belege in der
Übergabe, für diesen Punkt keine Nachprüfung.

## Ablauf für den Agenten

1. Lies `AGENTS.md`, `docs/prozess/zusammenarbeit.md` §2–4 und in `docs/arbeitspakete.md` **nur** die aktuelle Runde und deine
   Spur. Archiv, alte Reviews und `docs/prozess/anhang.md` nur bei konkretem Bedarf.
2. Einrichten nach `AGENTS.md` §3 (Claude Code: zusätzlich `CLAUDE.md`), Originaldaten nach `gamedata/`.
3. Offener PR der Spur mit Bearbeiter? Nur nach Übergabekommentar weiterarbeiten (Übernahme: `zusammenarbeit.md` §2), sonst den
   Nutzer fragen (geplante Läufe: „Teile und Fortsetzung“ Schritt 1). Neu: Branch `paket/<spur>` von aktuellem `main`, sofort
   Draft-PR mit `Bearbeiter: <Agent>` und Base-SHA.
4. Harte Regeln: reproduzieren → roter Regressionstest → Fix; Dateigrenzen; `[APPROX]`/`[ORIGINAL]`;
   Quelltext und Kommentare englisch.
5. Vor jedem Push: `git fetch origin && git rebase origin/main`, `tools/pruefen.sh --schnell` (`AGENTS.md` §5).
6. Fertig: Beschreibung nach `.github/pull_request_template.md`, Draft aufheben, Status im eigenen Abschnitt von
   `docs/arbeitspakete.md`. Mehrere kleine PRs sind besser als ein großer.
7. **Immer zum Schluss** (fertig wie unterbrochen): Übergabekommentar nach `docs/handoffs/VORLAGE.md` (`Bearbeiter: frei`).
8. Antwort an den Nutzer **knapp** (`AGENTS.md` §7): PR-Links, Ergebnis in einem Satz je PR, offene Fragen.

## Teile und Fortsetzung (geplante Läufe)

Gilt, wenn die Spur in `docs/arbeitspakete.md` eine geordnete Liste von Teilen hat und der Startaufruf auf diesen Abschnitt verweist
oder einen einzelnen Folgeteil nennt (dann ohne Schritt 5).

1. **Stand ermitteln:** offenen PR (`paket/<spur>`) und letzten Übergabekommentar lesen; dort steht „Fortsetzungen verbraucht:
   n von 3“ (nennt der Startaufruf einen anderen Zähler, gilt der höhere). Bearbeiter ≠ `frei` ohne Übergabekommentar **nach**
   dessen letztem Bearbeiter- oder Übernahme-Eintrag (abgebrochener oder laufender Lauf): nichts am Branch ändern, nur Label
   `spur-wartet` und Hinweiskommentar – **ohne** Übergabe und **ohne** `Bearbeiter: frei` –, keine Fortsetzung, Lauf beenden; der
   Hinweis ersetzt Ablauf Schritt 7.
2. **Ein Branch, ein PR:** Teil 1 legt `paket/<spur>` von aktuellem `main` an und öffnet den Draft-PR (Base `main`)
   mit der Teilliste als Aufgabenliste. Jeder Teil sind Commits auf diesem Branch; eine Fortsetzung übernimmt ihn nach der
   Übergabe (Übernahmekommentar, `zusammenarbeit.md` §3) – nacheinander, nie gleichzeitig. Vor dem Push auf `origin/main`
   rebasen, dann `git push --force-with-lease`.
3. **Teil abschließen:** grünes `--schnell`, gepusht, Teil im PR abgehakt, Übergabe mit „Fortsetzungen verbraucht: n von 3“.
   Der PR bleibt Draft, bis alle Teile fertig sind; dann den nächsten Teil im selben Lauf beginnen, solange Zeit bleibt.
4. **Teil blockiert** (kein grünes `--schnell`, Beleg fehlt, Referenz-Repo unerreichbar, fremde Datei nötig): Draft mit
   Übergabe, Label `spur-wartet`, in der Übergabe „blockiert – Entscheidung Nutzer“ und Grund. **Keine Fortsetzung**, den Teil
   nicht erneut versuchen.
5. **Fortsetzung anfordern** (nur wenn Teile offen sind, nichts blockiert und n < 3): Der Lauf legt **keine** geplante Aufgabe an
   (Nutzer 11.11.2026: Fortsetzungen legt nur die Leitstelle an). Label `spur-wartet`, in der Übergabe „Fortsetzung anlegen –
   Leitstelle“ mit fertigem Prompt (Name `Bomb R<runde> <spur> – Fortsetzung <n+1>`, Startaufruf mit Zähler) und „Modell der
   Fortsetzung: <modell>“; die Leitstelle legt an und setzt das Modell (`docs/prozess/anhang.md` F).
6. **Kettenende:** Alle Teile fertig → Beschreibung vervollständigen, „Ready for review“
   (`gh api -X POST repos/kaaasi/bomb-redone/pulls/<nr>/ccr/ready_for_review`, Antwort `{"draft":false}`; scheitert es,
   Fehlermeldung in der Übergabe, die Leitstelle setzt es), `spur-wartet`, Übergabe „Spur fertig – Review“. n = 3 und Teile offen:
   Draft lassen, `spur-wartet`, „Fortsetzungen erschöpft – Entscheidung Nutzer“. Keine Fortsetzung. Erstreview und Merge in der
   Integrationsfolge legt die Leitstelle an; einen eigenen Integrations-PR je Spur gibt es nicht.
7. Nie eine fremde Spur fortsetzen oder starten.
