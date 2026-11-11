# Vorlage: Einzelbilder aus Videos ablegen (nur Ablage, keine Analyse)

**Kurzaufruf** (der Nutzer schreibt ihn in einen neuen Chat und hängt die MP4-Dateien an; empfohlenes Modell:
**Claude Sonnet 5.5**):

> Repo kaaasi/bomb-redone. Arbeite nach `docs/vorlagen/video-bilder.md`. Videos: `<kurzname-1>` = `<Adresse 1>`,
> `<kurzname-2>` = `<Adresse 2>` (MP4 angehängt). Bildrate: `<1 Bild/s | …>`.
> Lege im Referenz-Repo kaaasi/bomb-referenz einen Branch `daten/videos-<kurzname>` an, committe dort die Bilder und die
> README und öffne einen PR gegen `main`. Diese GitHub-Schreibaktionen sind ausdrücklich autorisiert. Nicht mergen.

Zweck: Einzelbilder aus Gameplay-Videos des Originals als frühe Vorlage für Abläufe und Aufbau der Bildschirme
(`docs/entscheidungen.md`, Runde 1). **Keine Auswertung, keine Codeänderung, keine Bildbearbeitung.** Die Ablage im
Referenz-Repo erfolgt im Auftrag des Nutzers per PR; gemergt wird vom Nutzer.

## Ablauf für den Agenten

1. **Zugriff:** `add_repo` für `kaaasi/bomb-redone` (access `read`) und `kaaasi/bomb-referenz` (access `push`; ggf. erst per
   `ToolSearch` mit `select:mcp__claude-code-remote__add_repo` laden). Scheitert das, dem Nutzer melden und aufhören.
2. **Vorhandenes lesen:** im Referenz-Repo `videos/README.md` (Tabelle, Regeln). Ist ein Kurzname schon belegt, den Nutzer
   fragen.
3. **Arbeitsordner außerhalb beider Repos** (z. B. `~/videos-arbeit/<kurzname>/`); die MP4 nie in ein Repo kopieren. Das
   Video selbst wird **nicht** abgelegt, nur die Bilder.
4. **Video prüfen:** `ffprobe` (Auflösung, Bildrate, Dauer, Codec) und SHA-256 der MP4. Fehlt `ffmpeg`, dem Nutzer melden
   (nichts nachinstallieren, keinen Ersatzweg bauen).
5. **Bilder entnehmen**, unverändert in Größe und Ausschnitt, verlustfrei als PNG, Bildrate laut Kurzaufruf (Vorgabe 1 Bild/s):
   `ffmpeg -i <video> -vf fps=1 -start_number 0 <ziel>/%05d.png` – die Dateinummer ist damit die Sekunde im Video.
6. **Entdoppeln:** Bild weglassen, wenn es mit dem zuletzt behaltenen bytegleich dekodiert (gleiche Pixel). Nicht nach
   Ähnlichkeit ausdünnen, nichts zuschneiden, nichts skalieren. Die Nummern der behaltenen Bilder bleiben unverändert.
7. **Sichtprüfung an Stichproben** (erstes, letztes, etwa jedes hundertste Bild ansehen): Ist es das Spiel? Wo liegt das
   Spielbild im Videobild (Rand, Einblendungen)? Welche Fassung ist erkennbar (Titelbild, Versionsangabe)? Nur notieren.
8. **Ablage:** Branch `daten/videos-<kurzname>` vom aktuellen `main` des Referenz-Repos, Bilder nach `videos/<kurzname>/`.
   In `videos/README.md` je Video eine Tabellenzeile und darunter ein Abschnitt mit: Adresse, Titel und Kanal (vom Nutzer),
   Abrufdatum, SHA-256 und Maße der MP4, Bildrate, genauer `ffmpeg`-Aufruf, Anzahl entnommen und behalten, Lage des
   Spielbildes, erkennbare Fassung, Auffälligkeiten. Commit mit Agenten-Trailer.
9. **Größe:** je Commit und Push höchstens rund 50 MB (sonst lehnt der Server den Push ab), also in Abschnitten committen
   und pushen. Ergibt ein Video mehr als rund 300 MB Bilder, vor der Ablage den Nutzer fragen (geringere Bildrate oder
   Ausschnitt des Videos).
10. **PR** gegen `main` des Referenz-Repos, ein PR je Video (oder einer für beide, wenn der Nutzer es so will);
    **nicht mergen**. Keine Änderungen am Code-Repo.
11. **Antwort an den Nutzer knapp:** PR-Link, Anzahl Bilder, erkannte Fassung, Auffälligkeiten.

## Grenzen dieser Quelle

Videobilder sind skaliert und verlustbehaftet komprimiert; sie belegen Abläufe, Aufbau und Bedienung, aber keine
pixelgenaue Übereinstimmung. Dafür gelten Bildschirmfotos aus DOSBox (`docs/vorlagen/dosbox-aufnahme.md`).
