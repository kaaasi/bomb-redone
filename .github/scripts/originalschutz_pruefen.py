#!/usr/bin/env python3
"""Fails if the repository tracks material of the original game.

.gitignore only prevents accidents with known names and folders. This check looks at what is actually tracked:

1. content: SHA-256 of a tracked file equals one of the original files (catches renamed copies);
2. kind: file types that only occur as original material or captures (save games, raw images, screenshots,
   videos, archives, DOS binaries) unless the path is listed in .github/originalschutz-erlaubt.txt.

Usage: originalschutz_pruefen.py [root]   -> exit 1 and the list of violations, otherwise exit 0.
The hashes below identify the files of Tank Wars 3.2 (reference repository, original/SHA256SUMS); they are
not original content.
"""
import hashlib
import pathlib
import subprocess
import sys

ORIGINAL_SHA256 = {
    "8c38643090097d1601fb404163dc91d41890ed955e82b03673f2b6705e7b7e8c": "bomb32.zip",
    "bf65f84fdfa038ce547f5f00b18af70e0f05eb11d666417304c0d111a78aea50": "BOMB.DOC",
    "659ace6b9a97c30191a8f65cf25f22322c087166909e9fa1d522e529adbafd9d": "BOMB.EXE",
    "d877e236b3973931c4a573600d9b15c5b3a75c1d897c6df954e88314b74418ca": "BOMB.ICO",
    "3d50930c0603b2cf492a7f2b45169e3909290804841103d298ad49506be0bd44": "BOMB.ICP",
    "8186d7d4190350992a2a1b7036d9ba62cc4e300c3e9fd3ce901eddbcad5b466f": "BOMB.OVR",
    "7397f06d7c17e6e09338b27e9a4e5e245a2d23374382ab600395fcb08f856a68": "BOMB.TXT",
    "58043d41950338eae50dde4012facf1011f8c97d1659b98b37355b85d6e64a60": "BOMB1.RAW",
    "b827bfbc3a687c6f043c0201de8d345ad455943d10d2b0fcf5eb73fd33033b08": "BOMB2.RAW",
    "c908eece6b3417fd57df83ca14379c6f4d9b935608436991a92c044f60dae55f": "BOMBCFG.EXE",
    "2aeec782555dd5ead5a6cb55c7af47758826439c0a52a77529968fc0221bd0e9": "BOMBCFG.HLP",
    "e2595f44b1b7c324676cf04d59b534e87290ac0c179f26e7bbe1f8a8134111e6": "BOMBCFG.ICO",
    "723ec9be085e406d06878bd10fa68d4a32e6846f89c93bfd10e4b707a6be406f": "BOMBCFG.ICP",
    "c001585d62ceec52b9b65c99872497d8a520d0218f739df5994a1640cda55f9f": "REVISION.HST",
}

# Types that need an explicit entry in the allow list before they may be tracked.
GUARDED_SUFFIXES = {
    ".tws", ".cfg", ".raw", ".ovr", ".hlp", ".icp", ".hst", ".exe", ".com", ".zip", ".7z", ".rar",
    ".png", ".bmp", ".gif", ".jpg", ".jpeg", ".pcx", ".ico", ".zmbv", ".avi", ".mp4", ".mkv", ".webm",
    ".lst", ".asm",
}

ALLOW_FILE = ".github/originalschutz-erlaubt.txt"


def tracked_files(root: pathlib.Path) -> list[str]:
    out = subprocess.run(["git", "-C", str(root), "ls-files", "-z"], check=True, capture_output=True).stdout
    return [name for name in out.decode("utf-8").split("\0") if name]


def allowed_paths(root: pathlib.Path) -> set[str]:
    path = root / ALLOW_FILE
    if not path.exists():
        return set()
    lines = (line.strip() for line in path.read_text(encoding="utf-8").splitlines())
    return {line for line in lines if line and not line.startswith("#")}


def check(root: pathlib.Path) -> list[str]:
    errors = []
    allowed = allowed_paths(root)
    for name in tracked_files(root):
        path = root / name
        if not path.is_file():
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest in ORIGINAL_SHA256:
            errors.append(f"{name}: Inhalt ist die Originaldatei {ORIGINAL_SHA256[digest]}")
        elif path.suffix.lower() in GUARDED_SUFFIXES and name not in allowed:
            errors.append(f"{name}: Dateityp {path.suffix.lower()} ist nur mit Eintrag in {ALLOW_FILE} erlaubt")
    return errors


if __name__ == "__main__":
    errors = check(pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "."))
    for e in errors:
        print(f"::error title=Originalschutz::{e}")
    print("Keine Originaldateien eingecheckt." if not errors else f"{len(errors)} Verstoß/Verstöße.")
    sys.exit(1 if errors else 0)
