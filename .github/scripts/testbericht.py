#!/usr/bin/env python3
"""Test report split into synthetic tests, executed original-data tests and skipped tests.

Usage: testbericht.py <results folder>
Expects the TRX files of the two runs in <results folder>/synthetisch and <results folder>/originaldaten
(filters "Category!=OriginalData" and "Category=OriginalData", see .github/workflows/build.yml).
The Markdown report is appended to $GITHUB_STEP_SUMMARY (locally: stdout); warnings go to stdout as
GitHub annotations. The report text is German like the rest of the project documentation.
"""
import io
import os
import pathlib
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def results(folder: pathlib.Path):
    """Yields (test name, outcome, skip reason) of all tests in the TRX files of the folder."""
    for trx in sorted(folder.glob("*.trx")):
        root = ET.parse(trx).getroot()
        for r in root.iterfind(".//t:UnitTestResult", NS):
            reason = " ".join(m.text or "" for m in r.iterfind("t:Output/t:ErrorInfo/t:Message", NS))
            yield r.get("testName"), r.get("outcome"), reason


def main() -> int:
    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    report = io.StringIO() if summary_path else sys.stdout
    annotations = []
    status = _report(report, annotations)
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as f:
            f.write(report.getvalue())
    for line in annotations:
        print(line)
    return status


def _report(report, annotations) -> int:
    base = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "TestResults")
    groups = {name: list(results(base / name)) for name in ("synthetisch", "originaldaten")}

    def count(group, outcome):
        return sum(1 for _, o, _ in groups[group] if o == outcome)

    synthetic_run = count("synthetisch", "Passed") + count("synthetisch", "Failed")
    data_run = count("originaldaten", "Passed") + count("originaldaten", "Failed")
    skipped = sorted((n, why) for g in groups.values() for n, o, why in g if o == "NotExecuted")
    failed = sorted(n for g in groups.values() for n, o, _ in g if o == "Failed")

    print("## Testbericht\n", file=report)
    print("| Art | ausgeführt | davon fehlgeschlagen |", file=report)
    print("|---|---:|---:|", file=report)
    print(f"| Synthetisch (ohne Originaldaten) | {synthetic_run} | {count('synthetisch', 'Failed')} |", file=report)
    print(f"| Mit Originaldaten | {data_run} | {count('originaldaten', 'Failed')} |", file=report)
    print(f"| **Übersprungen** | **{len(skipped)}** | – |", file=report)
    print(file=report)
    if data_run == 0:
        print("> **Originaldaten in diesem Lauf nicht geprüft.** Grün belegt nur den synthetischen Teil, nicht die "
              "Originaltreue. Der Lauf mit Originaldaten wird lokal ausgeführt und im PR mit Zahlen nachgewiesen.\n",
              file=report)
    if skipped:
        print(f"<details><summary>Übersprungene Tests ({len(skipped)}) – nicht geprüft</summary>\n", file=report)
        for name, why in skipped:
            print(f"- `{name}` – {why.strip() or 'ohne Grund'}", file=report)
        print("\n</details>\n", file=report)
        annotations.append(f"::warning title=Übersprungene Tests::{len(skipped)} Tests wurden übersprungen "
                           "(Gründe im Testbericht). Sie gelten nicht als geprüft.")
    if failed:
        print(f"### Fehlgeschlagen ({len(failed)})\n", file=report)
        for name in failed:
            print(f"- `{name}`", file=report)
        print(file=report)
    if synthetic_run == 0:
        annotations.append("::error title=Testbericht::Keine synthetischen Tests gefunden.")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
