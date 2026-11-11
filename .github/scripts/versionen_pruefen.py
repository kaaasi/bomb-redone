#!/usr/bin/env python3
"""Checks that tool and package versions are pinned.

- global.json in the root folder with an exact SDK version (x.y.zzz)
- every <PackageReference> in *.csproj / Directory.*.props with an exact version (no * and no ranges)

Usage: versionen_pruefen.py [root]   -> exit 1 and the list of violations, otherwise exit 0.
"""
import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

EXACT = re.compile(r"^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.-]+)?$")


def check(root: pathlib.Path) -> list[str]:
    errors = []
    gj = root / "global.json"
    if not gj.exists():
        errors.append("global.json fehlt (SDK-Version nicht festgelegt)")
    else:
        sdk = json.loads(gj.read_text(encoding="utf-8")).get("sdk", {})
        if not EXACT.match(str(sdk.get("version", ""))):
            errors.append(f"global.json: sdk.version ist nicht exakt: {sdk.get('version')!r}")
    files = [p for p in root.rglob("*") if p.suffix == ".csproj" or p.name.startswith("Directory.") and p.suffix == ".props"]
    for f in sorted(files):
        if any(part in ("bin", "obj", "nuget-packages") for part in f.parts):
            continue
        for ref in ET.parse(f).getroot().iter("PackageReference"):
            version = ref.get("Version") or ref.findtext("Version") or ""
            if not EXACT.match(version.strip()):
                errors.append(f"{f.relative_to(root)}: {ref.get('Include')} Version {version!r} ist nicht exakt")
    return errors


if __name__ == "__main__":
    errors = check(pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "."))
    for e in errors:
        print(f"::error title=Versionen::{e}")
    print("Versionen festgeschrieben." if not errors else f"{len(errors)} Verstoß/Verstöße.")
    sys.exit(1 if errors else 0)
