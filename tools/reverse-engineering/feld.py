#!/usr/bin/env python3
"""Probe for the playfield set-up (spur gelaende, R2-D7 = A): runs the original code of BOMB.EXE and
BOMB.OVR in an emulated PC and records the screen, the background buffer, the DAC and the state
after O8:0984 (Spielfeldaufbau) and after O4:44A7 up to the first player (wind, start player).

Docs: docs/analyse/feld.md. The script holds no bytes of the originals (AGENTS.md section 2.1):
code regions are checked by SHA-256 only; outputs go to analyse/feld/ (protected, git-ignored).

Usage:
  feld.py [--gamedata DIR] befunde [--print]     check the code regions the probe relies on
  feld.py [...] run --seed HEX [--players N] [--wall W] [--background B] [--mask M] [--wind W]
                    [--invisible] [--no-fall] [--out DIR]
                                                 one case; writes screen/background/DAC dumps and result.json
  feld.py [...] call [--fill C] [--show] SEG:OFS(args) ...
                                                 after the boot: far call routines (segment of the load image or
                                                 On:ofs) with Pascal arguments, dump the screen (primitives)
  feld.py [...] cpu [--count N]                  cross-check the interpreter against real48.py (wave 1 cases)
  feld.py [...] hashes [--only I ...] [--write]  fixed case list -> SHA-256 per dump (TSV on stdout and in
                                                 analyse/feld/hashes.tsv); the C# tests compare against these

How the probe runs (all original code is executed, nothing is re-implemented):
  1. Boot BOMB.EXE from its entry point with a default BOMB.CFG built from docs/formate/konfiguration.md
     (column "Beispiel"); the title sequence (0000:046E, O1) is skipped. Stop before the main menu (0000:04DB).
  2. Load the ten overlays with their fixups into fixed segments and point every stub entry there (the
     overlay manager is no longer involved).
  3. O2:0216(n) for n = 1..10 (default player records, colours), then the options and the number of
     players are written to their variables (DS:47DA, DS:E8A8 ...).
  4. Far call O3[0] (O3:7847). At O3:7876 (after the shuffle O3:2417, before O4[0]) the records, control
     types and intelligence pointers saved before O3[0] are written back (undoes the shuffle: record i =
     player i) and RandSeed is set to --seed; from here the Random calls are counted.
  5. Snapshots: O4:44CC (return from O8[0] = O8:0984), O4:45F1 (wind done), O4:46FF (header and weapon bar
     done), O4:479A (start player chosen; the run stops there).
"""
import argparse
import hashlib
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bombre  # noqa: E402
import feld_machine as fm  # noqa: E402
from feld_cpu import CS, SS, SP  # noqa: E402

DATA_SEG = 0x14D5
RANDSEED = 0x3AD6
RANDOM_N = (0x12CE, 0x1675)
RANDOM_REAL = (0x12CE, 0x168B)
MAIN_SKIP_TITLE = (0x046E, 0x0473)
MAIN_BEFORE_MENU = 0x04DB
O2_DEFAULT_PLAYER = 0x0216
O3_AFTER_SHUFFLE = 0x7876
O4_AFTER_O8 = 0x44CC
O4_AFTER_WIND = 0x45F1
O4_AFTER_HEADER = 0x46FF
O4_START_PLAYER = 0x479A
O8_BACKGROUND = 0x0000
RECORD = 0x402E
RECORD_SIZE = 0x74

# Code regions the probe relies on: (overlay number or segment, start, end) -> SHA-256 of the bytes.
# Computed with 'befunde --print'; the bytes themselves are never stored here.
REGIONS = {
    "O8 Spielfeldaufbau": ("ovl", 8, 0x0000, 0x1268),
    "O4 Rundenbeginn bis Startspieler": ("ovl", 4, 0x44A7, 0x47A2),
    "O3 Spielfolge bis O4[0]": ("ovl", 3, 0x7847, 0x787B),
    "O2 Spielersatz": ("ovl", 2, 0x0216, 0x04A2),
    "Hauptprogramm bis Hauptmenue": ("seg", 0x0000, 0x00FA, 0x04E0),
}
REGION_HASHES = {
    "O8 Spielfeldaufbau":
        "00458132467260369fe9a77f58ddf46d753ebe2e35d05432eefac5d25e3409c9",
    "O4 Rundenbeginn bis Startspieler":
        "2297cf2f1e6c4e7a95ac7b7607a53c563b2df6f093c09ffebd49503c38441e7b",
    "O3 Spielfolge bis O4[0]":
        "ce762fc5b584d50db236d80895e0fb9fdbc5a0c9bd732a6a70b3dae8734db3bd",
    "O2 Spielersatz":
        "e3b23b3e5756aa80d6e3d26b473e4608057f3da379a65c85305138e61a472ed1",
    "Hauptprogramm bis Hauptmenue":
        "49010854ea0df577838c3bc4dceebccf42c2242bafd3bbacd6b98151f260ef5f",
}

# Default BOMB.CFG (docs/formate/konfiguration.md, column "Beispiel"): (size, value) per field.
DEFAULT_CFG = [
    (5, 0x00322E3304), (1, 1), (1, 0), (2, 2), (2, 75), (2, 0), (1, 0), (1, 1), (2, 10), (1, 1), (2, 9),
    (2, 0xFFFF), (1, 0), (1, 1), (1, 1), (1, 1), (1, 1), (1, 1), (2, 0), (1, 1), (2, 8), (2, 5), (2, 8),
    (2, 10), (2, 5), (1, 1), (1, 0), (2, 63),
]


def default_cfg():
    return b"".join(v.to_bytes(n, "little") for n, v in DEFAULT_CFG)


def lin(seg, ofs):
    return ((seg << 4) + (ofs & 0xFFFF)) & 0xFFFFF


class Probe:
    def __init__(self, gamedata, font):
        self.game = bombre.Game(gamedata)
        self.gamedata = gamedata
        self.font = font

    def region_bytes(self, kind, which, start, end):
        if kind == "ovl":
            return self.game.overlays[which - 1]["code"][start:end]
        return self.game.seg_bytes(which)[start:end]

    def boot(self):
        m = fm.Machine(self.gamedata, font8x8=self.font, extra_files={"BOMB.CFG": default_cfg()})
        c = m.cpu
        load = fm.LOAD_SEG

        def skip_title(mm):
            mm.cpu.ip = MAIN_SKIP_TITLE[1]
            return False

        m.traps[lin(load, MAIN_SKIP_TITLE[0])] = skip_title
        stop = lin(load, MAIN_BEFORE_MENU)
        m.run(stop=lambda mm: lin(c.S[CS], c.ip) == stop, limit=20_000_000)
        del m.traps[lin(load, MAIN_SKIP_TITLE[0])]
        self.install_overlays(m)
        return m

    def install_overlays(self, m):
        load = fm.LOAD_SEG
        seg = 0xC000
        self.ovseg = {}
        for i, o in enumerate(self.game.overlays, 1):
            code = bytearray(o["code"])
            for x in o["fixups"]:
                v = struct.unpack_from("<H", code, x)[0]
                struct.pack_into("<H", code, x, (v + load) & 0xFFFF)
            m.mem[seg << 4:(seg << 4) + len(code)] = code
            self.ovseg[i] = seg
            for k, e in enumerate(o["entries"]):
                a = lin(load + o["stub"], 0x20 + 5 * k)
                m.mem[a:a + 5] = bytes([0xEA]) + struct.pack("<HH", e, seg)
            seg += (len(code) + 15) // 16 + 1
        if seg > 0xE000:
            raise RuntimeError("overlays do not fit below E000h")

    def ds(self):
        return fm.LOAD_SEG + DATA_SEG

    def wds(self, m, ofs, value, size=2):
        a = lin(self.ds(), ofs)
        m.mem[a:a + size] = (value & ((1 << (8 * size)) - 1)).to_bytes(size, "little")

    def rds(self, m, ofs, size=2, signed=False):
        a = lin(self.ds(), ofs)
        return int.from_bytes(m.mem[a:a + size], "little", signed=signed)

    def run_case(self, case):
        m = self.boot()
        c = m.cpu
        load = fm.LOAD_SEG
        for n in range(1, 11):
            m.far_call(self.ovseg[2], O2_DEFAULT_PLAYER, args=(n,), limit=c.steps + 2_000_000)
        n_players = case["players"]
        self.wds(m, 0x47DA, n_players)
        self.wds(m, 0xE8A8, case["wind"])
        self.wds(m, 0xE8AA, case["wall"])
        self.wds(m, 0xE8AC, case["background"])
        self.wds(m, 0xE8AE, case["mask"])
        self.wds(m, 0xE8B4, 1 if case["invisible"] else 0, 1)
        self.wds(m, 0xE8B8, 0 if case["no_fall"] else 1, 1)
        counts = {"random_n": 0, "random_real": 0, "counting": False}
        snaps = {}
        backgrounds = []

        def count(key):
            def f(mm):
                if counts["counting"]:
                    counts[key] += 1
                return False
            return f

        m.traps[lin(load + RANDOM_N[0], RANDOM_N[1])] = count("random_n")
        m.traps[lin(load + RANDOM_REAL[0], RANDOM_REAL[1])] = count("random_real")

        # The shuffle O3:2417 swaps whole records (O3:0000). It is undone so that record i is player i
        # with the defaults of O2:0216(i); the probe input is RandSeed after the shuffle (runde.md section 4).
        saved = [(a, bytes(m.mem[a:a + n])) for a, n in (
            (lin(self.ds(), RECORD), RECORD_SIZE * 11), (lin(self.ds(), 0xE5A6), 2 * 11), (lin(self.ds(), 0x3BE8), 4 * 11))]

        def set_seed(mm):
            for a, b in saved:
                mm.mem[a:a + len(b)] = b
            self.wds(mm, RANDSEED, case["seed"], 4)
            counts["counting"] = True
            mm.dac_written.clear()
            return False

        m.traps[lin(self.ovseg[3], O3_AFTER_SHUFFLE)] = set_seed

        def on_background(mm):
            backgrounds.append(mm.cpu.rw(mm.cpu.S[SS], mm.cpu.R[SP] + 4))  # far procedure (push cs; call near)
            return False

        m.traps[lin(self.ovseg[8], O8_BACKGROUND)] = on_background

        def snap(name, stop=False):
            def f(mm):
                bg_ofs, bg_seg = struct.unpack_from("<HH", mm.mem, lin(self.ds(), 0xE5C0))
                b = lin(bg_seg, bg_ofs)
                snaps[name] = {
                    "screen": bytes(mm.mem[0xA0000:0xA0000 + 64000]),
                    "background": bytes(mm.mem[b:b + 64000]),
                    "dac": bytes(mm.dac),
                    # entries written since O3:7876: index + 6-bit RGB, ascending (compared by the C# tests)
                    "dac_set": b"".join(bytes([i]) + bytes(mm.dac[3 * i:3 * i + 3]) for i in sorted(mm.dac_written)),
                    "randseed": self.rds(mm, RANDSEED, 4),
                    "random_n": counts["random_n"],
                    "random_real": counts["random_real"],
                    "steps": mm.cpu.steps,
                }
                return stop
            return f

        m.traps[lin(self.ovseg[4], O4_AFTER_O8)] = snap("o8")
        m.traps[lin(self.ovseg[4], O4_AFTER_WIND)] = snap("wind")
        m.traps[lin(self.ovseg[4], O4_AFTER_HEADER)] = snap("header")
        m.traps[lin(self.ovseg[4], O4_START_PLAYER)] = snap("start", stop=True)
        m.far_call(load + self.game.overlays[2]["stub"], 0x20, limit=c.steps + 400_000_000)
        if "start" not in snaps:
            raise RuntimeError("O4:479A not reached")
        tanks = []
        for i in range(1, n_players + 1):
            r = RECORD + RECORD_SIZE * i
            tanks.append({"x": self.rds(m, r + 0x0F, signed=True), "y": self.rds(m, r + 0x11, signed=True),
                          "angle": self.rds(m, r + 0x1D, signed=True), "power": self.rds(m, r + 0x1B, signed=True),
                          "dir": self.rds(m, r + 0x1F, signed=True), "rounds_started": self.rds(m, r + 0x72)})
        result = {
            "case": case,
            "background_drawn": backgrounds,
            "wall_round": self.rds(m, 0x47EC),
            "wall_factor_real48": bytes(m.mem[lin(self.ds(), 0x47D2):lin(self.ds(), 0x47D2) + 6]).hex(),
            "wind_level": self.rds(m, 0x47E4),
            "wind": self.rds(m, 0xE8A2, signed=True),
            "start_player": self.rds(m, 0x47EE, 1),
            "pass": self.rds(m, 0x47EA),
            "tanks": tanks,
            "snapshots": {k: {kk: vv for kk, vv in v.items() if not isinstance(vv, bytes)} for k, v in snaps.items()},
            "log": [x for x in m.log if not x.startswith("open ")],
        }
        return result, snaps


def sha(b):
    return hashlib.sha256(b).hexdigest()


def core_screen(screen):
    """Screen without the BGI texts of O8:0984 (feld.md section 2): wall label x 0..6, name bar x 109..211, y 0..7.
    This is what Core/Playfield draws; the UI adds the texts (compared in full at A by the UI tests)."""
    b = bytearray(screen)
    for y in range(200):
        b[y * 320:y * 320 + 7] = bytes(7)
    for y in range(8):
        b[y * 320 + 109:y * 320 + 212] = bytes(103)
    return bytes(b)


def parse_case(args):
    return {"seed": int(args.seed, 16) & 0xFFFFFFFF, "players": args.players, "wall": args.wall,
            "background": args.background, "mask": int(args.mask, 0), "wind": args.wind,
            "invisible": args.invisible, "no_fall": args.no_fall}


def cmd_befunde(p, args):
    ok = True
    for key, (kind, which, s, e) in REGIONS.items():
        got = sha(p.region_bytes(kind, which, s, e))
        if args.print:
            print(key, "%s %s %04X-%04X" % (kind, which, s, e), got)
            continue
        good = got == REGION_HASHES[key]
        ok &= good
        print(("ok    " if good else "FEHLT ") + key)
    return 0 if ok else 1


def parse_target(p, text):
    """'O8:0351' (overlay n) or '0CC8:0351' (segment of the load image) -> (segment, offset) in the machine."""
    seg, ofs = text.split(":")
    if seg.upper().startswith("O"):
        return p.ovseg[int(seg[1:])], int(ofs, 16)
    return fm.LOAD_SEG + int(seg, 16), int(ofs, 16)


def cmd_call(p, args):
    """Boot, optionally clear the screen to a colour, far call one routine with Pascal arguments, dump the screen."""
    m = p.boot()
    if args.fill is not None:
        m.mem[0xA0000:0xA0000 + 64000] = bytes([args.fill]) * 64000
    for text in args.call:
        name, _, rest = text.partition("(")
        values = [int(v, 0) for v in rest.rstrip(")").split(",") if v.strip()] if rest else []
        seg, ofs = parse_target(p, name)
        m.far_call(seg, ofs, args=values, limit=m.cpu.steps + 50_000_000)
    screen = bytes(m.mem[0xA0000:0xA0000 + 64000])
    os.makedirs(args.root_out, exist_ok=True)
    path = os.path.join(args.root_out, "call-screen.bin")
    with open(path, "wb") as f:
        f.write(screen)
    print("screen", sha(screen), path)
    if args.show:
        base = args.fill or 0
        for y in range(200):
            row = screen[y * 320:(y + 1) * 320]
            if any(v != base for v in row):
                xs = [x for x in range(320) if row[x] != base]
                print("%3d %3d..%3d %s" % (y, xs[0], xs[-1], " ".join("%d:%d" % (x, row[x]) for x in xs[:24])))
    return 0


def cmd_barrel(p, args):
    """Barrel ends of U431:07BA for the angles 0..180: record 1 at (160, 100), direction +1, on a cleared screen; the end
    is the drawn pixel farthest from (160, 100) outside the tank body. Prints 'angle dx dy' (dy upwards)."""
    m = p.boot()
    m.far_call(p.ovseg[2], O2_DEFAULT_PLAYER, args=(1,), limit=m.cpu.steps + 2_000_000)
    r = RECORD + RECORD_SIZE
    body = set()
    for dx in range(-4, 5):
        body.add((dx, 0))
        if dx not in (-1, 1):
            body.add((dx, -1))
        if -3 <= dx <= 3:
            body.update({(dx, -2), (dx, -3)})
        if -1 <= dx <= 1:
            body.add((dx, -4))
    rows = []
    for angle in range(181):
        m.mem[0xA0000:0xA0000 + 64000] = bytes(64000)
        for ofs, value in ((0x0F, 160), (0x11, 100), (0x1D, angle), (0x1F, 1), (0x21, 100)):
            p.wds(m, r + ofs, value)
        p.wds(m, r + 0x23, 1, 1)
        m.far_call(fm.LOAD_SEG + 0x0431, 0x07BA, args=(1,), limit=m.cpu.steps + 5_000_000)
        pts = [(i % 320 - 160, i // 320 - 100) for i in range(64000) if m.mem[0xA0000 + i]]
        far = max((q for q in pts if q not in body), key=lambda q: q[0] * q[0] + q[1] * q[1])
        rows.append("%d %d %d" % (angle, far[0], -far[1]))
    print("\n".join(rows))
    return 0


def cmd_cpu(p, args):
    """Cross-check feld_cpu against the Cpu of real48.py on the Real48/Random cases of wave 1."""
    import real48
    import feld_cpu
    rt = real48.Runtime(p.gamedata)
    ok = True
    for op in real48.ENTRIES:
        ref = real48.op_hash(rt, op, args.count)
        saved = real48.Cpu
        real48.Cpu = lambda mem: feld_cpu.Cpu(mem)
        try:
            got = real48.op_hash(rt, op, args.count)
        finally:
            real48.Cpu = saved
        ok &= got == ref
        print(("ok    " if got == ref else "FEHLT ") + "%s (%d Faelle)" % (op, args.count))
    return 0 if ok else 1


def write_case(out, result, snaps):
    os.makedirs(out, exist_ok=True)
    for name, s in snaps.items():
        for k in ("screen", "background", "dac", "dac_set"):
            with open(os.path.join(out, "%s-%s.bin" % (name, k)), "wb") as f:
                f.write(s[k])
    with open(os.path.join(out, "result.json"), "w") as f:
        json.dump(result, f, indent=1)


def cmd_run(p, args):
    case = parse_case(args)
    result, snaps = p.run_case(case)
    out = args.out or os.path.join(args.root_out, "seed-%08X-n%d-w%d-b%d" % (
        case["seed"], case["players"], case["wall"], case["background"]))
    write_case(out, result, snaps)
    print(json.dumps({k: v for k, v in result.items() if k not in ("snapshots", "log")}))
    for name, s in snaps.items():
        print(name, "RandSeed %08X" % s["randseed"], "Random(n) %d" % s["random_n"],
              "Random:Real %d" % s["random_real"], "screen", sha(s["screen"])[:16], "background",
              sha(s["background"])[:16], "dac_set", sha(s["dac_set"])[:16], len(s["dac_set"]) // 4)
    print(out)
    return 0


# Fixed cases for the C# comparison (tests/BombRedone.Core.Tests/Playfield/PlayfieldProbeTests.cs): seed (RandSeed after the shuffle), players, wall, background, mask,
# wind, invisible, no_fall. Chosen to cover every wall label, every background, 2..10 players and the
# random options (5 = RND for wall/wind, 9 = RND and 10 = USER for the background).
CASES = [
    (0x12345678, 2, 0, 0, 0xFFFF, 2, False, False),
    (0x00000001, 3, 1, 1, 0xFFFF, 0, False, False),
    (0x9E3779B9, 4, 2, 2, 0xFFFF, 1, False, False),
    (0xDEADBEEF, 5, 3, 3, 0xFFFF, 3, False, False),
    (0x0BADF00D, 6, 4, 4, 0xFFFF, 4, False, False),
    (0x7FFFFFFF, 7, 5, 5, 0xFFFF, 5, False, False),
    (0x80000000, 8, 1, 6, 0xFFFF, 2, True, False),
    (0xCAFEBABE, 9, 2, 7, 0xFFFF, 2, False, True),
    (0x13579BDF, 10, 3, 8, 0xFFFF, 4, False, False),
    (0x2468ACE0, 2, 5, 9, 0xFFFF, 5, False, False),
    (0xFEDCBA98, 3, 5, 10, 0x0124, 5, False, False),
    # wind 4000 with three hits: -548 * 64 overflows 16 bits to 30464 and is clamped to 30000; -977 * 64 gives 3008 (#98)
    (0x00015446, 2, 0, 0, 0xFFFF, 4, False, False),
    (0x002B423B, 2, 0, 0, 0xFFFF, 4, False, False),
]


def cmd_hashes(p, args):
    rows = []
    header = "seed\tplayers\twall\tbackground\tmask\twind_opt\tinvisible\tno_fall\tsnapshot\trandseed\trandom_n\t" \
             "random_real\tscreen\tbackground_buffer\tdac_set\tdac_entries\twall_round\tbackground_drawn\twind\tstart_player\ttanks\t" \
             "screen_core\twind_level\tpass"
    print(header)
    selected = CASES if args.only is None else [CASES[i] for i in args.only]
    for seed, n, wall, bg, mask, wind, inv, nofall in selected:
        case = {"seed": seed, "players": n, "wall": wall, "background": bg, "mask": mask, "wind": wind,
                "invisible": inv, "no_fall": nofall}
        result, snaps = p.run_case(case)
        if args.write:
            write_case(os.path.join(args.root_out, "seed-%08X-n%d-w%d-b%d" % (seed, n, wall, bg)), result, snaps)
        tanks = " ".join("%d,%d" % (t["x"], t["y"]) for t in result["tanks"])
        for name, s in snaps.items():
            row = "%08X\t%d\t%d\t%d\t%04X\t%d\t%d\t%d\t%s\t%08X\t%d\t%d\t%s\t%s\t%s\t%d\t%d\t%s\t%d\t%d\t%s\t%s\t%d\t%d" % (
                seed, n, wall, bg, mask, wind, inv, nofall, name, s["randseed"], s["random_n"], s["random_real"],
                sha(s["screen"]), sha(s["background"]), sha(s["dac_set"]), len(s["dac_set"]) // 4, result["wall_round"],
                ",".join(map(str, result["background_drawn"])), result["wind"], result["start_player"], tanks,
                sha(core_screen(s["screen"])), result["wind_level"], result["pass"])
            rows.append(row)
            print(row, flush=True)
    os.makedirs(args.root_out, exist_ok=True)
    with open(os.path.join(args.root_out, "hashes.tsv"), "w") as f:
        f.write(header + "\n" + "\n".join(rows) + "\n")
    return 0


def main():
    root = os.path.normpath(os.path.join(HERE, "..", ".."))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--gamedata", default=os.path.join(root, "gamedata"))
    ap.add_argument("--font", default=os.path.expanduser("~/referenz/schriften/dosbox-0.74-3-vga-8x8.bin"),
                    help="8x8 VGA font (2048 bytes) for the BIOS font area; default from the reference clone")
    ap.add_argument("--root-out", default=os.path.join(root, "analyse", "feld"))
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("befunde")
    b.add_argument("--print", action="store_true")
    r = sub.add_parser("run")
    r.add_argument("--seed", required=True)
    r.add_argument("--players", type=int, default=2)
    r.add_argument("--wall", type=int, default=0)
    r.add_argument("--background", type=int, default=0)
    r.add_argument("--mask", default="0xFFFF")
    r.add_argument("--wind", type=int, default=2)
    r.add_argument("--invisible", action="store_true")
    r.add_argument("--no-fall", action="store_true")
    r.add_argument("--out")
    a = sub.add_parser("call")
    a.add_argument("call", nargs="+", help="routine with Pascal arguments, e.g. 0CC8:09E4(10,20,30,40,5) or O8:0000(3)")
    a.add_argument("--fill", type=lambda v: int(v, 0))
    a.add_argument("--show", action="store_true", help="print the changed pixels per row")
    sub.add_parser("barrel")
    k = sub.add_parser("cpu")
    k.add_argument("--count", type=int, default=2000)
    h = sub.add_parser("hashes")
    h.add_argument("--only", type=int, nargs="*", help="indices into the case list")
    h.add_argument("--write", action="store_true", help="also write the dumps of every case")
    args = ap.parse_args()
    font = None
    if os.path.exists(args.font):
        with open(args.font, "rb") as f:
            font = f.read()
        if len(font) != 2048:
            sys.exit("font must have 2048 bytes")
    elif args.cmd not in ("befunde", "cpu"):
        print("Warnung: keine 8x8-Schrift (%s), Text mit DefaultFont bleibt leer" % args.font, file=sys.stderr)
    p = Probe(args.gamedata, font)
    return {"befunde": cmd_befunde, "run": cmd_run, "hashes": cmd_hashes, "cpu": cmd_cpu, "call": cmd_call,
            "barrel": cmd_barrel}[args.cmd](p, args)


if __name__ == "__main__":
    sys.exit(main())
