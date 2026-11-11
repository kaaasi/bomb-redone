#!/usr/bin/env bash
# Loads folders of the private repository kaaasi/bomb-referenz into ~/referenz (sparse clone, only the folders named).
#   tools/claude/fetch-reference.sh [original] [schriften] [bilder] [bilder/<aufnahme> ...]
# `original` = original files (ZIP, gamedata/, SHA256SUMS) and save games from DOSBox (original/spielstaende/).
# `schriften` = reference font of DOSBox 0.74-3 (R1-schrift-1 = D, GPL; SHA256SUMS there).
# `bilder`   = all reference images; `bilder/<aufnahme>` = one capture series (index: bilder/README.md there).
# Without arguments: original and schriften. Target: ~/referenz, changeable with REFERENZ=<path>.
# An existing clone is never updated by this script: if it is outdated, the script stops with exit code 3.
# Never copy anything from there into this repository.
set -euo pipefail
REPO_URL="https://github.com/kaaasi/bomb-referenz"
TARGET="${REFERENZ:-$HOME/referenz}"
FOLDERS=("$@")
[ ${#FOLDERS[@]} -eq 0 ] && FOLDERS=(original schriften)

if [ ! -d "$TARGET/.git" ]; then
  git clone -q --depth 1 --filter=blob:none --sparse "$REPO_URL" "$TARGET"
else
  # Existing clone: the script only checks that the target is the root of a clone whose origin is exactly the
  # reference repository, fetches main into .git and stops if the clone is not on that commit. In that case
  # delete the folder yourself (save your own files in it first) and call the script again.
  top="$(git -C "$TARGET" rev-parse --show-toplevel 2>/dev/null || true)"
  url="$(git -C "$TARGET" remote get-url origin 2>/dev/null || true)"
  case "$url" in
    "$REPO_URL"|"$REPO_URL.git"|git@github.com:kaaasi/bomb-referenz.git) ;;
    *) url="" ;;
  esac
  if [ -z "$url" ] || [ "$top" != "$(cd "$TARGET" && pwd -P)" ]; then
    echo "Abbruch: $TARGET ist kein Klon von $REPO_URL." >&2
    exit 2
  fi
  git -C "$TARGET" fetch -q --depth 1 --filter=blob:none origin main
  have="$(git -C "$TARGET" rev-parse HEAD)"
  want="$(git -C "$TARGET" rev-parse FETCH_HEAD)"
  if [ "$have" != "$want" ]; then
    echo "Abbruch: $TARGET steht auf ${have:0:7}, das Referenz-Repo auf ${want:0:7}. Ordner löschen (eigene Dateien" \
      "darin vorher sichern) und erneut aufrufen; das Skript klont dann neu." >&2
    exit 3
  fi
fi
git -C "$TARGET" sparse-checkout add "${FOLDERS[@]}"
missing=0
for f in "${FOLDERS[@]}"; do
  if [ -d "$TARGET/$f" ]; then
    echo "$f: $(find "$TARGET/$f" -type f | wc -l) Dateien in $TARGET/$f"
  else
    echo "$f: fehlt im Referenz-Repo (Name prüfen, README dort)" >&2
    missing=1
  fi
done
if [ -f "$TARGET/original/SHA256SUMS" ]; then
  (cd "$TARGET/original" && sha256sum -c --quiet SHA256SUMS) && echo "original: Prüfsummen stimmen" || { echo "original: Prüfsummen weichen ab" >&2; missing=1; }
fi
if [ -f "$TARGET/schriften/SHA256SUMS" ]; then
  (cd "$TARGET/schriften" && sha256sum -c --quiet SHA256SUMS) && echo "schriften: Prüfsummen stimmen" || { echo "schriften: Prüfsummen weichen ab" >&2; missing=1; }
fi
exit "$missing"
