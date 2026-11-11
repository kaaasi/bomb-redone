#!/usr/bin/env bash
# Self-test of the protection of original material: ignore rules and the check of tracked files.
# Works on a temporary repository with synthetic files only; needs git and python3, no original data.
set -uo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"
fehler=0

git init -q "$TMP/repo"
cp "$ROOT/.gitignore" "$TMP/repo/"
mkdir -p "$TMP/repo/.github/scripts"
cp "$ROOT/.github/scripts/originalschutz_pruefen.py" "$TMP/repo/.github/scripts/"

ignoriert() {  # $1 = path, $2 = expected (ja|nein)
  if git -C "$TMP/repo" check-ignore -q --no-index "$1"; then ist=ja; else ist=nein; fi
  if [ "$ist" = "$2" ]; then echo "ok: $1 ignoriert: $ist"
  else echo "FEHLER: $1 ignoriert: $ist, erwartet $2"; fehler=1; fi
}

# Original names (any case, any folder) and the protected folders
for f in gamedata/x BOMB.EXE sub/bomb.exe BOMBCFG.EXE BOMB.OVR bomb1.raw BOMB2.RAW BOMBCFG.HLP BOMB.ICO BOMBCFG.ICO \
         BOMB.ICP BOMBCFG.ICP BOMB.CFG BOMB.DOC BOMB.TXT REVISION.HST bomb32.zip spiel/GAME1.TWS \
         aufnahmen/sitzung_01_start.png aufnahmen/zweck.CFG capture/a.png bilder/runde-1/a.png videos/v1/00001.png \
         analyse/bomb.lst export/BOMB1.png referenz/original/x original/x; do
  ignoriert "$f" ja
done
# Own files must stay addable
for f in assets/BombRedone.ico eigene/BombWerkzeug.exe docs/analyse/re-grundlagen.md src/BombRedone.Formats/RawImage.cs \
         docs/bilder.md BombRedone.slnx; do
  ignoriert "$f" nein
done

pruefung() {  # $1 = description, $2 = expected (bestanden|abgelehnt)
  git -C "$TMP/repo" add -A -f . >/dev/null 2>&1
  if python3 "$TMP/repo/.github/scripts/originalschutz_pruefen.py" "$TMP/repo" > "$TMP/out.txt" 2>&1
  then ist=bestanden; else ist=abgelehnt; fi
  if [ "$ist" = "$2" ]; then echo "ok: $1 → $ist"
  else echo "FEHLER: $1 → $ist, erwartet $2"; cat "$TMP/out.txt"; fehler=1; fi
}

mkdir -p "$TMP/repo/src" "$TMP/repo/docs"
echo "class A {}" > "$TMP/repo/src/A.cs"
pruefung "nur eigener Quelltext" bestanden
printf 'synthetic' > "$TMP/repo/docs/zweck.CFG"
pruefung "umbenannte Konfiguration außerhalb der Schutzordner (erzwungen hinzugefügt)" abgelehnt
rm "$TMP/repo/docs/zweck.CFG"; git -C "$TMP/repo" rm -q --cached docs/zweck.CFG
printf 'synthetic' > "$TMP/repo/docs/sitzung_01_start.png"
pruefung "Bildschirmfoto außerhalb der Schutzordner" abgelehnt
mkdir -p "$TMP/repo/.github"; echo "docs/sitzung_01_start.png" > "$TMP/repo/.github/originalschutz-erlaubt.txt"
pruefung "dieselbe Datei mit Eintrag in der Erlaubnisliste" bestanden

rm -rf "$TMP"
exit $fehler
