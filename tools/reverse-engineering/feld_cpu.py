#!/usr/bin/env python3
"""Real-mode 8086 interpreter for the playfield probe (feld.py).

Complete 8086 instruction set plus the few 80186 forms (push imm, imul imm, shifts by imm,
pusha/popa, enter/leave) that compilers of the time may emit. Interrupts go through the
interrupt vector table in emulated memory; the host (feld.py) installs vectors that point to
trap addresses and handles them in Python. Port I/O is delegated to the host as well.

This file holds no bytes of the original program (AGENTS.md section 2.1).
"""


class EmuError(Exception):
    pass


AX, CX, DX, BX, SP, BP, SI, DI = range(8)
REG_NAMES = ["ax", "cx", "dx", "bx", "sp", "bp", "si", "di"]
ES, CS, SS, DS = range(4)

PARITY = [bin(i).count("1") % 2 == 0 for i in range(256)]


class RegView:
    """Dict-like view of the general registers by name (compatibility with real48.py)."""

    def __init__(self, cpu):
        self._cpu = cpu

    def __getitem__(self, name):
        return self._cpu.R[REG_NAMES.index(name)]

    def __setitem__(self, name, value):
        self._cpu.R[REG_NAMES.index(name)] = value & 0xFFFF

    def items(self):
        return [(n, self._cpu.R[i]) for i, n in enumerate(REG_NAMES)]


class Cpu:
    def __init__(self, mem, host=None):
        self.mem = mem
        self.host = host
        self.R = [0] * 8
        self.S = [0] * 4
        self.ip = 0
        self.cf = self.pf = self.af = self.zf = self.sf = self.tf = self.of = False
        self.iflag = True
        self.df = False
        self.steps = 0
        self.r = RegView(self)
        self._seg_ovr = None
        self._ea_seg = 0
        self._ea_ofs = 0
        self.ops = [None] * 256
        self._build()

    # ---- segment registers as attributes (compatibility) ----
    cs = property(lambda s: s.S[CS], lambda s, v: s.S.__setitem__(CS, v & 0xFFFF))
    ds = property(lambda s: s.S[DS], lambda s, v: s.S.__setitem__(DS, v & 0xFFFF))
    es = property(lambda s: s.S[ES], lambda s, v: s.S.__setitem__(ES, v & 0xFFFF))
    ss = property(lambda s: s.S[SS], lambda s, v: s.S.__setitem__(SS, v & 0xFFFF))

    # ---- memory ----
    def rb(self, seg, ofs):
        return self.mem[((seg << 4) + (ofs & 0xFFFF)) & 0xFFFFF]

    def rw(self, seg, ofs):
        m = self.mem
        b = seg << 4
        return m[(b + (ofs & 0xFFFF)) & 0xFFFFF] | (m[(b + ((ofs + 1) & 0xFFFF)) & 0xFFFFF] << 8)

    def wb(self, seg, ofs, v):
        a = ((seg << 4) + (ofs & 0xFFFF)) & 0xFFFFF
        if a >= 0xF0000:
            return  # ROM area
        self.mem[a] = v & 0xFF

    def ww(self, seg, ofs, v):
        self.wb(seg, ofs, v)
        self.wb(seg, ofs + 1, v >> 8)

    def fetch8(self):
        v = self.mem[((self.S[CS] << 4) + self.ip) & 0xFFFFF]
        self.ip = (self.ip + 1) & 0xFFFF
        return v

    def fetch16(self):
        lo = self.fetch8()
        return lo | (self.fetch8() << 8)

    def fetchs8(self):
        v = self.fetch8()
        return v - 256 if v & 0x80 else v

    # ---- stack ----
    def push(self, v):
        sp = (self.R[SP] - 2) & 0xFFFF
        self.R[SP] = sp
        self.ww(self.S[SS], sp, v)

    def pop(self):
        sp = self.R[SP]
        v = self.rw(self.S[SS], sp)
        self.R[SP] = (sp + 2) & 0xFFFF
        return v

    # ---- registers ----
    def g8(self, i):
        v = self.R[i & 3]
        return (v >> 8) & 0xFF if i & 4 else v & 0xFF

    def s8(self, i, v):
        j = i & 3
        if i & 4:
            self.R[j] = (self.R[j] & 0x00FF) | ((v & 0xFF) << 8)
        else:
            self.R[j] = (self.R[j] & 0xFF00) | (v & 0xFF)

    # ---- flags ----
    def flags_word(self):
        return (self.cf | (1 << 1) | (self.pf << 2) | (self.af << 4) | (self.zf << 6) | (self.sf << 7)
                | (self.tf << 8) | (self.iflag << 9) | (self.df << 10) | (self.of << 11) | 0xF000)

    def set_flags_word(self, v):
        self.cf = bool(v & 1)
        self.pf = bool(v & 4)
        self.af = bool(v & 0x10)
        self.zf = bool(v & 0x40)
        self.sf = bool(v & 0x80)
        self.tf = bool(v & 0x100)
        self.iflag = bool(v & 0x200)
        self.df = bool(v & 0x400)
        self.of = bool(v & 0x800)

    def szp(self, v, w):
        if w:
            v &= 0xFFFF
            self.sf = v >= 0x8000
        else:
            v &= 0xFF
            self.sf = v >= 0x80
        self.zf = v == 0
        self.pf = PARITY[v & 0xFF]
        return v

    # ---- ModRM ----
    def modrm(self):
        """Decode ModRM. Returns (mod, reg, rm); for memory operands sets _ea_seg/_ea_ofs."""
        b = self.fetch8()
        mod, reg, rm = b >> 6, (b >> 3) & 7, b & 7
        if mod != 3:
            R = self.R
            if mod == 0 and rm == 6:
                ofs, seg = self.fetch16(), DS
            else:
                if rm == 0:
                    ofs, seg = R[BX] + R[SI], DS
                elif rm == 1:
                    ofs, seg = R[BX] + R[DI], DS
                elif rm == 2:
                    ofs, seg = R[BP] + R[SI], SS
                elif rm == 3:
                    ofs, seg = R[BP] + R[DI], SS
                elif rm == 4:
                    ofs, seg = R[SI], DS
                elif rm == 5:
                    ofs, seg = R[DI], DS
                elif rm == 6:
                    ofs, seg = R[BP], SS
                else:
                    ofs, seg = R[BX], DS
                if mod == 1:
                    ofs += self.fetchs8()
                elif mod == 2:
                    ofs += self.fetch16()
            self._ea_seg = self.S[self._seg_ovr if self._seg_ovr is not None else seg]
            self._ea_ofs = ofs & 0xFFFF
        return mod, reg, rm

    def rm_r(self, mod, rm, w):
        if mod == 3:
            return self.R[rm] if w else self.g8(rm)
        return self.rw(self._ea_seg, self._ea_ofs) if w else self.rb(self._ea_seg, self._ea_ofs)

    def rm_w(self, mod, rm, w, v):
        if mod == 3:
            if w:
                self.R[rm] = v & 0xFFFF
            else:
                self.s8(rm, v)
        elif w:
            self.ww(self._ea_seg, self._ea_ofs, v)
        else:
            self.wb(self._ea_seg, self._ea_ofs, v)

    # ---- ALU ----
    def alu(self, kind, a, b, w):
        mask = 0xFFFF if w else 0xFF
        top = 0x8000 if w else 0x80
        if kind == 0 or kind == 2:  # add, adc
            c = 1 if (kind == 2 and self.cf) else 0
            res = a + b + c
            self.cf = res > mask
            self.of = bool((~(a ^ b) & (a ^ res)) & top)
            self.af = bool((a ^ b ^ res) & 0x10)
        elif kind == 5 or kind == 7 or kind == 3:  # sub, cmp, sbb
            c = 1 if (kind == 3 and self.cf) else 0
            res = a - b - c
            self.cf = res < 0
            self.of = bool(((a ^ b) & (a ^ res)) & top)
            self.af = bool((a ^ b ^ res) & 0x10)
        elif kind == 1:
            res = a | b
            self.cf = self.of = self.af = False
        elif kind == 4:
            res = a & b
            self.cf = self.of = self.af = False
        else:  # 6 xor
            res = a ^ b
            self.cf = self.of = self.af = False
        return self.szp(res, w)

    def incdec(self, v, w, dec):
        cf = self.cf
        v = self.alu(5 if dec else 0, v, 1, w)
        self.cf = cf
        return v

    def shift(self, kind, v, count, w):
        bits = 16 if w else 8
        mask = (1 << bits) - 1
        top = 1 << (bits - 1)
        if count == 0:
            return v
        for _ in range(count):
            if kind == 0:  # rol
                c = (v >> (bits - 1)) & 1
                v = ((v << 1) | c) & mask
                self.cf = bool(c)
                self.of = bool(((v & top) != 0) ^ self.cf)
            elif kind == 1:  # ror
                c = v & 1
                v = (v >> 1) | (c << (bits - 1))
                self.cf = bool(c)
                self.of = bool(((v ^ (v << 1)) & top))
            elif kind == 2:  # rcl
                c = (v >> (bits - 1)) & 1
                v = ((v << 1) | self.cf) & mask
                self.cf = bool(c)
                self.of = bool(((v & top) != 0) ^ self.cf)
            elif kind == 3:  # rcr
                self.of = bool(((v & top) != 0) ^ self.cf)
                c = v & 1
                v = (v >> 1) | (self.cf << (bits - 1))
                self.cf = bool(c)
            elif kind == 4 or kind == 6:  # shl/sal
                self.cf = bool(v & top)
                v = (v << 1) & mask
                self.of = bool(((v & top) != 0) ^ self.cf)
                self.szp(v, w)
            elif kind == 5:  # shr
                self.of = bool(v & top)
                self.cf = bool(v & 1)
                v >>= 1
                self.szp(v, w)
            else:  # 7 sar
                self.cf = bool(v & 1)
                v = (v >> 1) | (v & top)
                self.of = False
                self.szp(v, w)
        return v

    def cond(self, c):
        if c == 0:
            return self.of
        if c == 1:
            return not self.of
        if c == 2:
            return self.cf
        if c == 3:
            return not self.cf
        if c == 4:
            return self.zf
        if c == 5:
            return not self.zf
        if c == 6:
            return self.cf or self.zf
        if c == 7:
            return not (self.cf or self.zf)
        if c == 8:
            return self.sf
        if c == 9:
            return not self.sf
        if c == 10:
            return self.pf
        if c == 11:
            return not self.pf
        if c == 12:
            return self.sf != self.of
        if c == 13:
            return self.sf == self.of
        if c == 14:
            return self.zf or self.sf != self.of
        return (not self.zf) and self.sf == self.of

    # ---- interrupts ----
    def interrupt(self, n):
        self.push(self.flags_word())
        self.push(self.S[CS])
        self.push(self.ip)
        self.iflag = False
        self.tf = False
        self.ip = self.rw(0, n * 4)
        self.S[CS] = self.rw(0, n * 4 + 2)

    # ---- execution ----
    def step(self):
        self.steps += 1
        self._seg_ovr = None
        rep = 0
        while True:
            op = self.fetch8()
            if op == 0x26 or op == 0x2E or op == 0x36 or op == 0x3E:
                self._seg_ovr = (op >> 3) & 3
                continue
            if op == 0xF2 or op == 0xF3:
                rep = op
                continue
            if op == 0xF0:
                continue
            break
        if rep and op in (0xA4, 0xA5, 0xA6, 0xA7, 0xAA, 0xAB, 0xAC, 0xAD, 0xAE, 0xAF):
            self.string_op(op, rep)
            return
        f = self.ops[op]
        if f is None:
            raise EmuError("opcode %02X at %04X:%04X" % (op, self.S[CS], (self.ip - 1) & 0xFFFF))
        f(op)

    def run_until(self, stop, limit=None):
        """Step until stop(cpu) is true or the step limit is reached."""
        while not stop(self):
            if limit is not None and self.steps >= limit:
                raise EmuError("step limit %d at %04X:%04X" % (limit, self.S[CS], self.ip))
            self.step()

    # ---- string instructions ----
    def string_op(self, op, rep):
        R = self.R
        w = op & 1
        size = 2 if w else 1
        delta = -size if self.df else size
        src_seg = self.S[self._seg_ovr if self._seg_ovr is not None else DS]
        es = self.S[ES]
        kind = op & 0xFE
        while True:
            if rep and R[CX] == 0:
                break
            if kind == 0xA4:  # movs
                v = self.rw(src_seg, R[SI]) if w else self.rb(src_seg, R[SI])
                if w:
                    self.ww(es, R[DI], v)
                else:
                    self.wb(es, R[DI], v)
                R[SI] = (R[SI] + delta) & 0xFFFF
                R[DI] = (R[DI] + delta) & 0xFFFF
            elif kind == 0xA6:  # cmps
                a = self.rw(src_seg, R[SI]) if w else self.rb(src_seg, R[SI])
                b = self.rw(es, R[DI]) if w else self.rb(es, R[DI])
                self.alu(7, a, b, w)
                R[SI] = (R[SI] + delta) & 0xFFFF
                R[DI] = (R[DI] + delta) & 0xFFFF
            elif kind == 0xAA:  # stos
                if w:
                    self.ww(es, R[DI], R[AX])
                else:
                    self.wb(es, R[DI], R[AX])
                R[DI] = (R[DI] + delta) & 0xFFFF
            elif kind == 0xAC:  # lods
                if w:
                    R[AX] = self.rw(src_seg, R[SI])
                else:
                    self.s8(0, self.rb(src_seg, R[SI]))
                R[SI] = (R[SI] + delta) & 0xFFFF
            elif kind == 0xAE:  # scas
                b = self.rw(es, R[DI]) if w else self.rb(es, R[DI])
                self.alu(7, R[AX] if w else R[AX] & 0xFF, b, w)
                R[DI] = (R[DI] + delta) & 0xFFFF
            if not rep:
                break
            R[CX] = (R[CX] - 1) & 0xFFFF
            if kind == 0xA6 or kind == 0xAE:
                if rep == 0xF3 and not self.zf:
                    break
                if rep == 0xF2 and self.zf:
                    break

    # ---- opcode table ----
    def _build(self):
        o = self.ops
        for base in range(0, 0x40, 8):
            if base + 5 < 0x40:
                for k in range(6):
                    o[base + k] = self.op_alu
        for x in (0x06, 0x0E, 0x16, 0x1E):
            o[x] = self.op_push_seg
        for x in (0x07, 0x17, 0x1F):
            o[x] = self.op_pop_seg
        o[0x27] = self.op_daa
        o[0x2F] = self.op_das
        o[0x37] = self.op_aaa
        o[0x3F] = self.op_aas
        for x in range(0x40, 0x50):
            o[x] = self.op_incdec_r
        for x in range(0x50, 0x58):
            o[x] = self.op_push_r
        for x in range(0x58, 0x60):
            o[x] = self.op_pop_r
        o[0x60] = self.op_pusha
        o[0x61] = self.op_popa
        o[0x68] = self.op_push_imm
        o[0x6A] = self.op_push_imm
        o[0x69] = self.op_imul_imm
        o[0x6B] = self.op_imul_imm
        for x in range(0x70, 0x80):
            o[x] = self.op_jcc
        for x in range(0x80, 0x84):
            o[x] = self.op_grp1
        o[0x84] = o[0x85] = self.op_test_rm
        o[0x86] = o[0x87] = self.op_xchg_rm
        for x in range(0x88, 0x8C):
            o[x] = self.op_mov_rm
        o[0x8C] = self.op_mov_rm_sreg
        o[0x8D] = self.op_lea
        o[0x8E] = self.op_mov_sreg_rm
        o[0x8F] = self.op_pop_rm
        for x in range(0x90, 0x98):
            o[x] = self.op_xchg_ax
        o[0x98] = self.op_cbw
        o[0x99] = self.op_cwd
        o[0x9A] = self.op_call_far
        o[0x9B] = self.op_nop
        o[0x9C] = self.op_pushf
        o[0x9D] = self.op_popf
        o[0x9E] = self.op_sahf
        o[0x9F] = self.op_lahf
        for x in range(0xA0, 0xA4):
            o[x] = self.op_mov_moffs
        for x in range(0xA4, 0xA8):
            o[x] = self.op_string
        o[0xA8] = o[0xA9] = self.op_test_imm
        for x in range(0xAA, 0xB0):
            o[x] = self.op_string
        for x in range(0xB0, 0xC0):
            o[x] = self.op_mov_imm_r
        o[0xC0] = o[0xC1] = self.op_shift_imm
        o[0xC2] = o[0xC3] = self.op_ret
        o[0xC4] = o[0xC5] = self.op_lxs
        o[0xC6] = o[0xC7] = self.op_mov_imm_rm
        o[0xC8] = self.op_enter
        o[0xC9] = self.op_leave
        o[0xCA] = o[0xCB] = self.op_retf
        o[0xCC] = self.op_int3
        o[0xCD] = self.op_int
        o[0xCE] = self.op_into
        o[0xCF] = self.op_iret
        for x in range(0xD0, 0xD4):
            o[x] = self.op_shift
        o[0xD4] = self.op_aam
        o[0xD5] = self.op_aad
        o[0xD7] = self.op_xlat
        for x in range(0xE0, 0xE4):
            o[x] = self.op_loop
        for x in (0xE4, 0xE5, 0xEC, 0xED):
            o[x] = self.op_in
        for x in (0xE6, 0xE7, 0xEE, 0xEF):
            o[x] = self.op_out
        o[0xE8] = self.op_call_near
        o[0xE9] = self.op_jmp_near
        o[0xEA] = self.op_jmp_far
        o[0xEB] = self.op_jmp_short
        o[0xF4] = self.op_hlt
        o[0xF5] = self.op_cmc
        o[0xF6] = o[0xF7] = self.op_grp3
        for x in range(0xF8, 0xFE):
            o[x] = self.op_flag
        o[0xFE] = o[0xFF] = self.op_grp45

    # ---- handlers ----
    def op_alu(self, op):
        kind = op >> 3
        form = op & 7
        if form < 4:
            w = form & 1
            mod, reg, rm = self.modrm()
            regv = self.R[reg] if w else self.g8(reg)
            rmv = self.rm_r(mod, rm, w)
            if form & 2:  # reg <- reg op rm
                res = self.alu(kind, regv, rmv, w)
                if kind != 7:
                    if w:
                        self.R[reg] = res
                    else:
                        self.s8(reg, res)
            else:
                res = self.alu(kind, rmv, regv, w)
                if kind != 7:
                    self.rm_w(mod, rm, w, res)
        elif form == 4:
            res = self.alu(kind, self.R[AX] & 0xFF, self.fetch8(), 0)
            if kind != 7:
                self.s8(0, res)
        else:
            res = self.alu(kind, self.R[AX], self.fetch16(), 1)
            if kind != 7:
                self.R[AX] = res

    def op_push_seg(self, op):
        self.push(self.S[(op >> 3) & 3])

    def op_pop_seg(self, op):
        self.S[(op >> 3) & 3] = self.pop()

    def op_daa(self, op):
        al = self.R[AX] & 0xFF
        cf = self.cf
        if (al & 0x0F) > 9 or self.af:
            al += 6
            self.af = True
        else:
            self.af = False
        if (self.R[AX] & 0xFF) > 0x99 or cf:
            al += 0x60
            self.cf = True
        else:
            self.cf = False
        self.s8(0, al)
        self.szp(al, 0)

    def op_das(self, op):
        al0 = self.R[AX] & 0xFF
        al = al0
        cf = self.cf
        if (al & 0x0F) > 9 or self.af:
            al -= 6
            self.af = True
        else:
            self.af = False
        if al0 > 0x99 or cf:
            al -= 0x60
            self.cf = True
        else:
            self.cf = False
        self.s8(0, al)
        self.szp(al, 0)

    def op_aaa(self, op):
        ax = self.R[AX]
        if (ax & 0x0F) > 9 or self.af:
            ax = ((ax + 0x106) & 0xFF00) | ((ax + 6) & 0x0F)
            self.af = self.cf = True
        else:
            ax &= 0xFF0F
            self.af = self.cf = False
        self.R[AX] = ax & 0xFFFF

    def op_aas(self, op):
        ax = self.R[AX]
        if (ax & 0x0F) > 9 or self.af:
            ax = ((ax - 0x106) & 0xFF00) | ((ax - 6) & 0x0F)
            self.af = self.cf = True
        else:
            ax &= 0xFF0F
            self.af = self.cf = False
        self.R[AX] = ax & 0xFFFF

    def op_incdec_r(self, op):
        i = op & 7
        self.R[i] = self.incdec(self.R[i], 1, op & 8)

    def op_push_r(self, op):
        i = op & 7
        if i == SP:
            v = (self.R[SP] - 2) & 0xFFFF  # 8086 pushes the decremented value
            self.push(v)
        else:
            self.push(self.R[i])

    def op_pop_r(self, op):
        self.R[op & 7] = self.pop()

    def op_pusha(self, op):
        sp = self.R[SP]
        for i in range(8):
            self.push(sp if i == SP else self.R[i])

    def op_popa(self, op):
        for i in range(7, -1, -1):
            v = self.pop()
            if i != SP:
                self.R[i] = v

    def op_push_imm(self, op):
        self.push((self.fetchs8() if op == 0x6A else self.fetch16()) & 0xFFFF)

    def op_imul_imm(self, op):
        mod, reg, rm = self.modrm()
        a = self.rm_r(mod, rm, 1)
        b = self.fetchs8() if op == 0x6B else self.fetch16()
        if b >= 0x8000:
            b -= 0x10000
        if a >= 0x8000:
            a -= 0x10000
        res = a * b
        self.R[reg] = res & 0xFFFF
        self.cf = self.of = not (-0x8000 <= res <= 0x7FFF)

    def op_jcc(self, op):
        d = self.fetchs8()
        if self.cond(op & 0x0F):
            self.ip = (self.ip + d) & 0xFFFF

    def op_grp1(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        a = self.rm_r(mod, rm, w)
        if op == 0x81:
            b = self.fetch16()
        elif op == 0x83:
            b = self.fetchs8() & 0xFFFF
        else:
            b = self.fetch8()
        res = self.alu(reg, a, b, w)
        if reg != 7:
            self.rm_w(mod, rm, w, res)

    def op_test_rm(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        self.alu(4, self.rm_r(mod, rm, w), self.R[reg] if w else self.g8(reg), w)

    def op_xchg_rm(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        a = self.rm_r(mod, rm, w)
        b = self.R[reg] if w else self.g8(reg)
        self.rm_w(mod, rm, w, b)
        if w:
            self.R[reg] = a
        else:
            self.s8(reg, a)

    def op_mov_rm(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        if op & 2:
            v = self.rm_r(mod, rm, w)
            if w:
                self.R[reg] = v
            else:
                self.s8(reg, v)
        else:
            self.rm_w(mod, rm, w, self.R[reg] if w else self.g8(reg))

    def op_mov_rm_sreg(self, op):
        mod, reg, rm = self.modrm()
        self.rm_w(mod, rm, 1, self.S[reg & 3])

    def op_lea(self, op):
        mod, reg, rm = self.modrm()
        if mod == 3:
            raise EmuError("lea with register operand")
        self.R[reg] = self._ea_ofs

    def op_mov_sreg_rm(self, op):
        mod, reg, rm = self.modrm()
        self.S[reg & 3] = self.rm_r(mod, rm, 1)

    def op_pop_rm(self, op):
        # The 8086 computes the address after the pop (SP already incremented).
        v = self.pop()
        mod, reg, rm = self.modrm()
        self.rm_w(mod, rm, 1, v)

    def op_xchg_ax(self, op):
        i = op & 7
        self.R[AX], self.R[i] = self.R[i], self.R[AX]

    def op_cbw(self, op):
        al = self.R[AX] & 0xFF
        self.R[AX] = al | 0xFF00 if al & 0x80 else al

    def op_cwd(self, op):
        self.R[DX] = 0xFFFF if self.R[AX] & 0x8000 else 0

    def op_call_far(self, op):
        ofs = self.fetch16()
        seg = self.fetch16()
        self.push(self.S[CS])
        self.push(self.ip)
        self.S[CS] = seg
        self.ip = ofs

    def op_nop(self, op):
        pass

    def op_pushf(self, op):
        self.push(self.flags_word())

    def op_popf(self, op):
        self.set_flags_word(self.pop())

    def op_sahf(self, op):
        ah = self.R[AX] >> 8
        f = self.flags_word() & 0xFF00
        self.set_flags_word(f | ah)

    def op_lahf(self, op):
        self.s8(4, self.flags_word() & 0xFF)

    def op_mov_moffs(self, op):
        ofs = self.fetch16()
        seg = self.S[self._seg_ovr if self._seg_ovr is not None else DS]
        w = op & 1
        if op & 2:
            if w:
                self.ww(seg, ofs, self.R[AX])
            else:
                self.wb(seg, ofs, self.R[AX])
        elif w:
            self.R[AX] = self.rw(seg, ofs)
        else:
            self.s8(0, self.rb(seg, ofs))

    def op_string(self, op):
        self.string_op(op, 0)

    def op_test_imm(self, op):
        if op & 1:
            self.alu(4, self.R[AX], self.fetch16(), 1)
        else:
            self.alu(4, self.R[AX] & 0xFF, self.fetch8(), 0)

    def op_mov_imm_r(self, op):
        if op & 8:
            self.R[op & 7] = self.fetch16()
        else:
            self.s8(op & 7, self.fetch8())

    def op_shift_imm(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        v = self.rm_r(mod, rm, w)
        count = self.fetch8() & 0x1F
        self.rm_w(mod, rm, w, self.shift(reg, v, count, w))

    def op_ret(self, op):
        n = self.fetch16() if op == 0xC2 else 0
        self.ip = self.pop()
        self.R[SP] = (self.R[SP] + n) & 0xFFFF

    def op_lxs(self, op):
        mod, reg, rm = self.modrm()
        self.R[reg] = self.rw(self._ea_seg, self._ea_ofs)
        self.S[ES if op == 0xC4 else DS] = self.rw(self._ea_seg, self._ea_ofs + 2)

    def op_mov_imm_rm(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        self.rm_w(mod, rm, w, self.fetch16() if w else self.fetch8())

    def op_enter(self, op):
        size = self.fetch16()
        level = self.fetch8() & 0x1F
        self.push(self.R[BP])
        frame = self.R[SP]
        for _ in range(1, level):
            self.R[BP] = (self.R[BP] - 2) & 0xFFFF
            self.push(self.rw(self.S[SS], self.R[BP]))
        if level:
            self.push(frame)
        self.R[BP] = frame
        self.R[SP] = (self.R[SP] - size) & 0xFFFF

    def op_leave(self, op):
        self.R[SP] = self.R[BP]
        self.R[BP] = self.pop()

    def op_retf(self, op):
        n = self.fetch16() if op == 0xCA else 0
        self.ip = self.pop()
        self.S[CS] = self.pop()
        self.R[SP] = (self.R[SP] + n) & 0xFFFF

    def op_int3(self, op):
        self.interrupt(3)

    def op_int(self, op):
        self.interrupt(self.fetch8())

    def op_into(self, op):
        if self.of:
            self.interrupt(4)

    def op_iret(self, op):
        self.ip = self.pop()
        self.S[CS] = self.pop()
        self.set_flags_word(self.pop())

    def op_shift(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        v = self.rm_r(mod, rm, w)
        count = (self.R[CX] & 0xFF) if op & 2 else 1
        self.rm_w(mod, rm, w, self.shift(reg, v, count, w))

    def op_aam(self, op):
        base = self.fetch8()
        if base == 0:
            self.interrupt(0)
            return
        al = self.R[AX] & 0xFF
        self.R[AX] = ((al // base) << 8) | (al % base)
        self.szp(self.R[AX] & 0xFF, 0)

    def op_aad(self, op):
        base = self.fetch8()
        ax = self.R[AX]
        al = ((ax >> 8) * base + (ax & 0xFF)) & 0xFF
        self.R[AX] = al
        self.szp(al, 0)

    def op_xlat(self, op):
        seg = self.S[self._seg_ovr if self._seg_ovr is not None else DS]
        self.s8(0, self.rb(seg, self.R[BX] + (self.R[AX] & 0xFF)))

    def op_loop(self, op):
        d = self.fetchs8()
        if op == 0xE3:
            if self.R[CX] == 0:
                self.ip = (self.ip + d) & 0xFFFF
            return
        cx = (self.R[CX] - 1) & 0xFFFF
        self.R[CX] = cx
        if op == 0xE2:
            take = cx != 0
        elif op == 0xE1:
            take = cx != 0 and self.zf
        else:
            take = cx != 0 and not self.zf
        if take:
            self.ip = (self.ip + d) & 0xFFFF

    def op_in(self, op):
        port = self.fetch8() if op < 0xE8 else self.R[DX]
        w = op & 1
        if w:
            v = self.host.port_in(port, 1)
            self.R[AX] = v & 0xFFFF
        else:
            self.s8(0, self.host.port_in(port, 0))

    def op_out(self, op):
        port = self.fetch8() if op < 0xE8 else self.R[DX]
        w = op & 1
        self.host.port_out(port, self.R[AX] if w else self.R[AX] & 0xFF, w)

    def op_call_near(self, op):
        d = self.fetch16()
        self.push(self.ip)
        self.ip = (self.ip + d) & 0xFFFF

    def op_jmp_near(self, op):
        d = self.fetch16()
        self.ip = (self.ip + d) & 0xFFFF

    def op_jmp_far(self, op):
        ofs = self.fetch16()
        seg = self.fetch16()
        self.ip = ofs
        self.S[CS] = seg

    def op_jmp_short(self, op):
        d = self.fetchs8()
        self.ip = (self.ip + d) & 0xFFFF

    def op_hlt(self, op):
        raise EmuError("hlt at %04X:%04X" % (self.S[CS], self.ip))

    def op_cmc(self, op):
        self.cf = not self.cf

    def op_grp3(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        v = self.rm_r(mod, rm, w)
        R = self.R
        if reg == 0 or reg == 1:  # test imm
            self.alu(4, v, self.fetch16() if w else self.fetch8(), w)
        elif reg == 2:  # not
            self.rm_w(mod, rm, w, ~v)
        elif reg == 3:  # neg
            res = self.alu(5, 0, v, w)
            self.cf = v != 0
            self.rm_w(mod, rm, w, res)
        elif reg == 4:  # mul
            if w:
                res = R[AX] * v
                R[AX] = res & 0xFFFF
                R[DX] = res >> 16
                self.cf = self.of = R[DX] != 0
            else:
                res = (R[AX] & 0xFF) * v
                R[AX] = res & 0xFFFF
                self.cf = self.of = (res >> 8) != 0
        elif reg == 5:  # imul
            if w:
                a = R[AX] - 0x10000 if R[AX] & 0x8000 else R[AX]
                b = v - 0x10000 if v & 0x8000 else v
                res = a * b
                R[AX] = res & 0xFFFF
                R[DX] = (res >> 16) & 0xFFFF
                self.cf = self.of = not (-0x8000 <= res <= 0x7FFF)
            else:
                al = R[AX] & 0xFF
                a = al - 0x100 if al & 0x80 else al
                b = v - 0x100 if v & 0x80 else v
                res = a * b
                R[AX] = res & 0xFFFF
                self.cf = self.of = not (-0x80 <= res <= 0x7F)
        elif reg == 6:  # div
            if v == 0:
                self.ip_fault()
                return
            if w:
                num = (R[DX] << 16) | R[AX]
                q, r = divmod(num, v)
                if q > 0xFFFF:
                    self.ip_fault()
                    return
                R[AX], R[DX] = q, r
            else:
                q, r = divmod(R[AX], v)
                if q > 0xFF:
                    self.ip_fault()
                    return
                R[AX] = (r << 8) | q
        else:  # idiv
            if w:
                num = (R[DX] << 16) | R[AX]
                if num & 0x80000000:
                    num -= 1 << 32
                d = v - 0x10000 if v & 0x8000 else v
                lim = 0x7FFF
            else:
                num = R[AX] - 0x10000 if R[AX] & 0x8000 else R[AX]
                d = v - 0x100 if v & 0x80 else v
                lim = 0x7F
            if d == 0:
                self.ip_fault()
                return
            q = abs(num) // abs(d)
            if (num < 0) != (d < 0):
                q = -q
            r = num - q * d
            if q > lim or q < -lim - 1:
                self.ip_fault()
                return
            if w:
                R[AX], R[DX] = q & 0xFFFF, r & 0xFFFF
            else:
                R[AX] = ((r & 0xFF) << 8) | (q & 0xFF)

    def ip_fault(self):
        # Divide error: the 8086 pushes the address of the next instruction.
        self.interrupt(0)

    def op_flag(self, op):
        if op == 0xF8:
            self.cf = False
        elif op == 0xF9:
            self.cf = True
        elif op == 0xFA:
            self.iflag = False
        elif op == 0xFB:
            self.iflag = True
        elif op == 0xFC:
            self.df = False
        else:
            self.df = True

    def op_grp45(self, op):
        w = op & 1
        mod, reg, rm = self.modrm()
        if reg == 0 or reg == 1:
            self.rm_w(mod, rm, w, self.incdec(self.rm_r(mod, rm, w), w, reg))
            return
        if not w:
            raise EmuError("FE /%d" % reg)
        if reg == 2:
            t = self.rm_r(mod, rm, 1)
            self.push(self.ip)
            self.ip = t
        elif reg == 3:
            ofs = self.rw(self._ea_seg, self._ea_ofs)
            seg = self.rw(self._ea_seg, self._ea_ofs + 2)
            self.push(self.S[CS])
            self.push(self.ip)
            self.ip, self.S[CS] = ofs, seg
        elif reg == 4:
            self.ip = self.rm_r(mod, rm, 1)
        elif reg == 5:
            self.ip = self.rw(self._ea_seg, self._ea_ofs)
            self.S[CS] = self.rw(self._ea_seg, self._ea_ofs + 2)
        elif reg == 6:
            self.push(self.rm_r(mod, rm, 1))
        else:
            raise EmuError("FF /7")
