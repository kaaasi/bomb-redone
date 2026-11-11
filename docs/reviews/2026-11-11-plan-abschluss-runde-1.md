# Review: Plan Abschluss Runde 1 (PR #80)

Base `f3185f31cc7794152d9177667257d98c2d59f4c3`, Head `64fc70c937d5f4afc7f0d6dfd2d3d58c1bfaa534`; Reviewer Claude (Sonnet 5.5), frische
Sitzung ohne Mitarbeit am PR. Reine Doku-Prüfung: `git diff --stat f3185f3 64fc70c` = nur `docs/arbeitspakete.md` (+26/−1) und
`docs/entscheidungen.md` (+1); kein Build, keine Tests (`tools/pruefen.sh` nicht ausgeführt, da kein Code betroffen).

## 1. Merge-Empfehlung

**Ja** für diesen Head, mit einem Vorbehalt: Punkt (1) der Anfrage ist mangels Vorlage im Repo nur eingeschränkt prüfbar (P3-1).
Der Plan ist übernahmefähig, die Dateigrenzen überschneiden sich nicht, alle offenen P3 sind zugeordnet oder begründet ausgenommen,
die Fundstellen stimmen. Nur Doku-Lesen, kein Testnachweis nach `zusammenarbeit.md` §6 Nr. 4.

## 2. Befunde

- **P3-1 (Verdacht) – „Option C der Vorlage“ nicht nachprüfbar.** Die Optionsvorlage zu GR1-1 liegt weder im Repo
  (`grep -rn "GR1-1" docs` trifft nur die neue Zeile in `docs/entscheidungen.md`) noch in [#78](https://github.com/kaaasi/bomb-redone/issues/78)
  (Text: „Umsetzung oder Änderung der Entscheidung – Vorlage an den Nutzer“, keine Kommentare) noch in Teil 2 des Gesamtreviews
  (nur P2-1, keine Optionsliste). Geprüft ist daher nur, dass die Zeile in sich und zu #78 passt: Sie löst P2-1 durch „Beschreibung
  jetzt, Leser in Runde 2“, also eine „Änderung für später“ im Sinn von #78. Dass der Wortlaut dem Buchstaben C der (nicht
  einsehbaren) Vorlage entspricht, ist Annahme; die Leitstelle möge die Vorlage im PR zitieren.
- **P3-2 – R1-D3 bleibt unverändert, GR1-1 weicht davon ab.** [Zeile R1-D3](https://github.com/kaaasi/bomb-redone/blob/64fc70c937d5f4afc7f0d6dfd2d3d58c1bfaa534/docs/entscheidungen.md#L24)
  sagt „in Runde 1 nur lesen“ für CFG und TWS; die neue [GR1-1-Zeile](https://github.com/kaaasi/bomb-redone/blob/64fc70c937d5f4afc7f0d6dfd2d3d58c1bfaa534/docs/entscheidungen.md#L30)
  verschiebt den TWS-Leser nach Runde 2 und sagt „R1-D3 gilt für `BOMB.CFG` unverändert“. Ein Leser nur von R1-D3 sieht den
  Widerspruch. Vorschlag: in R1-D3 „(TWS: siehe GR1-1)“ ergänzen. Zeilenreihenfolge: GR1-1 steht vor R1-D4 (Tabelle ohnehin
  nicht sortiert, unkritisch).
- **P3-3 – Spur `tws-format` ist nach `zusammenarbeit.md` §2 nicht vollständig.** [Abschnitt](https://github.com/kaaasi/bomb-redone/blob/64fc70c937d5f4afc7f0d6dfd2d3d58c1bfaa534/docs/arbeitspakete.md#L277):
  Branchname fehlt (Lauf `abschluss` nennt `fix/abschluss-runde-1`; Standard `paket/<name>` aus Zeile 4 trägt, aber nicht ausdrücklich),
  Testanforderung/Abhängigkeit zu Originaldaten fehlt (die Analyse braucht `BOMB.OVR`/`BOMB.EXE` und die Listings aus dem
  Referenz-Repo, `re-grundlagen` §9; Spur liest nur). Modell für den Erstreview ist im Abschnittskopf für beide Läufe genannt.
  Die Spur-Dateien enthalten keinen Eintrag in `docs/formate/README.md` (Verzeichnis); ein neuer Verweis auf `spielstand.md` wäre
  dort nötig oder gehört in die Dateigrenzen.
- **P3-4 – Gemeinsame Datei `docs/arbeitspakete.md`.** Beide Läufe tragen Status im „eigenen Statusabschnitt“; die Abschnitte
  `abschluss` und `tws-format` sind aber Unterabschnitte desselben Blocks. Konflikte beim Rebase sind nur bei gleichzeitigem Status
  möglich; unkritisch, da `AGENTS.md` §6 Status im eigenen Abschnitt vorsieht.

Keine P0–P2.

## 3. Geprüft

**Aufruf (2) – Übernahmefähigkeit und Zuordnung.** Offene Issues mit Label `befund`
(`gh api "repos/kaaasi/bomb-redone/issues?state=open&labels=befund"`): #15–#21, #34, #39, #46–#48, #61, #68, #71, #74, #75, #78, #79.
Zuordnung im Plan: #79, #15–#21, #34 (mit Bedingung „nur ohne neues Testprojekt“), #46–#48, #61, #68, #74, #75 → `abschluss`;
#78 → `tws-format`; #39 („braucht DOSBox“, deckt sich mit `re-grundlagen` §8 Nr. 2) und #71 („Runde 2“, passt zu #71: Sortierung der
0x74-Sätze gehört in die Spielfolge) begründet ausgenommen. Keine Lücke. Dateigrenzen: `abschluss` = Dateien der Issues (`bildschirme.md`,
`tools/claude/fetch-reference.sh`, `tools/pruefen.sh`, `src/BombRedone.Desktop/*`, Review-Dateien/Tests laut Issue), `tws-format` =
`docs/formate/spielstand.md`, `tools/reverse-engineering/tws*`; der Plan schließt Dateien von `tws-format` für `abschluss` aus; keine
Schnittmenge außer `docs/arbeitspakete.md` (eigene Abschnitte). Inhalt/Abnahme/Modell/Base nach Abschnittskopf vorhanden; Abnahme
von `abschluss` (roter Test, `Fixes #<nr>`, alle drei Läufe) entspricht `AGENTS.md` §5; `tws-format` „kein Code in `src/`“ vereinbar mit
Nur-Analyse. `#79`-Beschreibung (Crumble 75, Walls None) deckt sich mit R1-D5 = C (Zeile 27, schon vor PR vorhanden).

**Aufruf (3) – Fundstellen.** `docs/analyse/re-grundlagen.md` §6, Tabelle: „Einkauf, Statistik, Sichern, Laden | `O3:0E1F`,
`O3:2508`, `O3:46DE`, `O3:4D97` | `0x098AB`, `0x0AF94`, `0x0D16A`, `0x0D823`“ → `O3:46DE` = Sichern, `O3:4D97` = Laden; der Plan
nennt „Speichern `O3:46DE`, Laden `O3:4D97`“ – stimmt; passt zu `docs/anforderungen/wertung-einkauf.md` Z. 88 (Sichern `O3:46DE`).
Bezüge: 0x74-Byte-Spielersätze (#71, `src/BombRedone.Core/Economy/PlayerAccount.cs`) und `Core/Economy` existieren.

**Erwartet offen:** #39 und #71 sind offen (Liste oben) und im Plan unberührt – bestätigt.

**Nicht ausgeführt:** Build, `tools/pruefen.sh`, Gegenbeispiele (keine genannt). Offene Annahme: Option C (P3-1).
Akzeptierte `[APPROX]`: keine betroffen.
