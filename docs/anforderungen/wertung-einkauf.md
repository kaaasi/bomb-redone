# Wertung, Endergebnis und Einkauf (Spur `wertung-einkauf`)

Stand 11.11.2026, Claude Opus 5.5. Fundstellen: `Seg:Ofs`/`On:Ofs` wie in `docs/analyse/re-grundlagen.md`; Datensegment
`DS:0000` = `BOMB.EXE 0x163F0`.

## 1. Waffen- und Ausrüstungstabelle (`Core/Economy/ItemCatalog`)

Je Gruppe vier parallele Felder im Datensegment (typisierte Konstanten): Langname und Kurzname (`string[255]`, je 256 Byte),
Preis (LongInt), Bündelmenge (Word). Der Einkauf `O3:0E1F` liest genau diese Felder.

| Gruppe | Anzahl | Langname | Kurzname | Preis | Menge |
|---|---:|---|---|---|---|
| Waffen | 18 | `DS:087C` (`0x16C6C`) | `DS:1A7C` (`0x17E6C`) | `DS:2C7C` (`0x1906C`) | `DS:2CC4` (`0x190B4`) |
| Lenksysteme | 2 | `DS:2CE8` (`0x190D8`) | `DS:2EE8` (`0x192D8`) | `DS:30E8` (`0x194D8`) | `DS:30F0` (`0x194E0`) |
| Abwehr | 3 | `DS:30F4` (`0x194E4`) | `DS:33F4` (`0x197E4`) | `DS:36F4` (`0x19AE4`) | `DS:3700` (`0x19AF0`) |

Werte (Test `ItemCatalogOriginalTests` vergleicht die eingebaute Tabelle mit `BOMB.EXE`): 18 + 2 + 3 Einträge wie in `BOMB.DOC`
„WEAPONS“. Alle 16 Einträge der Videotafel (`bildschirme.md` §4, TW 00100) stimmen in Menge und Preis überein.

**Bestätigung zu #19 (R11-05):** CRI 20/D steht in der Tabelle (`DS:247C`, 1 für 15000). In TW 00100 fehlt er, weil der
Spieler 12930 Dollar hatte und der Einkauf nur Bezahlbares oder Vorhandenes zeigt (`O3:01CB`). Lead Ball und Hand Grenade
haben Menge 0 und Preis −1; der Einkauf überspringt jeden Eintrag mit Menge 0 (`O3:020B`, `BOMB.OVR 0x08C97`) – „nicht
käuflich“ ist damit belegt.

## 2. Spielerdaten (`Core/Economy/PlayerAccount`)

Spielersätze zu 0x74 Byte ab `DS:402E` (Satz 0 = Tauschpuffer, `O3:0000`). Benutzt: `+13h` Punkte (LongInt), `+17h`
ausgegebenes Geld (LongInt), `+2Ah` Lenksysteme (Word[2]), `+2Eh` Waffen (Word[18]), `+52h` Abwehr (LongInt[3]).
**Geld = Punkte − ausgegeben** (`O3:0F65`; `BOMB.DOC` „Money is directly related to score“).

## 3. Einkauf – Regeln (`Core/Economy/Shop`)

- **Wer kommt in den Einkauf** (`O3:0E3D..0F90`): Geld ≥ billigster käuflicher Preis (3000). Computer zusätzlich nur mit
  „Computers Can Buy“ und Entscheidung `U129:018D` – nicht Teil dieser Spur.
- **Angebot** (`O3:015B`): je Gruppe in Tabellenreihenfolge jeder Eintrag mit Menge ≠ 0, der vorrätig (> 0) oder mit
  Punkte − ausgegeben bezahlbar ist.
- **+** (`+`, `=`, Leertaste, rechte Maustaste; `O3:1CF1`): höchstens 9 Bündel, nur wenn der Preis ≤ nicht zugeteiltes Geld.
  **−** (`-`, linke Maustaste; `O3:1B33`): nur abwählen, kein Verkauf.
- **Purchase** (`P`, Enter; `O3:2080`): ausgegeben += Preis × Anzahl, Bestand += Menge × Anzahl (Waffen/Lenksysteme 16 Bit,
  Abwehr 32 Bit), danach neues Angebot. **Done** (`D`, Esc): kauft ebenso und schreibt erst jetzt `+17h` zurück (`O3:23C5`).
