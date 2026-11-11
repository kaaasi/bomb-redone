#!/usr/bin/env bash
# Uniform check before every PR - the same for all agents (see AGENTS.md, section "Tests").
#
#   tools/pruefen.sh            build + three test runs (integration, test infrastructure), table for the PR
#   tools/pruefen.sh --schnell  build + one run: with original data if present, otherwise without (normal PR)
#
# Runs:
#   1. with original data     BOMB_REQUIRE_GAMEDATA=1 - fails if gamedata/ is missing (only if present)
#   2. without original data  BOMB_SKIP_GAMEDATA=1    - like CI: original-data tests visibly skipped
#   3. mandatory run without data (both variables)    - MUST fail; otherwise original-data tests run silently empty
# Exit code 0 only if the build has no warnings, run 1 (if data exists) and run 2 are green and run 3 fails.
set -uo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
SLN=BombRedone.slnx
QUICK=0; [ "${1:-}" = "--schnell" ] && QUICK=1
LOG="$(mktemp -d)"
STATUS=0

# Every test project must produce a result line; a missing one means a crashed or unloaded project.
PROJEKTE=$(ls tests/*/*.csproj 2>/dev/null | wc -l)
ergebnisse_da() {
  local n; n=$(grep -cE "^(Passed|Failed)! " "$1")
  if [ "$n" -ne "$PROJEKTE" ]; then
    echo "FEHLER: $2 lieferte Ergebnisse für $n von $PROJEKTE Testprojekten (Absturz oder Ladefehler) – Protokoll: $1"
    tail -15 "$1"; STATUS=1; return 1
  fi
}

# Sums the lines "Passed!/Failed! - Failed: x, Passed: y, Skipped: z" of all test projects.
summe() {
  grep -E "^(Passed|Failed)! " "$1" | sed -E 's/.*Failed: *([0-9]+), Passed: *([0-9]+), Skipped: *([0-9]+).*/\1 \2 \3/' |
    awk '{f+=$1; p+=$2; s+=$3} END {printf "%d %d %d", p, f, s}'
}

echo "== Build (Release, Warnungen = Fehler)"
if ! dotnet build "$SLN" -c Release -warnaserror -nologo -v q > "$LOG/build.txt" 2>&1; then
  tail -30 "$LOG/build.txt"; echo "Build fehlgeschlagen."; exit 1
fi

HAVE_DATA=0; ls gamedata/ 2>/dev/null | grep -qix 'BOMB\.EXE' && HAVE_DATA=1
ROWS=()

if [ $HAVE_DATA = 1 ]; then
  echo "== Tests mit Originaldaten"
  BOMB_REQUIRE_GAMEDATA=1 dotnet test "$SLN" -c Release --no-build -nologo -v q > "$LOG/mit.txt" 2>&1
  rc=$?; ergebnisse_da "$LOG/mit.txt" "Lauf mit Originaldaten"; read -r p f s <<< "$(summe "$LOG/mit.txt")"
  ROWS+=("| Mit Originaldaten (\`BOMB_REQUIRE_GAMEDATA=1\`) | $p | $f | $s |")
  [ $rc -ne 0 ] && STATUS=1
  # Skipped tests in the run with data need an explanation (known open findings).
  [ "$s" != "0" ] && echo "Hinweis: $s Tests trotz Originaldaten übersprungen – im PR begründen."
else
  echo "Keine Originaldaten (gamedata/BOMB.EXE fehlt) – Lauf mit Originaldaten entfällt. Im PR angeben!"
  ROWS+=("| Mit Originaldaten | – | – | – (keine Daten vorhanden) |")
fi

if [ $QUICK = 0 ] || [ $HAVE_DATA = 0 ]; then
  echo "== Tests ohne Originaldaten"
  BOMB_SKIP_GAMEDATA=1 dotnet test "$SLN" -c Release --no-build -nologo -v q > "$LOG/ohne.txt" 2>&1
  rc=$?; ergebnisse_da "$LOG/ohne.txt" "Lauf ohne Originaldaten"; read -r p f s <<< "$(summe "$LOG/ohne.txt")"
  ROWS+=("| Ohne Originaldaten (\`BOMB_SKIP_GAMEDATA=1\`) | $p | $f | $s |")
  [ $rc -ne 0 ] && STATUS=1
fi

if [ $QUICK = 0 ]; then
  echo "== Pflichtlauf ohne Daten (muss scheitern)"
  BOMB_SKIP_GAMEDATA=1 BOMB_REQUIRE_GAMEDATA=1 dotnet test "$SLN" -c Release --no-build -nologo -v q \
    --logger "trx;LogFilePrefix=pflicht" --results-directory "$LOG/pflicht" > "$LOG/pflicht.txt" 2>&1
  rc=$?; read -r p f s <<< "$(summe "$LOG/pflicht.txt")"
  ergebnisse_da "$LOG/pflicht.txt" "Pflichtlauf ohne Daten"; vollstaendig=$?
  # Only a failure BECAUSE of missing original data is wanted: results present, at least one failure,
  # and every failure reports the missing original data. A crash or other errors count as NOT passed.
  grund=$(python3 - "$LOG/pflicht" "$PROJEKTE" <<'PY'
import pathlib, sys, xml.etree.ElementTree as ET
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
failed = other = 0
files = sorted(pathlib.Path(sys.argv[1]).glob("*.trx"))
if len(files) != int(sys.argv[2]):
    print(f"trx-dateien:{len(files)}-von-{sys.argv[2]}"); sys.exit()
for trx in files:
    root = ET.parse(trx).getroot()
    # Aborted run of a project (test host crash): ResultSummary "Aborted"/"Error" or a RunInfo that reports
    # an abort. (RunInfo "Error" alone is normal: xUnit writes every [FAIL] line there.)
    summary = root.find(".//t:ResultSummary", ns)
    texts = " ".join((i.findtext("t:Text", default="", namespaces=ns) or "").lower() for i in root.iterfind(".//t:RunInfo", ns))
    if summary is not None and summary.get("outcome") in ("Aborted", "Error") \
            or "aborted" in texts or "crash" in texts:
        print(f"abgebrochen:{trx.name}"); sys.exit()
    for r in root.iterfind(".//t:UnitTestResult", ns):
        if r.get("outcome") == "Failed":
            failed += 1
            msg = " ".join(m.text or "" for m in r.iterfind("t:Output/t:ErrorInfo/t:Message", ns))
            if "Original data missing" not in msg:
                other += 1
print("keine-ergebnisse" if failed == 0 else f"andere-fehler:{other}" if other else "ok")
PY
)
  if [ $rc -ne 0 ] && [ $vollstaendig -eq 0 ] && [ "$grund" = "ok" ]; then
    ROWS+=("| Pflichtlauf ohne Daten (scheitert wie gewollt) | $p | $f | $s |")
  else
    ROWS+=("| Pflichtlauf ohne Daten | $p | $f | $s – **FEHLER: kein Scheitern wegen fehlender Originaldaten ($grund, Exit $rc)** |"); STATUS=1
  fi
fi

echo
echo "| Lauf | bestanden | fehlgeschlagen | übersprungen |"
echo "|---|---:|---:|---:|"
printf '%s\n' "${ROWS[@]}"
echo
echo "Commit: $(git rev-parse HEAD)$(git diff --quiet && git diff --cached --quiet || echo ' (mit nicht eingecheckten Änderungen!)')"
echo "SDK: $(dotnet --version)"
[ $STATUS -ne 0 ] && echo "Ergebnis: NICHT bestanden (Protokolle in $LOG)" || echo "Ergebnis: bestanden"
exit $STATUS
