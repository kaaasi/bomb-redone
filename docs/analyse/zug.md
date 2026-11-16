# Analyse `zug` – Probe von Zug, Eingaben, Panzerinfo, Zielwahl und MIRV-Dialog (Spur `zug`)

Spur `zug`, Runde 3 (Stand 15.11.2026, Claude Opus 5.5). Probe nach R3-D2 = A: Der Originalcode läuft unverändert im emulierten PC
von [feld.md](feld.md) (`feld_machine.py`, `feld_cpu.py`, nur importiert); das Remake (`Core/Turn`, UI) wird in den Teilen 2–4 gegen
die Ergebnisse geprüft. Inhalt des Zugs: [runde.md](runde.md) §6; Korrekturen dort in §6.3 (Beleg: diese Probe und der Code).

## 1. Werkzeug

`tools/reverse-engineering/zug.py` setzt einen Fall von `feld.py` bei `O4:479A` (Startspieler gewählt) fort und lässt `O4:44A7`
weiterlaufen: Zugbeginn Z1, Kopfzeile, `O7[3]` = `O7:040C` mit der Eingabe `O7:0EF7`, Panzerinfo `O9:0040`, Zielwahl `O9:092E` und –
bei Waffe 7 – der Dialog am Anfang von `O4:13D4`. Keine Bytes der Originale im Skript; Ausgabe nur nach `analyse/feld/`.

| Aufruf | Ergebnis |
|---|---|
| `python3 -I tools/reverse-engineering/zug.py befunde` | SHA-256 von `O7:040C–15C7`, `O9:0040–0DDB`, `O4:13D4–1AE0` (MIRV-Dialog), `O4:4704–49CA` (Zugbeginn, Zugfolge), `U0C48` (Tastatur), `U0E11` (Maus), `U0DBD:022A–026A` (Maus x/y); 7/7 ok |
| `… run --seed <hex> [Fall wie feld.py] [--shooter N] [--control 1\|2] [--set FELD=W …] [--steps "…"] [--mirv] [--sound] [--polls]` | ein Zug nach `analyse/feld/zug-…/` (`result.json`, Bildschirm und DAC am Ende, mit `--polls` an jeder Abfrage) |
| `… hashes [--only I …]` | feste Liste (40 Züge, §3) → `analyse/feld/zug-hashes.tsv` |
| `… folge [--only I …]` | feste Liste (5 Fälle) der Zugfolge ohne Züge → `analyse/feld/zugfolge.tsv` |

Laufzeit: ein Zug 1–2 s, `hashes` rund 60 s, `folge` rund 15 s. `--set` schreibt Felder des Satzes (Schütze oder `N:FELD=W`) bei
`O4:479A`, also **vor** Z1; Felder: x, y, power +1Bh, angle +1Dh, dir +1Fh, energy +21h, alive +23h, weapon +24h, guidance +26h,
target +28h, herb +2Ah, vick +2Ch, stock0..17 +2Eh + 2w, shield/inertia/repulser +52h/+56h/+5Ah, shots +5Eh, turns +70h, score,
cash, wins. `--shooter` überschreibt den Startspieler (`[bp−2]` von `O4:44A7` und `DS:47EE`); Steuerart `DS:E5A6 + 2i`; bei
Steuerart 2 wird die Maus als vorhanden gesetzt (`DS:E94E` = 1, sonst 0 wie beim Start ohne Maustreiber).

## 2. Eingabemodell der Probe

Das Original liest drei Quellen; die Probe speist sie ein, ohne Originalcode zu ersetzen:

- **Gehaltene Tasten** `DS:452A + Scancode` (E0-Tasten + 100h), sonst vom eigenen INT-9-Handler `U0C48:0008` gepflegt. Die Probe
  schreibt die Tabelle direkt (kein INT 9).
- **BIOS-Tastaturpuffer** `0040:001A–003D` für `Crt.KeyPressed`/`ReadKey` in O9 (dort schaltet `U0C48:0269` den eigenen Handler auf
  Weitergabe an das BIOS um, `U0C48:0227` leert den Puffer und schaltet zurück). Die Probe beantwortet INT 16h aus diesem Puffer.
- **Maus** über INT 33h Funktion 3 (`U0E11`): x wird vom Spiel halbiert (`U0DBD:022A`), die Probe liefert also 2x.

Ein **Schritt** setzt den ganzen Zustand: gehaltene Tasten (alle anderen los), optional Maus „m:x,y,Tasten“ (ohne: Tasten los, Lage
bleibt), optional BIOS-Tasten „b:Name“. Schritte werden nur an **Abfragestellen** verbraucht: jeder Eintritt in `O7:0EF7`, jeder
Durchlauf der Schummelschleife `O7:0FA6`, die Warteschleifen von `O9:0040` (`0866` Taste, `0875` Maus), die Schleifen der Zielwahl
(`O9:0B33` Taste, `0CE4` Maus) und des MIRV-Dialogs (`O4:15BE`/`18E8` Tasten, `16B1`/`19FC` Maus). An den **Warte-auf-Loslassen**-
Schleifen (z. B. `O7:1203` ←, `1097` I, `13EB` Mausleiste, `0E97` Feuer mit der Maus) lässt die Probe genau die Tasten los, auf die
die Schleife wartet; ein Schritt wird dabei nicht verbraucht. `Crt.Delay` (`11F8:029C`) wird übersprungen und mit Argument gezählt
(der Takt des Zugs). Aufgezeichnet: Rückgabe jedes `O7:0EF7` (Befehl), Delays, Loslassen, die Abfragestellen, SHA-256 des Bildschirms
an jeder Abfragestelle (Spalte `trace` = SHA-256 über die aneinandergehängten 32-Byte-Digests), Bildschirm und DAC nach Z1
(`O4:4825`) und am Ende (`O4:4965` nach `O7:040C`, bei `--mirv` `O4:1AB9` nach dem Dialog, dort auch Muster `[bp−102h]` und
Detonation `[bp−108h]`), alle Sätze, `RandSeed`.

Das Modell ist bewusst schlicht: eine gehaltene Taste gilt bis zum nächsten Schritt (also für genau eine Abfrage), Loslassen
geschieht nur an Warteschleifen. Typematic-Wiederholungen der BIOS-Tastatur und Hardware-Unterbrechungen gibt es nicht. Teil 2
bildet dieses Modell als abstrakte Eingabe nach (`[APPROX]` gegenüber echter Hardware nur dort, wo Zeit zwischen zwei Abfragen eine
Rolle spielt).

## 3. Fälle und Ergebnis

Fall A wie `feld_schuss.py` (2 Spieler, Wind −44), Fall B 4 Spieler, Wand 2, Hintergrund 2 (Panzer bei (29, 167), (117, 142),
(205, 164), (293, 151)). Alle 40 Züge laufen bis zum Ende (`ok`), zwei Läufe sind byte-gleich. Die Liste (`TURNS` im Skript)
deckt ab: Pfeile mit und ohne Alt (beide Alt-Tasten, graue und Ziffernblock-Pfeile), ↑/↓, BildAuf/BildAb (auch mit Alt), Grenzen 0
und 10 · Energie, Kürzung in Z1, Winkel über 90° und unter 0°, I, Tab und Umschalt + Tab (beide Umschalttasten) mit Umlauf 17 ↔ 0
und leeren Waffen, Waffenwechsel ohne Bestand in Z1, Laser mit Bestand ≤ 100 und Kraftgrenze am Laserbestand, H/V mit und ohne
Bestand, Schummeltaste (W, G, D, C; Ende mit Leertaste und mit Einfg + Entf; graue Einfg + Entf wirkungslos), Panzerinfo 1, 2, 3,
4 (mit Waffen, Herb, Vick, Schild, tot) und Taste 0, Zielwahl mit Tastatur (Schütze und Tote übersprungen, fremde Taste, Herb + Vick)
und Maus (Fehlklick, eigener Panzer), jedes Mausfeld der Kopfzeile und Leiste samt Rändern, Panzerinfo per Klick, MIRV-Dialog mit
Tasten (Grenzen, Esc zurück) und Maus, Ton an.

**Zugfolge** (`folge`): ab `O4:479A` wird jeder Aufruf von `O7:040C` übersprungen (zurück nach `O4:4770`); aufgezeichnet nach Z1 je
Zug Spieler, Durchgang `DS:47EA`, Waffe, +5Eh und +70h. Fälle: 2 und 4 Spieler, Tote (vor und am Ende eines Durchgangs, 5 Spieler),
Waffenwechsel ohne Bestand in Z1 einschließlich 17 → 0. Ergebnis: Tote werden übersprungen, der Durchgang zählt beim Umlauf
nach Spieler N hoch, +5Eh und +70h zählen je Zug des Spielers.

## 4. Befunde aus Code und Probe (Ergänzungen und Korrekturen zu runde.md §6)

1. **Alt umgekehrt** (Korrektur §6.3): ←/→/↑/↓/BildAuf/BildAb **ohne Alt wiederholen** (kein Warten; je Abfrage ein Befehl),
   **mit Alt** (`DS:452A+38h` oder `+138h`) wartet die Taste aufs Loslassen (ein Schritt). Fälle 0 und 1: dreimal ← ohne Alt drei
   Befehle ohne Loslassen, mit Alt je ein Loslassen.
2. **Immer warten** I, Tab, Umschalt + Tab, H, V; **nie warten** Feuer (Leertaste, Enter, Ziffernblock-Enter) und die Ziffern
   1..0 (Panzerinfo). Umschalt + Tab ist 7, wenn beim Tab eine der Umschalttasten (2Ah, 36h) gehalten ist; danach wird Tab nicht
   noch einmal als 8 gewertet, weil die Taste schon los ist.
3. **Schummeltaste nur mit Einfg und Entf des Ziffernblocks** (Scancodes 52h/53h, nicht E0 52h/53h; Fall 22). In der Schleife wirken
   C, W, G, D bei jedem Durchlauf, Ende mit Einfg + Entf oder Leertaste; danach werden Einfg, Entf und Leertaste gelöscht und der
   Befehl ist 0 (der Feuerknopf muss neu gedrückt werden, Fall 20).
4. **Taste 0** ruft `O9:0040(0, …)` (`O7:112C`), `O9:0054–005A` macht daraus 10; bei n > N kehrt `O9:0040` **sofort** zurück (`0190`) – ohne `U0C48:0227`, die
   Tastatur bleibt also im BIOS-Weitergabemodus (Tasten landen zusätzlich im BIOS-Puffer, bei vollem Puffer piept das BIOS;
   im Remake `[APPROX]` nicht nachgebildet, weil ohne Wirkung auf Bild und Spielzustand).
5. **Laser mit Bestand ≤ 100** und **Waffe ohne Bestand**: `O7:0EF7` gibt 8 zurück, **ohne** `Delay(TS)` (Fall 15: ein Delay bei
   zwei Befehlen). Der Waffenwechsel 7/8 (`O7:05FF`) überspringt Waffen ohne Bestand und den Laser erst bei Bestand **< 100** – ein
   Laser mit genau 100 wird also gewählt und von der nächsten Abfrage sofort weitergeschaltet.
6. **Zielwahl mit der Tastatur:** Trifft → / ← den Schützen oder einen Toten, wird **ohne neue Taste** in dieselbe Richtung
   weitergezählt (`O9:0BD4` springt nach `0B5C` mit dem alten Zeichen). Ende mit einem Zeichen aus der Menge `O9:08F7` (Enter
   und Leertaste belegt durch die Fälle 27/28); andere Tasten werden gelesen und verworfen (Fall 28). Mit der Maus wird auch der **eigene** Panzer angenommen (Fall 37).
7. **MIRV-Dialog:** Muster 0..4 und Detonation 0..2 mit ↑/↓ (das Spiel löscht die Taste selbst nach dem Lesen, also ein Schritt je
   Druck), Ende mit Enter, Ziffernblock-Enter oder Leertaste; **Esc** auf Seite 2 stellt den Bildschirm her und beginnt wieder bei
   Seite 1 (Fall 32). Mit der Maus zählt **nur y** (Seite 1: y < 89 → 0, 92..98 → 1, 102..108 → 2, 112..118 → 3, > 121 → 4; Seite 2:
   < 89 → 0, 92..98 → 1, > 101 → 2), jede Taste beendet die Seite. Die gewählte Zeile ist Farbe 130 + i = (63, 22, 22), die anderen
   (0, 0, 44). Vor jeder Seite wartet der Dialog, bis beide Maustasten los sind.
8. **Ton an** (Fall 39): Winkeländerung spielt `Sound` mit `Delay(10)`, Richtungswechsel vier Töne mit `Delay(5)`; ohne Ton keine
   weiteren Delays als `Delay(TS)` je Abfrage.

## 5. Ergebnis Teil 2 (`Core/Turn`)

`TurnSession` bildet Z1 (`Begin`), `O7:040C` mit `O7:0EF7` (`Play`), `O9:0040`, `O9:092E` und den MIRV-Dialog (`MirvSetup`) als
Koroutine nach: Jede Abfragestelle der Probe ist ein `TurnWait.Poll`, `Delay`/`Sound`/`NoSound` werden an den Gastgeber gereicht,
an den Warteschleifen meldet Core `TurnWait.ReleaseKeys` (Tasten) bzw. `ReleaseButtons` (`O7:0E97` und die Mausfelder von
`O7:0EF7`, MIRV-Seiten) und lässt die Tasten in `TurnInput` nach dem Fortsetzen selbst los; die Probe setzt sofort fort. BGI-Text und Fenster zeichnet die UI über `ITurnView` (Teil 3);
Rohr, Panzer, Rahmen und DAC zeichnet Core selbst. `TurnOrder.Next` ist R5, `WeaponBar` Z2.

Abnahme: alle 40 Züge **bitgleich** zur Probe (`TurnProbeTests`: Status, verbrauchte Schritte, Abfragestellen, jeder Befehl, jede
Verzögerung, jedes Loslassen, Satz des Schützen, MIRV-Wahl, alle geschriebenen DAC-Einträge, Rahmen und Feld – beim MIRV-Dialog ohne
das Feld, weil der Dialog darüber liegt), alle 5 Zugfolgen gleich (`TurnOrderProbeTests`). Kein Zug verbraucht `Random`.
Gegenprobe: zwei absichtlich eingebaute Fehler (BildAuf + 98, Alt-Bedingung) lassen 5 Fälle scheitern.

`[APPROX]` in Teil 2: `U431:0594` überspringt die Adresse 64000 (außerhalb beider Puffer); die Zielwahl mit der Tastatur ohne
weiteren lebenden Spieler hängt im Original, das Remake wirft eine Ausnahme; der Computer (Steuerart 3) kommt mit Runde 4.

## 6. Ergebnis Teil 3 (UI)

`PlayfieldTurnView` (`ITurnView` auf einem `PlayfieldScreen`) zeichnet Kopfzeile Z1 und die vier Felder, `TankInfoWindow`,
`TargetSelectBar` und `MirvSetupDialog`; Fenster sichern den Bildschirm und stellen ihn beim Schließen wieder her wie der Puffer
`DS:E5BC`. Abnahme: in allen 40 Zügen ist der **ganze Bildschirm an jeder Abfragestelle** gleich der Probe (Spalte `poll_screens`,
`PlayfieldTurnViewOriginalTests`, mit Originaldaten), dazu der Bildschirm am Ende (außer MIRV: die Probe hält vor dem Wiederherstellen).

Aus Code und Probe dazu: In der Panzerinfo trägt die gewählte Waffe auch ihre Zahl (beides Farbe 4); Schild = Wert div 10 „%“ bis
1000, Inertia Wert „%“ bis 100, darüber „100%“, Repulser der Wert (je das untere Wort). Ein Fenster mit mehr als 11 Zeilen reicht
über den Bildschirm hinaus (Fall 24: y −52 bis 252); BGI schneidet Rahmen pixelweise ab und lässt Textzeilen der DefaultFont, deren
Zelle nicht ganz in y 0..199 liegt, ganz weg. Beim Öffnen setzt O9 die Farbe 19 + n auf volle Helligkeit (`O10:01AC(100, …)`), beim
Schließen wieder nach der Energie.

## 7. Ergebnis Teil 4 (Desktop)

`PlayfieldTurnScreen` (UI, ohne MonoGame testbar) ersetzt die Vorschau `RoundPreviewScreen`: Z1 und Zug aus Core mit Tastatur und
Maus, MIRV-Dialog bei Waffe 7, dann der Schuss über `PlayfieldShotScreen`/`Core/Shot` und der nächste Spieler nach R5. Je Bild läuft
die Koroutine bis zur nächsten Abfrage ohne vorherige Verzögerung; die Verzögerungen TS des Zugs werden von der Bildzeit abgezogen
(gehaltener Pfeil ≈ 125 Schritte/s wie im Original bei TS = 8). An `ReleaseKeys`/`ReleaseButtons` bleibt die Koroutine stehen, bis
der Spieler die Tasten bzw. Knöpfe physisch losgelassen hat, und läuft dann mit der Eingabe dieses Augenblicks weiter: der Befehl
wirkt wie im Original erst nach dem Loslassen (`O7:054E` → `0551`), währenddessen gedrückte Tasten liest die Routine danach aus der
laufenden Tabelle, der Mausschuss startet erst bei losgelassenen Knöpfen (Review 2026-11-16 P2-1). Von Core ohne Warten gelöschte
Tasten (MIRV-Dialog, Schummelschleife) bleiben gesperrt, bis der Spieler sie loslässt. `DesktopGame` liefert die Tabelle `DS:452A`
aus dem Tastaturzustand (F2: nächste Runde) und übergibt TS aus `BOMB.CFG` (`GameConfig.TurretSpeed`) und das Teamspiel der
Spieloptionen samt Teamnamen; Ton ausdrücklich aus (`[APPROX]`, P2-2). Restlücke R2 „Desktop-Zug ohne Kopfzeile“ damit erledigt.

**Ziffernblock und NumLock** (#160, `p3-runde-3`): MonoGame 3.8.6 DesktopGL übersetzt SDL-Tastencodes (`SDL_Keycode`), keine
Scancodes: `KeyboardUtil._map` (per Reflexion aus `MonoGame.Framework.dll` gelesen) bildet `SDLK_KP_0` (`0x40000062`) auf
`Keys.NumPad0`, `SDLK_KP_PERIOD` (`0x40000063`) auf `Keys.Decimal` und `SDLK_INSERT` (`0x40000049`) bzw. `SDLK_DELETE` (`0x7F`) auf
`Keys.Insert`/`Keys.Delete` ab; `DesktopGame.ToScancode` macht daraus 52h/53h bzw. E0 52h/53h. Ob SDL 2.32 (`MonoGame.Library.SDL`)
für den Ziffernblock bei NumLock aus `SDLK_KP_*` oder `SDLK_INSERT`/`SDLK_DELETE` meldet, entscheidet SDL; das ist im Code dieses
Repos nicht belegbar. `[APPROX]`: Die Schummeltaste kann bei NumLock aus am Desktop wirkungslos sein (dann wie die grauen Tasten);
mit NumLock an gilt die Zuordnung oben. Eine Unterscheidung nach Scancode bietet `KeyboardState` nicht.

`[APPROX]` bis zur Spur `runde` bzw. Runde 4: kein Tod und kein Rundenende (Energie 0 zählt als ausgeschieden, unter zwei Panzern
folgt die nächste Runde), Waffen ohne Core-Schuss (Laser, Roller, Sonic, MIRV-Teilung, Repulser im Feld) überspringen den Schuss,
der Bestand sinkt je Schuss hier um 1 (später `O4:01B7` in Core/Shot – beim Integrieren mit `waffen` nicht doppelt zählen),
Computer schießen sofort, Bestände wie ein neuer Satz, kein Ton, keine Typematic-Wiederholung.

Offen: keine Frage an den Nutzer; Bildvergleich in `bilder-s4` (R3-D2).
