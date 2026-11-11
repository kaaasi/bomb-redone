# Review PR #64 – `wertung-einkauf`

Prüfer: Claude Opus 5.5, frische Sitzung ohne Implementierungskontext (geplanter Lauf, unbeaufsichtigt). Base
`9f64019d207c5f026ed323a0e9aeb997d8ac03da` (im Head enthalten), Head `e81df6b6e320f911940069ce1cb7f783fa5ebfd8` (ausgecheckt,
`git rev-parse HEAD` = Head, `git status --porcelain` leer). Originaldaten und Referenzschrift mit
`tools/claude/fetch-reference.sh` (Prüfsummen stimmen); Disassembly selbst erzeugt (`bombre.py listings` nach `analyse/`,
Einzelstellen mit `ndisasm` ab Dateioffset `BOMB.OVR 0x08A8C + Ofs` bzw. `BOMB.EXE 0x059B0 + Ofs`); Videobilder TW 00100,
LP 00064 aus dem Referenz-Repo (`videos/`), nur gelesen.

## 1. Merge-Empfehlung

**Ja** für diesen Head. Waffentabelle, Einkaufsregeln, Rangfolge einzeln/Teams und die nachgeschlagenen Zeichenparameter
stimmen mit der Disassembly überein; die gerenderten Bildschirme decken sich nach Lage und Form mit B5 (LP 00064) und B6
(TW 00100). Architektur und Dateigrenzen eingehalten, keine Originaldaten im Diff, alle drei Läufe grün. Offen sind nur P3:
zwei ungetestete Rangbedingungen, ein Hinweis für die Integration und ein Zählfehler in der Doku.

## 2. Befunde

- **P3 – Rang bei gleichen Punkten, aber verschiedenen Siegen ist ungetestet**
  ([Standings.cs#L33](https://github.com/kaaasi/bomb-redone/blob/e81df6b6e320f911940069ce1cb7f783fa5ebfd8/src/BombRedone.Core/Economy/Standings.cs#L33)).
  Gegenprobe: `|| list[i].Wins != list[i + 1].Wins` entfernt → `dotnet test tests/BombRedone.Core.Tests --filter
  FullyQualifiedName~Economy` 51/51 grün, `tests/BombRedone.UI.Tests --filter FullyQualifiedName~Score` 60/60 grün. Fall:
  A (2 Siege, 100 Punkte), B (1 Sieg, 100 Punkte) → richtig Rang 1/2, mutiert 1/1. Der Code selbst entspricht `O3:5F12–5F62`
  (Punkte `+13h`, dann Siege `+6Ah`, je Abweichung `inc [bp-0Ch]`). Vorschlag: genau dieser Testfall.
- **P3 – Teamrang: Bedingung „mittlere Siege verschieden“ ungetestet**
  ([TeamStandings.cs#L119](https://github.com/kaaasi/bomb-redone/blob/e81df6b6e320f911940069ce1cb7f783fa5ebfd8/src/BombRedone.Core/Economy/TeamStandings.cs#L119)).
  Gegenprobe: `|| CompareWins(slot, next) != 0` entfernt → Core-Economy 51/51 und UI-Score 60/60 grün. Original
  `O3:6F72–7003`: Real48-Vergleich Team `+11h` (Punktsumme), dann `+1Dh` (Siegsumme/Mitglieder) – wie umgesetzt.
- **P3 – Hinweis für die Integration: die Wertung sortiert die Spielersätze dauerhaft um.** `O3:0000` tauscht ganze 0x74-Byte-
  Sätze über Satz 0 (drei `Move` à 0x74, `BOMB.OVR 0x08A8C`); der Einkauf läuft danach über die Sätze 1..n (`O3:0F21–0F33`),
  also in Ranglistenreihenfolge, ebenso alles Spätere, was über die Satznummer geht. `Standings.Rank` liefert eine neue Liste
  und lässt die Eingabe unverändert ([Standings.cs#L9-L14](https://github.com/kaaasi/bomb-redone/blob/e81df6b6e320f911940069ce1cb7f783fa5ebfd8/src/BombRedone.Core/Economy/Standings.cs#L9-L14)
  nennt den Satztausch, nicht die Folge). Für diese Spur ohne Wirkung (kein Spielablauf); in die Anforderungen bzw. den Plan
  der Spielfolge aufnehmen, damit die Einkaufsreihenfolge dort übernommen wird.
- **P3 – Doku: „sieben Real48-Summen“ sind sechs**
  ([wertung-einkauf.md#L108](https://github.com/kaaasi/bomb-redone/blob/e81df6b6e320f911940069ce1cb7f783fa5ebfd8/docs/anforderungen/wertung-einkauf.md#L108)).
  `O3:6713–67B3` nullt `+11h, +17h, +1Dh, +23h, +29h, +2Fh` (6 × 6 Byte bis `+34h`); `O3:67E4–69B9` addiert Spieler `+13h`
  (LongInt) und die Worte `+68h, +6Ah, +6Ch, +6Eh, +70h` = 1 + 5. Beleg: `ndisasm -b16` ab `BOMB.OVR 0x0F19F` (= `0x08A8C + 0x6713`).

**Gegenprüfung ohne Befund** (Disassembly selbst gelesen):

- **Tabelle** (`BOMB.EXE`, DS = `0x163F0`): Layout 18/2/3 Einträge, Namen je 256 Byte, Preise LongInt, Mengen Word; Lücken der
  Felder passen (`2CC4−2C7C = 18×4`, `2CE8−2CC4 = 18×2`, `3700−36F4 = 3×4`). Python-Probe: Index 0 „Lead Ball“ −1/0, Index 10
  „Chain Reaction Inducer 20/D“ / „CRI 20/D“ 15000/1, Index 11 „Hand Grenade“ −1/0; Abwehr 20000/10000/15000 bei 1000/100/1.
  `BOMB.DOC` „18 weapons, 2 guidance systems, and 3 defenses“. **#19 bestätigt:** CRI 20/D ist käuflich (Menge 1, 15000);
  Lead Ball und Hand Grenade fallen durch `cmp word [di+2CC4h],0` (`O3:0211`) aus dem Angebot.
- **Einkauf:** Zugang `O3:0E3D–0F90` (Mindestpreis über alle Gruppen, 16 Bit mit `cwd`, Start 7FFFh, nur Menge ≠ 0; Geld =
  `+13h − +17h` ≥ Minimum; Computer nur mit `U129:018D` ≠ 0 und `E8BA` ≠ 0); Angebot `O3:01CB–0218` (Bestand > 0 oder Geld ≥
  Preis, dann Menge ≠ 0); „+“ `O3:1CF1–1DC2` (Menge < 9, Preis16 ≤ Punkte − `3FE2` − `3FDA`, `3FDA` += Preis16); „−“ `O3:1B33–1B8F`
  (Menge > 0, `3FDA` −= Preis 32 Bit – im Code `Price` statt `Price16`, wie im Original); Kauf `O3:2080–2239` (`3FE2` += Preis16 ×
  Anzahl, Waffen `+2Eh`/Lenk `+2Ah` Word, Abwehr `+52h` LongInt, Neuaufbau nur bei Anzahl ≠ 0 und `[4751]` = 0). Kein Verkauf im
  Original (kein „sell“ in `BOMB.DOC`/`BOMB.OVR`) – „nur abwählen“ ist richtig. Kacheln (5,40), 99 × 13, +18, Umbruch > 197,
  +104; Name `(x1+x2) div 2 − 7`, y1 + 3, Kurzname `DS:1A7C`.
- **Wertung einzeln** `O3:5AEF`: Kasten 100 ∓ (5n + 15); zwei Blasensortierungen (Siege `+6Ah` <, dann bei gleichen Siegen Punkte);
  Zeilen y = top + 10 + 10i, Rang bei 50, Name 72, Farbe 19 + i. **Teams** `O3:6696`: Summen, Mitglieder `+0Fh`, leere Plätze
  ohne Zeile und ohne Rangschritt (`O3:6EB7`), Platz 10 ohne Vergleich (`O3:6F72`). **Statusfenster** `U431:0F1B`: Bar und
  Rahmen (10,25)–(310,175)/(12,27)–(308,173) Farbe 13, Titel 15 bei (160,35), `MoveTo(20,45)`, Farben 1/5/14; kein
  `SetTextStyle` im Fenster (stützt das `[APPROX]` DefaultFont). **Endergebnis:** Spalten (Score bis Spalte 24, Wins bis 31) passen
  zum Kopf.
- **Videobilder:** `bombtool render shop` vs. TW 00100 und `render score` vs. LP 00064 nebeneinander (2×): Kopfzeile, Knöpfe,
  Geldtexte, Kachelfolge (2 Spalten, 9 + 7), Bestände, „Inertia Damp.“ in Abwehrfarbe, Titel- und Wertungskasten, Spalten und
  Farben stimmen nach Lage und Form.

## 3. Ausgeführte Prüfungen

- `git ls-remote https://github.com/kaaasi/bomb-referenz HEAD` → `70201591…`; `tools/claude/fetch-reference.sh` (ohne Argumente),
  `tools/claude/setup-workspace.sh ~/referenz/original/gamedata`, `dotnet --version` = 10.0.402.
- `tools/pruefen.sh` (alle drei Läufe), Commit `e81df6b`, Ergebnis: bestanden

  | Lauf | bestanden | fehlgeschlagen | übersprungen |
  |---|---:|---:|---:|
  | Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 499 | 0 | 0 |
  | Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 435 | 0 | 42 |
  | Pflichtlauf ohne Daten (scheitert wie gewollt) | 435 | 64 | 0 |

- **Gegenproben** (temporärer Worktree auf dem Head, je eine Mutation, Lauf mit Originaldaten, betroffene Testklassen):

  | Mutation | Ergebnis |
  |---|---|
  | `ItemCatalog`: CRI 20/D 15000 → 14000 | rot (1, `ItemCatalogOriginalTests`) |
  | `Shop.Select`: `>= MaxQuantity` → `>` | rot (1) |
  | `Shop.Done`: `Account.Spent = Spent` entfernt | rot (1) |
  | `Shop.BuildOffers`: Bestandsbedingung entfernt | rot (2) |
  | `Shop.MinimumPrice`: `&& item.ForSale` entfernt | rot (2) |
  | `Standings`: Siegvergleich im Rang entfernt | **grün** (P3 oben) |
  | `TeamStandings`: Mitgliedersortierung umgekehrt | rot (6) |
  | `TeamStandings`: Siegvergleich im Rang entfernt | **grün** (P3 oben) |
  | `Shop.Deselect`: `Price` → `Price16` | grün – gleichwertig, alle käuflichen Preise < 32768 |
  | `ScoreScreen.Box`: unten 5n + 14 + 100 | rot (4) |
  | `ScoreTeamScreen`: 5000 ms → 2000 ms | rot (1) |
  | `ShopScreen`: Namensfarbe Waffen 4 → 5 | rot (2, mit Prüfsumme) |
  | `StatusWindow.FrameColor` 13 → 12 | rot (1) |
  | `ScoreResults`: `GotoXY(17)` → 18 | rot (2) |

- Architektur: `Core/Economy` ohne `UI`/MonoGame, kein `Random`/`DateTime`/`Stopwatch`; Wartezeiten der Wertung kommen über
  `Update(TimeSpan elapsed, …)`. Dateigrenzen: Diff nur in `Core/Economy`, `Screens/Score*|Shop*|Status*`, `Tools/RenderCommand.cs`,
  eine Zeile `Program.cs`, eigener Statusabschnitt in `docs/arbeitspakete.md`, eigene Tests und Anforderungen; `Surface.cs`,
  `ScreenInput.cs`, `Desktop/*` unverändert. Keine gesperrten Dateitypen im Diff.
- **Akzeptierte `[APPROX]`** (bestätigt, wie erwartet offen): „+“-Form `UCC8:017D`; fehlende Palettenrotation `UC79:003D` und
  Blenden (Einkauf `O3:23E2`, Einzelwertung `O3:65D1`, Teamwertung `O3:7472`); Rundung `Str(…:5:2)`; DefaultFont im
  Statusfenster; Tasten getippt statt gehalten; Prüfsummen ohne DOSBox-Bild; kein Desktop-Übergang (Runde fehlt).
- **Annahmen:** Gleichwertigkeit des exakten Mittelwertvergleichs mit dem Real48-Vergleich nur begründet, nicht nachgerechnet.
