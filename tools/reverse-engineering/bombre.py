#!/usr/bin/env python3
"""Reverse-engineering helpers for Tank Wars 3.2 (BOMB.EXE / BOMB.OVR).

Reads the original files from gamedata/ and writes derived material (listings,
string tables) only to analyse/ (a protected, git-ignored folder). Nothing this
script writes may be committed to the code repository.

Usage:
  bombre.py [--gamedata DIR] [--out DIR] segments|overlays|strings|xref|fonts|karte|befunde|listings|all

'befunde' checks every claim of docs/analyse/re-grundlagen.md against the
files and exits non-zero if one does not hold.
"""
import argparse
import collections
import hashlib
import os
import re
import shutil
import struct
import subprocess
import sys

# Known segments (paragraph relative to the load image) and what they are.
# The identification of each entry is checked in 'befunde'.
UNITS = {
    0x0000: "main program (BOMB)",
    0x0054: "overlay stub O1 (title)",
    0x0059: "game unit U59 (memory summary, overlay setup)",
    0x0129: "game unit U129",
    0x0431: "game unit U431 (dialogs, linked BGI driver and fonts)",
    0x0B02: "overlay stub O2 (menus, options, players)",
    0x0B08: "overlay stub O3 (shop, statistics, save/load)",
    0x0B0C: "overlay stub O4 (round: aiming, MIRV setup)",
    0x0B0F: "overlay stub O5",
    0x0B13: "overlay stub O6",
    0x0B16: "overlay stub O7",
    0x0B1A: "overlay stub O8 (wall modes)",
    0x0B1D: "overlay stub O9 (tank info, guidance)",
    0x0B20: "overlay stub O10",
    0x0B25: "unit UB25 (Graph procedure name records)",
    0x0C02: "game unit UC02 (emergency save 'BOSS')",
    0x0C48: "game unit UC48 (keyboard handler INT 09h, 'XKEY')",
    0x0C79: "game unit UC79 (palette rotation interrupt INT 0Ah/1Ch, 'CYCLER')",
    0x0CC8: "game unit UCC8 (direct VGA mode 13h, DAC palette)",
    0x0DBD: "game unit UDBD (graphics init/error, PIC)",
    0x0E11: "game unit UE11 (mouse, INT 33h)",
    0x0E22: "Borland Printer",
    0x0E26: "Borland Graph (BGI kernel)",
    0x11E6: "Borland Dos",
    0x11F8: "Borland Crt",
    0x125A: "Borland Overlay",
    0x12CE: "Borland System (TP 6.0 runtime)",
    0x14D5: "DATA (initialised variables and typed constants, DS)",
}

# Selected runtime entry points (segment, offset) -> name; checked in 'befunde'.
NAMES = {
    (0x12CE, 0x0000): "System.@Init",
    (0x12CE, 0x1675): "System.Random(Word)",
    (0x12CE, 0x168B): "System.Random:Real",
    (0x12CE, 0x16FC): "System.Randomize",
    (0x12CE, 0x11B6): "System.@RealAdd",
    (0x12CE, 0x11BC): "System.@RealSub",
    (0x12CE, 0x11C8): "System.@RealMul",
    (0x12CE, 0x11CE): "System.@RealDiv",
    (0x12CE, 0x11D8): "System.@RealCmp",
    (0x12CE, 0x11DC): "System.@RealFloat(LongInt)",
    (0x12CE, 0x11E0): "System.@RealTrunc",
    (0x12CE, 0x11E8): "System.@RealRound",
    (0x11F8, 0x029C): "Crt.Delay",
    (0x11F8, 0x02C7): "Crt.Sound",
    (0x11F8, 0x02F4): "Crt.NoSound",
    (0x11F8, 0x02FB): "Crt.KeyPressed",
    (0x11F8, 0x030D): "Crt.ReadKey",
    (0x11E6, 0x00E7): "Dos.GetIntVec",
    (0x11E6, 0x00FF): "Dos.SetIntVec",
    (0x11E6, 0x000B): "Dos.Intr",
    (0x0C79, 0x00E3): "UC79.PaletteCycleIsr",
}

MZ_HEADER = "<2s13H"


class Game:
    """Load image, relocations, segments and overlays."""

    def __init__(self, gamedata):
        self.exe = read(gamedata, "BOMB.EXE")
        self.ovr = read(gamedata, "BOMB.OVR")
        h = struct.unpack(MZ_HEADER, self.exe[:28])
        (self.sig, self.lastp, self.pages, self.nrel, self.hdrpar, self.minalloc, self.maxalloc,
         self.ss, self.sp, self.csum, self.ip, self.cs, self.relo, self.ovlno) = h
        self.hdr = self.hdrpar * 16
        size = (self.pages - 1) * 512 + self.lastp if self.lastp else self.pages * 512
        self.img = self.exe[self.hdr:size]
        self.relocs = [struct.unpack("<HH", self.exe[self.relo + 4 * i:self.relo + 4 * i + 4])
                       for i in range(self.nrel)]
        self.overlays = self._overlays()
        self.segments = self._segments()

    def exe_off(self, seg, ofs=0):
        return self.hdr + seg * 16 + ofs

    def word(self, data, at):
        return struct.unpack("<H", data[at:at + 2])[0]

    def _overlays(self):
        """Overlay stubs: segment starts with INT 3Fh followed by a zero word."""
        res = []
        p = 0
        while p * 16 < len(self.img):
            b = p * 16
            if self.img[b:b + 4] == b"\xcd\x3f\x00\x00":
                fileofs, codesize, fixsize, entries = struct.unpack("<IHHH", self.img[b + 4:b + 14])
                if 8 <= fileofs < len(self.ovr) and fileofs + codesize + fixsize <= len(self.ovr):
                    ents = []
                    for k in range(entries):
                        e = b + 0x20 + 5 * k
                        assert self.img[e:e + 2] == b"\xcd\x3f"
                        ents.append(self.word(self.img, e + 2))
                    code = self.ovr[fileofs:fileofs + codesize]
                    fix = struct.unpack("<%dH" % (fixsize // 2), self.ovr[fileofs + codesize:fileofs + codesize + fixsize])
                    res.append(dict(stub=p, fileofs=fileofs, codesize=codesize, fixsize=fixsize,
                                    entries=ents, code=code, fixups=fix))
                    p += (0x20 + 5 * entries + 15) // 16
                    continue
            p += 1
        return res

    def _segments(self):
        """Segment bases = all segment words patched by EXE relocations or overlay fixups."""
        segs = collections.Counter()
        for o, s in self.relocs:
            segs[self.word(self.img, s * 16 + o)] += 1
        for ov in self.overlays:
            for x in ov["fixups"]:
                segs[self.word(ov["code"], x)] += 1
        bases = sorted(segs)
        out = []
        for i, s in enumerate(bases):
            end = bases[i + 1] if i + 1 < len(bases) else len(self.img) // 16 + (1 if len(self.img) % 16 else 0)
            out.append(dict(seg=s, refs=segs[s], size=min(end * 16, len(self.img)) - s * 16))
        return out

    def seg_bytes(self, seg):
        for s in self.segments:
            if s["seg"] == seg:
                return self.img[seg * 16:seg * 16 + s["size"]]
        raise KeyError(hex(seg))

    def code_blocks(self):
        """(name, data, file, file offset of byte 0) for every code segment and overlay."""
        for s in self.segments:
            if s["seg"] == 0x14D5 or any(o["stub"] == s["seg"] for o in self.overlays):
                continue
            yield ("seg_%04X" % s["seg"], self.seg_bytes(s["seg"]), "BOMB.EXE", self.exe_off(s["seg"]))
        for i, o in enumerate(self.overlays, 1):
            yield ("ovl_O%d_%04X" % (i, o["stub"]), o["code"], "BOMB.OVR", o["fileofs"])


def read(gamedata, name):
    for f in os.listdir(gamedata):
        if f.upper() == name:
            with open(os.path.join(gamedata, f), "rb") as fh:
                return fh.read()
    sys.exit("missing %s in %s" % (name, gamedata))


def pascal_strings(data, minlen=2):
    """Length-prefixed printable strings (Turbo Pascal string constants)."""
    res = []
    i = 0
    while i < len(data) - 1:
        n = data[i]
        if n >= minlen and i + 1 + n <= len(data):
            s = data[i + 1:i + 1 + n]
            if all(0x20 <= c < 0x7F for c in s):
                res.append((i, s.decode("ascii")))
                i += 1 + n
                continue
        i += 1
    return res


def far_calls(data):
    """Offsets and targets of 'call far seg:ofs' (9A ofs seg)."""
    for m in re.finditer(rb"\x9a(..)(..)", data, re.S):
        yield m.start(), struct.unpack("<H", m.group(2))[0], struct.unpack("<H", m.group(1))[0]


def cmd_segments(g, args):
    print("MZ: Kopf %d Byte, Bild %d Byte (0x%X), %d Relokationen, Einsprung %04X:%04X, Stapel %04X:%04X"
          % (g.hdr, len(g.img), len(g.img), g.nrel, g.cs, g.ip, g.ss, g.sp))
    print("Dateioffset = 0x%X + Segment*16 + Offset" % g.hdr)
    print("%-6s %-8s %6s %5s  %s" % ("Seg", "Datei", "Byte", "Refs", "Einheit"))
    for s in g.segments:
        print("%04X   0x%05X %6d %5d  %s" % (s["seg"], g.exe_off(s["seg"]), s["size"], s["refs"],
                                           UNITS.get(s["seg"], "?")))
    # unit initialisation order: far calls at the program entry before 'push bp'
    entry = g.exe_off(g.cs, g.ip) - g.hdr
    order = []
    p = entry
    while g.img[p] == 0x9A:
        ofs, seg = struct.unpack("<HH", g.img[p + 1:p + 5])
        order.append("%04X:%04X %s" % (seg, ofs, UNITS.get(seg, "?")))
        p += 5
    print("\nInitialisierung (Reihenfolge der Aufrufe am Einsprung, BOMB.EXE 0x%X):" % (entry + g.hdr))
    for o in order:
        print("  " + o)


def cmd_overlays(g, args):
    print("BOMB.OVR: Kennung %r, Länge laut Kopf %d (Datei %d)" % (g.ovr[:4], struct.unpack("<I", g.ovr[4:8])[0] + 8,
                                                                 len(g.ovr)))
    print("%-4s %-5s %-9s %6s %6s %4s  %s" % ("Nr", "Stub", "OVR-Ofs", "Code", "Fixup", "Ein", "Einsprünge (Offset im Overlay)"))
    for i, o in enumerate(g.overlays, 1):
        print("O%-3d %04X  0x%05X %6d %6d %4d  %s" % (i, o["stub"], o["fileofs"], o["codesize"], o["fixsize"],
                                                     len(o["entries"]), " ".join("%04X" % e for e in o["entries"])))
    last = g.overlays[-1]
    print("Lückenlos: %s (letztes Overlay endet bei %d)" % (
        all(a["fileofs"] + a["codesize"] + a["fixsize"] == b["fileofs"] for a, b in zip(g.overlays, g.overlays[1:])),
        last["fileofs"] + last["codesize"] + last["fixsize"]))


def cmd_strings(g, args):
    os.makedirs(args.out, exist_ok=True)
    path = os.path.join(args.out, "texte.tsv")
    n = collections.Counter()
    with open(path, "w", encoding="ascii") as fh:
        fh.write("datei\tdateioffset\tblock\toffset\tlaenge\ttext\n")
        blocks = list(g.code_blocks()) + [("seg_14D5", g.seg_bytes(0x14D5), "BOMB.EXE", g.exe_off(0x14D5))]
        for name, data, f, base in blocks:
            for o, s in pascal_strings(data, 3):
                fh.write("%s\t0x%05X\t%s\t0x%04X\t%d\t%s\n" % (f, base + o, name, o, len(s), s.replace("\t", " ")))
                n[name] += 1
    print("Texttabelle: %s (%d Einträge)" % (path, sum(n.values())))
    for k, v in n.items():
        print("  %-18s %5d" % (k, v))


def proc_starts(data):
    return [m.start() for m in re.finditer(rb"\x55\x89\xe5", data)]


def cmd_xref(g, args):
    """Map procedures to the string constants they load (mov di,imm; push cs)."""
    os.makedirs(args.out, exist_ok=True)
    path = os.path.join(args.out, "prozeduren.tsv")
    with open(path, "w", encoding="ascii") as fh:
        fh.write("block\tdatei\tproc_offset\tdateioffset\ttexte\n")
        for name, data, f, base in g.code_blocks():
            strs = dict(pascal_strings(data, 2))
            starts = proc_starts(data)
            refs = collections.defaultdict(list)
            for m in re.finditer(rb"\xbf(..)\x0e\x57", data, re.S):
                t = struct.unpack("<H", m.group(1))[0]
                if t in strs:
                    p = max([s for s in starts if s <= m.start()], default=0)
                    refs[p].append(strs[t])
            for p in starts:
                fh.write("%s\t%s\t0x%04X\t0x%05X\t%s\n" % (name, f, p, base + p, " | ".join(refs.get(p, []))))
    print("Prozeduren mit Textbezügen: %s" % path)


BORLAND = (0x0E22, 0x0E26, 0x11E6, 0x11F8, 0x125A, 0x12CE)


def is_borland(block):
    return block.startswith("seg_") and int(block[4:], 16) in BORLAND


FONT_VARS = {0xE910: "EURO", 0xE912: "LCOM", 0xE914: "SCRI", 0xE916: "SIMP", 0xE918: "TSCR", 0xE91A: "TINY"}
FONT_CONSTS = {0: "DefaultFont (8x8)", 1: "TriplexFont", 2: "SmallFont (LITT)", 3: "SansSerifFont", 4: "GothicFont"}
SET_TEXT_STYLE = (0x0E26, 0x1496)


def pushed_args(data, at, n):
    """Decode the last n 'push' arguments before a call (mov ax,imm / xor ax,ax / push word [imm])."""
    args = []
    p = at
    while len(args) < n and p > 0:
        if data[p - 1] == 0x50:  # push ax
            if p >= 4 and data[p - 4] == 0xB8:
                args.append(struct.unpack("<H", data[p - 3:p - 1])[0]); p -= 4; continue
            if p >= 3 and data[p - 3:p - 1] == b"\x31\xc0":
                args.append(0); p -= 3; continue
            if p >= 3 and data[p - 3] == 0xB0:  # mov al,imm (byte argument)
                args.append(data[p - 2]); p -= 3; continue
        if p >= 4 and data[p - 4:p - 2] == b"\xff\x36":
            args.append(("var", struct.unpack("<H", data[p - 2:p])[0])); p -= 4; continue
        break
    return list(reversed(args))


def cmd_fonts(g, args):
    """SetTextStyle calls: font, direction, size per procedure."""
    tally = collections.Counter()
    for name, data, f, base in g.code_blocks():
        starts = proc_starts(data)
        for at, s, o in far_calls(data):
            if (s, o) != SET_TEXT_STYLE:
                continue
            a = pushed_args(data, at, 3)
            if len(a) != 3:
                font = "?"
            elif isinstance(a[0], tuple):
                font = "%s ([%04X])" % (FONT_VARS.get(a[0][1], "?"), a[0][1])
            else:
                font = FONT_CONSTS.get(a[0], str(a[0]))
            size = a[2] if len(a) == 3 else "?"
            p = max([x for x in starts if x <= at], default=0)
            tally[(font, size)] += 1
            print("%-14s proc %04X (%s 0x%05X)  Schrift %-18s Richtung %s Größe %s" % (
                name, p, f, base + p, font, a[1] if len(a) == 3 else "?", size))
    print("Summe:", ", ".join("%s Größe %s: %d" % (k[0], k[1], v) for k, v in sorted(tally.items(), key=str)))


def find(data, pattern):
    return [m.start() for m in re.finditer(pattern, data, re.S)]


# Code regions checked by 'befunde', as (segment, offset, length, SHA-256). Segment None means a
# file offset in BOMB.EXE. Only the position and a digest of the original bytes are kept here, never
# the bytes themselves (AGENTS.md 2.1, decision R1-D2); what each region contains is described in
# words in docs/analyse/re-grundlagen.md. Note: digests of very short regions identify the bytes
# but do not hide them from someone who already has the files - they only keep the bytes out of
# this repository.
REGIONS = {
    "A3": (0x12CE, 0x0000, 5, "25bcd97b11260f690b1f85b44081af905a657e0d63669bcb6cce1d533a6d812f"),
    "C1a": (0x11F8, 0x02CD, 6, "436a5023c8f107f3a9fbb3cd50a9f0c25852cf8bafcac1fa3dc3f80a84e5776f"),
    "C1b": (0x11F8, 0x02E5, 4, "1a653f6e90505e61bc1a46e5afddf1f112988b35e690cb32fd6f7b837a9ae863"),
    "C2": (0x11F8, 0x02F4, 7, "38b59669f41a591e487a96dc127ff35aa26d0a5d0e3cd34773f7a29b7a012fc3"),
    "C3a": (0x11F8, 0x0077, 9, "400c2bb19b1bfbc582dd5877e680388a20959ca71457fe50215bc456335aa172"),
    "C3b": (0x11F8, 0x02B0, 4, "fb3605b43c143d938ed5ab2f14b07aac5a4f792d7ed7001c656061b780bcb269"),
    "C4a": (0x11E6, 0x00ED, 4, "1bc33f6ccf35eed1bb10277bb8d49ca189a5c4d7c2753d3e33389d440a86fe78"),
    "C4b": (0x11E6, 0x0109, 4, "da2f4ce2661eaa39e5310e7e252ae09a3dcad7aeac8b49fff8c01cd2236103d6"),
    "D1": (0x12CE, 0x16C4, 56, "8aa01153e3ac638a40be6c446357604220b0c2130d7029cc735384e9c56bbee8"),
    "D2": (0x12CE, 0x1675, 22, "e4337c9ed94c3d0a72864fcf23dd6e8fb30693d25680a1d52401c919fcbbfeb9"),
    "D3": (0x12CE, 0x16FC, 12, "ae16e15087d57f0787fe403ddd61dca96b68903622713d2f642cf5cd3b1f5e6b"),
    "E2": (0x12CE, 0x11F0, 15, "af40ae9a7c664017dd20b6c9f615f5979deb9b3ff3077e0e4404aaa10b8694d3"),
    "F6": (0x0DBD, 0x03BC, 37, "a3d15c69c3f44b3d7985a23e47cbdfc228a9956cd8df3a1bb3b172434a394a9d"),
    "G1a": (0x0CC8, 0x0000, 5, "0673340d076a3c937beba02b14085f857905b6c8e663f621e8117e1f215ae232"),
    "G1b": (0x0CC8, 0x0F32, 6, "d7ee381a0bc646ade3a2c6c13622d4857e2551c119705d55405b1de896f0d97a"),
    "G1c": (0x0CC8, 0x0F43, 6, "1eb1b024364a44b88c691d28e7ad1d0d781a1ff012f32f0dea0731a829fae3d1"),
    "G3a": (0x0C79, 0x02A9, 4, "48584b91b360d2b793fc66ad75bbec7c7db7fe27f2ed711bbffa4a3c1cf4caf9"),
    "G3b": (0x0C79, 0x0333, 4, "5354d87c23075d91ac47184a0a23604c3b8b99e3a7584946d465971e5b42d43c"),
    "G3c": (0x0C79, 0x02C5, 5, "b154dd69369616cb465b7981ceaa028f58429f3201698203559e290b088dafa0"),
    "G4a": (0x0C79, 0x012A, 12, "ea74395e110abe3b87d4f0d6842e340dc47dd3422c6c0855414a54a399d12a8b"),
    "G4b": (0x0C79, 0x0154, 15, "deba7dfb8efed3b2e91ac488daa20231c1badec9e72068bf0b109d9452f2b535"),
    "H6": (0x11F8, 0x0077, 16, "3c68cadb1b6ff7334f57506d37c1ec351b35a2727753cf249d7d05581b454211"),
    "F1": (None, 0x07EB7, 38, "31f028c9349e3174d782d71e1cba1cb5b535bf72f1388100eeb97c061a29bbb0"),
    "F2": (None, 0x08BE8, 7, "320259f81d0c8a23661ebd4cdccf7fe9da6cfebf8b896fa10e5e2188ce8b09a3"),
    "F3": (None, 0x09FF4, 7, "ba6959a8e6482c2c13ef610779417773e1d492a6d596304579e8b74730b6c86f"),
    "F4": (None, 0x0C0F9, 7, "f9ac5c75b93b0bfc020c84c6e930395146e807a8c9400796dc1ee62d84f62da3"),
}


def region_ok(g, key):
    seg, ofs, n, sha = REGIONS[key]
    data = g.exe if seg is None else g.seg_bytes(seg)
    return len(data) >= ofs + n and hashlib.sha256(data[ofs:ofs + n]).hexdigest() == sha


def far_call(seg, ofs):
    """Encoding of 'call far seg:ofs' (opcode 9Ah), used to search for calls of a known routine."""
    return b"\x9a" + struct.pack("<HH", ofs, seg)


def mov_dx(value):
    """Encoding of 'mov dx, imm16' (opcode BAh)."""
    return b"\xba" + struct.pack("<H", value)


# Embedded BGI files in segment 0431: (name, file start 'PK' + 08h 08h, region with the header).
# The file start is the offset named in the analysis. For the fonts the checked header (1Ah 80h 00h
# + font name) follows behind the copyright text of the file; for VGA256 the checked region is the
# file start with the driver text.
BGI_FILES = [("F1", "VGA256-Treiber 2.00", 0x07EB7), ("F2", "Strichschrift LITT", 0x08B90),
             ("F3", "Strichschrift EURO", 0x09F9B), ("F4", "Strichschrift TINY", 0x0C0AE)]


def cmd_befunde(g, args):
    """Every claim of docs/analyse/re-grundlagen.md as a check."""
    ok = True
    results = []

    def check(fid, claim, cond, where):
        nonlocal ok
        ok &= bool(cond)
        results.append((fid, "OK  " if cond else "FAIL", claim, where))

    sysb = g.seg_bytes(0x12CE)
    crt = g.seg_bytes(0x11F8)
    dos = g.seg_bytes(0x11E6)
    so = lambda seg, ofs: "BOMB.EXE 0x%05X" % g.exe_off(seg, ofs)

    # A: executable layout
    check("A1", "MZ ohne Packer: Bild endet am Dateiende, 1439 Relokationen",
          g.nrel == 1439 and g.hdr + len(g.img) == len(g.exe), "BOMB.EXE 0x0000")
    check("A2", "Turbo-Pascal-6-Laufzeit: Copyright-Text 1983,90 im System-Segment",
          b"Portions Copyright (c) 1983,90 Borland" in sysb, so(0x12CE, sysb.find(b"Portions")))
    check("A3", "System-Init lädt DS = 14D5 (mov dx,14D5; mov ds,dx)", region_ok(g, "A3"), so(0x12CE, 0))
    check("A4", "28 Segmente, davon 10 Overlay-Stubs", len(g.segments) == 28 and len(g.overlays) == 10, "Relokationen")
    # B: overlays
    lastov = g.overlays[-1]
    check("B1", "BOMB.OVR: Kennung FBOV, Längenfeld = Dateilänge - 8, Overlays lückenlos",
          g.ovr[:4] == b"FBOV" and struct.unpack("<I", g.ovr[4:8])[0] == len(g.ovr) - 8
          and lastov["fileofs"] + lastov["codesize"] + lastov["fixsize"] == len(g.ovr), "BOMB.OVR 0x0000")
    o1 = g.overlays[0]["code"]
    check("B2", "O1 enthält die Namen beider Titelbilder", b"bomb1.raw" in o1 and b"bomb2.raw" in o1,
          "BOMB.OVR 0x%05X" % (8 + o1.find(b"bomb1.raw")))
    ovl = g.seg_bytes(0x125A)
    check("B3", "Overlay-Einheit: EMS-Kennung EMMXXXX0 und PATH= im Segment 125A",
          b"EMMXXXX0" in ovl and b"PATH=" in ovl, so(0x125A, ovl.find(b"EMMXXXX0")))
    # C: runtime units
    check("C1", "Crt.Sound: Teiler 1234DDh/Hz, Port 43h←B6h, 42h, 61h|=3",
          region_ok(g, "C1a") and region_ok(g, "C1b"), so(0x11F8, 0x2C7))
    check("C2", "Crt.NoSound: Port 61h &= FCh", region_ok(g, "C2"), so(0x11F8, 0x2F4))
    check("C3", "Crt.Delay: Schleife je ms mit DelayCnt [EC44], Kalibrierung über BIOS-Takt 0040:006C / 55",
          region_ok(g, "C3a") and region_ok(g, "C3b"),
          so(0x11F8, 0x64))
    check("C4", "Dos.GetIntVec/SetIntVec: INT 21h AH=35h/25h",
          region_ok(g, "C4a") and region_ok(g, "C4b"), so(0x11E6, 0xE7))
    gr = g.seg_bytes(0x0E26)
    check("C5", "Graph-Einheit: BGI-Fehlertexte im Segment 0E26", b"Graphics not initialized (use InitGraph)" in gr,
          so(0x0E26, gr.find(b"Graphics not")))
    check("C6", "Printer-Einheit: Text LPT1 am Segmentanfang 0E22", g.seg_bytes(0x0E22)[:5] == b"\x04LPT1", so(0x0E22, 0))
    # D: random generator
    check("D1", "RandSeed = DS:3AD6 (LongInt); Faktor 8405h im Code, Rest als Schiebe-Additionen (08088405h)",
          region_ok(g, "D1"), so(0x12CE, 0x16C4))
    check("D2", "Random(n) = HiWord(RandSeed neu) mod n", region_ok(g, "D2"),
          so(0x12CE, 0x1675))
    check("D3", "Randomize: INT 21h AH=2Ch, RandSeed := CX:DX (Uhrzeit)",
          region_ok(g, "D3"), so(0x12CE, 0x16FC))
    main = g.seg_bytes(0x0000)
    check("D4", "Hauptprogramm ruft Randomize beim Start", far_call(0x12CE, 0x16FC) in main,
          so(0, main.find(far_call(0x12CE, 0x16FC))))
    rnd = sum(1 for _, d, _, _ in g.code_blocks() for _o, s, o in far_calls(d) if (s, o) == (0x12CE, 0x1675))
    check("D5", "Random(n) wird im Spielcode vielfach aufgerufen (>100)", rnd > 100, "%d Aufrufe" % rnd)
    # E: number type
    fl = sum(1 for _, d, _, _ in g.code_blocks() for _o, s, o in far_calls(d) if (s, o) == (0x12CE, 0x11DC))
    game = [d for n, d, _, _ in g.code_blocks() if not is_borland(n)]
    emu = sum(len(find(d, rb"\xcd[\x34-\x3d]")) for d in game)
    x87 = sum(1 for d in game for _o, s, o in far_calls(d) if (s, o) == (0x12CE, 0x16A8))
    check("E1", "Real48 per Software: LongInt→Real (System 11DC) vielfach; im Spielcode keine 8087-Emulator-Interrupts "
          "INT 34h–3Dh und kein Aufruf der 8087-Fassung von Random (System 16A8)",
          fl > 100 and emu == 0 and x87 == 0, "%d Aufrufe 11DC, %d INT 34h–3Dh, %d Aufrufe 16A8" % (fl, emu, x87))
    check("E2", "Real-Hilfen melden Laufzeitfehler 205/207/200 (CDh/CFh/C8h)",
          region_ok(g, "E2"), so(0x12CE, 0x11F0))
    # F: graphics drivers and fonts
    for fid, what, start in BGI_FILES:
        head = REGIONS[fid][1]
        check(fid, what + " eingebunden im Segment 0431 (Dateianfang, Kopf)",
              g.exe[start:start + 2].upper() == b"PK" and start <= head and region_ok(g, fid)
              and g.exe_off(0x0431) <= start < g.exe_off(0x0B02), "BOMB.EXE 0x%05X, Kopf 0x%05X" % (start, head))
    check("F5", "Hauptprogramm: Schritte REGISTER DRIVER, REGISTER FONTS, INITGRAPH, CYCLER, TITLE",
          all(k in main for k in [b"REGISTER DRIVER", b"REGISTER FONTS", b"INITGRAPH", b"CYCLER", b"TITLE"]), so(0, 0))
    udbd = g.seg_bytes(0x0DBD)
    check("F6", "UDBD-Init: InstallUserDriver('vga256'), InstallUserFont EURO LCOM SCRI SIMP TSCR TINY → DS:E910..E91A",
          region_ok(g, "F6") and udbd.count(far_call(0x0E26, 0x1845)) == 6, so(0x0DBD, 0x3BC))
    used = set()
    for _n, d, _f, _b in g.code_blocks():
        for at, s_, o in far_calls(d):
            if (s_, o) == SET_TEXT_STYLE:
                a = pushed_args(d, at, 3)
                used.add(a[0] if a and not isinstance(a[0], tuple) else (a[0][1] if a else None))
    check("F7", "SetTextStyle nur mit DefaultFont (0), SmallFont (2 = LITT), EURO [E910] und TINY [E91A]",
          used == {0, 2, 0xE910, 0xE91A}, "bombre.py fonts")
    o2code = g.overlays[1]["code"]
    starts = proc_starts(o2code)
    texts = {}
    for p in (0x06BD, 0x07BA):
        end = min(x for x in starts if x > p)
        texts[p] = sum(1 for _a, s_, o in far_calls(o2code[p:end]) if (s_, o) == (0x0E26, 0x1F12))
    check("F8", "Hauptmenü: Kopfzeile (O2:06BD) 4 Aufrufe OutTextXY, Knopf START (O2:07BA) 5",
          texts == {0x06BD: 4, 0x07BA: 5}, "O2:06BD %d, O2:07BA %d" % (texts[0x06BD], texts[0x07BA]))
    # G: video, palette
    vga = g.seg_bytes(0x0CC8)
    check("G1", "UCC8: Modus 13h per INT 10h, Fenster 0..319 × 0..199, Segment A000",
          region_ok(g, "G1a") and region_ok(g, "G1b") and region_ok(g, "G1c"),
          so(0x0CC8, 0))
    check("G2", "UCC8: DAC-Ports 3C8h/3C9h und Strahlrücklauf 3DAh", all(mov_dx(p) in vga for p in (0x3C8, 0x3C9, 0x3DA)),
          so(0x0CC8, vga.find(mov_dx(0x3DA))))
    cyc = g.seg_bytes(0x0C79)
    check("G3", "UC79: Palettenrotation hängt an INT 0Ah (IVT 0028h, IRQ2, CRTC-Register 11h) oder INT 1Ch (IVT 0070h)",
          region_ok(g, "G3a") and region_ok(g, "G3b") and region_ok(g, "G3c"), so(0x0C79, 0x275))
    check("G4", "UC79: Rotation alle [E8DD] Takte, Fenster im Palettenpuffer rückt je Schritt um 3 Byte",
          region_ok(g, "G4a") and region_ok(g, "G4b"),
          so(0x0C79, 0x123))
    kb = g.seg_bytes(0x0C48)
    check("G5", "UC48: Tastatur-Handler über Dos.Get/SetIntVec(09h)", b"\xb0\x09" in kb and far_call(0x11E6, 0x00FF) in kb,
          so(0x0C48, 0x19C))
    mouse = g.seg_bytes(0x0E11)
    check("G6", "UE11: Maus über INT 33h", mouse.count(b"\xcd\x33") >= 10, so(0x0E11, mouse.find(b"\xcd\x33")))
    # H: options and timing
    o2 = g.overlays[1]["code"]
    tbl = o2.find(b"\x02TS\x02FS\x02ES\x02SS\x02LS")
    check("H1", "Optionstabelle in O2: TS FS ES SS LS (kein PS, abweichend von BOMB.DOC)",
          tbl > 0 and b"\x02PS" not in o2, "BOMB.OVR 0x%05X" % (g.overlays[1]["fileofs"] + tbl))
    check("H2", "Sound nur über Crt.Sound (PIT-Kanal 2): keine weiteren Zugriffe auf Port 42h/43h",
          sum(len(find(d, rb"\xe6[\x42\x43]")) for n, d, _, _ in g.code_blocks() if n != "seg_11F8") == 0, "alle Codeblöcke")
    dl = sum(1 for _, d, _, _ in g.code_blocks() for _o, s, o in far_calls(d) if (s, o) == (0x11F8, 0x029C))
    check("H3", "Crt.Delay wird im Spielcode aufgerufen", dl > 50, "%d Aufrufe" % dl)
    snd = sum(1 for _, d, _, _ in g.code_blocks() for _o, s, o in far_calls(d) if (s, o) == (0x11F8, 0x02C7))
    check("H4", "Crt.Sound wird im Spielcode aufgerufen", snd > 50, "%d Aufrufe" % snd)

    hits = 0
    for _n, d, _f, _b in g.code_blocks():
        for v in (0xE8C4, 0xE8C6, 0xE8C8, 0xE8CC):
            hits += len(find(d, re.escape(b"\xff\x36" + struct.pack("<H", v)) + far_call(0x11F8, 0x029C)))
    tsv = {}
    for name, var in [("TS", 0xE8C4), ("FS", 0xE8C8), ("ES", 0xE8C6), ("SS", 0xE8CA), ("LS", 0xE8CC)]:
        at = o2.find(b"\x02" + name.encode())
        tsv[name] = at > 0 and bool(find(o2, b"\xbf" + re.escape(struct.pack("<H", at)) + b"\x0e\x57.{0,80}?\xa3"
                                         + re.escape(struct.pack("<H", var))))
    check("H5", "Optionen TS/FS/ES/SS/LS landen in DS:E8C4/E8C8/E8C6/E8CA/E8CC; TS/FS/ES/LS gehen direkt als Millisekunden an Crt.Delay",
          all(tsv.values()) and hits >= 15, "%s, %d Aufrufe Delay([var])" % (tsv, hits))
    check("H6", "Crt.Delay-Sättigung: Kalibrierzähler 16 Bit (CX=FFFFh), DelayCnt höchstens FFFFh/55 = 1191",
          region_ok(g, "H6"), so(0x11F8, 0x77))
    for fid, st, claim, where in results:
        print("%-3s %s %s  [%s]" % (fid, st, claim, where))
    print("Ergebnis: %d von %d Befunden bestätigt" % (sum(r[1] == "OK  " for r in results), len(results)))
    return 0 if ok else 1


def annotate(line, code_names):
    m = re.search(r"call 0x([0-9a-f]+):0x([0-9a-f]+)", line)
    if m:
        k = (int(m.group(1), 16), int(m.group(2), 16))
        name = NAMES.get(k) or ("%s +%04X" % (UNITS[k[0]], k[1]) if k[0] in UNITS else None)
        stub = STUBS.get(k[0])
        if stub and (k[1] - 0x20) % 5 == 0 and 0 <= (k[1] - 0x20) // 5 < len(stub[1]):
            e = (k[1] - 0x20) // 5
            name = "O%d[%d] -> Overlay-Offset %04X" % (stub[0], e, stub[1][e])
        if name:
            return line + "  ; " + name
    return line


# Screen -> procedure map: (screen, overlay index or segment, procedure offset, key text)
SCREENS = [
    ("Titelfolge", "O1", 0x0014, "bomb1.raw"),
    ("Hauptmenü: Kopfzeile", "O2", 0x06BD, "TANK WARS   "),
    ("Hauptmenü: Knopf START", "O2", 0x07BA, "START"),
    ("Hauptmenü: Einträge", "O2", 0x1E7F, "Game Options"),
    ("Hauptmenü: Überschrift", "O2", 0x0909, "Main Menu"),
    ("Game Options: Beschriftungen", "O2", 0x11CC, "Crumble Percent:"),
    ("Game Options: Hintergrundnamen", "O2", 0x0E35, "Stars"),
    ("Game Options: Wandnamen", "O2", 0x0F81, "ELASTIC"),
    ("Game Options: Windstärke", "O2", 0x1042, "Gale"),
    ("Spielerauswahl: Steuerart", "O2", 0x509C, "KEYBOARD"),
    ("Spielerauswahl: Namenseingabe", "O2", 0x42E8, "NAME:"),
    ("Spielerauswahl: Computergegner", "O2", 0x481B, "Random (   "),
    ("Teamspiel", "O2", 0x2749, "Team Setup"),
    ("Spielfeld: Kopfzeile", "O4", 0x44A7, "Angle:"),
    ("Spielfeld: MIRV-Einstellung", "O4", 0x13D4, "MIRV Setup"),
    ("Spielfeld: Wandbeschriftung links (senkrecht)", "O8", 0x0984, "ACCELERATE"),
    ("Spielfeld: Panzerinfo", "O9", 0x0040, "Maximum Power:"),
    ("Spielfeld: Lenkwaffen-Ziel", "O9", 0x092E, "Current Target:"),
    ("Spielstand (Statusfenster)", "S0431", 0x0F1B, "Game Status"),
    ("Wertungstafel", "O3", 0x5AEF, "Played out of "),
    ("Wertungstafel Teams", "O3", 0x6696, "Team"),
    ("Endergebnis", "O3", 0x7566, "Results  "),
    ("Einkauf", "O3", 0x0E1F, "Purchase"),
    ("Statistik Spieler", "O3", 0x2508, "Kill Rate:"),
    ("Sichern", "O3", 0x46DE, "SAVE GAME"),
    ("Laden", "O3", 0x4D97, "Select Game to Load:"),
]


def cmd_karte(g, args):
    """Check and print the screen -> procedure map."""
    ok = True
    blocks = {}
    for name, data, f, base in g.code_blocks():
        key = "O%s" % name.split("_")[1][1:] if name.startswith("ovl") else "S" + name[4:]
        blocks[key] = (name, data, f, base)
    for screen, blk, proc, text in SCREENS:
        name, data, f, base = blocks[blk]
        strs = {o: s for o, s in pascal_strings(data, 2)}
        starts = proc_starts(data)
        nxt = min([x for x in starts if x > proc], default=len(data))
        refs = [strs[struct.unpack("<H", m.group(1))[0]] for m in re.finditer(rb"\xbf(..)\x0e\x57", data[proc:nxt], re.S)
                if struct.unpack("<H", m.group(1))[0] in strs]
        good = proc in starts and any(r.strip().startswith(text.strip()) for r in refs)
        ok &= good
        print("%-4s %-46s %s:%04X  %s 0x%05X" % ("OK" if good else "FAIL", screen, blk, proc, f, base + proc))
    return 0 if ok else 1


STUBS = {}


def cmd_listings(g, args):
    STUBS.update({o["stub"]: (i, o["entries"]) for i, o in enumerate(g.overlays, 1)})
    if not shutil.which("ndisasm"):
        sys.exit("ndisasm (nasm) fehlt")
    d = os.path.join(args.out, "listings")
    os.makedirs(d, exist_ok=True)
    tmp = os.path.join(args.out, ".tmp")
    os.makedirs(tmp, exist_ok=True)
    count = 0
    for name, data, f, base in g.code_blocks():
        binp = os.path.join(tmp, name + ".bin")
        with open(binp, "wb") as fh:
            fh.write(data)
        # skip string constants so that the instruction stream stays aligned
        skips = []
        for o, s in pascal_strings(data, 4):
            skips += ["-k", "%d,%d" % (o, len(s) + 1)]
        out = subprocess.run(["ndisasm", "-b16"] + skips + [binp], capture_output=True, text=True, check=True).stdout
        seg = int(name[-4:], 16)
        hdr = ["; Tank Wars 3.2 - %s (%s), Dateioffset von Offset 0: %s 0x%05X" % (name, UNITS.get(seg, "?"), f, base),
               "; Erzeugt mit tools/reverse-engineering/bombre.py listings (ndisasm). Abgeleitet aus Originalcode:",
               "; nur im Referenz-Repo (analyse/) ablegen, nie im Code-Repo.",
               "; Spalten: Offset im Block, Bytes, Befehl; '; Name' = bekannte Ziele (Befunde in docs/analyse/re-grundlagen.md)."]
        if name.startswith("ovl"):
            ov = next(o for o in g.overlays if o["stub"] == seg)
            hdr.append("; Einsprünge (Stub %04X): %s" % (seg, " ".join("%04X" % e for e in ov["entries"])))
        body = [annotate(l, NAMES) for l in out.splitlines()]
        with open(os.path.join(d, name + ".asm"), "w", encoding="ascii", errors="replace") as fh:
            fh.write("\n".join(hdr + body) + "\n")
        count += 1
    shutil.rmtree(tmp)
    print("%d Listings in %s" % (count, d))


def main():
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--gamedata", default=os.path.join(root, "gamedata"))
    ap.add_argument("--out", default=os.path.join(root, "analyse", "re-grundlagen"))
    ap.add_argument("cmd", choices=["segments", "overlays", "strings", "xref", "fonts", "karte", "befunde", "listings", "all"])
    args = ap.parse_args()
    g = Game(args.gamedata)
    cmds = {"segments": cmd_segments, "overlays": cmd_overlays, "strings": cmd_strings, "xref": cmd_xref,
            "befunde": cmd_befunde, "listings": cmd_listings, "fonts": cmd_fonts, "karte": cmd_karte}
    rc = 0
    for c in (cmds if args.cmd == "all" else [args.cmd]):
        rc |= cmds[c](g, args) or 0
    return rc


if __name__ == "__main__":
    sys.exit(main())
