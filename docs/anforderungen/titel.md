# Anforderungen: Titelfolge und Bildschirm-Grundgerüst

Spur `titel` (Runde 1, Welle 2, Stufe 2a). Quelle: Disassembly von O1:0014 (`BOMB.OVR 0x0001C`, `docs/analyse/re-grundlagen.md`
§6), Listing `ovl_O1_0054.asm` im Referenz-Repo; Dateioffsets in `BOMB.OVR` = Listing-Offset + 8. Bildschirm B1 in
`bildschirme.md`. Umsetzung: `src/BombRedone.UI/Screens/`.

## 1. Grundgerüst

- Ein Bildschirm (`IScreen`) bekommt mit `Update(Zeit, Eingabe)` die verstrichene Zeit und die gesammelte Eingabe und gibt den
  nächsten Bildschirm zurück (sich selbst, einen anderen oder `null` = Ende). `Draw(Surface)` zeichnet Pixel und Palette und
  ändert keinen Zustand.
- `ScreenHost` hält den aktuellen Bildschirm und wechselt; der neue Bildschirm bekommt sein erstes `Update` mit dem nächsten
  Aufruf (Restzeit verfällt).
- Eingabe (`ScreenInput`): „Taste gedrückt“ (seit dem letzten Update, nur Tasten, die das BIOS in den Tastaturpuffer legt –
  nicht Shift, Strg, Alt allein), „beide Shift gehalten“, gehaltene Maustasten (links = 1, rechts = 2 wie INT 33h).
- Desktop: Esc beendet, F10 schaltet 4:3, F11 Vollbild; diese Tasten gehen nicht an die Bildschirme. Ohne Originaldaten wird der
  Titel übersprungen (wie ohne `BOMB2.RAW`). Nach dem Titel folgt ein schwarzer Platzhalter (Hauptmenü: Spur `menue`).

## 2. Titelfolge (`[ORIGINAL]`, Offsets in `BOMB.OVR`)

1. **Dateien:** Existenzprüfung per `FindFirst` (Attribut 0) für `bomb1.raw` (0x00008) und `bomb2.raw` (0x00012) bei 0x001FF /
   0x00229. Vorhandene Datei: ein Datensatz von 0xFD00 = 64.768 Byte (0x00254 / 0x002B2); längere Dateien werden nur bis dahin
   gelesen.
2. **Ohne `BOMB2.RAW`** endet die Folge sofort, nichts wird gezeigt (0x002EF → 0x00501). **Ohne `BOMB1.RAW`** ist das
   Grundbild ganz Index 0 (0x0029A). Beides wie `BOMB.TXT` „GRAPHICS FILES“.
3. **Anfang:** Palettenteil von BOMB1 wird genullt (0x002F9), DAC darauf gesetzt (0x0031F), Pixel von BOMB1 auf den Schirm kopiert
   (0x00333), dann die **Palette von BOMB2** gesetzt (0x00349). BOMB1 erscheint also in den Farben von BOMB2; die eigene Palette
   von BOMB1 wird nie benutzt.
4. **Aufbau:** Schritte in dieser Reihenfolge: Farben 135–154 (0x00369), 16–20 (0x0039F), 115–124 (0x003D4), dann die Paare
   (155, 175) … (164, 184) (0x00409). Je Farbe werden alle Pixel von BOMB2 mit diesem Index über den Schirm kopiert
   (`BOMB.EXE 0x05E20`). Die Schleife läuft von Offset 0xFA00 abwärts bis 1: **Pixel (0, 0) wird nie kopiert**. BOMB2 hat außerhalb
   der Liste nur die Farben 0 und 15; diese Pixel erscheinen nie.
5. **Abbruch im Aufbau:** vor dem ersten Schritt, nach jedem Schritt und noch einmal nach dem letzten (0x0035D ff., 0x00458):
   beide Shift-Tasten gehalten (BIOS-Flags 0040:0017 Bits 0 und 1) **oder** linke und rechte Maustaste zugleich
   (`BOMB.EXE 0x06379`). Einzelne Tasten beenden den Aufbau nicht.
6. **Tastaturpuffer** wird nach dem Aufbau geleert (0x00464): Tastendrücke während des Aufbaus zählen nicht.
7. **Farbrotation:** Schritt i = 1 … 20, nach 20 wieder 1 (0x00484), setzt DAC 135–154 aus den Einträgen i−1 … i+18 einer Tabelle
   mit 40 Einträgen (0x00491), wartet `Delay(60)` (0x004B2) und prüft dann Taste (`KeyPressed`, 0x004BB) oder linke bzw. rechte
   Maustaste (0x004C9, 0x004D6). Tabelle (0x000AB–0x00108): Rot = `Trunc(((e + 1) mod 20) × 6,3)` mit 6,3 als Real48 `83 9A 99 99 99
   49`, Grün und Blau 0. Zwei weitere Tabellen (mod 10 in Blau 0x0013F und Grün 0x001B1) werden berechnet, aber nie benutzt.
8. **Ende:** Tastaturpuffer leeren, DAC auf die zu Beginn gelesene Palette zurück (0x00501, gelesen mit `BOMB.EXE 0x0EF40`), der
   Bildinhalt bleibt stehen.
9. **Palettenaufrufe** übergeben die Anzahl als Byte (255 für die ganze Palette; `BOMB.EXE 0x0EFB9`): DAC-Eintrag 255 wird von
   der Titelfolge nie gesetzt. Beide Bilder benutzen Index 255 nicht.

## 3. Annahmen `[APPROX]`

| Punkt | Annahme | Prüfen mit |
|---|---|---|
| Dauer eines Kopierdurchgangs | 85 ms (64.000 × 4 Befehle bei `cycles=3000`); Aufbau gesamt 55 × 85 ms ≈ 4,7 s. Longplay: Schriftzug erscheint in etwa 4 s (LP 00000–00003) | DOSBox-Aufnahme `runde-1` |
| Warten auf den Strahlrücklauf beim Setzen der DAC in der Rotation | vernachlässigt, Schritt = 60 ms | DOSBox |
| DAC-Werte über 63 (Rot-Tabelle bis 119) | VGA-DAC übernimmt die unteren 6 Bit (Hardware, nicht aus den Originaldateien) | DOSBox-Bild der Rotation |
| Palette vor dem Titel | schwarz (im Original die vom Hauptprogramm gesetzte Palette) | Disassembly Hauptprogramm |
| Heap-Puffer | beginnen bei Offset 0 (die Kopierroutine nutzt nur die Segmente) | DOSBox-Debugger |
| Zu kurze oder unlesbare Datei | Rest mit 0 aufgefüllt (Original: `{$I-}`, Rest des Heaps unbestimmt) | – (nicht nachbildbar) |
| Taste im Update, das den Aufbau beendet | zählt als vor dem Leeren des Puffers | – |

## 4. Abnahme

- Synthetische Tests (`tests/BombRedone.UI.Tests/Screens/`): Reihenfolge und Farben je Schritt, Pixel (0, 0), Zeiten mit
  vorgegebenem Takt, Abbruch je Eingabe und Phase, Rotationstabelle und Umlauf, Rücksetzen der Palette, fehlende, zu kurze und zu
  lange Dateien, Unabhängigkeit von der Aufteilung in Einzelbilder, `Draw` ändert nichts.
- Originaldaten: Anfangsbild = Pixel von BOMB1 (SHA-256 `e54ecb608342…`, wie `formate`), Palette = BOMB2; jeder Aufbauschritt
  pixelgleich zur Überlagerung der dekodierten Bilder; Endbild SHA-256 `b75cd2b71b6b…`; Rotation ändert nur 135–154.
