#!/usr/bin/env python3
"""Probe for the Real48 and Random routines of the Turbo Pascal 6.0 runtime in BOMB.EXE.

Runs the original routines of the System segment (12CE) in a small 8086 emulator and
derives reference results for the remake (docs/analyse/real48.md). The routines are
read from gamedata/BOMB.EXE at run time; this script contains no bytes of the
originals (AGENTS.md section 2.1). Output goes only to analyse/real48/ (protected,
git-ignored folder).

Usage:
  real48.py [--gamedata DIR] [--out DIR] befunde       check the routine regions (SHA-256)
  real48.py [--gamedata DIR] hashes [--count N]        SHA-256 per operation over N generated cases
  real48.py [--gamedata DIR] [--out DIR] cases [--count N]   write the cases as TSV to analyse/real48/
  real48.py [--gamedata DIR] eval OP A [B]             run one operation (operands as 12 hex digits,
                                                       memory byte order; float/random/random_n take hex
                                                       integers: LongInt, RandSeed, RandSeed and n)

The case generator (splitmix64, fixed seed per operation) is mirrored in
tests/BombRedone.Core.Tests/Numerics/Real48ProbeCases.cs; both sides hash the results
the same way, so the C# test compares against the hashes printed here.
"""
import argparse
import hashlib
import os
import struct
import sys

SYSTEM_SEG = 0x12CE
DATA_SEG = 0x14D5
RANDSEED = 0x3AD6

# Entry points in segment 12CE (docs/analyse/real48.md) and how they are called.
ENTRIES = {
    "add": 0x11B6,
    "sub": 0x11BC,
    "sqr": 0x11C2,
    "mul": 0x11C8,
    "div": 0x11CE,
    "cmp": 0x11D8,
    "float": 0x11DC,
    "trunc": 0x11E0,
    "round": 0x11E8,
    "random": 0x168B,
    "random_n": 0x1675,
}
RUNERROR = 0x00E2

# Checked regions of segment 12CE: (start, end) -> SHA-256. Computed from the original with this
# script ('befunde --print'); the bytes themselves are never stored here.
REGIONS = {
    "real48": (0x0EAE, 0x1202),
    "random": (0x1675, 0x170A),
}
REGION_HASHES = {
    "real48": "b38e4a079563fece462ac02174d165263ba69db3ca799ff68876a68f84cc0dbc",
    "random": "afe015890f68f4a8a28d397eb4e164d0d6d6cb2d0d2b18a4d19f0ce535658904",
}


class EmuError(Exception):
    pass


class Cpu:
    """Minimal real-mode 8086 subset: exactly what the runtime routines above use."""

    R16 = ["ax", "cx", "dx", "bx", "sp", "bp", "si", "di"]

    def __init__(self, mem):
        self.mem = mem
        self.r = {n: 0 for n in self.R16}
        self.cs = self.ds = self.ss = self.es = 0
        self.ip = 0
        self.cf = self.zf = self.sf = self.of = False
        self.steps = 0

    # registers
    def g16(self, i):
        return self.r[self.R16[i]]

    def s16(self, i, v):
        self.r[self.R16[i]] = v & 0xFFFF

    def g8(self, i):
        v = self.r[self.R16[i & 3]]
        return (v >> 8) & 0xFF if i & 4 else v & 0xFF

    def s8(self, i, v):
        n = self.R16[i & 3]
        if i & 4:
            self.r[n] = (self.r[n] & 0x00FF) | ((v & 0xFF) << 8)
        else:
            self.r[n] = (self.r[n] & 0xFF00) | (v & 0xFF)

    # memory
    def lin(self, seg, ofs):
        return ((seg << 4) + (ofs & 0xFFFF)) & 0xFFFFF

    def rb(self, seg, ofs):
        return self.mem[self.lin(seg, ofs)]

    def rw(self, seg, ofs):
        return self.rb(seg, ofs) | (self.rb(seg, ofs + 1) << 8)

    def wb(self, seg, ofs, v):
        self.mem[self.lin(seg, ofs)] = v & 0xFF

    def ww(self, seg, ofs, v):
        self.wb(seg, ofs, v)
        self.wb(seg, ofs + 1, v >> 8)

    def fetch8(self):
        v = self.rb(self.cs, self.ip)
        self.ip = (self.ip + 1) & 0xFFFF
        return v

    def fetch16(self):
        return self.fetch8() | (self.fetch8() << 8)

    def push(self, v):
        self.r["sp"] = (self.r["sp"] - 2) & 0xFFFF
        self.ww(self.ss, self.r["sp"], v)

    def pop(self):
        v = self.rw(self.ss, self.r["sp"])
        self.r["sp"] = (self.r["sp"] + 2) & 0xFFFF
        return v

    def flags_word(self):
        return (self.cf << 0) | (self.zf << 6) | (self.sf << 7) | (self.of << 11) | 0x0002

    def set_flags_word(self, v):
        self.cf, self.zf, self.sf, self.of = bool(v & 1), bool(v & 0x40), bool(v & 0x80), bool(v & 0x800)

    # ModRM: returns (is_reg, index or (seg, ofs)), reg field
    def modrm(self, seg_override):
        b = self.fetch8()
        mod, reg, rm = b >> 6, (b >> 3) & 7, b & 7
        if mod == 3:
            return (True, rm), reg
        r = self.r
        bases = [(r["bx"] + r["si"], "ds"), (r["bx"] + r["di"], "ds"), (r["bp"] + r["si"], "ss"),
                 (r["bp"] + r["di"], "ss"), (r["si"], "ds"), (r["di"], "ds"), (r["bp"], "ss"), (r["bx"], "ds")]
        if mod == 0 and rm == 6:
            ofs, seg = self.fetch16(), "ds"
        else:
            ofs, seg = bases[rm]
            if mod == 1:
                d = self.fetch8()
                ofs += d - 256 if d & 0x80 else d
            elif mod == 2:
                ofs += self.fetch16()
        seg = seg_override or seg
        return (False, (getattr(self, seg), ofs & 0xFFFF)), reg

    def rd(self, op, w):
        is_reg, x = op
        if is_reg:
            return self.g16(x) if w else self.g8(x)
        return self.rw(*x) if w else self.rb(*x)

    def wr(self, op, w, v):
        is_reg, x = op
        if is_reg:
            (self.s16 if w else self.s8)(x, v)
        else:
            (self.ww if w else self.wb)(x[0], x[1], v)

    def szf(self, v, w):
        bits = 16 if w else 8
        v &= (1 << bits) - 1
        self.zf = v == 0
        self.sf = bool(v >> (bits - 1))
        return v

    def alu(self, kind, a, b, w):
        bits = 16 if w else 8
        mask = (1 << bits) - 1
        top = 1 << (bits - 1)
        if kind in (0, 2):  # add, adc
            c = 1 if kind == 2 and self.cf else 0
            res = a + b + c
            self.cf = res > mask
            self.of = bool((~(a ^ b) & (a ^ res)) & top)
        elif kind in (3, 5, 7):  # sbb, sub, cmp
            c = 1 if kind == 3 and self.cf else 0
            res = a - b - c
            self.cf = res < 0
            self.of = bool(((a ^ b) & (a ^ res)) & top)
        elif kind == 1:
            res = a | b
            self.cf = self.of = False
        elif kind == 4:
            res = a & b
            self.cf = self.of = False
        elif kind == 6:
            res = a ^ b
            self.cf = self.of = False
        else:
            raise EmuError(kind)
        return self.szf(res, w)

    def shift(self, kind, v, count, w):
        bits = 16 if w else 8
        mask = (1 << bits) - 1
        for _ in range(count):
            if kind == 2:  # rcl
                c = v >> (bits - 1)
                v = ((v << 1) | self.cf) & mask
                self.cf = bool(c)
            elif kind == 3:  # rcr
                c = v & 1
                v = (v >> 1) | (self.cf << (bits - 1))
                self.cf = bool(c)
            elif kind == 4:  # shl
                self.cf = bool(v >> (bits - 1))
                v = (v << 1) & mask
                self.szf(v, w)
            elif kind == 5:  # shr
                self.cf = bool(v & 1)
                v >>= 1
                self.szf(v, w)
            else:
                raise EmuError("shift %d" % kind)
        return v

    def cond(self, c):
        t = [self.of, not self.of, self.cf, not self.cf, self.zf, not self.zf, self.cf or self.zf,
             not (self.cf or self.zf), self.sf, not self.sf, None, None, self.sf != self.of,
             self.sf == self.of, self.zf or self.sf != self.of, not self.zf and self.sf == self.of]
        if t[c] is None:
            raise EmuError("parity")
        return t[c]

    def step(self):
        self.steps += 1
        if self.steps > 100000:
            raise EmuError("runaway")
        seg = None
        op = self.fetch8()
        while op in (0x2E, 0x36, 0x26, 0x3E):
            seg = {0x2E: "cs", 0x36: "ss", 0x26: "es", 0x3E: "ds"}[op]
            op = self.fetch8()
        if op < 0x40 and (op & 7) < 4:
            kind, d, w = op >> 3, (op >> 1) & 1, op & 1
            m, reg = self.modrm(seg)
            regop = (True, reg)
            dst, src = (regop, m) if d else (m, regop)
            res = self.alu(kind, self.rd(dst, w), self.rd(src, w), w)
            if kind != 7:
                self.wr(dst, w, res)
        elif op < 0x40 and (op & 7) in (4, 5):
            kind, w = op >> 3, op & 1
            imm = self.fetch16() if w else self.fetch8()
            a = self.r["ax"] if w else self.g8(0)
            res = self.alu(kind, a, imm, w)
            if kind != 7:
                (self.s16(0, res) if w else self.s8(0, res))
        elif 0x40 <= op <= 0x4F:
            i = op & 7
            cf = self.cf
            self.s16(i, self.alu(0 if op < 0x48 else 5, self.g16(i), 1, 1))
            self.cf = cf
        elif 0x50 <= op <= 0x57:
            self.push(self.g16(op & 7))
        elif 0x58 <= op <= 0x5F:
            self.s16(op & 7, self.pop())
        elif 0x70 <= op <= 0x7F:
            d = self.fetch8()
            if self.cond(op & 15):
                self.ip = (self.ip + (d - 256 if d & 0x80 else d)) & 0xFFFF
        elif op in (0x80, 0x81, 0x83):
            w = op != 0x80
            m, kind = self.modrm(seg)
            if op == 0x81:
                imm = self.fetch16()
            else:
                imm = self.fetch8()
                if op == 0x83 and imm & 0x80:
                    imm |= 0xFF00
            res = self.alu(kind, self.rd(m, w), imm, w)
            if kind != 7:
                self.wr(m, w, res)
        elif op in (0x84, 0x85):
            w = op & 1
            m, reg = self.modrm(seg)
            self.alu(4, self.rd(m, w), self.rd((True, reg), w), w)
        elif op in (0x86, 0x87):
            w = op & 1
            m, reg = self.modrm(seg)
            a, b = self.rd(m, w), self.rd((True, reg), w)
            self.wr(m, w, b)
            self.wr((True, reg), w, a)
        elif 0x88 <= op <= 0x8B:
            d, w = (op >> 1) & 1, op & 1
            m, reg = self.modrm(seg)
            if d:
                self.wr((True, reg), w, self.rd(m, w))
            else:
                self.wr(m, w, self.rd((True, reg), w))
        elif 0x91 <= op <= 0x97:
            i = op & 7
            a, b = self.r["ax"], self.g16(i)
            self.r["ax"] = b
            self.s16(i, a)
        elif op == 0x9C:
            self.push(self.flags_word())
        elif op == 0x9D:
            self.set_flags_word(self.pop())
        elif op == 0xA1:
            self.r["ax"] = self.rw(getattr(self, seg or "ds"), self.fetch16())
        elif op == 0xA3:
            self.ww(getattr(self, seg or "ds"), self.fetch16(), self.r["ax"])
        elif 0xB0 <= op <= 0xB7:
            self.s8(op & 7, self.fetch8())
        elif 0xB8 <= op <= 0xBF:
            self.s16(op & 7, self.fetch16())
        elif op == 0xC3:
            self.ip = self.pop()
        elif op == 0xCA:
            n = self.fetch16()
            self.ip = self.pop()
            self.cs = self.pop()
            self.r["sp"] = (self.r["sp"] + n) & 0xFFFF
        elif op == 0xCB:
            self.ip = self.pop()
            self.cs = self.pop()
        elif op in (0xD0, 0xD1, 0xD3):
            w = op & 1
            m, kind = self.modrm(seg)
            count = 1 if op != 0xD3 else self.g8(1)
            self.wr(m, w, self.shift(kind, self.rd(m, w), count, w))
        elif op == 0xE2:
            d = self.fetch8()
            self.r["cx"] = (self.r["cx"] - 1) & 0xFFFF
            if self.r["cx"]:
                self.ip = (self.ip + (d - 256 if d & 0x80 else d)) & 0xFFFF
        elif op == 0xE8:
            d = self.fetch16()
            self.push(self.ip)
            self.ip = (self.ip + d) & 0xFFFF
        elif op == 0xE9:
            d = self.fetch16()
            self.ip = (self.ip + d) & 0xFFFF
        elif op == 0xEB:
            d = self.fetch8()
            self.ip = (self.ip + (d - 256 if d & 0x80 else d)) & 0xFFFF
        elif op == 0xF5:
            self.cf = not self.cf
        elif op == 0xF8:
            self.cf = False
        elif op == 0xF9:
            self.cf = True
        elif op in (0xF6, 0xF7):
            w = op & 1
            m, kind = self.modrm(seg)
            v = self.rd(m, w)
            if kind == 0:
                imm = self.fetch16() if w else self.fetch8()
                self.alu(4, v, imm, w)
            elif kind == 2:
                self.wr(m, w, ~v)
            elif kind == 3:
                res = self.alu(5, 0, v, w)
                self.cf = v != 0
                self.wr(m, w, res)
            elif kind == 4:
                if w:
                    p = self.r["ax"] * v
                    self.r["ax"], self.r["dx"] = p & 0xFFFF, p >> 16
                    self.cf = self.of = self.r["dx"] != 0
                else:
                    p = self.g8(0) * v
                    self.r["ax"] = p
                    self.cf = self.of = (p >> 8) != 0
            elif kind == 6:
                if not w or v == 0:
                    raise EmuError("div")
                n = (self.r["dx"] << 16) | self.r["ax"]
                q, rem = divmod(n, v)
                if q > 0xFFFF:
                    raise EmuError("divide overflow")
                self.r["ax"], self.r["dx"] = q, rem
            else:
                raise EmuError("F6/F7 /%d" % kind)
        elif op == 0xFE:
            m, kind = self.modrm(seg)
            cf = self.cf
            self.wr(m, 0, self.alu(0 if kind == 0 else 5, self.rd(m, 0), 1, 0))
            self.cf = cf
            if kind > 1:
                raise EmuError("FE /%d" % kind)
        else:
            raise EmuError("opcode %02X at %04X" % (op, (self.ip - 1) & 0xFFFF))


class Runtime:
    """BOMB.EXE load image in emulator memory; runs single runtime calls."""

    LOAD = 0x1000  # load segment of the image
    RET_SEG, RET_OFS = 0xF000, 0x0000

    def __init__(self, gamedata):
        exe = open(os.path.join(gamedata, "BOMB.EXE"), "rb").read()
        hdr = struct.unpack("<H", exe[8:10])[0] * 16
        lastp, pages = struct.unpack("<HH", exe[2:6])
        size = (pages - 1) * 512 + lastp if lastp else pages * 512
        self.image = exe[hdr:size]
        self.mem = bytearray(1 << 20)
        base = self.LOAD << 4
        self.mem[base:base + len(self.image)] = self.image
        self.cs = self.LOAD + SYSTEM_SEG
        self.ds = self.LOAD + DATA_SEG

    def segment_bytes(self, start, end):
        at = SYSTEM_SEG * 16
        return self.image[at + start:at + end]

    def call(self, entry, regs, stack_words=()):
        cpu = Cpu(self.mem)
        cpu.cs, cpu.ds, cpu.es = self.cs, self.ds, self.ds
        cpu.ss, cpu.r["sp"] = 0x9000, 0xFFF0
        for k, v in regs.items():
            cpu.r[k] = v & 0xFFFF
        for wv in stack_words:
            cpu.push(wv)
        cpu.push(self.RET_SEG)
        cpu.push(self.RET_OFS)
        cpu.ip = entry
        while True:
            if cpu.cs == self.cs and cpu.ip == RUNERROR:
                return cpu, cpu.r["ax"]
            if cpu.cs == self.RET_SEG and cpu.ip == self.RET_OFS:
                return cpu, 0
            cpu.step()


def real_regs(b, names):
    """6 bytes (memory order) -> register triple (low, mid, high)."""
    lo, mid, hi = struct.unpack("<HHH", bytes(b))
    return dict(zip(names, (lo, mid, hi)))


def regs_real(cpu):
    return struct.pack("<HHH", cpu.r["ax"], cpu.r["bx"], cpu.r["dx"])


def run(rt, op, a, b=None):
    """Return (result bytes, error code). Real operands are 6-byte memory images."""
    e = ENTRIES[op]
    if op in ("add", "sub", "mul", "div", "cmp"):
        regs = real_regs(a, ("ax", "bx", "dx"))
        regs.update(real_regs(b, ("cx", "si", "di")))
        cpu, err = rt.call(e, regs)
        if op == "cmp":
            return bytes([0 if cpu.zf else (0xFF if cpu.cf else 1)]), err
        return (b"" if err else regs_real(cpu)), err
    if op == "sqr":
        cpu, err = rt.call(e, real_regs(a, ("ax", "bx", "dx")))
        return (b"" if err else regs_real(cpu)), err
    if op == "float":
        cpu, err = rt.call(e, {"ax": a & 0xFFFF, "dx": a >> 16})
        return regs_real(cpu), err
    if op in ("trunc", "round"):
        cpu, err = rt.call(e, real_regs(a, ("ax", "bx", "dx")))
        return (b"" if err else struct.pack("<HH", cpu.r["ax"], cpu.r["dx"])), err
    if op == "random_n":
        rt.mem[(rt.ds << 4) + RANDSEED:(rt.ds << 4) + RANDSEED + 4] = struct.pack("<I", a)
        cpu, err = rt.call(e, {}, stack_words=(b,))
        seed = struct.unpack("<I", bytes(rt.mem[(rt.ds << 4) + RANDSEED:(rt.ds << 4) + RANDSEED + 4]))[0]
        return struct.pack("<HI", cpu.r["ax"], seed), err
    if op == "random":
        rt.mem[(rt.ds << 4) + RANDSEED:(rt.ds << 4) + RANDSEED + 4] = struct.pack("<I", a)
        cpu, err = rt.call(e, {})
        seed = struct.unpack("<I", bytes(rt.mem[(rt.ds << 4) + RANDSEED:(rt.ds << 4) + RANDSEED + 4]))[0]
        return regs_real(cpu) + struct.pack("<I", seed), err
    raise KeyError(op)


# ---- case generator (mirrored in Real48ProbeCases.cs) ----

MASK64 = (1 << 64) - 1


class SplitMix64:
    def __init__(self, seed):
        self.s = seed & MASK64

    def next(self):
        self.s = (self.s + 0x9E3779B97F4A7C15) & MASK64
        z = self.s
        z = ((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9) & MASK64
        z = ((z ^ (z >> 27)) * 0x94D049BB133111EB) & MASK64
        return z ^ (z >> 31)


SEEDS = {"add": 1, "sub": 2, "sqr": 3, "mul": 4, "div": 5, "cmp": 6, "float": 7, "trunc": 8, "round": 9,
         "random": 10, "random_n": 11}

SPECIAL_EXP = [0, 1, 2, 0x7F, 0x80, 0x81, 0x82, 0x9E, 0x9F, 0xA0, 0xA1, 0xFE, 0xFF]


def gen_real(g, near=None):
    """One 6-byte operand. near: exponent of the other operand (to get close exponents)."""
    r = g.next()
    mode = r & 7
    mant = (r >> 8) & ((1 << 40) - 1)  # 39 mantissa bits + sign
    if mode == 0:
        exp = (r >> 48) & 0xFF  # anything, including 0 with garbage mantissa
    elif mode == 1:
        exp = SPECIAL_EXP[((r >> 48) & 0xFF) % len(SPECIAL_EXP)]
    elif mode == 2:
        if (r >> 60) & 1:
            mant &= ~0xFFFFFF  # low 24 bits clear: short multiply path
        exp = 0x61 + ((r >> 48) & 0x3F)
    elif mode == 3 and near is not None:
        exp = max(0, min(255, near + ((r >> 48) & 0x7F) - 0x3F))
    elif mode == 4:
        mant = ((r >> 8) & 0xFF) << 32 | ((r >> 16) & 1) << 39  # few mantissa bits set
        exp = 0x81 + ((r >> 48) & 0x1F)
    elif mode == 5:
        return bytes(6)
    else:
        exp = 0x61 + ((r >> 48) & 0x3F)
    return bytes([exp]) + mant.to_bytes(5, "little")


def gen_case(op, g):
    if op == "float":
        r = g.next()
        mode = r & 3
        v = (r >> 32) & 0xFFFFFFFF
        if mode == 1:
            v = (1 << ((r >> 8) & 31)) + ((r >> 16) & 3) - 1
            if (r >> 24) & 1:
                v = -v & 0xFFFFFFFF
        return (v,)
    if op == "random":
        return ((g.next() >> 16) & 0xFFFFFFFF,)
    if op == "random_n":
        r = g.next()
        n = (r >> 48) & 0xFFFF if r & 1 else (r >> 48) & 0x3FF  # small ranges are the common case
        return ((r >> 8) & 0xFFFFFFFF, n)
    a = gen_real(g)
    if op in ("sqr", "trunc", "round"):
        return (a,)
    return (a, gen_real(g, near=a[0]))


def result_record(res, err):
    return bytes([err & 0xFF, len(res)]) + res


def op_hash(rt, op, count):
    g = SplitMix64(SEEDS[op])
    h = hashlib.sha256()
    for _ in range(count):
        res, err = run(rt, op, *gen_case(op, g))
        h.update(result_record(res, err))
    return h.hexdigest()


def fmt_arg(x):
    return "%08X" % x if isinstance(x, int) else x.hex().upper()


def cmd_befunde(rt, args):
    ok = True
    for key, (s, e) in REGIONS.items():
        got = hashlib.sha256(rt.segment_bytes(s, e)).hexdigest()
        if args.print:
            print(key, "%04X-%04X" % (s, e), got)
            continue
        good = got == REGION_HASHES[key]
        ok &= good
        print(("ok    " if good else "FEHLT ") + "12CE:%04X-%04X %s" % (s, e - 1, key))
    return 0 if ok else 1


def cmd_hashes(rt, args):
    for op in ENTRIES:
        print(op, args.count, op_hash(rt, op, args.count))
    return 0


def cmd_cases(rt, args):
    os.makedirs(args.out, exist_ok=True)
    path = os.path.join(args.out, "cases.tsv")
    with open(path, "w") as f:
        f.write("op\ta\tb\terror\tresult\n")
        for op in ENTRIES:
            g = SplitMix64(SEEDS[op])
            for _ in range(args.count):
                c = gen_case(op, g)
                res, err = run(rt, op, *c)
                f.write("%s\t%s\t%s\t%d\t%s\n" % (op, fmt_arg(c[0]), fmt_arg(c[1]) if len(c) > 1 else "",
                                                  err, res.hex().upper()))
    print(path)
    return 0


def cmd_eval(rt, args):
    def parse(s):
        return int(s, 16) if args.op in ("float", "random", "random_n") else bytes.fromhex(s)
    ops = [parse(x) for x in args.args]
    res, err = run(rt, args.op, *ops)
    print("error %d result %s" % (err, res.hex().upper()))
    return 0


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    root = os.path.normpath(os.path.join(here, "..", ".."))
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--gamedata", default=os.path.join(root, "gamedata"))
    p.add_argument("--out", default=os.path.join(root, "analyse", "real48"))
    sub = p.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("befunde")
    b.add_argument("--print", action="store_true", help="print region hashes instead of checking")
    h = sub.add_parser("hashes")
    h.add_argument("--count", type=int, default=10000)
    c = sub.add_parser("cases")
    c.add_argument("--count", type=int, default=1000)
    e = sub.add_parser("eval")
    e.add_argument("op", choices=sorted(ENTRIES))
    e.add_argument("args", nargs="+")
    args = p.parse_args()
    rt = Runtime(args.gamedata)
    return {"befunde": cmd_befunde, "hashes": cmd_hashes, "cases": cmd_cases, "eval": cmd_eval}[args.cmd](rt, args)


if __name__ == "__main__":
    sys.exit(main())
