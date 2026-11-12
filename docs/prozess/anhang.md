# Anhang zur Prozessdoku – Sonderfälle

Nur bei Bedarf lesen. Ergänzt [zusammenarbeit.md](zusammenarbeit.md) und die Vorlagen in `docs/vorlagen/`; bei Widerspruch gilt
`AGENTS.md`. Die Leitstelle liest den Anhang vollständig.

## A. Vertretung der Leitstelle

Ist die Leitstelle nicht verfügbar (z. B. Kontingent erschöpft):

1. Der Nutzer ernennt ausdrücklich einen anderen Agenten (Nachricht an den Agenten; Eintrag in `zusammenarbeit.md` §1 per PR,
   sobald möglich).
2. Die neue Leitstelle liest `AGENTS.md`, `zusammenarbeit.md`, diesen Anhang, `docs/arbeitspakete.md` (aktuelle Runde), die
   offenen PRs mit Übergabekommentaren und die letzten Reviews – mehr ist nicht nötig.
3. Gleiche Regeln: Merge-Prüfung (`zusammenarbeit.md` §6), Review durch eine andere Sitzung oder einen anderen Agenten. **Hat sie
   selbst umgesetzt, gibt sie nicht selbst frei.** Steht kein zweiter Agent bereit, entscheidet der Nutzer anhand des
   dokumentierten Review- und Teststands.
4. Kann der Agent nicht mergen (fehlende Rechte), bereitet er die Merge-Prüfung als PR-Kommentar vor; der Nutzer merged in der
   Weboberfläche (§6 Nr. 7 beachten).
5. Kehrt die alte Leitstelle zurück, übernimmt sie per Übergabekommentar bzw. Eintrag in §1.

Es gibt **keine automatische Integration**, nur weil ein Kontingent erschöpft ist.

## B. GitHub Free: was gilt, was nicht

| Funktion | Lage im privaten Repo | Folge |
|---|---|---|
| Branch-Schutz, Rulesets, Pflicht-Checks, Pflicht-Reviews | nicht verfügbar (HTTP 403 „Upgrade to GitHub Pro“) | Prozessregeln (§6), keine technische Sperre |
| Merge per REST mit `sha` | verfügbar | einzige serverseitige Bindung an den geprüften Head |
| Draft-PRs | verfügbar | Sperre für Branches und Zwischenstände |
| Issues, Labels, Milestones | verfügbar | Befunde als Issues (§5) |
| GitHub Actions | 2.000 Minuten/Monat, 500 MB Artefakte (stündlich gemessen) | Verbrauch beobachten, Artefakte sparsam |
| CODEOWNERS | ohne Branch-Schutz wirkungslos | nicht eingesetzt |

## C. Gesamtreview am Rundenende (risikoorientiert)

Ziel: Entspricht das Remake dem Original hinreichend, und enthalten die Änderungen der Runde konkrete Fehler? Nachgeprüft wird nur
das Delta von Fixes aus dem Gesamtreview (nur bei P0/P1, sonst Delta-Prüfung der Leitstelle, `zusammenarbeit.md` §5 Punkt 4).
Prüflauf: `tools/pruefen.sh --schnell` auf dem Head (`AGENTS.md` §5); ohne Originaldaten läuft er wie die CI, dann im Bericht
„Originaldaten nicht geprüft“. Die Leitstelle setzt im Kurzaufruf hinter „Besonders prüfen:“ diesen Absatz ein und
ergänzt die riskantesten Änderungen der Runde:

> Risikoorientiert, keine Vollinventur: Prüfe bevorzugt riskante Änderungen und Übergänge – Laden und Speichern, Rundenablauf,
> Schussphysik, Geld und Einkauf, Computergegner, Bedienung – und die Entscheidungen der Runde. Nutze Originalstände, Bildschirmfotos und
> `BOMB.EXE`/`BOMB.OVR` gezielt dort, wo sie eine relevante Frage klären. Führe die genannten Gegenbeispiele erneut aus. Tests und Mutationen dienen
> der Untersuchung konkreter Risiken oder vermuteter Fehler; keine Mutation jedes Tests und keine Einzelrückführung jedes Sollwerts,
> Fingerabdrucks oder jeder Saat. Melde Testlücken als solche (Kennzeichen „Testlücke“), getrennt von nachgewiesenen Produktfehlern.
> Bekannte `[APPROX]`-Punkte und die erwartet offenen Issues nur bestätigen, nicht als neue Befunde melden. Die Merge-Empfehlung
> richtet sich nach den konkreten Befunden, ihrer Auswirkung und den verbleibenden relevanten Unsicherheiten; nicht Geprüftes nennst
> du kurz, es bedeutet nicht von selbst „Nein“. Höchstens 120 Zeilen; reicht eine Sitzung nicht, Bericht mit Stand ablegen, Fortsetzung
> als neue Datei `<datei>-teil<n>.md` auf neuem Branch (siehe `docs/prozess/anhang.md` D).

Die umfassende Prüfung aller Tests, Sollwerte, Originalfundstellen, Bildschirmzustände und Entscheidungen erfolgt einmal als
gesonderte **Abschlussprüfung**, wenn alles umgesetzt ist (vor v1.0), nicht in den Runden.

## D. Aufrufe für ChatGPT/Codex

Codex klont das Repo beim Start mit dem dort gewählten Branch und legt den PR selbst an. **Netz:** Reviews durch
ChatGPT laufen in Codex **mit Netzzugang** (Vorgabe); nur dann setzt Codex Labels. Hat die Umgebung nach dem Klonen kein Netz
(z. B. `git fetch`/`clone` scheitern mit „Failed to connect to proxy port 8080“), setzt Codex kein Label – die
Leitstelle wartet auf den Review-PR `review/*` und setzt das Label beim Auswerten. Für jeden Kurzaufruf an ChatGPT gilt in beiden Fällen:

1. **Head nur auf dem gewählten Branch:** Der Head-SHA ist der Stand des Branches, den der Nutzer in Codex wählt (meist `main`);
   liegt er auf einem anderen Branch (z. B. Fix-PR), nennt die Leitstelle diesen Branch. Base und ältere Stände liegen in dessen Historie.
2. **Ablage immer neu:** neuer Branch `review/<thema>`, neue Datei, neuer PR; nie einen bestehenden Branch fortschreiben, auch
   nicht bei einer Fortsetzung (dann `…-teil<n>.md`).
3. **Keine Netzbefehle im Aufruf:** keine Vorprüfung mit `git fetch`, `git ls-remote` oder `add_repo`. Originaldaten und Spielstände
   kommen über den GitHub-Connector oder das Setup der Codex-Umgebung; fehlen sie, gehört das als offener Prüfbereich in den Bericht.
4. Der Zugriffsabsatz aus `spur-start.md` gilt nur für Claude-Läufe.

## E. Review ohne vollständigen Klon (zu `review-anfrage.md` Schritt 2)

Fehlen Commit oder Bericht im Checkout und scheitert der Fetch, einen verfügbaren GitHub-Connector nutzen (Compare, Dateien mit
festem Head-Ref, früherer Bericht, PR-Kommentare) und den Checkout für Build und Tests darüber rekonstruieren, soweit möglich. Den
vollständigen Git-Tree einschließlich Dateimodi und gelöschter Pfade gegen den Zielcommit verifizieren: Tree-SHA des Checkouts
(z. B. `git add -A && git write-tree`) gleich Tree-SHA des Zielcommits; Blob-Hashes einzelner Dateien genügen nicht
(`umgebung.md`). Ist das nicht erbracht (nicht möglich, unterlassen, abweichend, nur teilweise), Grenze und blockierte Prüfungen
dokumentieren; Läufe daran sind höchstens Diagnose, im Bericht „unverifizierter Stand“, kein Test des exakten Heads.
Dokumentieren, ob nur Codelesen oder auch Ausführung am nachgewiesenen Checkout erfolgte.

## F. Geplante Läufe – Leitstelle

- **Zugriff vor dem ersten `create_trigger` der Sitzung:** Läufe erhalten nur die Repos, die die **anlegende Sitzung** eingebunden
  hat; `add_repo` steht im Lauf nicht verlässlich
  bereit. Deshalb vorher `leitstelle-start.md` Schritt 0 erledigt (beide Repos `push`, belegt durch das `add_repo`-Ergebnis) und
  `git ls-remote https://github.com/kaaasi/bomb-referenz HEAD` erfolgreich – sonst keinen Lauf anlegen und dem Nutzer
  melden (Fehlermeldung von `add_repo` bzw. `ls-remote`). Jeder Startaufruf enthält den Zugriffsabsatz aus `spur-start.md`.
- **Modell:** nach dem Anlegen mit `update_trigger` setzen, wenn es von Opus abweicht – nur auf ausdrücklichen Wunsch des Nutzers
  (z. B. bestätigter Plan bzw. Go mit Modell) oder für Mechanisches nach `zusammenarbeit.md` §5 „Modellwahl“.
- **Geplante Aufgaben legt nur die Leitstelle an** – Fortsetzungen (Nutzer 11.11.2026) wie Nachprüfungen und alle übrigen (Nutzer
  12.11.2026); Startaufrufe erlauben Läufen kein `create_trigger`.
  Namen geplanter Aufgaben: `Bomb R<runde> <spur> – <Art>` (Nutzer 11.11.2026).
- **`spur-wartet` durchsehen:** „Fortsetzung anlegen – Leitstelle“ heißt, die Aufgabe mit dem Prompt aus der Übergabe anlegen –
  vorher gegen den Kurzaufruf „Geplanter Lauf mit Teilen“ prüfen; abweichen dürfen nur Spur, Modell, Zähler und PR-Nummer.
  Draft-Status, den ein Lauf nicht aufheben konnte, hebt die Leitstelle selbst auf (`…/pulls/<nr>/ccr/ready_for_review`), nicht der
  Nutzer. Wartet ein Punkt auf den Nutzer, entfernt sie `spur-wartet`, sobald sie ihn vorgelegt und das im PR vermerkt hat.
- **Warten über Labels** (`zusammenarbeit.md` §5 Punkt 8): eine Warteschleife fragt **je Label eine Abfrage**
  (`gh api …/issues?labels=<label>&state=open`; mehrere Labels in einer Abfrage sind UND-verknüpft), bei Reviews durch ChatGPT/Codex
  zusätzlich offene PRs mit Head `review/*`; Intervall 60 s. Claude: Schleife im Hintergrund (`run_in_background`), Obergrenze
  rund zwei Stunden (z. B. `for i in $(seq 1 115)`), danach abbrechen und den Lauf prüfen; `send_later` stand nicht verlässlich
  bereit; nach einem Neustart der Arbeitsumgebung Schleifen neu anlegen.
- **Ende eines Laufs:** Läufe melden ihr Ende nicht. Vor jeder Statusauskunft `list_triggers`
  (`last_run.status`/`finished_at`) prüfen, nicht nur den PR; ein Draft-PR heißt nicht „läuft noch“. Bei ChatGPT/Codex-Reviews Titel
  und Head-SHA der Review-Datei vor der Auswertung prüfen.

## G. Claude Code: Git im flachen Klon

- Fetch-Regeln für einzelne Branches (`remote.origin.fetch`) brechen `git fetch` ab, sobald der Branch auf GitHub gelöscht ist
  („couldn't find remote ref“) – dann `git config --unset-all remote.origin.fetch 'refs/heads/<b>:'` (zweites Argument ist ein
  regulärer Ausdruck, ohne führendes `+`; Treffer vorher mit `git config --get-all remote.origin.fetch` prüfen).
- Meldet der Stop-Hinweis „unpushed commits / no remote branch“, obwohl gepusht ist, fehlt nur das Tracking:
  `git config branch.<b>.remote origin`, `branch.<b>.merge refs/heads/<b>` und Fetch-Regel
  `+refs/heads/<b>:refs/remotes/origin/<b>` eintragen, dann `git fetch`.
