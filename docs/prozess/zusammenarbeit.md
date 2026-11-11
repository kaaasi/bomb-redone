# Zusammenarbeit mehrerer Agenten

Gilt für alle Agenten und ergänzt [AGENTS.md](../../AGENTS.md). Jeder geeignete Agent kann ein Paket, ein Review oder – nach
Freigabe des Nutzers – die Leitstelle übernehmen, **nur anhand dessen, was in GitHub steht**; kein Schritt setzt einen Chatverlauf
voraus. Sonderfälle (Vertretung, GitHub Free, Gesamtreview, Codex, geplante Läufe der Leitstelle): [anhang.md](anhang.md).

## 1. Rollen

| Rolle | Aufgaben | Darf nicht |
|---|---|---|
| **Nutzer** (Projektinhaber) | Ziele, Freigaben, Ernennung der Leitstelle, letzte Entscheidung bei Uneinigkeit | – |
| **Leitstelle** (vom Nutzer ernannt) | Pakete schneiden und verteilen, integrieren, Merge-Prüfung, Review-Anfragen, Plan pflegen | eigene Umsetzungen (auch Integrations-Commits) ohne unabhängiges Review mergen |
| **Implementierer** | ein Paket auf **einem** Branch umsetzen, PR mit Pflichtangaben | eigenen PR freigeben oder mergen; fremde Branches ohne Übergabe ändern |
| **Reviewer** (anderer Agent oder frische Sitzung ohne Implementierungskontext) | unabhängiges Review mit festen SHAs, Datei in `docs/reviews/` | Code oder den geprüften PR ändern (außer Label `review-ja`/`review-nein`) |

**Aktuelle Leitstelle:** Claude Code (seit 11.11.2026, Runde 0), je Runde eine frische Sitzung; Stand nur in GitHub
(`docs/arbeitspakete.md`, PRs, Labels). **Implementierer:** Claude-Sitzungen. **Standard-Reviewer:** ChatGPT – und nur dafür
(Nutzer 11.11.2026). Einen Wechsel entscheidet der Nutzer; er wird hier per PR eingetragen (Datum, neue Leitstelle, Grund).

**Go des Nutzers:** Die Leitstelle plant und legt Startaufrufe vor; Spuren (auch geplante Läufe) starten erst nach dem
ausdrücklichen Go des Nutzers (`AGENTS.md` §2.8). Fortsetzungen, Fixläufe und Nachprüfungen einer freigegebenen Spur brauchen
kein neues Go. **Entscheidungen** legt die Leitstelle gesammelt vor (`AGENTS.md` §2.9).

## 2. Arbeitspaket übernehmen oder beginnen

Übernahmefähig ist ein Paket, wenn in `docs/arbeitspakete.md` oder im Draft-PR stehen: Ziel und Befundnummern, Abnahme mit
Regressionstest; Branch, Base-SHA, Dateigrenzen, Abhängigkeiten; Testanforderungen (Originaldaten); Stand
(Draft-PR, letzter Head-SHA, Bearbeiter). Ablauf: `docs/vorlagen/spur-start.md`. Dabei gilt: **Bearbeiter ohne
Übergabekommentar → nicht anfangen**, den Nutzer fragen (geplante Läufe: `spur-start.md` „Teile und Fortsetzung“ Schritt 1).
Wer übernimmt, vergleicht `git log -1` mit dem Head-SHA der letzten Übergabe und arbeitet erst bei gleichem Stand und grünem
`tools/pruefen.sh --schnell` weiter. Neu: Branch von aktuellem `main`, sofort **Draft-PR** (Titel `Spur <name>: …`,
`Bearbeiter: <Agent>`, Base-SHA) – er ist die Sperre für den Branch. Fertig: Beschreibung nach Vorlage, „Ready for review“,
Leitstelle informieren.

## 3. Übergabe zwischen Agenten

**Jede Arbeitssitzung endet mit einem Übergabekommentar** im PR nach [docs/handoffs/VORLAGE.md](../handoffs/VORLAGE.md) –
nach erledigter Arbeit (Teil fertig, Befunde abgearbeitet, Warten auf Review) wie bei Abbruch (Kontingent, Sitzungsende,
Umverteilung); bei absehbarem Abbruch **vorher**. Alles ist gepusht (keine lokalen Änderungen), der Kommentar nennt den
**Head-SHA**, erledigt/offen/nächsten Schritt, Testtabelle, Risiken und neue `[APPROX]` und `Bearbeiter: frei` (oder den
genannten Nachfolger). **Höchstens 15 Zeilen plus Testtabelle**; was in PR oder Review steht, wird verlinkt.

Jede Spur hat genau einen PR; die Übergabe steht dort – als Kommentar, weil er nicht mit Branches kollidiert. Ohne
PR (z. B. reine Analyse): `docs/handoffs/<datum>-<thema>.md` auf eigenem Branch. Abbruch ohne Kommentar: Es gilt der letzte
gepushte Stand; der Nachfolger schreibt zuerst einen Übernahmekommentar („übernommen ab SHA …, Tests …“).

## 4. Parallele Arbeit ohne Überschreiben

- **Ein Branch – ein Bearbeiter.** Gleichzeitig am selben Paket: getrennte Branches `paket/<spur>-<teil>` mit getrennten PRs.
  Fortsetzungen einer Spur arbeiten **nacheinander** auf `paket/<spur>` mit einem PR (`spur-start.md` „Teile und Fortsetzung“).
- Kein `git push --force` auf fremde Branches; auf eigenen nur `--force-with-lease` nach Rebase.
- Dateigrenzen einhalten; Berührungen gemeinsamer Dateien im PR begründen.
- `docs/arbeitspakete.md` nur im eigenen Statusabschnitt; Planabschnitte ändert die Leitstelle.

## 5. Unabhängiges Review

- Die Leitstelle gibt die Anfrage als ausgefüllten **Kurzaufruf** aus `docs/vorlagen/review-anfrage.md` (PR, Base- und Head-SHA,
  Gegenbeispiele, erwartet offen, Ablage); der Ablauf steht in der Vorlage.
- Reviewer ist nie die umsetzende Sitzung; hat derselbe Agent umgesetzt, prüft eine **neue** Sitzung ohne diesen Verlauf.
- Ergebnis: `docs/reviews/JJJJ-MM-TT-<thema>.md` auf `review/<thema>`, PR nur mit dieser Datei; ohne Schreibzugriff Datei an den
  Nutzer, die Leitstelle legt ab.
- Ändert sich der Head nach dem Review, ist eine **Nachprüfung** des Deltas nötig (`…-nachpruefung.md`) – außer in den Fällen
  von Punkt 4 unten (nur P2/P3; Rebase ohne Konflikt samt vom Plan verlangter Anpassung, §6 Nr. 1).
- **Sparsame Kette:**
  1. **Erstreview** (Spur- oder Integrations-PR): vollständig (Claude: Opus 5.5) – alle drei Läufe von `tools/pruefen.sh`,
     Mutationen, Pixelvergleich, Fehlerfälle/Fuzzing, Fundstellen in `BOMB.EXE`/`BOMB.OVR`.
  2. **Fixlauf:** `tools/pruefen.sh --schnell` (den Lauf ohne Daten übernimmt die CI).
  3. **Nachprüfung** nur, wenn der Fixlauf **P0/P1** behebt: nur Delta, Gegenbeispiele und Rückmutationen dieser Befunde (keine
     erneute Vollprüfung, kein Pixelvergleich außerhalb der Befunde).
  4. **Delta-Prüfung der Leitstelle** statt Nachprüfung, wenn das Delta **nur P2/P3** behebt oder nur Rebase ohne Konflikt samt
     vom Plan verlangter Anpassung ist (`git range-diff`): Delta (`compare`/`--stat`, betroffene Dateien), CI auf dem Head,
     Testtabelle des Fixlaufs; Vermerk im Merge-Kommentar („Delta geprüft durch Leitstelle, nur P2/P3“ bzw. „… nur Rebase“).
  5. **P3** bekommt keinen eigenen Lauf: Issue mit Label `P3`, gesammelt an den letzten Lauf der Runde.
     Ein ohnehin laufender Fixlauf darf P3 mitnehmen.
  6. **Gesamtreview** am Rundenende (ChatGPT): letztes Netz, risikoorientiert C); die
     Vollprüfung erfolgt einmal als Abschlussprüfung, wenn alles umgesetzt ist.
  7. **Befund nur mit Beleg:** Wer eine Lesart von `BOMB.EXE`/`BOMB.OVR`, einen Originalwert oder „fehlt im Remake“
     behauptet, nennt einen **ausführbaren Beleg** (roter Test oder Probe: Befehl, Eingabedaten, Ergebnis; bei „fehlt“ zusätzlich
     die erfolglose Codesuche). Ohne Beleg höchstens P3 „Verdacht“. Widerspricht ein Fixlauf mit eigenem Beleg: Label
     `beleg-strittig`, **keine** Nachprüfung für diesen Punkt; die Leitstelle führt beide Belege aus und entscheidet, nur wenn das
     nicht gelingt, gibt es eine Nachprüfung allein dieses Punkts.
  8. **Verkettung und Labels:** Der Fixlauf legt bei behobenem P0/P1 selbst die Nachprüfung als geplanten
     Lauf an (Kurzaufruf aus `review-anfrage.md`, Modell nach „Modellwahl“, etwa 5 Minuten später, vorher `list_triggers` auf
     eine ausstehende Nachprüfung dieses PRs). Lehnt die Freigabe `create_trigger` ab: `spur-wartet`, Prompt im
     Übergabekommentar (wie bei Fortsetzungen). Die Leitstelle wartet auf Labels bzw. Review-PRs, nicht auf Kommentare, legt
     nach `review-nein` Fixläufe an und merged nach §6. **Wer ein Label abarbeitet, entfernt es:**

     | Label | gesetzt von, wann | entfernt von, wann |
     |---|---|---|
     | `review-ja` / `review-nein` | jedes Review am geprüften PR (das andere entfernen) | gegenseitig durch das nächste Review; `review-nein` der Fixlauf mit dem Push des neuen Heads |
     | `leitstelle-delta` | Fixlauf, der nur P2/P3 behebt, und Fixlauf ohne Befund (nur Rebase/Planpflicht) | Leitstelle nach der Delta-Prüfung |
     | `beleg-strittig` | Fixlauf, der einem belegten Befund mit eigenem Beleg widerspricht | Leitstelle nach ihrer Entscheidung |
     | `spur-wartet` | Lauf ohne Fortsetzung (Zugriff oder Teil blockiert, Kettenende, Freigabe `create_trigger` abgelehnt, laufender Fremdlauf) | Leitstelle nach Anlegen des Laufs bzw. Entscheidung; wartet der Punkt auf den Nutzer, sobald sie ihn dem Nutzer vorgelegt und das im PR vermerkt hat |

     Reviews ohne Netz (ChatGPT/Codex) setzen kein Label; dann wartet die Leitstelle auf den Review-PR (`review/*`) und setzt das
     Label beim Auswerten ([anhang.md](anhang.md) D).
  9. **Länge:** Review-Bericht höchstens 120 Zeilen (Gesamtreview: je Teil), nur Urteil, Befunde mit Beleg,
     Prüftabelle; keine Nacherzählung. PR-Beschreibungen ebenso knapp.

  **Modellwahl:** Claude Opus 5.5 überall, wo es Nacharbeit vermeidet – Spielregeln, Spielkern, Disassembly,
  Dateiformate, Erstreviews und Nachprüfungen von Code. Claude Sonnet 5.5 nur für Mechanisches: Integrationen ohne eigene Regeländerung,
  reine Doku-Prüfungen (auch Nachprüfungen reiner Doku-PRs), Kommentare, einfache Testergänzungen. Im Zweifel Opus. Die
  Leitstelle setzt das Modell ([anhang.md](anhang.md) F).
- **Befunde als Issues**: je Befund ein Issue `<ID>: <Kurzbeschreibung>`, Labels `befund` und `P0`–`P3`, Link
  auf die Review-Datei; der behebende PR schließt es (`Fixes #<nr>`). Pakete und Status bleiben in `docs/arbeitspakete.md`. Ohne
  Zugriff auf Issues Befunde im PR nennen; die Leitstelle legt die Issues an.

## 6. Merge-Prüfung (vor jedem Merge, von Leitstelle oder Nutzer)

GitHub Free erzwingt nichts davon ([anhang.md](anhang.md) B); die Liste ist verbindlich und wird im Merge-Kommentar abgehakt:

1. [ ] **Head-SHA** = vom Review freigegebener SHA (sonst Nachprüfung; Ausnahmen mit Delta-Prüfung der Leitstelle nach §5 Punkt 4:
   Delta behebt nur P2/P3, oder nur Rebase ohne Konflikt samt vom Plan verlangter Anpassung – `git range-diff` zeigt nichts anderes).
2. [ ] **Base aktuell:** gegen aktuellen `main` gebaut bzw. ohne Konflikt; ist `main` seit dem Review neu: Integrationstest lokal
   wiederholen (`--schnell` mit Originaldaten auf `main` + PR, Tabelle im Merge-Kommentar; `leitstelle-start.md` Schritt 2).
3. [ ] **CI grün auf genau diesem Head** (Check-Runs des Head-SHA). Nur `docs/**`/`*.md` geändert: CI startet nicht
   (`build.yml`, `paths-ignore`) → „entfällt, nur Doku“ nach Bestätigung per `compare`/`--stat`.
4. [ ] **Originaldaten-Nachweis:** Testtabelle mit Originaldaten zum Head (PR oder Review); bei Integrations-PRs alle drei Läufe
   einmal (Integration oder Erstreview); nach einem Fixlauf genügt dessen `--schnell`-Tabelle mit grüner CI auf dem Head. Ein
   Review „nur Codelesen“ oder an unverifiziertem Stand (`review-anfrage.md` Schritt 2) zählt nicht.
5. [ ] **Review-Freigabe** als Datei in `docs/reviews/`; offene P0/P1/P2 behoben oder vom Nutzer ausdrücklich zurückgestellt.
6. [ ] Kein Draft; Pflichtangaben vollständig; Restlücken in `docs/arbeitspakete.md` übertragen.
7. [ ] Merge **mit SHA-Bindung**: `PUT /repos/kaaasi/bomb-redone/pulls/<nr>/merge` mit `sha=<freigegebener Head>`
   (GitHub lehnt bei geändertem Head ab). In der Weboberfläche: Head-SHA unmittelbar vor dem Klick vergleichen.
8. [ ] Danach: CI auf `main` prüfen; Review-PR mergen; Status in `docs/arbeitspakete.md` nachziehen.

## 7. Vertretung der Leitstelle → [anhang.md](anhang.md) A

## 8. GitHub Free → [anhang.md](anhang.md) B

Alle Agenten schreiben über dasselbe GitHub-Konto; wer was gemacht hat, zeigen nur Commit-Trailer, `Bearbeiter:` und
Übergabekommentare – deshalb Pflicht.
