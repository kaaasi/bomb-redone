#!/usr/bin/env python3
"""Probe for the turn (spur zug, R3-D2 = A): continues a feld.py case at O4:479A (start player chosen) and lets the
original O4:44A7 run on: turn start Z1, header, the turn O7:040C with its input routine O7:0EF7, the tank info O9:0040,
the target selection O9:092E and the MIRV set-up dialog at the start of O4:13D4. The input is fed in as held keys
(DS:452A + scancode), BIOS keys (keyboard buffer 0040:001E) and mouse states (INT 33h); Crt.Delay is skipped and
counted (the turn clock).

Docs: docs/analyse/zug.md. No bytes of the originals in this script (AGENTS.md section 2.1): code regions are checked
by SHA-256 only; outputs go to analyse/feld/ (protected, git-ignored).

Usage:
  zug.py [--gamedata DIR] befunde [--print]       check the code regions the probe relies on
  zug.py [...] run --seed HEX [feld.py case options] [--shooter N] [--control 1|2] [--set FIELD=V ...]
                   [--steps "STEP;STEP;..."] [--mirv] [--out DIR]
                                                  one turn; writes result.json and the final screen
  zug.py [...] hashes [--only I ...]              fixed turn list -> analyse/feld/zug-hashes.tsv
  zug.py [...] folge [--only I ...]               fixed turn order list (turns skipped) -> analyse/feld/zugfolge.tsv

Input model (what a step is, docs/analyse/zug.md section 2):
  - A step sets the whole input state: the held keys (names below, joined with '+'; all others are released),
    optionally a mouse state "m:X,Y,B" (x 0..319, y 0..199, buttons bit 0 left, bit 1 right; without it the buttons
    are released and the position stays) and BIOS keys "b:NAME" appended to the keyboard buffer. Parts of a step are
    separated by spaces, steps by ';'. "-" is an empty step (nothing held).
  - Steps are consumed at the poll sites (SITES, kind "step"): each entry of O7:0EF7, each pass of the cheat loop,
    the waits of O9:0040, the polling loops of O9:092E and of the MIRV dialog. Without a step left the run stops
    ("exhausted").
  - At the wait-for-release loops (kind "release") the keys or buttons the loop waits for are released (the player
    lets go); no step is consumed.
"""
import argparse
import hashlib
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import feld  # noqa: E402
import feld_machine as fm  # noqa: E402
from feld_cpu import AX, BX, CX, DX, BP, SP, SS, CS  # noqa: E402

KEYS = 0x452A              # held keys: DS:452A + scancode (+100h for E0 keys)
MOUSE_PRESENT = 0xE94E
CONTROL = 0xE5A6           # control type of player i: DS:E5A6 + 2i (1 keyboard, 2 mouse, 3 computer)
CRT_DELAY = (0x11F8, 0x029C)
O4_START_PLAYER = 0x479A
O4_AFTER_Z1 = 0x4825
O4_CALL_TURN = 0x4960
O4_AFTER_TURN = 0x4965
O4_NEXT_PLAYER = 0x4770
O4_MIRV_END = 0x1AB9       # dialog done, screen restored next
O7_POLL_RETURN = 0x0551    # O7:040C after the call of O7:0EF7 (AX = command)

SC = {"Esc": 0x01, "1": 0x02, "2": 0x03, "3": 0x04, "4": 0x05, "5": 0x06, "6": 0x07, "7": 0x08, "8": 0x09,
      "9": 0x0A, "0": 0x0B, "Tab": 0x0F, "W": 0x11, "Enter": 0x1C, "Ctrl": 0x1D, "D": 0x20, "G": 0x22, "H": 0x23,
      "I": 0x17, "LShift": 0x2A, "C": 0x2E, "V": 0x2F, "RShift": 0x36, "Alt": 0x38, "Space": 0x39,
      "KpUp": 0x48, "KpPgUp": 0x49, "KpLeft": 0x4B, "KpRight": 0x4D, "KpDown": 0x50, "KpPgDn": 0x51,
      "KpIns": 0x52, "KpDel": 0x53, "KpEnter": 0x11C, "RAlt": 0x138,
      "Up": 0x148, "PgUp": 0x149, "Left": 0x14B, "Right": 0x14D, "Down": 0x150, "PgDn": 0x151,
      "Ins": 0x152, "Del": 0x153}
# BIOS keys (INT 16h AH=0 word): ASCII, scancode.
BIOS = {"Enter": (13, 0x1C), "Space": (32, 0x39), "Esc": (27, 0x01), "Right": (0, 0x4D), "Left": (0, 0x4B),
        "Up": (0, 0x48), "Down": (0, 0x50), "A": (ord("a"), 0x1E), "X": (ord("x"), 0x2D)}
MOUSE = "mouse"

# Poll sites: (overlay, offset) -> ("step", name) or ("release", name, scancodes or MOUSE).
SITES = {
    (7, 0x0EF7): ("step", "0EF7"),
    (7, 0x0FA6): ("step", "cheat"),
    (7, 0x1097): ("release", "I", (0x17,)),
    (7, 0x10AA): ("release", "H", (0x23,)),
    (7, 0x10BD): ("release", "V", (0x2F,)),
    (7, 0x10DE): ("release", "Shift+Tab", (0x0F,)),
    (7, 0x10F1): ("release", "Tab", (0x0F,)),
    (7, 0x1104): ("release", "I", (0x17,)),
    (7, 0x1203): ("release", "Left", (0x4B, 0x14B)),
    (7, 0x1232): ("release", "Right", (0x4D, 0x14D)),
    (7, 0x125C): ("release", "Down", (0x50, 0x150)),
    (7, 0x1290): ("release", "Up", (0x48, 0x148)),
    (7, 0x12DC): ("release", "PgUp", (0x49, 0x149)),
    (7, 0x1328): ("release", "PgDn", (0x51, 0x151)),
    (7, 0x13EB): ("release", "mouse bar", MOUSE),
    (7, 0x1428): ("release", "mouse Herb", MOUSE),
    (7, 0x1465): ("release", "mouse Vick", MOUSE),
    (7, 0x1562): ("release", "mouse direction", MOUSE),
    (7, 0x0E97): ("release", "mouse fire", MOUSE),
    (9, 0x0866): ("step", "info key"),
    (9, 0x0875): ("step", "info mouse"),
    (9, 0x0B33): ("step", "target key"),
    (9, 0x0CE4): ("step", "target mouse"),
    (4, 0x157A): ("release", "mirv mouse 1", MOUSE),
    (4, 0x15BE): ("step", "mirv key 1"),
    (4, 0x16B1): ("step", "mirv mouse 1"),
    (4, 0x18A4): ("release", "mirv mouse 2", MOUSE),
    (4, 0x18E8): ("step", "mirv key 2"),
    (4, 0x19FC): ("step", "mirv mouse 2"),
}

# Code regions the probe relies on: (overlay number or segment, start, end) -> SHA-256 ('befunde --print').
REGIONS = {
    "O7 Zug und Eingaben": ("ovl", 7, 0x040C, 0x15C8),
    "O9 Panzerinfo und Zielwahl": ("ovl", 9, 0x0040, 0x0DDC),
    "O4 MIRV-Dialog": ("ovl", 4, 0x13D4, 0x1AE1),
    "O4 Zugbeginn und Zugfolge": ("ovl", 4, 0x4704, 0x49CB),
    "0C48 Tastatur": ("seg", 0x0C48, 0x0000, 0x030C),
    "0E11 Maus": ("seg", 0x0E11, 0x0000, 0x0109),
    "0DBD Maus x/y": ("seg", 0x0DBD, 0x022A, 0x026B),
}
REGION_HASHES = {
    "O7 Zug und Eingaben":
        "c8d2d3b149ba1bf4bf79af3cf024046dc5d26db1cc7f4fc7e4973eb6136b5aaa",
    "O9 Panzerinfo und Zielwahl":
        "0bbf982e7c97eba8ac830ff15bb0d3abc8ba52efb9c1aa9641202ef741474e9e",
    "O4 MIRV-Dialog":
        "d5ac74106e8c5ba2548e7a7786708b417074877b8abc672a1dc48e87ebede113",
    "O4 Zugbeginn und Zugfolge":
        "acaebb47e6af6de63899a39692cdd413fa68515155b1b320b6a73bfec65560be",
    "0C48 Tastatur":
        "e93ab4f12e43aae3e8e32bc1ed54a7ce94167f547498687aa7b082fe61849e3a",
    "0E11 Maus":
        "b9c317c1a60b2e2e45c7c842fcfa6d1f53578b80794828f6619df62d519c2f73",
    "0DBD Maus x/y":
        "f705873ea1f55d81ab72601d42c78284de397fa5e6e45cc4e87ccf3e3ab71570",
}

# Record fields (offset in the player record, size, signed) written by --set and read back.
FIELDS = {"x": (0x0F, 2, True), "y": (0x11, 2, True), "power": (0x1B, 2, True), "angle": (0x1D, 2, True), "dir": (0x1F, 2, True), "energy": (0x21, 2, True),
          "alive": (0x23, 1, False), "weapon": (0x24, 2, True), "guidance": (0x26, 2, True),
          "target": (0x28, 2, True), "herb": (0x2A, 2, True), "vick": (0x2C, 2, True),
          "shield": (0x52, 4, True), "inertia": (0x56, 4, True), "repulser": (0x5A, 4, True),
          "shots": (0x5E, 2, True), "turns": (0x70, 2, True), "score": (0x13, 4, True), "cash": (0x17, 4, True),
          "wins": (0x6A, 2, True)}
for _w in range(18):
    FIELDS["stock%d" % _w] = (0x2E + 2 * _w, 2, True)


class Exhausted(Exception):
    pass


def sha(b):
    return hashlib.sha256(b).hexdigest()


def parse_steps(text):
    steps = []
    for raw in (text or "").split(";"):
        raw = raw.strip()
        if raw == "":
            continue
        st = {"keys": set(), "mouse": None, "bios": []}
        for part in raw.split():
            if part == "-":
                continue
            if part.startswith("m:"):
                x, y, b = (int(v) for v in part[2:].split(","))
                st["mouse"] = (x, y, b)
            elif part.startswith("b:"):
                st["bios"].append(BIOS[part[2:]])
            else:
                for k in part.split("+"):
                    st["keys"].add(SC[k])
        steps.append(st)
    return steps


def parse_sets(items):
    out = []
    for item in items or ():
        k, v = item.split("=")
        if ":" in k:
            p, k = k.split(":")
            out.append((int(p), k, int(v, 0)))
        else:
            out.append((None, k, int(v, 0)))
    return out


class Turn:
    """Input feeding and recording for one run of O4:44A7 from O4:479A."""

    def __init__(self, p, m, steps):
        self.p, self.m = p, m
        self.steps = list(steps)
        self.used = 0
        self.mouse = [160, 100, 0]
        self.polls = []        # (site name, screen SHA-256) at each consumed step
        self.trace = hashlib.sha256()
        self.commands = []
        self.delays = []
        self.releases = []
        self.keep = None       # list: keep (site, screen, DAC) of every poll (run --polls)

    def ds(self):
        return self.p.ds()

    def set_key(self, sc, v):
        self.m.mem[feld.lin(self.ds(), KEYS + sc)] = 1 if v else 0

    def bios_put(self, ascii_, scan):
        mem = self.m.mem
        head, tail = struct.unpack_from("<HH", mem, 0x41A)
        nxt = tail + 2 if tail + 2 < 0x3E else 0x1E
        if nxt == head:
            raise RuntimeError("BIOS keyboard buffer full")
        struct.pack_into("<H", mem, 0x400 + tail, (scan << 8) | ascii_)
        struct.pack_into("<H", mem, 0x41C, nxt)

    def apply_next(self, site):
        screen = bytes(self.m.mem[0xA0000:0xA0000 + 64000])
        d = hashlib.sha256(screen).digest()
        self.trace.update(d)
        self.polls.append((site, d.hex()))
        if self.keep is not None:
            self.keep.append((site, screen, bytes(self.m.dac)))
        if self.used >= len(self.steps):
            raise Exhausted(site)
        st = self.steps[self.used]
        self.used += 1
        for sc in range(0x200):
            self.set_key(sc, sc in st["keys"])
        if st["mouse"] is not None:
            self.mouse = list(st["mouse"])
        else:
            self.mouse[2] = 0
        for a, s in st["bios"]:
            self.bios_put(a, s)

    def install(self):
        m, p = self.m, self.p
        for (ovl, ofs), spec in SITES.items():
            a = feld.lin(p.ovseg[ovl], ofs)
            if spec[0] == "step":
                m.traps[a] = self._step(spec[1])
            else:
                m.traps[a] = self._release(spec[1], spec[2])
        m.traps[feld.lin(p.ovseg[7], O7_POLL_RETURN)] = self._command
        m.traps[feld.lin(fm.LOAD_SEG + CRT_DELAY[0], CRT_DELAY[1])] = self._delay
        m.int_hooks[0x33] = self._int33
        m.int_hooks[0x16] = self._int16

    def _step(self, name):
        def f(mm):
            self.apply_next(name)
            return False
        return f

    def _release(self, name, what):
        def f(mm):
            if what == MOUSE:
                if self.mouse[2]:
                    self.releases.append(name)
                self.mouse[2] = 0
            else:
                if any(self.m.mem[feld.lin(self.ds(), KEYS + sc)] for sc in what):
                    self.releases.append(name)
                for sc in what:
                    self.set_key(sc, False)
            return False
        return f

    def _command(self, mm):
        self.commands.append(mm.cpu.R[AX])
        return False

    def _delay(self, mm):
        c = mm.cpu
        ip, cs, ms = c.pop(), c.pop(), c.pop()
        self.delays.append(ms)
        c.S[CS], c.ip = cs, ip
        return False

    def _int33(self, mm):
        c = mm.cpu
        ax = c.R[AX]
        if ax == 3:
            c.R[BX] = self.mouse[2]
            c.R[CX] = self.mouse[0] * 2
            c.R[DX] = self.mouse[1]
        elif ax == 0:
            c.R[AX] = 0
        return True

    def _int16(self, mm):
        c = mm.cpu
        ah = c.R[AX] >> 8
        mem = mm.mem
        head, tail = struct.unpack_from("<HH", mem, 0x41A)
        if ah in (0x00, 0x10):
            if head == tail:
                raise Exhausted("ReadKey")
            c.R[AX] = struct.unpack_from("<H", mem, 0x400 + head)[0]
            struct.pack_into("<H", mem, 0x41A, head + 2 if head + 2 < 0x3E else 0x1E)
        elif ah in (0x01, 0x11):
            if head != tail:
                c.R[AX] = struct.unpack_from("<H", mem, 0x400 + head)[0]
            mm.ret_zf(head == tail)
        elif ah in (0x02, 0x12):
            c.s8(0, 0)
        return True


def write_fields(p, m, player, sets):
    for who, k, v in sets:
        off, n, _ = FIELDS[k]
        p.wds(m, feld.RECORD + feld.RECORD_SIZE * (who or player) + off, v, n)


def read_records(p, m, n_players):
    out = []
    raw = b""
    for i in range(1, n_players + 1):
        r = feld.RECORD + feld.RECORD_SIZE * i
        raw += bytes(m.mem[feld.lin(p.ds(), r):feld.lin(p.ds(), r) + feld.RECORD_SIZE])
        out.append({k: p.rds(m, r + o, n, s) for k, (o, n, s) in FIELDS.items()})
    return out, raw


def run_turn(p, case, turn):
    """turn: shooter (None = start player of the case), control, sets [(player or None, field, value)], steps, mirv."""
    out = {}

    def after(m, counts, snaps):
        c = m.cpu
        player = turn["shooter"] or p.rds(m, 0x47EE, 1)
        if turn["shooter"]:
            c.ww(c.S[SS], (c.R[BP] - 2) & 0xFFFF, player)
            p.wds(m, 0x47EE, player, 1)
        p.wds(m, CONTROL + 2 * player, turn["control"])
        p.wds(m, MOUSE_PRESENT, 1 if turn["control"] == 2 else 0, 1)
        p.wds(m, 0xE8B3, 1 if turn.get("sound") else 0, 1)
        write_fields(p, m, player, turn["sets"])
        m.dac_written.clear()
        t = Turn(p, m, parse_steps(turn["steps"]))
        if turn.get("keep"):
            t.keep = []
        t.install()
        n0, r0 = counts["random_n"], counts["random_real"]
        marks = {}

        def mark(name, stop):
            def f(mm):
                marks[name] = {"screen": bytes(mm.mem[0xA0000:0xA0000 + 64000]),
                               "dac_set": b"".join(bytes([i]) + bytes(mm.dac[3 * i:3 * i + 3])
                                                   for i in sorted(mm.dac_written))}
                if name == "mirv":
                    bp = mm.cpu.R[BP]
                    marks[name]["pattern"] = mm.cpu.rw(mm.cpu.S[SS], (bp - 0x102) & 0xFFFF)
                    marks[name]["detonation"] = mm.cpu.rw(mm.cpu.S[SS], (bp - 0x108) & 0xFFFF)
                return stop
            return f

        m.traps[feld.lin(p.ovseg[4], O4_AFTER_Z1)] = mark("z1", False)
        if turn.get("mirv"):
            m.traps[feld.lin(p.ovseg[4], O4_MIRV_END)] = mark("mirv", True)
        else:
            m.traps[feld.lin(p.ovseg[4], O4_AFTER_TURN)] = mark("turn", True)
        status = "ok"
        try:
            m.run(limit=c.steps + 300_000_000)
        except Exhausted as e:
            status = "exhausted at %s" % e.args[0]
        end = "mirv" if turn.get("mirv") else "turn"
        screen = bytes(m.mem[0xA0000:0xA0000 + 64000])
        dac_set = b"".join(bytes([i]) + bytes(m.dac[3 * i:3 * i + 3]) for i in sorted(m.dac_written))
        records, raw = read_records(p, m, case["players"])
        out.update({
            "player": player, "status": status, "steps_used": t.used, "steps": len(t.steps),
            "commands": t.commands, "delays": t.delays, "releases": t.releases,
            "polls": [s for s, _ in t.polls], "trace": t.trace.hexdigest(),
            "screen": screen, "dac_set": dac_set, "records": records, "records_raw": raw,
            "z1": marks.get("z1"), "end": marks.get(end),
            "randseed": p.rds(m, feld.RANDSEED, 4), "random_n": counts["random_n"] - n0,
            "random_real": counts["random_real"] - r0,
            "poll_screens": [h for _, h in t.polls], "dac": bytes(m.dac),
        })
        if t.keep is not None:
            out["kept"] = t.keep
        if "mirv" in marks:
            out["mirv"] = {"pattern": marks["mirv"]["pattern"], "detonation": marks["mirv"]["detonation"]}

    result, _ = p.run_case(case, after=after)
    return result, out


def run_order(p, case, order):
    """Turn order R5 without turns: from O4:479A on, each call of O7:040C is skipped (back to O4:4770); kills
    {turn index: [players]} set +23h = 0 before the next player is chosen. Records (player, pass) after Z1."""
    out = {"turns": []}

    def after(m, counts, snaps):
        c = m.cpu
        for who, k, v in order["sets"]:
            write_fields(p, m, who, [(who, k, v)])
        seen = out["turns"]

        def z1(mm):
            pl = p.rds(mm, 0x47EE, 1)
            r = feld.RECORD + feld.RECORD_SIZE * pl
            seen.append({"player": pl, "pass": p.rds(mm, 0x47EA), "weapon": p.rds(mm, r + 0x24),
                         "shots": p.rds(mm, r + 0x5E), "turns": p.rds(mm, r + 0x70)})
            return len(seen) >= order["count"]

        def skip(mm):
            k = len(seen) - 1
            for pl in order["kills"].get(k, ()):
                p.wds(mm, feld.RECORD + feld.RECORD_SIZE * pl + 0x23, 0, 1)
            mm.cpu.pop()  # argument of O7:040C
            mm.cpu.ip = O4_NEXT_PLAYER
            return False

        m.traps[feld.lin(p.ovseg[4], O4_AFTER_Z1)] = z1
        m.traps[feld.lin(p.ovseg[4], O4_CALL_TURN)] = skip
        m.run(limit=c.steps + 200_000_000)

    result, _ = p.run_case(case, after=after)
    return result, out


def summary(out):
    s = {k: v for k, v in out.items() if not isinstance(v, (bytes, dict)) and k not in ("poll_screens", "kept")}
    s["screen"] = sha(out["screen"])
    s["dac_set"] = sha(out["dac_set"])
    s["records"] = out["records"]
    for k in ("z1", "end"):
        if out.get(k):
            s[k] = {"screen": sha(out[k]["screen"]), "dac_set": sha(out[k]["dac_set"])}
    if "mirv" in out:
        s["mirv"] = out["mirv"]
    return s


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


def case_from(t):
    seed, n, wall, bg, mask, windopt, inv, nofall = t
    return {"seed": seed, "players": n, "wall": wall, "background": bg, "mask": mask, "wind": windopt,
            "invisible": inv, "no_fall": nofall}


def cmd_run(p, args):
    case = feld.parse_case(args)
    turn = {"shooter": args.shooter, "control": args.control, "sets": parse_sets(args.set), "steps": args.steps,
            "mirv": args.mirv, "sound": args.sound, "keep": args.polls}
    result, out = run_turn(p, case, turn)
    s = summary(out)
    d = args.out or os.path.join(args.root_out, "zug-%08X" % case["seed"])
    os.makedirs(d, exist_ok=True)
    with open(os.path.join(d, "turn-screen.bin"), "wb") as f:
        f.write(out["screen"])
    with open(os.path.join(d, "turn-dac.bin"), "wb") as f:
        f.write(out["dac"])
    for i, (site, scr, dac) in enumerate(out.get("kept", ())):
        with open(os.path.join(d, "poll-%02d-screen.bin" % i), "wb") as f:
            f.write(scr)
        with open(os.path.join(d, "poll-%02d-dac.bin" % i), "wb") as f:
            f.write(dac)
    with open(os.path.join(d, "result.json"), "w") as f:
        json.dump(s, f, indent=1)
    print(json.dumps(s, indent=1))
    print(d)
    return 0


# Fixed turns. A: case A of feld_schuss.py (two players, tanks (33, 123) and (300, 107), wind -44). B: four players,
# wall 2, background 2, tanks (29, 167), (117, 142), (205, 164), (293, 151); a tank pixel of player i: TANK_PIXEL[i].
A = (0x12345678, 2, 0, 0, 0xFFFF, 2, False, False)
B = (0x9E3779B9, 4, 2, 2, 0xFFFF, 1, False, False)
TANK_PIXEL = {1: (33, 167), 2: (121, 142), 3: (209, 164), 4: (297, 151)}
FULL = tuple(("stock%d" % w, 5) for w in range(18))

# (case, shooter, control, sets for the shooter (field, value) or (player, field, value), steps, options)
TURNS = [
    (A, 1, 1, (), "Left;Left;Left;Space", {}),                                   # 0 arrows without Alt: repeat
    (A, 1, 1, (), "Alt+Left;Alt+Left;Space", {}),                                # 1 with Alt: wait for release
    (A, 1, 1, (), "Right;KpRight;Alt+Right;RAlt+KpRight;Space", {}),             # 2 grey and keypad, both Alts
    (A, 1, 1, (), "Down;Down;Alt+Down;Up;Alt+KpUp;KpDown;Space", {}),            # 3 power -1 / +1
    (A, 1, 1, (), "PgUp;PgUp;PgDn;Alt+KpPgUp;Alt+PgDn;Space", {}),               # 4 power +-100 (99 + 1)
    (A, 1, 1, (("power", 995),), "Up;Up;Up;Up;Up;Up;PgUp;Space", {}),           # 5 limit 10 * energy
    (A, 1, 1, (("power", 50),), "PgDn;Down;Up;Space", {}),                       # 6 limit 0
    (A, 1, 1, (("power", 2000),), "Space", {}),                                  # 7 Z1: power cut to 10 * energy
    (A, 1, 1, (("angle", 88), ("dir", 1)), "Left;Left;Left;Left;Right;Space", {}),   # 8 over 90 degrees
    (A, 1, 1, (("angle", 1), ("dir", -1)), "Left;Left;Left;Right;Right;Space", {}),  # 9 below 0 degrees
    (A, 1, 1, (), "I;I;I;Space", {}),                                            # 10 direction (waits)
    (A, 1, 1, FULL, "Tab;Tab;Tab;LShift+Tab;RShift+Tab;Space", {}),              # 11 next / previous weapon
    (A, 1, 1, (("weapon", 17),) + FULL, "Tab;LShift+Tab;LShift+Tab;Space", {}),  # 12 wrap 17 -> 0 and back
    (A, 1, 1, (("weapon", 3), ("stock3", 0)), "Space", {}),                     # 13 Z1: weapon without stock
    (A, 1, 1, (("weapon", 2), ("stock2", 1), ("stock4", 2)), "Tab;Tab;LShift+Tab;Space", {}),  # 14 skips empty ones
    (A, 1, 1, (("weapon", 5), ("stock5", 100)), "-;Space", {}),                 # 15 laser with stock <= 100: 8
    (A, 1, 1, (("weapon", 5), ("stock5", 150), ("power", 140)), "Up;Up;Up;Up;Up;Up;Up;Up;Up;Up;Up;Space", {}),
    # 16 laser: power <= stock
    (A, 1, 1, (("herb", 1), ("vick", 1)), "H;V;H;Space;b:Enter", {}),            # 17 Herb/Vick on and off, target
    (A, 1, 1, (), "H;V;Space", {}),                                              # 18 without stock: no change
    (A, 1, 1, (("weapon", 5), ("stock5", 0)), "Tab;Space", {}),                  # 19 Z1 and Tab with an empty laser
    (A, 1, 1, (), "KpIns+KpDel;W;G;D;C;Space;Space", {}),                        # 20 cheat key, left with Space
    (A, 1, 1, (), "KpIns+KpDel;W;KpIns+KpDel;Space", {}),                        # 21 cheat key, left with Ins + Del
    (A, 1, 1, (), "Ins+Del;Space", {}),                                          # 22 grey Ins + Del: no cheat
    (A, 1, 1, (), "1;b:Space;2;b:Enter;Space", {}),                              # 23 tank info 1 and 2
    (B, 3, 1, (("herb", 3), ("vick", 2)) + FULL, "3;b:Space;Space", {}),         # 24 tank info: weapons, Herb, Vick
    (B, 3, 1, ((2, "alive", 0), (4, "shield", 120)), "2;b:Space;4;b:Space;Space", {}),  # 25 dead tank, shield
    (B, 3, 1, (), "0;b:Space;Space", {}),                                        # 26 key 0: tank 10
    (B, 3, 1, (("herb", 2),), "H;Space;b:Right;b:Right;b:Enter", {}),            # 27 target: skips the shooter
    (B, 3, 1, (("vick", 2), (4, "alive", 0)), "V;Enter;b:Left;b:Left;b:Left;b:X;b:Space", {}),  # 28 skips dead
    (B, 3, 1, (("herb", 2), ("vick", 2)), "H;V;KpEnter;b:Right;b:Enter", {}),    # 29 Herb + Vick: type 3
    (B, 3, 1, (("weapon", 7), ("stock7", 3)), "Space;Down;Down;Enter;Down;Space", {"mirv": True}),  # 30 MIRV keys
    (B, 3, 1, (("weapon", 7), ("stock7", 3)), "Space;Down;Down;Down;Down;Down;Up;Space;Down;Down;Down;Enter",
     {"mirv": True}),                                                            # 31 MIRV limits 4 / 2
    (B, 3, 1, (("weapon", 7), ("stock7", 3)), "Space;Down;Enter;Esc;Up;Enter;Enter", {"mirv": True}),  # 32 Esc back
    (B, 3, 2, (), "m:60,4,1;m:60,4,2;m:70,0,1;m:71,0,1;m:60,12,1;m:86,17,2;m:56,10,1;m:20,12,1;m:54,17,2;"
     "m:30,9,1;m:299,4,1;m:0,0,3", {}),                                          # 33 mouse: every header field
    (B, 3, 2, FULL, "m:10,18,1;m:100,27,2;m:279,20,2;m:280,20,1;m:9,20,1;m:150,28,1;m:0,0,3", {}),
    # 34 mouse: weapon bar
    (B, 3, 2, (("herb", 2), ("vick", 2)), "m:280,20,1;m:295,20,1;m:300,22,1;m:0,0,3;m:121,142,1",
     {}),                                                                        # 35 mouse: Herb, Vick, target
    (B, 3, 2, (), "m:121,142,1;m:0,0,0;m:0,0,1;m:297,151,2;m:5,5,2;m:0,0,3", {}),  # 36 mouse: tank info by click
    (B, 3, 2, (("herb", 2),), "m:280,20,1;m:0,0,3;m:5,5,1;m:209,164,1;m:150,100,0;m:121,142,1", {}),
    # 37 mouse target: empty, own tank
    (B, 3, 2, (("weapon", 7), ("stock7", 3)), "m:0,0,3;m:5,90,0;m:5,100,0;m:5,130,1;m:5,100,0;m:5,92,2",
     {"mirv": True}),                                                            # 38 MIRV dialog with the mouse
    (A, 1, 1, (), "Left;Alt+Right;I;Space", {"sound": True}),                    # 39 sound on (Delay of the tones)
]


def turn_from(t):
    case_t, shooter, control, sets, steps, opt = t
    sl = []
    for x in sets:
        sl.append((None, x[0], x[1]) if len(x) == 2 else x)
    return case_from(case_t), {"shooter": shooter, "control": control, "sets": sl, "steps": steps,
                               "mirv": opt.get("mirv", False), "sound": opt.get("sound", False)}


TURN_HEADER = ("nr\tseed\tplayers\tshooter\tcontrol\tstatus\tsteps_used\tcommands\tdelays\treleases\tpolls\ttrace"
               "\tz1_screen\tz1_dac\tscreen\tdac_set\trecords\tshooter_state\tmirv\trandseed\trandom_n\tscreen_field\tdac"
               "\twall\tbackground\twind_opt\tsets\tsteps\toptions\tpoll_screens")


def shooter_state(r):
    stocks = ",".join(str(r["stock%d" % w]) for w in range(18))
    return "%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d;%s" % (
        r["angle"], r["power"], r["dir"], r["energy"], r["weapon"], r["guidance"], r["target"], r["herb"], r["vick"],
        r["shots"], r["turns"], stocks)


def field_crop(screen):
    """Frame and field (x 8..312, y 28..192) as in feld_schuss.py: what Core/Playfield draws."""
    return b"".join(screen[y * 320 + 8:y * 320 + 313] for y in range(28, 193))


def dac_text(dac_set):
    """Written DAC entries as index:r,g,b (6-bit values the game computed, no bytes of the files)."""
    return " ".join("%d:%d,%d,%d" % tuple(dac_set[k:k + 4]) for k in range(0, len(dac_set), 4)) or "-"


def compress(seq):
    out = []
    for v in seq:
        if out and out[-1][0] == v:
            out[-1][1] += 1
        else:
            out.append([v, 1])
    return ",".join(str(v) if n == 1 else "%sx%d" % (v, n) for v, n in out)


def cmd_hashes(p, args):
    rows = []
    print(TURN_HEADER)
    selected = range(len(TURNS)) if args.only is None else args.only
    for i in selected:
        case, turn = turn_from(TURNS[i])
        result, o = run_turn(p, case, turn)
        r = o["records"][o["player"] - 1]
        z1 = o.get("z1") or {"screen": b"", "dac_set": b""}
        mirv = o.get("mirv")
        row = "\t".join(str(v) for v in (
            i, "%08X" % case["seed"], case["players"], o["player"], turn["control"], o["status"], o["steps_used"],
            ",".join(str(c) for c in o["commands"]), compress(o["delays"]) or "-", ",".join(o["releases"]) or "-",
            compress(o["polls"]), o["trace"], sha(z1["screen"]), sha(z1["dac_set"]), sha(o["screen"]),
            sha(o["dac_set"]), sha(o["records_raw"]), shooter_state(r),
            "-" if mirv is None else "%d,%d" % (mirv["pattern"], mirv["detonation"]),
            "%08X" % o["randseed"], o["random_n"], sha(field_crop(o["screen"])), dac_text(o["dac_set"]),
            case["wall"], case["background"], case["wind"],
            " ".join(("%s=%d" % (k, v)) if w is None else ("%d:%s=%d" % (w, k, v)) for w, k, v in turn["sets"]) or "-",
            turn["steps"], ",".join(k for k in ("mirv", "sound") if turn.get(k)) or "-",
            " ".join(h[:24] for h in o["poll_screens"])))
        rows.append(row)
        print(row, flush=True)
    os.makedirs(args.root_out, exist_ok=True)
    with open(os.path.join(args.root_out, "zug-hashes.tsv"), "w") as f:
        f.write(TURN_HEADER + "\n" + "\n".join(rows) + "\n")
    return 0


C5 = (0xDEADBEEF, 5, 3, 3, 0xFFFF, 3, False, False)

# Turn order without turns: (case, count, kills {after turn index: players}, sets (player, field, value)).
ORDERS = [
    (A, 6, {}, ()),                                                  # 0 two players
    (B, 10, {}, ()),                                                 # 1 four players, passes
    (B, 10, {0: (2,)}, ()),                                          # 2 a dead player is skipped
    (C5, 12, {1: (1, 5), 4: (3,)}, ()),                              # 3 five players, deaths at the end of a pass
    (B, 6, {}, ((2, "weapon", 3), (2, "stock3", 0), (3, "weapon", 17), (3, "stock17", 0),
               (4, "weapon", 0), (4, "stock0", 0))),                 # 4 Z1: weapon change without stock, wrap 17 -> 0
]
ORDER_HEADER = "nr\tseed\tplayers\tcount\tturns"


def cmd_folge(p, args):
    rows = []
    print(ORDER_HEADER)
    selected = range(len(ORDERS)) if args.only is None else args.only
    for i in selected:
        case_t, count, kills, sets = ORDERS[i]
        case = case_from(case_t)
        result, o = run_order(p, case, {"count": count, "kills": kills, "sets": list(sets)})
        turns = " ".join("%d,%d,%d,%d,%d" % (t["player"], t["pass"], t["weapon"], t["shots"], t["turns"])
                         for t in o["turns"])
        row = "%d\t%08X\t%d\t%d\t%s" % (i, case["seed"], case["players"], count, turns)
        rows.append(row)
        print(row, flush=True)
    os.makedirs(args.root_out, exist_ok=True)
    with open(os.path.join(args.root_out, "zugfolge.tsv"), "w") as f:
        f.write(ORDER_HEADER + "\n" + "\n".join(rows) + "\n")
    return 0


def main():
    root = os.path.normpath(os.path.join(HERE, "..", ".."))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--gamedata", default=os.path.join(root, "gamedata"))
    ap.add_argument("--font", default=os.path.expanduser("~/referenz/schriften/dosbox-0.74-3-vga-8x8.bin"))
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
    r.add_argument("--shooter", type=int)
    r.add_argument("--control", type=int, default=1)
    r.add_argument("--set", nargs="*", help="FIELD=V or PLAYER:FIELD=V (fields: %s)" % " ".join(FIELDS))
    r.add_argument("--steps", default="")
    r.add_argument("--mirv", action="store_true", help="run on into the MIRV dialog (weapon 7) and stop after it")
    r.add_argument("--sound", action="store_true")
    r.add_argument("--polls", action="store_true", help="also write the screen and DAC at every poll")
    r.add_argument("--out")
    h = sub.add_parser("hashes")
    h.add_argument("--only", type=int, nargs="*")
    q = sub.add_parser("folge")
    q.add_argument("--only", type=int, nargs="*")
    args = ap.parse_args()
    font = None
    if os.path.exists(args.font):
        with open(args.font, "rb") as f:
            font = f.read()
    p = feld.Probe(args.gamedata, font)
    return {"befunde": cmd_befunde, "run": cmd_run, "hashes": cmd_hashes, "folge": cmd_folge}[args.cmd](p, args)


if __name__ == "__main__":
    sys.exit(main())
