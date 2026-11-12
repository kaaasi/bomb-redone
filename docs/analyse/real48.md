# Real48 und Zufall der TP-6-Laufzeit (Spur `real48`)

Grundlage: [re-grundlagen.md](re-grundlagen.md) §3 (E1, E2, D1–D5), Entscheidung R2-D2 = A. Alle Stellen im System-Segment
`12CE` (`BOMB.EXE 0x14380` + Offset). Umsetzung: `src/BombRedone.Core/Numerics/`.

## Probe

`tools/reverse-engineering/real48.py` lädt das Ladebild von `BOMB.EXE` in einen kleinen 8086-Emulator (nur die Befehle dieser
Routinen) und ruft die Einsprünge unten wie das Spiel auf (Register, `CALL FAR`, Ende bei `RETF` oder beim Sprung nach
`12CE:00E2` = RunError mit Code in AX). Keine Bytes der Originale im Skript; `befunde` prüft die Bereiche
`12CE:0EAE–1201` (Real48) und `12CE:1675–1709` (Zufall mit `Randomize`) über SHA-256.

| Aufruf | Ergebnis |
|---|---|
| `python3 tools/reverse-engineering/real48.py befunde` | Prüfsummen der Routinenbereiche |
| `… hashes --count 20000` | SHA-256 je Operation über erzeugte Fälle (Sollwerte von `Real48ProbeTests`) |
| `… cases --count 1000` | alle Fälle als TSV nach `analyse/real48/` (Schutzordner) |
| `… eval mul 820000000000 830000000000` | ein Fall; Reals als 12 Hex-Ziffern in Speicherreihenfolge |

Der Fallgenerator (splitmix64, je Operation fester Startwert; Mischung aus Zufallsbits, Sonderexponenten 0/1/2/7F–82/9E–A1/FE/FF,
nahen Exponenten, kurzen Mantissen und Nullen mit Restbits) steht gleich in `tests/BombRedone.Core.Tests/Numerics/Real48ProbeCases.cs`.

## Darstellung

6 Byte: Byte 0 Exponent (Bias 129, 0 = Null unabhängig von den Mantissenbits), Byte 1–5 Mantisse little endian ohne
verstecktes Bit, Bit 7 von Byte 5 Vorzeichen. In Registern DX:BX:AX (AL Exponent, AH unterstes Mantissenbyte), zweiter
Operand CX:SI:DI.

## Einsprünge und Verhalten

| Einsprung | Datei | Routine | Operation | Aufrufe EXE/OVR |
|---|---|---|---|---:|
| `11B6` | 0x15536 | `0EB2` | Add DX:BX:AX + CX:SI:DI, Fehler 205 | 11/60 |
| `11BC` | 0x1553C | `0EAE` | Sub: Vorzeichen von DI umdrehen, weiter wie Add | 8/63 |
| `11C2` | 0x15542 | `0F75` | Sqr: CX:SI:DI := DX:BX:AX, dann Mul | 2/0 |
| `11C8` | 0x15548 | `0F75` | Mul, Fehler 205 | 39/84 |
| `11CE` | 0x1554E | `107A` | Div DX:BX:AX / CX:SI:DI; Exponent CL = 0 → Fehler 200, sonst 205 | 31/56 |
| `11D8` | 0x15558 | `10F1` | Cmp, Ergebnis in CF/ZF | 10/63 |
| `11DC` | 0x1555C | `111B` | LongInt DX:AX → Real | 84/164 |
| `11E0` | 0x15560 | `115A` (CH=0) | Trunc → LongInt DX:AX, Fehler 207 | 36/90 |
| `11E8` | 0x15568 | `115A` (CH=1) | Round, Fehler 207 | 0/0 |
| `1675` | 0x159F5 | `16C4` | Random(n), n als Wort auf dem Stapel | 15/126 |
| `168B` | 0x15A0B | `16C4` | Random als Real | 1/18 |

Aufrufe: Muster `CALL FAR 12CE:xxxx` in beiden Dateien. Round und Sqr kommen im Spiel praktisch nicht vor (Sqr nur zweimal im
Hauptprogramm), werden aber mit umgesetzt.

**Add/Sub (`0EB2`).** Exponent des zweiten Operanden 0 → erster unverändert zurück (auch mit Restbits), erster 0 → zweiter.
Größerer Exponent nach CX:SI:DI; Abstand ≥ 40 → größerer Operand unverändert (also kein Runden bei genau 2⁻⁴⁰). Sonst
48-Bit-Mantissen mit Schutzbyte (AL/CL = 0), die kleinere wird byte- und bitweise **abgeschnitten** nach rechts geschoben
(kein Sticky-Bit). Gleiche Vorzeichen: Summe, Übertrag → eine Stelle rechts, Exponent + 1 (255 + 1 → Fehler 205).
Verschiedene: Differenz, bei Borgen Zweierkomplement und Vorzeichen des größeren; 0 → Null; Normalisieren nach links, Exponent
0 erreicht → Null (Unterlauf ohne Fehler). Runden: Schutzbyte + 80h (halbe Stelle aufwärts im Betrag); Übertrag → nur DX
eine Stelle rechts mit gesetztem Bit, Exponent + 1.

**Cmp (`10F1`).** Verschiedene Vorzeichenbits entscheiden allein (auch bei Null: +0 > −x, −0 < +0). Gleiche: Exponent, bei
gleichem Exponent 0 gleich, sonst DX, BX, AH vorzeichenlos; bei negativen Zahlen umgekehrt. Das Remake liefert −1/0/1;
`==` des Typs vergleicht Bits, der Pascal-Vergleich ist `Real48.Compare`.

**LongInt → Real (`111B`).** 0 → Null. Betrag, Exponent A0h, bei oberem Wort 0 Wortschiebung (90h), bei oberem Byte 0
Byteschiebung (88h), dann normalisieren. Exakt, AH bleibt 0.

**Mul (`0F75`).** Ein Exponent 0 → Null. Exponentensumme und Vorzeichen in DX, Mantissen mit verstecktem Bit. Sind die unteren
24 Bit eines Operanden 0 (AH = 0 und BX = 0), Kurzweg 16 × 40 Bit, sonst volles Produkt der Wörter mit Abschneiden der
untersten Teilprodukte; danach ggf. eine Stelle normalisieren, Exponent − 8081h, Runden wie Add, Bit 6 des oberen
Exponentbytes → Null (Unterlauf), Bit 0 → Fehler 205. Die Umsetzung übernimmt die Wortschritte einzeln.

**Div (`107A`).** Dividend 0 → Null. Schriftliche Division mit 42 Quotientenbits (16 + 16 + 10, invertiert gesammelt),
dann Normalisieren, Exponent + 8080h, Runden und Prüfung wie Mul.

**Trunc/Round (`115A`).** Exponent > 9Fh → Fehler 207 (also auch −2³¹). Exponent < 80h → 0. Sonst die oberen 32 Bit der
Mantisse (AH wird verworfen) um A0h − Exponent nach rechts; Round addiert das zuletzt hinausgeschobene Bit (halb weg von
Null), Überlauf auf 2³¹ → Fehler 207; danach Vorzeichen.

**Zufall.** `RandSeed` `DS:3AD6`; `16C4` rechnet `RandSeed := RandSeed × 08088405h + 1`. `Random(n)` (`1675`): oberes Wort
mod n, 0 für n = 0. `Random` als Real (`168B`): neuer `RandSeed` als vorzeichenlose 32-Bit-Zahl, Exponent 80h, normalisiert
(höchstens 32 Schritte, sonst 0) – also genau `RandSeed / 2³²`. `Randomize` (`16FC`, `BOMB.EXE 0x15A7C`): INT 21h AH=2Ch,
dann **CX (Stunde, Minute) ins untere Wort** `DS:3AD6` und DX (Sekunde, Hundertstel) ins obere `DS:3AD8` – die bisherige
Desktop-Fassung hatte die Wörter vertauscht (`[APPROX]`, jetzt behoben). Für R2-D3: bei fest gesetzter Uhrzeit ist das untere
Wort bekannt, offen bleiben nur Sekunde und Hundertstel (6.000 Werte).

`TurboPascalRandom` liegt jetzt in `Core/Numerics` (vorher `Desktop`); die Folge von `Random(n)` ist durch einen
Charakterisierungstest vor dem Umzug festgehalten und durch die Probe (`random_n`, 20.000 Fälle) belegt.

**Rundung gegenüber exakt gerundetem Ergebnis** (Fälle aus `cases --count 20000` mit Operanden und Ergebnis ≠ 0, verglichen
mit dem exakten Wert, auf 40 Bit gerundet, halb aufwärts): Add 140 von 14.909, Sub 164 von 14.957, Mul 510 von 13.789
abweichend (abgeschnittene Ausrichtung bzw. Teilprodukte), Div 0 von 14.187. `double` wäre also auch bei 40 Bit Mantisse nicht
gleichwertig.

## Abgleich

`Real48ProbeTests`: je Operation 20.000 Fälle, Fehlercode und Ergebnisbytes über SHA-256 gegen die Probe (Werte im Test,
erzeugt mit `hashes --count 20000`). `Real48Tests`: lesbare Randfälle (Null mit Restbits, Vorzeichen, Unter-/Überlauf,
Rundung bei ,5), alle mit `eval` aus dem Original gewonnen.
