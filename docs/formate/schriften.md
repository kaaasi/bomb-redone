# Schriften

Spur `schrift` (Runde 1, Welle 2). Code: `src/BombRedone.Formats/BgiFont.cs`, `BgiFontLibrary.cs`, `BitmapFont.cs`,
`src/BombRedone.UI/Text/`. Grundlage: `docs/analyse/re-grundlagen.md` §4 (F1–F8). Alle Offsets sind Dateioffsets.

## 1. Überblick

| Schrift | Graph-Name | Herkunft | Fundstelle |
|---|---|---|---|
| LITT | SmallFont (Nr. 2) | BGI-Strichschrift, eingebunden | `BOMB.EXE 0x08B90`, 5.131 Byte |
| EURO | (Nummer aus `DS:E910`) | BGI-Strichschrift, eingebunden | `BOMB.EXE 0x09F9B`, 8.467 Byte |
| TINY | (Nummer aus `DS:E91A`) | BGI-Strichschrift, eingebunden | `BOMB.EXE 0x0C0AE`, 1.540 Byte |
| DefaultFont 8 × 8 | DefaultFont (Nr. 0) | **nicht im Original**: ROM-BIOS des Rechners | Treiber `BOMB.EXE 0x08769`–`0x087A2` |

Keine Schriftdaten im Code-Repo: Tests mit Originaldaten lesen `gamedata/BOMB.EXE`, synthetische Tests bauen eigene Schriften
(`tests/**/SyntheticFont.cs`).

## 2. BGI-Strichschrift (`*.CHR`)

**Dateikopf** (je Schrift `PK` 08h 08h, Text, 1Ah; dahinter):

| Offset nach 1Ah | Größe | Inhalt | LITT / EURO / TINY |
|---|---|---|---|
| +0 | 2 | Kopfgröße = Beginn der Schriftdaten ab Dateianfang | 80h / 80h / 80h |
| +2 | 4 | Name | `LITT` / `EURO` / `TINY` |
| +6 | 2 | Größe der Schriftdaten | 5.003 / 8.339 / 1.412 |
| +8 | 2 | Version (Haupt, Neben) | 1.0 |
| +10 | 2 | Mindestversion | 1.0 |

**Schriftdaten** (ab Kopfgröße), so wie der Graph-Kern sie liest (`BOMB.EXE 0x131E0` kopiert 16 Byte und prüft `+`):

| Offset | Größe | Inhalt | LITT / EURO / TINY |
|---|---|---|---|
| +0 | 1 | Kennung `+` | |
| +1 | 2 | Zeichenanzahl | 223 / 223 / 96 |
| +4 | 1 | erstes Zeichen | 32 / 32 / 32 |
| +5 | 2 | Offset der Strichdaten (ab Schriftdaten) | 685 / 685 / 304 |
| +7 | 1 | Scan-Kennung (Füllen; der Kern löscht sie nach der Kopie, `0x131FB`) | 0 |
| +8 | 1 | Ursprung → Oberkante (vorzeichenbehaftet) | 7 / 45 / 4 |
| +9 | 1 | Ursprung → Grundlinie | 0 |
| +10 | 1 | Ursprung → Unterlänge | −2 / −10 / −7 |
| +16 | 2 × n | Offsets der Zeichen (ab Strichdaten) | |
| +16 + 2n | n | Breitentabelle | |

**Strichbefehle** je 2 Byte (`BOMB.EXE 0x1331E`, `0x133A9`): Bit 7 des ersten Bytes × 2 + Bit 7 des zweiten = Befehl
(0 Ende, 1 Scan – vom Kern übersprungen, 2 Bewegen, 3 Zeichnen); die unteren 7 Bit sind x bzw. y, vorzeichenbehaftet ab
Bit 6, y wächst nach oben. Jedes Zeichen endet mit „Bewegen“ auf seinen Vorschub.

**Befund:** In LITT und EURO ist dieser letzte Punkt immer (Breite, 0). In TINY haben 27 Zeichen Breite 0 in der Tabelle,
enden aber mit „Bewegen (3, 0)“ – darunter die Kleinbuchstaben `b`–`z`, die keine Striche haben (TINY hat keine
Kleinbuchstaben; `a` ist leer mit Vorschub 0). `TextWidth` und tatsächlicher Vorschub weichen dort ab (Test
`LastStrokeAndWidthTableDisagreeOnlyInTiny`).

## 3. Textausgabe des Graph-Kerns

Der BGI-Kern der Unit `Graph` (Segment `0E26`) adressiert seine Variablen relativ zum Dateioffset `0x11B23`
(Kernoffset 14F0h = `BOMB.EXE 0x13013`; die Variablen 1400h–140Ch liegen bei `0x12F23` ff. zwischen dem Code). Belegt:

- **Größentabelle** (`BOMB.EXE 0x13013`, gelesen bei `0x13032`, Größe auf 10 begrenzt bei `0x13036`): Faktor
  (Multiplikator/Divisor) für Größe 0–10: –, 3/5, 2/3, 3/4, 1/1, 4/3, 5/3, 2/1, 5/2, 3/1, 4/1. Größe 4 ist die Originalgröße.
  Größe 0 nimmt die Werte von `SetUserCharSize` (`0x13171`); Divisor 0 zählt als 1 (`0x13184`, `0x1318F`).
- **Seitenverhältnis:** Bei waagrechter Schrift würde der Kern den x-Faktor mit 10000 / Kernwort 81h korrigieren, wenn das
  Wort unter 10000 liegt (`0x131AC`). Es ist in der Datei 10000 (`0x11BA4`), ein Schreibzugriff wurde nicht gefunden →
  `[APPROX]` keine Korrektur. Der Treiber meldet zwar 8.500 (`BOMB.EXE 0x08021`), doch der Kern rechnet dort mit `AX`,
  das an dieser Stelle nicht den Faktor enthält (`0x131B7`) – ungeklärt, siehe §5.
- **Skalieren** (`0x133BE`): jeder Punkt einzeln, `x · mul / div` mit Abschneiden gegen null (`IMUL`/`IDIV`).
- **Vorschub** (`0x132D9`): um den letzten skalierten Punkt des Zeichens, nicht um die Breitentabelle.
- **TextWidth** (`0x13242`): Summe der Breitentabelle aller Zeichen im Zeichenvorrat (andere übersprungen, NUL zählt nicht als
  Ende), **einmal** skaliert. **TextHeight** (`0x13214`): (Oberkante − Unterlänge) · mulY / divY.
- **Ausrichtung** (`0x13445`, `0x13478`) verschiebt den Ursprung (Grundlinie): waagrecht Mitte `x − (B/2 − 1)`, rechts
  `x − B`; senkrecht Mitte `y + H/2`, oben `y + H`, unten unverändert (B = TextWidth, H = TextHeight). Senkrechte Schrift:
  zuerst `x + H`, dann waagrecht mit H statt B, senkrecht mit B statt H.
- **Richtung senkrecht:** um 90° gegen den Uhrzeigersinn gedreht (Schrift läuft nach oben), Bildpunkt
  `(x − gy, y − gx)` (`0x134B1`, Annahme Kernbyte 92h = 0, `[APPROX]`).
- **Ausgabe** (`0x133EC`): Linienart vorher auf durchgezogen, Dicke 1, danach zurück (`0x133F5`, Name der Routine erschlossen);
  ein NUL beendet die Ausgabe (`0x1341C`); Zeichen außerhalb des Vorrats werden ohne Vorschub übersprungen (`0x13299`).
- **DefaultFont:** Ausrichtung im Kern (`0x130AE`, `0x130D5`), danach ist (x, y) die linke obere Ecke; mit x < 0 wird nichts
  gezeichnet (`0x1307D`). Zellgröße liefert der Treiber (Funktion 24h); angenommen 8 · Größe (`[APPROX]`, Treiber-Status-
  tabelle: Zeichenzelle 8 × 8 bei `0x08023`). Treiberfunktion 26h (`0x080FA`–`0x08135`): waagrecht ab dem ersten, senkrecht ab
  dem letzten Zeichen rückwärts, je eine Zelle weiter; ein NUL beendet die Ausgabe in dieser Reihenfolge (`0x08104`). Das
  Zeichnen eines Zeichens (`0x08650`) ist nicht abgeleitet (`[APPROX]`).

## 4. DefaultFont 8 × 8

Der Treiber VGA256 holt Zeichen 0–127 aus dem ROM-BIOS (`F000:FA6E`) und 128–255 über den Vektor INT 1Fh (`0000:007C`);
ist der Vektor 0, nimmt er das Leerzeichen des ROMs (`BOMB.EXE 0x08769`–`0x087A2`). **Die Glyphen gehören also nicht zum
Spiel**, in DOSBox sind es die eingebauten VGA-Zeichen. `BitmapFont` hält nur das Format (8 Byte je Zeichen, Bit 7 links);
woher die Daten kommen, ist offen (§5).

## 5. Offene Punkte

- **Quelle der 8 × 8-Schrift** (Entscheidung, gebündelt über die Leitstelle): Optionen und Empfehlung im PR der Spur.
- Kernwort 81h und der Codepfad `0x131AC` (nutzt `AX` ohne sichtbare Ladung) – nur relevant, falls das Wort zur Laufzeit
  unter 10000 fällt; prüfbar mit einem DOSBox-Bild einer waagrechten Strichschrift.
- Linienalgorithmus des Treibers (Pixelwahl bei Bresenham-Gleichstand) und Clipping am Sichtfenster (`0x134E6`):
  `[APPROX]` bis zum pixelgenauen Abgleich mit DOSBox-Bildern.
- Hinweis an `re-grundlagen`: Die Menüeinträge (`O2:1E7F`) rufen nach `SetTextStyle(SmallFont, 0, 1)` noch
  `SetUserCharSize(1, 1, 1, 1)` (`0E26:16E3`, `BOMB.OVR 0x024B8`) – wirksam ist also Größe 0 mit 1:1, nicht 3/5.

## 6. Abgleich mit dem Video

| Text | Aufrufe | gerendert (320 × 200) | Longplay 00009 (÷ 1,8) |
|---|---|---|---|
| Kopfzeile „TANK WARS  3.2“ | `O2:06BD`, EURO Größe 1, Mitte/Mitte, vier Aufrufe | x 28–288, y 11–39 | x 27,8–288,3, y 10,0–38,9 |
| Menüeintrag „Sound Options“ | `O2:1E7F`, LITT 1:1, Mitte/Mitte (160, 95) | x 123–197, y 93–101 | x 124,4–196,1, y 92,2–99,4 |

Lage und Form stimmen auf etwa einen Bildpunkt (Videobild skaliert und komprimiert, daher `[APPROX]`, bis ein DOSBox-Bild
vorliegt). Prüfsummen gerenderter Probetexte: `GraphTextOriginalTests` (Regressionsschutz, nicht aus dem Original).
