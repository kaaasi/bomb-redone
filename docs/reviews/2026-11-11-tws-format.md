# Review PR #82 – `tws-format`

Prüfer: Claude Opus 5.5, frische Sitzung ohne Implementierungskontext (geplanter Lauf, unbeaufsichtigt). Base
`35d8b420a4d9d4e17785f1a9b010ef9c5ca85e9b`, Head `326485d85b5fe63c65a49ad9aa9efa64765e7a78` (ausgecheckt, `git rev-parse HEAD`
= Head, `git status --porcelain` leer). Originaldaten aus `kaaasi/bomb-referenz` (`fetch-reference.sh`, Prüfsummen stimmen),
`ndisasm` 2.16.

## 1. Merge-Empfehlung

**Ja** für diesen Head. Reiner Analyse-PR: `git diff --stat 35d8b42 326485d` zeigt nur `docs/formate/spielstand.md`,
`docs/formate/README.md` (1 Verweiszeile), `tools/reverse-engineering/tws.py` und `### Status tws-format` in
`docs/arbeitspakete.md` – kein Produktcode. Der Aufbau von `*.TWS` stimmt in allen nachgeschlagenen Punkten mit eigener
Disassembly von `O3:3B54`, `O3:3FE4`, `O3:46DE` und `O3:4D97` überein; `befunde` 29/29, `selbsttest` bestanden, alle
Gegenproben in den beiden Dialogroutinen schlagen wie erwartet fehl. Keine Originalbytes im Repo. Offen sind nur P3.

## 2. Befunde

- **P3 – T6b belegt nicht alles, was er behauptet**
  ([tws.py#L390-L395](https://github.com/kaaasi/bomb-redone/blob/326485d85b5fe63c65a49ad9aa9efa64765e7a78/tools/reverse-engineering/tws.py#L390-L395)).
  Geprüft werden nur die fünf Tastenvergleiche und die Texte `GAME`/`SAVE GAME`, nicht dass **S** und **Q** `O3:3B54` rufen und
  nicht „Name String[20]“. Gegenproben (Kopie von `BOMB.OVR`, `tws.py --gamedata <kopie> befunde`):
  `O3:4CC7` (Ziel von `call 0x3b54` bei S, Datei `0x0D753`) `8B→8C` → **29/29 OK**; `O3:476F` (`mov ax,0x14` vor
  `StrAssign` nach `DS:4822`, Datei `0x0D1FB`) `14→15` → **29/29 OK**. Die Aussagen selbst stimmen (eigene Disassembly:
  `O3:4CBA cmp [bp-2],1 → call 0x3b54`; `O3:4CC9 cmp [bp-2],5 → call 0x3b54`, `call 0x11f8:0x16b`, `Halt(0)`;
  `O3:476E mov ax,0x14`). Analog T6d: „Namen String[10]“ ist nur als Menge `{mov ax,0xa, mov ax,0xb}` geprüft –
  `O3:4E2D` `0A→09` → 29/29 OK (mehrfach vorhanden). Vorschlag: Ziel der Sichern-Aufrufe und Länge an fester Stelle prüfen.
- **P3 – Widerspruch Boss-Taste / „immer zwischen zwei Spielen“**
  ([spielstand.md#L146](https://github.com/kaaasi/bomb-redone/blob/326485d85b5fe63c65a49ad9aa9efa64765e7a78/docs/formate/spielstand.md#L146)
  gegen [#L174](https://github.com/kaaasi/bomb-redone/blob/326485d85b5fe63c65a49ad9aa9efa64765e7a78/docs/formate/spielstand.md#L174)).
  Die Boss-Taste sichert laut T6c und Aufnahmeliste Nr. 7 jederzeit, also auch mitten in einer Runde. Satz einschränken
  („Dialog und Autosave: zwischen zwei Spielen“).
- **P3 – Aufnahmeliste Nr. 7 nennt die Bedienung nicht.** Laut `BOMB.DOC` (Abschnitt „Boss Key“): **F10** zeigt eine
  Schein-DOS-Eingabe, erst **ESC** beendet und sichert als `BOSS`; Leertaste kehrt ohne Sichern zurück. Ergänzen, sonst
  entsteht bei der Aufnahme kein Stand. Wo `BOSS.TWS` landet („Herkunft des Pfads nicht untersucht“), nach der Aufnahme im
  DOSBox-Verzeichnis nachsehen.
- **P3 – `__pycache__` in `tools/reverse-engineering/`** (nicht neu, `bombre.py` aus früherem PR): jeder Lauf legt
  `tools/reverse-engineering/__pycache__/` an, `git check-ignore` → nicht ignoriert. Kein Original, aber Gefahr eines
  versehentlichen Commits; `.gitignore` später ergänzen oder `PYTHONDONTWRITEBYTECODE`. Gehört nicht in diesen PR.

Erwartet offen – bestätigt: unbelegte Feldbedeutungen (`DS:47D8`, `DS:47E8`, Intelligenz +0Bh/+0Eh, Satzfelder; im Dokument als
„unbelegt“ markiert), Fortsetzung nach dem Laden (`O3:7847`, als unbelegt markiert), #71 (Tausch von `DS:E5A6`/`DS:3BE8` in
`O3:0000` offen), kein Originalstand im Referenz-Repo (Liste im Dokument); PR verweist korrekt mit `Refs #78`, nicht `Fixes`.

## 3. Ausgeführte Prüfungen

- `python3 -I tools/reverse-engineering/tws.py befunde` → **29 von 29 bestätigt**; `… selbsttest` → 890 / 1015 / 1904 Byte,
  Kurz- und Kopffehler erkannt, **bestanden**.
- **Gegenproben** (je ein Byte in einer Kopie von `BOMB.OVR`, Datei = O3-Offset + `0x8A8C`, `--out` im Scratch-Ordner):

  | Stelle | Änderung | Ergebnis |
  |---|---|---|
  | `O3:4AC6` Taste Q (`cmp [47EF],51h`) | `51→52` | T6b FAIL |
  | `O3:4AF6` Taste S | `53→54` | T6b FAIL |
  | `O3:4AEA` Taste N | `4E→4D` | T6b FAIL |
  | `O3:465F` Text „SAVE GAME“ | `45→58` | T6b FAIL |
  | `O3:543D` `call 0x3fe4` im Ladedialog | `A5→A6` | T6d FAIL |
  | `O3:4FF5` `Halt` (`call 12CE:00E9`) | `E9→EA` | T6d FAIL |
  | `O3:4D4D` Maske `*.TWS` | `2A→2B` | T6d FAIL |
  | `O3:4D58` „No Save Files Found“ | `53→58` | T6d FAIL |
  | `O3:3C81` Größe `DS:47D8` (2→3) | `02→03` | T2b FAIL |
  | `O3:3F7C` Größe Teamtabelle `0x247` | `47→48` | T2b FAIL |
  | `O3:4CC7`, `O3:476F`, `O3:4E2D` | s. Befund 1 | 29/29 OK (Lücke) |

- **Eigene Disassembly (Stichprobe, `ndisasm -b16`):** `O3:3B54`: `Assign(…'.TWS')` `O3:3B90`, `Rewrite(f,1)` `O3:3B9F`;
  Kopf `BlockWrite` ab `[bp-287h]` mit Länge aus dem Längenbyte (`O3:3C5F–3C70`); dann 26 `BlockWrite` in genau der Reihenfolge
  und Größe der Tabelle (`47D8…47EC` je 2, `47EE/4812/4814/4815` je 1, `E8A2…E8AE` je 2, `E8B2…E8BC` je 1); Schleife
  `1..[47DA]`: `74h·i + 402Eh` (74h Byte), `E5A6h + 2i` (2), bei `=3` `U129:00C6` mit `[3BE8h + 4i]`; Teamtabelle `4838h`,
  `247h` = 583 Byte; `Close`. Alle Ergebnisvariablen nil, kein `IOResult`. `O3:3FE4`: `Reset` `O3:402F`, 27 `BlockRead` bis
  `O3:443F`, dann `3BE8h` mit `2Ch` leeren, Schleife mit 74h/`E5A6h`, Art in lokale Variable (`O3:44D7`), Konstruktoren
  `U129:050A`, `06A8` … – stimmt mit T2/T3. Längenformel nachgerechnet: 30 + 41 + 583 = 654; 116 + 2 = 118 je Spieler.
- **Originalschutz:** `python3 .github/scripts/originalschutz_pruefen.py` → „Keine Originaldateien eingecheckt.“ Im Skript nur
  Offsets, Befehlstexte und kurze Texte (Kopf, `.TWS`, Dialogtexte) als Erwartungswerte – zulässig nach
  `tools/reverse-engineering/README.md` (kurze Texte). Schreibzugriffe im Skript nur Temporärdateien in `--out`
  (Standard `analyse/tws/`, Schutzordner); `lesen` gibt nur aus.
- **Aufnahmeliste** (Abschnitt „Benötigte Originalstände“): mit `docs/vorlagen/dosbox-aufnahme.md` und der Bedienung aus
  `BOMB.DOC` (SAVE in der Wertung, AutoSave, LOAD im Hauptmenü) ausführbar; nur Nr. 7 braucht die Ergänzung aus Befund 3.
- `tools/pruefen.sh --schnell` (laut Auftrag genügt `--schnell`, kein Produktcode):

  | Lauf | bestanden | fehlgeschlagen | übersprungen |
  |---|---:|---:|---:|
  | Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 621 | 0 | 0 |

  Commit `326485d…`, SDK 10.0.402, Ergebnis: bestanden. Für den PR-Head meldet GitHub keine Check-Runs (`get_check_runs` → 0);
  vor dem Merge den CI-Stand prüfen.

**Akzeptierte `[APPROX]`:** nur der Vorschlag für den Leser in Runde 2 (bis ein Originalstand vorliegt).
**Annahmen (nicht geprüft):** Bedeutung der Tastenwerte `[bp-2]` 2–4 (N, C, A) nur bis zur Zuweisung verfolgt; `O3:0000`
(#71) und `O3:7847` nicht disassembliert.
