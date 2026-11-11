# Review PR #9 – `re-grundlagen`

Prüfer: Claude Opus 5.5, frische Sitzung (geplanter Lauf, unbeaufsichtigt), nicht an der Spur beteiligt. Base
`ca689e9590c93292c59aae9e8083eb659b9cfa50`, Head `27d0b0345010100ffe3e60c0039d07c3c3c908c1` (ausgecheckt,
`git rev-parse HEAD` = Head, `git status --porcelain` leer). Originaldateien aus `kaaasi/bomb-referenz` (`7e4bd33`,
Prüfsummen stimmen).

## 1. Merge-Empfehlung

**Nein** für diesen Head – allein wegen P2-1 (wörtliche Byte-Folgen aus `BOMB.EXE` im Skript). Inhaltlich hält die Analyse:
alle Stichproben mit eigenen, vom Skript unabhängigen Proben bestätigt (MZ, FBOV, TP-6-Laufzeit, Zufallsgenerator bitgenau,
Real48, Ports 42h/43h/61h, Delay-Kalibrierung, Init-Reihenfolge, Schriften). Nach Behebung von P2-1 genügt eine Delta-Prüfung
der Leitstelle (nur P2/P3, `zusammenarbeit.md` §5 Nr. 4); alternativ stellt der Nutzer P2-1 ausdrücklich zurück bzw. erlaubt
kurze Signaturen (Eintrag in `docs/entscheidungen.md`), dann **ja**.

## 2. Befunde

- **P2-1 – Ausschnitte der Originale im Code-Repo** ([bombre.py L357–456](https://github.com/kaaasi/bomb-redone/blob/27d0b0345010100ffe3e60c0039d07c3c3c908c1/tools/reverse-engineering/bombre.py#L357-L456)).
  `befunde` vergleicht gegen wörtlich eingebettete Code-Bytes: 4 `bytes.fromhex`-Blöcke (22, 12, 13, 7 Byte; der 22-Byte-Block
  L373 ist die vollständige Prozedur `Random(Word)` `12CE:1675`) und 36 `b"\x…"`-Literale ≥ 3 Byte, dazu der 37-Byte-Block
  der Treiber-/Schriftnamen (L404). Das widerspricht `AGENTS.md` §2.1 („Ausschnitte daraus niemals committen“) und R1-D2 = B
  („im Code-Repo nur eigene Beschreibungen mit Offsets“). Beleg:
  `grep -c 'bytes.fromhex' tools/reverse-engineering/bombre.py` → 4; `grep -oE 'b"(\\x[0-9a-f]{2}){3,}' tools/reverse-engineering/bombre.py | wc -l`
  → 36. Kurze Texte (Menütexte, Dateinamen) sind dagegen nach bisheriger Praxis (`konfiguration`) üblich.
  Vorschlag: Byte-Vergleiche durch SHA-256 des Bereichs (Offset, Länge, Hash) ersetzen; die Bedeutung steht schon in Worten in
  `docs/analyse/re-grundlagen.md`. Oder Nutzerentscheidung „Signaturen bis n Byte erlaubt“.
- **P3-1 – Uneinheitliche Offsets der Schriften:** §4 nennt LITT/EURO/TINY bei `0x08B90`/`0x09F9B`/`0x0C0AE`
  ([re-grundlagen.md L89](https://github.com/kaaasi/bomb-redone/blob/27d0b0345010100ffe3e60c0039d07c3c3c908c1/docs/analyse/re-grundlagen.md#L89)), `befunde` F2–F4 meldet `0x08BE8`/`0x09FF4`/`0x0C0F9`. Beides
  stimmt (Dateianfang `PK\x08\x08` bzw. Kopf `1Ah 80h` + Name, Probe: Bytes an beiden Offsets gelesen), ist aber nicht erklärt.
- **P3-2 – „viermal versetzt“ gilt nur für die Kopfzeile** ([L93–94](https://github.com/kaaasi/bomb-redone/blob/27d0b0345010100ffe3e60c0039d07c3c3c908c1/docs/analyse/re-grundlagen.md#L93-L94)): Zählung der
  Aufrufe `0E26:1F12` (OutTextXY) je Prozedur → `O2:06BD` 4, `O2:07BA` (START) **5**.
- **P3-3 – Totes Byte in G4** ([L425](https://github.com/kaaasi/bomb-redone/blob/27d0b0345010100ffe3e60c0039d07c3c3c908c1/tools/reverse-engineering/bombre.py#L425)): 13-Byte-Literal wird mit `[:12]`
  gekürzt; kosmetisch, entfällt mit P2-1.
- **Hinweis (kein Befund):** `docs/analyse/README.md` ist nach R1-D2 veraltet (im PR schon an die Leitstelle gemeldet).

**Erwartet offen – bestätigt offen:** Liste §8 (1–6); R1-D1 `cycles=3000` (Rechnung nachvollzogen: Kalibrierschleife
`cmp/jnz/loop` = 3 Befehle, Sättigung bei `65535 × 3 / 55 ≈ 3.575` Befehlen/ms, Annahme 1 Zyklus je Befehl bleibt `[APPROX]`);
`[APPROX]` Graph-Einsprungnamen; Inhalt O5–O7/`0129`; LS = PS; Zyklengrenze ≈ 3.580.

## 3. Ausgeführte Prüfungen

- **Umfang:** `git diff --stat ca689e9..27d0b03` → nur `docs/analyse/re-grundlagen.md`, `docs/arbeitspakete.md` (eigener
  Statusabschnitt), `tools/reverse-engineering/{README.md,bombre.py}`; kein Produktcode → `--schnell` genügt. CI auf dem Head:
  `build` success.
- `tools/pruefen.sh --schnell` (SDK 10.0.402, Commit 27d0b03, Ergebnis bestanden):

  | Lauf | bestanden | fehlgeschlagen | übersprungen |
  |---|---:|---:|---:|
  | Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 28 | 0 | 0 |

- `bombre.py befunde` → 39/39, `karte` → 26/26, `--out <scratch> all` → 592 Texte, 27 Listings, keine Fehler.
- **Unabhängige Proben (eigenes Skript, nicht `bombre.py`):**
  - MZ: `lastp` 208, 208 Seiten → Bildende 106.192 = Dateilänge, 1.439 Relokationen, `CS:IP 0000:00FA`, `SS:SP 23C0:2800`;
    keine Packer-Kennung (UPX, LZEXE, PKLITE, LZ91, diet).
  - `BOMB.OVR`: `FBOV`, Längenfeld + 8 = 127.951 = Dateilänge.
  - TP 6: „Portions Copyright (c) 1983,90 Borland“ bei `BOMB.EXE 0x14599`; System-Init `ba d5 14 8e da`.
  - Zufall: `ndisasm` von `0x15A44` (`12CE:16C4`) nachgerechnet und als Python-Emulation gegen
    `seed × 08088405h + 1 mod 2³²` verglichen: 200.000 Zufallsstartwerte, 0 Abweichungen. `Random(n)` (`0x159F5`): ruft den
    Schritt, `xchg ax,dx; div bx; xchg` → oberes Wort `mod n` (n = 0 → 0). `Randomize` (`0x15A7C`): `AH=2Ch, INT 21h`, CX:DX
    nach `DS:3AD6/3AD8`.
  - Real48: im ganzen `BOMB.EXE`-Bild und `BOMB.OVR` keine `9Bh D8h–DFh` (x87 mit WAIT); `INT 34h–3Dh` nur in `Crt` (`0x137CB`)
    und in der nie gerufenen 8087-Fassung von `Random` (`0x15A2B–0x15A3F`, System `16A8`), nicht im Spielcode.
  - Ports: `out 42h` (`0x1390B`, `0x1390F`), `out 43h` (`0x13907`), `in/out 61h` (`0x138FB–0x13918`) liegen alle in `Crt`
    (`0x13620–0x13C3F`); in `BOMB.OVR` keine; kein `mov dx,42h/43h/61h`.
  - Delay: `ndisasm` `11F8:0064` bzw. `:029C` bestätigt Kalibrierung über `0040:006C`, `CX=FFFFh`, `not ax / 55` → `DS:EC44`.
  - Init-Reihenfolge am Einsprung (`0x179A`, 13 Far-Calls bis `push bp` bei `0000:013B`) = Liste §1.
  - Schriften: `O2:06BD`/`07BA` EURO Größe 1, `O2:0909` DefaultFont, `O2:1E7F` SmallFont, `O8:0984` SmallFont senkrecht.
- **Gegenproben (Mutation in Kopien unter Scratch):** `BOMB.EXE 0x15A7A` (Faktor 8405h) Bit gekippt → D1 FAIL;
  `\x02LS` → `\x02PS` in `BOMB.OVR` → H1 und H5 FAIL; „Crumble Percent:“ verändert → `karte` FAIL, Rückgabe 1.
- **Schutzordner:** Standardausgabe `analyse/re-grundlagen/` (`.gitignore:14 /analyse/`), `listings` legt `.tmp` darunter an
  und löscht es; nach allen Läufen `git status --porcelain` leer.
- **Referenz-PR kaaasi/bomb-referenz#7** (offen, nicht gemergt, Head `7d63c99`): 31 Dateien, alle unter `analyse/`
  (`analyse/README.md` + 30 unter `analyse/re-grundlagen/`).

**Akzeptierte `[APPROX]`:** Graph-Einsprungnamen aus Argumentmustern; Inhalt O5–O7 und `0129` (Kandidaten); LS = PS der
Anleitung; Zyklengrenze 3.580 (1 Zyklus je Befehl). **Annahmen des Prüfers:** DOSBox zählt im Kern `normal` etwa einen Zyklus je
Befehl (nicht geprüft, gehört zu §8 Nr. 1).

