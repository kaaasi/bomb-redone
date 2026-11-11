# Vorlage: Unabhängiges Review

**Kurzaufruf** (die Leitstelle liefert nur diesen Block, der Nutzer kopiert ihn in einen **neuen** Chat des Reviewers):

> Repo kaaasi/bomb-redone. Unabhängiges Review nach `docs/vorlagen/review-anfrage.md`.
> PR: #`<nr>` · Base: `<sha>` · Head: `<sha>` · Ablage: `docs/reviews/<JJJJ-MM-TT>-<thema>.md`
> Gegenbeispiele erneut ausführen: `<IDs mit einer Zeile Ablauf>`
> Erwartet offen (nur bestätigen): `<IDs | –>`
> Besonders prüfen: `<1–3 Punkte | –>`
> Erstelle im Repo einen neuen Remote-Branch `review/<thema>` auf Basis von `main`, committe dort ausschließlich
> die Review-Datei mit Agenten-Trailer und öffne einen Review-PR gegen `main`; setze am PR #`<nr>` das Label `review-ja` bzw.
> `review-nein` (mit Netzzugang). Diese GitHub-Schreibaktionen sind
> ausdrücklich autorisiert. Nicht mergen. Falls Schreiben nicht möglich ist, stelle die fertige Datei bereit.

**Geplanter Lauf:** zusätzlich „Unbeaufsichtigt, keine Rückfragen.“ und – wenn Originaldaten, Spielstände oder Bilder nötig
sind – der Zugriffsabsatz aus `spur-start.md` (Kurzaufruf „Geplanter Lauf“). **Nachprüfung:** zusätzlich „Nachprüfung des
Reviews `<datei>`, bisher geprüfter Head `<sha>`, nur Delta `<alt>..<neu>` und die genannten Befunde.“, Ablage `…-nachpruefung.md`.
**Gesamtreview am Rundenende** und **Aufrufe für ChatGPT/Codex:** `docs/prozess/anhang.md` C und D.

## Ablauf für den Reviewer

1. Lies `AGENTS.md` und `docs/prozess/zusammenarbeit.md` §5–6. Hast du an diesem PR mitgearbeitet, brich ab und sag es – das
   Review muss aus einer anderen Sitzung kommen.
2. SDK nach `AGENTS.md` §3 einrichten, im Repo `dotnet --version` prüfen (Probleme: `docs/prozess/umgebung.md`). Prüfe genau den
   genannten Head: `git rev-parse HEAD` = Head-SHA und `git status --porcelain` leer, sonst „unverifizierter Stand“ (ohne
   vollständigen Klon: `docs/prozess/anhang.md` E). Selbst bauen und `tools/pruefen.sh` ausführen – Erstreview: alle drei Läufe;
   Nachprüfung und Gesamtreview: `--schnell` (`zusammenarbeit.md` §5 „Sparsame Kette“). Aussagen der PR-Beschreibung
   nicht ungeprüft übernehmen.
3. Zuerst die genannten Gegenbeispiele erneut ausführen, dann Diff, betroffene Aufrufer, neue Tests. Eine Gegenprobe pro neuem
   Test, wo es geht (gezielte Mutation in temporärer Kopie → Test muss rot werden; Gesamtreview: siehe `anhang.md` C).
4. Bericht mit genau drei Teilen, **höchstens 120 Zeilen**, keine Wiederholung der PR-Beschreibung, keine Freigabe nach Testanzahl:
   1. **Merge-Empfehlung** ja/nein für genau diesen Head, mit Begründung
   2. **Befunde** P0–P3 mit Datei/Zeile (Link auf den Head) und **ausführbarem Beleg** (roter Test oder Probe mit Befehl und
      Ergebnis; „fehlt im Remake“ zusätzlich mit erfolgloser Codesuche); ohne Beleg höchstens P3 „Verdacht“ (§5 Punkt 7)
   3. **Ausgeführte Prüfungen** (Befehle, Tabelle aus `tools/pruefen.sh`) und **akzeptierte `[APPROX]`**, getrennt von Annahmen

   Ohne nachgewiesenen Checkout (nur Codelesen oder unverifizierter Stand) kein Testnachweis nach `zusammenarbeit.md` §6 Nr. 4;
   die Empfehlung sagt dann ausdrücklich „nur Codelesen“, der Originaldaten-Nachweis muss im PR stehen (auch als Kommentar der
   Leitstelle).
5. Ablage: nichts am Code oder am geprüften PR ändern (außer dem Label in Schritt 6), nichts ins Referenz-Repo schreiben. Datei auf
   neuem Branch `review/<thema>` (Basis `main`), PR mit **nur dieser Datei**, Commit-Trailer mit deinem Agentennamen. Ohne
   Schreibzugriff: Datei an den Nutzer.
6. Am geprüften PR `review-ja` bzw. `review-nein` setzen (das andere entfernen), sonst nichts am PR ändern. Ohne Schreibzugriff
   setzt es die Leitstelle.
7. Antwort an den Nutzer: Link zum Review-PR, Empfehlung in einem Satz.
