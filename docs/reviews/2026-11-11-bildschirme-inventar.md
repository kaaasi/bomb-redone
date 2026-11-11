# Review PR #11 – `bildschirme-inventar`

Prüfer: Claude Sonnet 5.5, frische Sitzung (geplanter Lauf, unbeaufsichtigt). Base `ca689e9590c93292c59aae9e8083eb659b9cfa50`,
Head `a7785c763ac86f509dee875ff8104071f29d5072` (ausgecheckt, `git status --porcelain` leer).
Reine Doku-Prüfung: kein Build, kein `tools/pruefen.sh` (nur `docs/` geändert).

## 1. Merge-Empfehlung

**Ja** für diesen Head. Abnahme der Spur erfüllt: alle Videobilder genau einem Bildschirm oder Übergang zugeordnet, Stichproben
stimmen, Wortlaute und Preise stimmen, keine Bilder im Repo. Es gibt nur P3-Befunde (Lesefehler an Stellen, die das Dokument
selbst teils als „unsicher“ führt); keiner blockiert den Merge, sie lassen sich beim nächsten Anfassen der Datei bereinigen.

## 2. Befunde

Alle Belege: Bildnummern gegen `videos/<kurzname>/<nnnnn>.png` im Referenz-Repo (HEAD `7e4bd33`), Strings gegen
`original/gamedata/BOMB.EXE`/`BOMB.OVR` (`strings -n3`), Text gegen `BOMB.DOC`.

- **P3 – Zeile „Computer Can Buys“ falsch gelesen** (B2b, `bildschirme.md`). LP 00017 zeigt klar „Computer Can Buy:“;
  `BOMB.OVR` enthält denselben String („Computer Can Buy:“). Im Dokument als „so gelesen; … unsicher“ geführt, ist aber lesbar.
- **P3 – Linker Einzelknopf im Hauptmenü ist lesbar:** LP 00012/00016 zeigen „Load Game“ (rötlich, abgeblendet), nicht „ohne lesbare
  Beschriftung (unsicher: Laden)“ (B2 und Abschnitt 6 Nr. 1).
- **P3 – LP 00338 ist kein „leerer oder fast leerer“ Einkauf des Computers** (B6, Zeile in 1.1): das Bild zeigt „Lob & Shoot“ mit
  sechs Einträgen (Incinerator, Mark II Incin., CRI 4/D, Sonic Blaster, Ball 'O' Dirt, Mark II Dirt). Nur LP 00214 ist leer.
- **P3 – „Player 1D“ (B3):** LP 00028 zeigt „Player 10“ lesbar.
- **P3 – Abschnitt 4, Lücke bei 18 Waffen:** Das Dokument nennt als „nicht in der Tafel“ nur Lead Ball und Hand Grenade (15 + 2 ≠ 18).
  `BOMB.EXE` führt 18 Namen: zusätzlich **CRI 20/D** (Strings „Chain Reaction Inducer 20/D“, „CRI 20/D“). Das passt zu „Tafel
  unten abgeschnitten“ (TW 00100 endet mit CRI 20/ND am Bildrand). Außerdem: „nicht käuflich“ für Lead Ball/Hand Grenade ist
  nicht belegt; `BOMB.DOC` („WEAPONS“) nennt alle 18 als „available for purchase“, „the first three are available when the game
  begins“.
- **P3 – „00359 … pixelgleich zu 00358“** (Prüfzeile unter 1.1): nicht nachvollziehbar. 00359 existiert nicht; 00358 ist weder zu
  00357 noch zu 00360 pixelgleich (`PIL ImageChops.difference().getbbox()` ≠ `None`). Satz streichen oder präzisieren.
- **P3 – „Erwartet offen“ (nur bestätigt, Abschnitt 7):** Beim Lesen der Bilder sind TW 00031 („8 Games“) und TW 00094
  („9 Games“) klar lesbar; LP 00428 zeigt unter dem Zeiger „Exit“. Die Einträge in Abschnitt 7 können damit geschlossen werden;
  die übrigen Einträge (Walls-Zwischenwerte, Explosionsfarben, Waffensymbole, „(?)“/„(B)“, fehlender Einkauf nach Spiel 3,
  untere Tafelzeilen) bleiben offen. Hinweis: „Random“ steht in `BOMB.EXE` bei den Computergegnern – „(?)“ also wohl Zufallswahl.

## 3. Ausgeführte Prüfungen

- `git diff --stat ca689e9..a7785c7`: nur `docs/anforderungen/bildschirme.md` (+317) und `docs/arbeitspakete.md` (+6, eigener
  Statusabschnitt). Keine Bilder, keine Binärdateien, keine Originalauszüge.
- **Zuordnung (Skript über beide Tabellen 1.1/1.2):** LP 00000–00439 ohne 00359: 0 Lücken, 0 Doppelungen, 0 Fremdnummern;
  TW 00000–00151: 0 Lücken, 0 Doppelungen. Dateien im Referenz-Repo: 439 bzw. 152, einzig 00359 fehlt – stimmt mit dem Dokument
  (591 Bilder).
- **Stichproben gegen die Bilder** (Montage ausgewertet): LP 00008/9/12/17/25/28/37/38/39/41/63/64/67/68/72/73/105/106/114/115/
  140–143/172/214/215/338/339/383–385/393/394/426/427/428/431/432; TW 00005/30–32/37/38/93–95/100–102/149–151. Zuordnung,
  Übergangsbilder, Kopfzeile, Wertungstexte („1 Game Played out of 10“, Add/Continue/Save bzw. Add/Exit/Save, Punkte 4260),
  Einkauf-Kopf („YOU HAVE UNALLOCATED:“, „5260 DOLLARS“), Optionslisten (Zeilenanzahl 10) und Computerliste (8 Zeilen)
  stimmen bis auf die oben genannten Punkte.
- **Waffenpreise TW 00100 (alle 16 Zeilen):** stimmen mit dem Bild überein; Namen stimmen mit den Kurznamen in `BOMB.EXE`.
- **Abgleich `BOMB.DOC`:** Standardwerte (Crumble 50 %, Walls Random, Games 10, Players 2, Wind Normal), sieben Computer,
  „OK, ADD, and SAVE“, „18 weapons, 2 guidance systems, 3 defenses“, Zeiten „Laser 1000“ stimmen mit Abschnitt 5 des Dokuments.
  `BOMB.EXE`/`.OVR` stützen Optionsnamen („Effect:“, „Flying:“, „Crumble Percent:“, „Cycle Delay:“ …) – nur Zusatzprobe.
- Nicht ausgeführt: Build, Tests, CI-Prüfung (reine Doku). **Akzeptierte `[APPROX]`:** keine (kein Code). Annahme: Zuordnungen
  außerhalb der Stichproben sind nicht einzeln geprüft.
