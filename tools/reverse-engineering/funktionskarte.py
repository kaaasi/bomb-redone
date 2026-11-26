#!/usr/bin/env python3
"""Function map of BOMB.EXE / BOMB.OVR and its coverage by the remake.

Adopted from eishockey-manager-redone (tools/reverse-engineering/funktionskarte.py, em.exe) on 2026-11-26 and
changed for Tank Wars 3.2 (Turbo Pascal 6.0, overlays in a separate file). Differences are listed in
docs/analyse/abdeckung-grafik/README.md ("Abweichungen von der Vorlage").

Usage:
    python3 tools/reverse-engineering/funktionskarte.py <gamedata dir> [--repo .] [--write] [--csv out.csv]

<gamedata dir> holds BOMB.EXE and BOMB.OVR. Without --write the generated Markdown is printed to stdout. With
--write the block between the markers in docs/analyse/re-abdeckung.md is replaced (hand-written text outside the
markers stays; the file is created if missing). Needs `ndisasm` (nasm). Reads the original files only; nothing
from them is written except addresses, sizes and short text snippets used as hints (no code, no graphics).

Steps:
 1. Layout from bombre.Game (docs/analyse/re-grundlagen.md sections 1 and 2): code segments of the load image
    (without the overlay stubs and the data segment 14D5) and the ten overlay units of BOMB.OVR. Segment 0431
    is split: its code, then the four embedded BGI files (VGA256 driver, fonts LITT, EURO, TINY; findings F1-F4),
    each one data block without functions.
 2. Function starts: segment starts, stub entries, prologs (push bp; mov bp,sp = 55 89 E5 in Turbo Pascal),
    near and far call targets (far calls to a stub entry resolve into the overlay). A function ends where the
    next one starts (same segment/overlay).
 3. Coverage: addresses in src/**/*.cs comment blocks ([ORIGINAL]/[APPROX]) and docs/**/*.md (except
    docs/archiv/, docs/reviews/ and the generated or inventory docs, NOT_EVIDENCE_DOCS) are mapped onto the
    functions. Forms: On:XXXX and On[k] (overlay n, offset or stub entry k), [U]SSSS:XXXX (segment of the load
    image), "BOMB.EXE 0x..." / "BOMB.OVR 0x..." (file offsets; later bare 0x values of the same sentence belong
    to the file named last). A bare 0x value without a file name counts only if it is the exact start of a
    function in exactly one of the two files. Values after DS:, CS:, ES:, "+", "[" or in a line naming DGROUP/DS
    values are never addresses. A stub-entry call ("O2[3]") under [ORIGINAL] only says that the call is
    original: if another hit marks the same function [APPROX] and only calls back the "umgesetzt", the function
    counts as "genähert".
 4. Layout constants (file offsets of segment and overlay starts, header size 0x16A0, data segment 0x163F0) are
    not references, as in the template (CONSTANTS); the BGI data blocks stay addressable by their start.
 5. Hints: Pascal strings loaded by the function (mov di,imm; push cs), callers.

Each hit keeps the line of the marker it belongs to (the nearest [ORIGINAL]/[APPROX] before it in the same
comment block, otherwise the next one); tools/reverse-engineering/abdeckung_grafik.py uses that to take the class
from the marker list.
"""

from __future__ import annotations

import argparse
import bisect
import csv
import re
import struct
import subprocess
import sys
import tempfile
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bombre  # noqa: E402  (layout of the original files, re-grundlagen)

DATA_SEG = 0x14D5
OVR_BASE = 0x100000  # internal address space: EXE file offsets below, BOMB.OVR offsets from here
MIN_LIST_SIZE = 64
THIN_SIZE = 2048

# Embedded BGI files in segment 0431 (re-grundlagen F1-F4): start in BOMB.EXE, name. Each runs to the next one,
# the last one to the end of the segment.
BGI_BLOCKS = [(0x07EB7, "BGI-Treiber VGA256"), (0x08B90, "BGI-Schrift LITT"), (0x09F9B, "BGI-Schrift EURO"),
              (0x0C0AE, "BGI-Schrift TINY")]

STATUS_ORDER = ["umgesetzt", "genähert", "dokumentiert", "unbekannt"]

# Role of the EXE segments (docs/analyse/re-grundlagen.md section 1). "System" = platform layer the remake replaces
# by .NET/MonoGame (Borland units, drivers, memory); left out of the gap lists.
BASE_KIND = {
    0x0000: ("Spiel", "Hauptprogramm"),
    0x0059: ("System", "Speicherübersicht, EMS-/Overlay-Meldungen"),
    0x0129: ("Spiel", "Spieleinheit U129 (Computergegner, Kandidat)"),
    0x0431: ("Spiel", "Spieleinheit U431: Dialoge, Statusfenster"),
    0x0B25: ("Spiel", "Einheit UB25: Sin/Cos-Tabellen"),
    0x0C02: ("System", "Notsicherung „BOSS“ bei Speichermangel"),
    0x0C48: ("System", "Tastatur-Handler INT 09h („XKEY“)"),
    0x0C79: ("Spiel", "Palettenrotation („CYCLER“)"),
    0x0CC8: ("Spiel", "eigener VGA-Zugriff (Modus 13h, DAC)"),
    0x0DBD: ("System", "Grafikstart (Treiber/Schriften anmelden, Fehlermeldung)"),
    0x0E11: ("System", "Maus (INT 33h)"),
    0x0E22: ("System", "Borland Printer"),
    0x0E26: ("System", "Borland Graph (BGI-Kern)"),
    0x11E6: ("System", "Borland Dos"),
    0x11F8: ("System", "Borland Crt"),
    0x125A: ("System", "Borland Overlay (EMS)"),
    0x12CE: ("System", "Borland System (TP-6-Laufzeit, Real48, Zufall)"),
}


@dataclass
class Region:
    """A code area: EXE segment, overlay unit or data block."""
    name: str          # "Seg 0431", "O4", "Seg 0431 BGI-Schrift EURO"
    key: str
    file: str          # "BOMB.EXE" / "BOMB.OVR"
    start: int         # internal address (file offset, + OVR_BASE for BOMB.OVR)
    size: int
    seg: int | None = None       # segment value in the load image
    ovl: int | None = None       # overlay number 1..10
    stub: int | None = None      # stub segment of the overlay
    entries: list[int] = field(default_factory=list)  # overlay offsets of the stub entries
    data: str = ""               # name of a data block (no functions inside)

    @property
    def end(self) -> int:
        return self.start + self.size

    def file_offset(self, addr: int) -> int:
        return addr - OVR_BASE if self.file == "BOMB.OVR" else addr


@dataclass
class Function:
    start: int
    end: int
    region: Region
    reasons: set[str] = field(default_factory=set)
    refs: dict[str, list[str]] = field(default_factory=lambda: defaultdict(list))
    markers: dict[str, str] = field(default_factory=dict)  # hit "file:line" -> marker "file:line" ("" = none)
    strings: list[str] = field(default_factory=list)
    callers: set[int] = field(default_factory=set)
    callees: set[int] = field(default_factory=set)
    call_refs: set[str] = field(default_factory=set)
    entry: int | None = None   # stub entry index if exported

    @property
    def size(self) -> int:
        return self.end - self.start

    @property
    def status(self) -> str:
        done = self.refs.get("umgesetzt")
        if done and self.refs.get("genähert") and all(w in self.call_refs for w in done):
            return "genähert"
        for s in STATUS_ORDER[:3]:
            if self.refs.get(s):
                return s
        return "unbekannt"

    def offset(self) -> str:
        return f"{self.region.file} 0x{self.region.file_offset(self.start):05X}"

    def address(self) -> str:
        r = self.region
        if r.ovl is not None:
            return f"O{r.ovl}:{self.start - r.start:04X}"
        return f"{r.seg:04X}:{self.start - SEG_FILE(r.seg):04X}"


def SEG_FILE(seg: int) -> int:
    return 0x16A0 + seg * 16


# ---------------------------------------------------------------- layout

def read_layout(g: bombre.Game) -> list[Region]:
    stubs = {o["stub"] for o in g.overlays}
    regions: list[Region] = []
    for s in g.segments:
        seg = s["seg"]
        if seg == DATA_SEG or seg in stubs:
            continue
        start = g.exe_off(seg)
        if seg == 0x0431:
            first = BGI_BLOCKS[0][0]
            regions.append(Region("Seg 0431", "E0431", "BOMB.EXE", start, first - start, seg=seg))
            for i, (b, name) in enumerate(BGI_BLOCKS):
                e = BGI_BLOCKS[i + 1][0] if i + 1 < len(BGI_BLOCKS) else start + s["size"]
                regions.append(Region(f"Seg 0431 {name}", f"E0431_{i}", "BOMB.EXE", b, e - b, seg=seg, data=name))
            continue
        regions.append(Region(f"Seg {seg:04X}", f"E{seg:04X}", "BOMB.EXE", start, s["size"], seg=seg))
    for i, o in enumerate(g.overlays, 1):
        regions.append(Region(f"O{i}", f"O{i:02d}", "BOMB.OVR", OVR_BASE + o["fileofs"], o["codesize"],
                              ovl=i, stub=o["stub"], entries=list(o["entries"])))
    return regions


# ---------------------------------------------------------------- disassembly

INSN = re.compile(r"^([0-9A-F]{8})\s+([0-9A-F]+)\s+(.*)$")
FAR_CALL = re.compile(r"^(call|jmp) (?:word )?0x([0-9a-f]+):0x([0-9a-f]+)$")
NEAR_CALL = re.compile(r"^call 0x([0-9a-f]+)$")


def disassemble(blob: bytes, sync: list[int]) -> list[tuple[int, bytes, str]]:
    with tempfile.NamedTemporaryFile(suffix=".bin") as tmp:
        tmp.write(blob)
        tmp.flush()
        cmd = ["ndisasm", "-b16", "-o0"]
        for off in sorted(set(sync)):
            cmd += ["-s", str(off)]
        out = subprocess.run(cmd + [tmp.name], capture_output=True, text=True, check=True).stdout
    res = []
    for line in out.splitlines():
        m = INSN.match(line)
        if m:
            res.append((int(m.group(1), 16), bytes.fromhex(m.group(2)), m.group(3)))
    return res


class Analyzer:
    def __init__(self, g: bombre.Game):
        self.g = g
        self.regions = read_layout(g)
        self.code_regions = [r for r in self.regions if not r.data]
        self.by_seg = {r.seg: r for r in self.code_regions if r.seg is not None}
        self.by_ovl = {r.ovl: r for r in self.regions if r.ovl is not None}
        self.by_stub = {r.stub: r for r in self.regions if r.stub is not None}
        self.code: dict[str, list[tuple[int, bytes, str]]] = {}
        self.functions: list[Function] = []
        self.starts: list[int] = []

    def blob(self, r: Region) -> bytes:
        data = self.g.ovr if r.file == "BOMB.OVR" else self.g.exe
        o = r.file_offset(r.start)
        return data[o:o + r.size]

    def seg_addr(self, seg: int, off: int) -> int | None:
        """Address of seg:off in the load image (code segment or stub entry)."""
        if seg in self.by_stub:
            r = self.by_stub[seg]
            if off >= 0x20 and (off - 0x20) % 5 == 0 and (off - 0x20) // 5 < len(r.entries):
                return r.start + r.entries[(off - 0x20) // 5]
            return None
        r = self.by_seg.get(seg)
        if r is None:
            return None
        a = SEG_FILE(seg) + off
        return a if r.start <= a < r.end else None

    def region_of(self, addr: int) -> Region | None:
        for r in self.regions:
            if r.start <= addr < r.end:
                return r
        return None

    def run(self) -> None:
        starts: dict[int, set[str]] = defaultdict(set)
        for r in self.regions:
            starts[r.start].add("Datenblock" if r.data else "Segmentanfang")
            if r.data:
                continue
            for off in r.entries:
                starts[r.start + off].add("Stub")
            for m in re.finditer(rb"\x55\x89\xe5", self.blob(r)):
                starts[r.start + m.start()].add("Prolog")
        calls: list[tuple[int, int]] = []
        for _ in range(4):
            before = len(starts)
            calls = []
            near_targets: dict[int, int] = defaultdict(int)
            for r in self.code_regions:
                sync = [a - r.start for a in starts if r.start <= a < r.end]
                code = disassemble(self.blob(r), sync)
                self.code[r.key] = code
                for off, raw, text in code:
                    addr = r.start + off
                    m = NEAR_CALL.match(text)
                    if m and raw[0] == 0xE8:
                        tgt = int(m.group(1), 16)
                        if tgt < r.size:
                            near_targets[r.start + tgt] += 1
                            calls.append((addr, r.start + tgt))
                        continue
                    m = FAR_CALL.match(text)
                    if m and raw[0] == 0x9A:
                        tgt = self.seg_addr(int(m.group(2), 16), int(m.group(3), 16))
                        if tgt is not None and self.region_of(tgt) and not self.region_of(tgt).data:
                            starts[tgt].add("Aufruf")
                            calls.append((addr, tgt))
            for tgt, n in near_targets.items():
                if tgt in starts or n >= 2:
                    reg = self.region_of(tgt)
                    if reg and not reg.data:
                        starts[tgt].add("Aufruf")
            if len(starts) == before:
                break
        calls = [(s, t) for s, t in calls if t in starts]
        ordered = sorted(a for a in starts if self.region_of(a))
        funcs = []
        for idx, a in enumerate(ordered):
            reg = self.region_of(a)
            nxt = ordered[idx + 1] if idx + 1 < len(ordered) else reg.end
            f = Function(a, min(nxt, reg.end), reg, reasons=starts[a])
            if reg.ovl is not None and a - reg.start in reg.entries:
                f.entry = reg.entries.index(a - reg.start)
            funcs.append(f)
        self.functions = funcs
        self.starts = [f.start for f in funcs]
        for site, tgt in calls:
            caller, callee = self.func_at(site), self.func_at(tgt)
            if caller and callee and caller is not callee:
                caller.callees.add(callee.start)
                callee.callers.add(caller.start)
        self._strings()

    def func_at(self, addr: int) -> Function | None:
        i = bisect.bisect_right(self.starts, addr) - 1
        if i >= 0 and self.functions[i].start <= addr < self.functions[i].end:
            return self.functions[i]
        return None

    def _strings(self) -> None:
        """Pascal strings a function loads from its own code block (mov di,imm; push cs; push di)."""
        for r in self.code_regions:
            data = self.blob(r)
            strs = dict(bombre.pascal_strings(data, 2))
            for m in re.finditer(rb"\xbf(..)\x0e\x57", data, re.S):
                t = struct.unpack("<H", m.group(1))[0]
                f = self.func_at(r.start + m.start())
                if t in strs and f is not None and strs[t] not in f.strings:
                    f.strings.append(strs[t])


# ---------------------------------------------------------------- references

FILE_KW = re.compile(r"BOMB\.(EXE|OVR)\b", re.I)
OVLREF = re.compile(r"(?<![\w.])O(\d{1,2})(?:\[(\d+)\]|:([0-9A-Fa-f]{3,4})(?:\s*(?:–|-|\.\.)\s*([0-9A-Fa-f]{3,4}))?)"
                    r"(?![0-9A-Fa-f])")
SEGREF = re.compile(r"(?<![\w.:])U?(0?[0-9A-F]{3,4}):([0-9A-F]{4})(?:\s*(?:–|-|\.\.)\s*([0-9A-F]{4}))?(?![\w:])")
HEX = r"0x[0-9A-Fa-f]{3,5}"
RANGE = re.compile(rf"({HEX})`?\s*(?:–|-|\.\.|bis)\s*`?({HEX})")
BARE = re.compile(rf"(?<![\w:+]){HEX}(?![\w]|:[0-9A-Fa-f])")
NOT_ADDR_BEFORE = re.compile(r"(DS|CS|ES|SS|DGROUP|\+|\[)\s*:?\s*`?$")
SENTENCE_CUT = re.compile(r"\.\s|;|\n\s*(?:///?|\*)?\s*\n|\[(?:ORIGINAL|APPROX)\]|\|")
# docs that list addresses as an inventory of findings, not as evidence (the generated outputs of this map)
NOT_EVIDENCE_DOCS = {"docs/analyse/re-abdeckung.md", "docs/analyse/restliste-v1.md"}
NOT_EVIDENCE_DIRS = ("docs/analyse/abdeckung-grafik/",)
# docs about other binary files: "0x..." there is an offset in that file (save game, configuration)
OTHER_FILE_DOCS_RE = re.compile(r"docs/formate/(spielstand|konfiguration|tws|cfg)", re.I)


class RefCollector:
    def __init__(self, an: Analyzer):
        self.an = an
        self.hits = 0
        # file offsets of segment and overlay starts are layout constants ("file offset 0x0E320 + offset"), not
        # references to the first function; the BGI data blocks are named by their start and stay addressable
        self.constants = {r.start for r in an.code_regions} | {0x16A0, 0x163F0}

    def exact_start(self, val: int) -> list[int]:
        out = []
        for a in (val, OVR_BASE + val):
            if a in self.constants:
                continue
            f = self.an.func_at(a)
            if f and f.start == a and not f.region.data:
                out.append(a)
        return out

    def file_addr(self, kind: str, val: int) -> int | None:
        a = val + (OVR_BASE if kind == "OVR" else 0)
        return a if self.an.region_of(a) and a not in self.constants else None

    def addresses(self, text: str, other_file: bool = False) -> list[tuple[int, int, int, bool]]:
        """(position, first, last, is_call) of the addresses mentioned in `text`."""
        an = self.an
        out, used = [], []
        for m in OVLREF.finditer(text):
            r = an.by_ovl.get(int(m.group(1)))
            used.append(m.span())
            if r is None:
                continue
            if m.group(2) is not None:
                k = int(m.group(2))
                if k < len(r.entries):
                    a = r.start + r.entries[k]
                    out.append((m.start(), a, a, True))
                continue
            a = int(m.group(3), 16)
            b = int(m.group(4), 16) if m.group(4) else a
            if a < r.size:
                out.append((m.start(), r.start + a, r.start + min(max(a, b), r.size - 1), False))
        for m in SEGREF.finditer(text):
            if any(s <= m.start() < e for s, e in used):
                continue
            seg, off = int(m.group(1), 16), int(m.group(2), 16)
            used.append(m.span())
            if NOT_ADDR_BEFORE.search(text[max(0, m.start() - 4):m.start()]):
                continue
            a = an.seg_addr(seg, off)
            if a is None:
                continue
            is_call = seg in an.by_stub
            b = an.seg_addr(seg, int(m.group(3), 16)) if m.group(3) and not is_call else None
            out.append((m.start(), a, b if b and b >= a else a, is_call))
        cuts = [0] + [c.end() for c in SENTENCE_CUT.finditer(text)]
        for rx, rng in ((RANGE, True), (BARE, False)):
            for m in rx.finditer(text):
                if any(s <= m.start() < e for s, e in used):
                    continue
                used.append(m.span())
                pos = m.start()
                before = text[max(0, pos - 12):pos]
                if NOT_ADDR_BEFORE.search(before):
                    continue
                line = text[text.rfind("\n", 0, pos) + 1:pos]
                if re.search(r"\b(DS|DGROUP):?\s", line) and not FILE_KW.search(line):
                    continue
                sent = text[max(c for c in cuts if c <= pos):pos]
                kws = list(FILE_KW.finditer(sent))
                a = int(m.group(1 if rng else 0), 16)
                b = int(m.group(2), 16) if rng else a
                if kws:
                    kind = kws[-1].group(1).upper()
                    fa, fb = self.file_addr(kind, a), self.file_addr(kind, b)
                    if fa is not None and fb is not None and fb >= fa and fb - fa < 0x3000:
                        out.append((pos, fa, fb, False))
                    continue
                if other_file or rng:
                    continue
                cand = self.exact_start(a)
                if len(cand) == 1:
                    out.append((pos, cand[0], cand[0], False))
        return out

    def mark(self, first: int, last: int, status: str, where: str, marker: str, is_call: bool = False) -> None:
        an = self.an
        i = max(bisect.bisect_right(an.starts, first) - 1, 0)
        while i < len(an.functions) and an.functions[i].start <= last:
            f = an.functions[i]
            if f.end > first and (f.start <= first or f.start <= last - 3):
                if where not in f.refs[status]:
                    f.refs[status].append(where)
                    f.markers[where] = marker
                    self.hits += 1
                    if is_call and f.start == first == last:
                        f.call_refs.add(where)
                elif not is_call:
                    f.call_refs.discard(where)
            i += 1

    def scan_code(self, root: Path) -> None:
        for path in sorted((root / "src").rglob("*.cs")):
            rel = path.relative_to(root)
            if any(p in ("bin", "obj") for p in rel.parts):
                continue
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
            blocks: list[list[tuple[int, str]]] = []
            cur: list[tuple[int, str]] = []
            for no, line in enumerate(lines, 1):
                s = line.strip()
                if s.startswith("//") or s.startswith("*") or s.startswith("/*") or "//" in s or "[APPROX]" in s \
                        or "[ORIGINAL]" in s:
                    cur.append((no, line))
                else:
                    if cur:
                        blocks.append(cur)
                    cur = []
            if cur:
                blocks.append(cur)
            for block in blocks:
                text = "\n".join(l for _, l in block)
                markers = [(m.start(), m.group(1)) for m in re.finditer(r"\[(ORIGINAL|APPROX)\]", text)]
                for pos, a, b, is_call in self.addresses(text):
                    prev = [(p, k) for p, k in markers if p <= pos]
                    nxt = [(p, k) for p, k in markers if p > pos]
                    sel = prev[-1] if prev else (nxt[0] if nxt else None)
                    status = {"ORIGINAL": "umgesetzt", "APPROX": "genähert", None: "dokumentiert"}[sel and sel[1]]
                    line_no = block[0][0] + text[:pos].count("\n")
                    mline = f"{rel}:{block[0][0] + text[:sel[0]].count(chr(10))}" if sel else ""
                    self.mark(a, b, status, f"{rel}:{line_no}", mline, is_call)

    def scan_docs(self, root: Path) -> None:
        for path in sorted((root / "docs").rglob("*.md")):
            rel = path.relative_to(root).as_posix()
            parts = rel.split("/")
            if len(parts) > 2 and parts[1] in ("archiv", "reviews"):
                continue
            if rel in NOT_EVIDENCE_DOCS or rel.startswith(NOT_EVIDENCE_DIRS):
                continue
            other = bool(OTHER_FILE_DOCS_RE.search(rel))
            lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
            para: list[tuple[int, str]] = []

            def flush():
                if not para:
                    return
                text = "\n".join(l for _, l in para)
                for pos, a, b, _ in self.addresses(text, other):
                    self.mark(a, b, "dokumentiert", f"{rel}:{para[0][0] + text[:pos].count(chr(10))}", "")
                para.clear()

            for no, line in enumerate(lines, 1):
                if not line.strip() or line.lstrip().startswith("#"):
                    flush()
                    continue
                if line.lstrip().startswith("|"):
                    flush()
                    para.append((no, line))
                    flush()
                    continue
                if re.match(r"^\s*([*-]|\d+\.)\s", line):
                    flush()
                para.append((no, line))
            flush()


# ---------------------------------------------------------------- report

def is_system(r: Region) -> bool:
    return r.seg is not None and BASE_KIND.get(r.seg, ("Spiel",))[0] == "System"


def region_kind(r: Region) -> str:
    if r.data:
        return r.data
    if r.ovl is not None:
        return "Overlay"
    return BASE_KIND.get(r.seg, ("Spiel", "?"))[1]


def pct(part: int, whole: int) -> str:
    return f"{100 * part / whole:.0f} %" if whole else "–"


def hint(f: Function, an: Analyzer) -> str:
    if f.region.data:
        return f.region.data
    strs = [s for s in f.strings if sum(c.isalpha() for c in s) >= 3][:3]
    if strs:
        return "„" + "“, „".join(s[:28].replace("|", "/") for s in strs) + "“"
    known = [an.func_at(c) for c in sorted(f.callers)]
    known = [c for c in known if c and (c.status != "unbekannt" or c.strings)]
    if known:
        c = known[0]
        return f"gerufen von {c.address()} ({c.strings[0][:24] if c.strings else c.status})"
    return "–"


def render(an: Analyzer, rc: RefCollector) -> str:
    out = []
    w = out.append
    funcs = an.functions
    game = [f for f in funcs if not is_system(f.region) and not f.region.data]
    w("<!-- BEGIN generiert: tools/reverse-engineering/funktionskarte.py – nicht von Hand ändern -->")
    w("")
    w(f"Erzeugt aus `BOMB.EXE` ({len(an.g.exe)} Byte) und `BOMB.OVR` ({len(an.g.ovr)} Byte): {len(funcs)} Funktionen "
      f"und Datenblöcke in {len(an.regions)} Bereichen ({sum(1 for r in an.regions if r.ovl is None and not r.data)} "
      f"Segmente, {sum(1 for r in an.regions if r.ovl is not None)} Overlays, "
      f"{sum(1 for r in an.regions if r.data)} BGI-Dateien), {sum(f.size for f in funcs)} Byte; {rc.hits} Zuordnungen.")
    w("")
    w("| Status | Funktionen | Bytes | Anteil | Funktionen Spielcode | Bytes Spielcode | Anteil Spielcode |")
    w("|---|---:|---:|---:|---:|---:|---:|")
    tot, gtot = sum(f.size for f in funcs), sum(f.size for f in game)
    for st in STATUS_ORDER:
        fs = [f for f in funcs if f.status == st]
        gs = [f for f in game if f.status == st]
        w(f"| {st} | {len(fs)} | {sum(f.size for f in fs)} | {pct(sum(f.size for f in fs), tot)} | {len(gs)} | "
          f"{sum(f.size for f in gs)} | {pct(sum(f.size for f in gs), gtot)} |")
    w(f"| **Summe** | {len(funcs)} | {tot} | 100 % | {len(game)} | {gtot} | 100 % |")
    w("")
    w("| Bereich | Datei | Größe | Funktionen | umgesetzt | genähert | dokumentiert | unbekannt | Rolle |")
    w("|---|---|---:|---:|---:|---:|---:|---:|---|")
    for r in an.regions:
        fs = [f for f in funcs if f.region is r]
        size = sum(f.size for f in fs)
        by = {st: sum(f.size for f in fs if f.status == st) for st in STATUS_ORDER}
        w(f"| {r.name} | {r.file} 0x{r.file_offset(r.start):05X} | {r.size} | {len(fs)} | "
          + " | ".join(pct(by[st], size) for st in STATUS_ORDER) + f" | {region_kind(r)} |")
    w("")
    w(f"Dünn belegte große Funktionen (> {THIN_SIZE} Byte, höchstens zwei Fundstellen; der Status gilt je Funktion, "
      "schon eine Fundstelle zählt die ganze Funktion):")
    w("")
    w("| Datei | Adresse | Größe | Status | Fundstellen | Hinweis |")
    w("|---|---|---:|---|---|---|")
    for f in sorted(game, key=lambda f: -f.size):
        refs = sorted({x for v in f.refs.values() for x in v})
        if f.size > THIN_SIZE and f.status in ("umgesetzt", "dokumentiert") and len(refs) <= 2:
            w(f"| {f.offset()} | {f.address()} | {f.size} | {f.status} | {', '.join('`' + x + '`' for x in refs)} | "
              f"{hint(f, an)} |")
    w("")
    w(f"Unbekannte und nur genäherte Funktionen > {MIN_LIST_SIZE} Byte (Spielcode):")
    w("")
    w("| Datei | Adresse | Größe | Status | Aufrufer | Hinweis |")
    w("|---|---|---:|---|---:|---|")
    for f in sorted(game, key=lambda f: f.start):
        if f.size > MIN_LIST_SIZE and f.status in ("unbekannt", "genähert"):
            w(f"| {f.offset()} | {f.address()} | {f.size} | {f.status} | {len(f.callers)} | {hint(f, an)} |")
    w("")
    w("<!-- END generiert -->")
    return "\n".join(out)


def write_csv(an: Analyzer, path: Path) -> None:
    with path.open("w", newline="", encoding="utf-8") as fh:
        wr = csv.writer(fh)
        wr.writerow(["datei", "bereich", "adresse", "groesse", "status", "erkannt_durch", "stub_eintrag",
                     "aufrufer", "aufgerufene", "texte", "fundstellen", "marker"])
        for f in an.functions:
            refs = "; ".join(f"{s}: {', '.join(v)}" for s, v in f.refs.items() if v)
            marks = "; ".join(f"{k}>{v}" for k, v in f.markers.items())
            wr.writerow([f.offset(), f.region.name, f.address(), f.size, f.status, "+".join(sorted(f.reasons)),
                         "" if f.entry is None else str(f.entry),
                         " ".join(an.func_at(c).offset() for c in sorted(f.callers)),
                         " ".join(an.func_at(c).offset() for c in sorted(f.callees)),
                         " / ".join(f.strings[:8]), refs, marks])


def analyse(gamedata: Path, repo: Path) -> tuple[Analyzer, RefCollector]:
    an = Analyzer(bombre.Game(str(gamedata)))
    an.run()
    rc = RefCollector(an)
    rc.scan_code(repo)
    rc.scan_docs(repo)
    return an, rc


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("gamedata", type=Path)
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    ap.add_argument("--write", action="store_true", help="update docs/analyse/re-abdeckung.md between the markers")
    ap.add_argument("--csv", type=Path, help="write all functions as CSV (outside the repo)")
    args = ap.parse_args()
    an, rc = analyse(args.gamedata, args.repo)
    text = render(an, rc)
    if args.csv:
        write_csv(an, args.csv)
    if args.write:
        doc = args.repo / "docs" / "analyse" / "re-abdeckung.md"
        old = doc.read_text(encoding="utf-8") if doc.exists() else "# Abdeckung von BOMB.EXE/BOMB.OVR\n"
        new = re.sub(r"<!-- BEGIN generiert.*?<!-- END generiert -->", lambda _: text, old, flags=re.S)
        if "<!-- BEGIN generiert" not in old:
            new = old.rstrip() + "\n\n" + text + "\n"
        doc.write_text(new, encoding="utf-8")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
