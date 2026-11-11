## Merge-Empfehlung

**Nein** für Head `86f27198839cc921b161bfafb97e609cf1fe29c5` (Base `a0c6a3f76e188ec37d5e621445e6dfaad1e84757`): Die Ignore-Regeln lassen vorgesehene umbenannte Original-Konfigurationen und Aufnahmen außerhalb der Schutzordner durch und erfassen zugleich eigene Dateien. PR #1 war zum Reviewzeitpunkt bereits gemergt; das Urteil bezieht sich auf den genannten Head.

## Befunde

1. **P2 – Umbenannte Original-Konfigurationen und Aufnahmen sind nicht allgemein geschützt.** [`.gitignore` Zeilen 14–23](https://github.com/kaaasi/bomb-redone/blob/86f27198839cc921b161bfafb97e609cf1fe29c5/.gitignore#L14-L23) schützt nur `BOMB*.CFG` und den Ordner `capture/`. Die [Aufnahmevorlage](https://github.com/kaaasi/bomb-redone/blob/86f27198839cc921b161bfafb97e609cf1fe29c5/docs/vorlagen/dosbox-aufnahme.md#L23-L25) sieht `<zweck>.CFG` und PNG-Bilder vor. Probe im Checkout: `git check-ignore -v --no-index originaldaten/zweck.CFG` und `git check-ignore -v --no-index aufnahmen/sitzung_01_start.png` geben jeweils nichts aus und enden mit Exit 1. Solche Dateien erscheinen damit bei Ablage außerhalb der ignorierten Ordner als unversioniert und können versehentlich aufgenommen werden. Ein genereller Dateinamenschutz für beliebige PNGs und CFGs ist nicht ohne Kollision mit eigenen Dateien möglich; Ablage und Schutzgrenzen müssen zusammen festgelegt werden.
2. **P2 – Eigene Projektdateien werden durch Originalmuster ignoriert.** [`.gitignore` Zeilen 8–13](https://github.com/kaaasi/bomb-redone/blob/86f27198839cc921b161bfafb97e609cf1fe29c5/.gitignore#L8-L13): `git check-ignore -v --no-index assets/BombRedone.ico eigene/BombWerkzeug.exe` meldet Zeile 12 bzw. 8. Eigene Icons und ausführbare Werkzeuge mit naheliegendem Projektnamen werden dadurch bei `git add` übergangen, obwohl es keine Originaldateien sein müssen.

## Ausgeführte Prüfungen

- Exakter sauberer Checkout: `git rev-parse HEAD` = genannter Head, `git status --porcelain` leer vor Tests. Referenzdaten nur lesend via ignoriertem `gamedata`-Symlink; `(cd /workspace/bomb-referenz/original && sha256sum -c --quiet SHA256SUMS)` erfolgreich.
- SDK `10.0.402`; Offline-Restore aus dem Werkzeug-Release mit `NuGetAudit=false`, danach `dotnet build BombRedone.slnx -c Release --no-restore -warnaserror`: 0 Warnungen, 0 Fehler. `tools/pruefen-selbsttest.sh`: sechs Fälle bestanden. `tools/pruefen.sh`:

| Lauf | bestanden | fehlgeschlagen | übersprungen |
|---|---:|---:|---:|
| Mit Originaldaten (`BOMB_REQUIRE_GAMEDATA=1`) | 28 | 0 | 0 |
| Ohne Originaldaten (`BOMB_SKIP_GAMEDATA=1`) | 19 | 0 | 5 |
| Pflichtlauf ohne Daten (scheitert wie gewollt) | 19 | 9 | 0 |

- Die vier TRX-Dateien des Pflichtlaufs separat mit Python/XML ausgewertet: 1+1+6+1 Fehler, alle neun enthalten `Original data missing`; kein anderer Fehler in diesem konkreten Lauf. Der Selbsttest verwirft grünen Pflichtlauf, Absturz, zwei Teilabstürze und fremden Fehler.
- `git check-ignore -v` für alle 13 Archivnamen, `bomb32.zip`, `foo.TWS`, `BOMB.CFG`: alle ignoriert; Gegenproben siehe Befunde. `python3 .github/scripts/versionen_pruefen.py .`: bestanden.
- Suche `rg -ni 'eishockey|hockey|liga|verein|mannschaft|saison|torwart|stadion|puck' AGENTS.md CLAUDE.md docs .github tools`: kein fremder fachlicher Inhalt. `docs/prozess/umgebung.md:4` nennt die Vorlage ausdrücklich als Herkunft; `docs/arbeitspakete.md:17` nennt das andere Repo als zeitweilige Werkzeugquelle. Das sind erkennbare Herkunfts-/Übergangshinweise, keine übernommene Fachlogik.
- Keine einzelnen Testmutationen ausgeführt; kein Pixelvergleich oder Fensterstart. Für das noch leere Fenster liegen keine Vergleichsbilder vor. Das Werkzeug-Release existiert inzwischen auf GitHub; der Zustand vor dem Merge wurde nicht nachträglich behauptet. Der laut PR nicht erfolgte Fensterstart bleibt offen.
- Akzeptiertes `[APPROX]`: optionale 4:3-Darstellung in `Presentation` gemäß E6, nicht als belegte Originaldarstellung bewertet. Annahmen: keine weiteren.
