#!/usr/bin/env python3
"""Emulated PC for the playfield probe: DOS, BIOS, VGA mode 13h with DAC, BOMB.EXE loader.

The program runs unchanged from its entry point: Turbo Pascal runtime, Crt, Graph with the
linked VGA256 driver, the game units and the overlay manager. Only BIOS/DOS services and
I/O ports are provided by the host. Files are read from the game data folder (read only);
writes to files are refused. Nothing of the originals is stored in this file
(AGENTS.md section 2.1).
"""
import os
import struct

from feld_cpu import AX, BX, CX, DX, SI, DI, BP, SP, CS, DS, ES, SS, Cpu, EmuError

ENV_SEG = 0x0080
PSP_SEG = 0x0100
LOAD_SEG = PSP_SEG + 0x10
MEM_TOP = 0xA000
TRAP_SEG = 0xF000          # interrupt vectors point to TRAP_SEG:0100+n (an IRET)
RET_TRAP = 0x0300          # far return address for calls started by the host
FONT_OFS = 0xFA6E          # BIOS 8x8 font (characters 0..127)
FONT_HI_OFS = 0xE000       # characters 128..255 (INT 1Fh)
TICK_STEPS = 20000         # emulated instructions per BIOS timer tick


def vga_default_dac():
    """DAC after INT 10h mode 13h. [APPROX] Model of the VGA BIOS default palette (EGA colours, grey ramp,
    9 hue rings), not taken from the original files; only entries the game never sets depend on it."""
    ega = [(0, 0, 0), (0, 0, 42), (0, 42, 0), (0, 42, 42), (42, 0, 0), (42, 0, 42), (42, 21, 0), (42, 42, 42),
           (21, 21, 21), (21, 21, 63), (21, 63, 21), (21, 63, 63), (63, 21, 21), (63, 21, 63), (63, 63, 21),
           (63, 63, 63)]
    grey = [0, 5, 8, 11, 14, 17, 20, 24, 28, 32, 36, 40, 45, 50, 56, 63]
    out = list(ega) + [(g, g, g) for g in grey]
    for steps in ([0, 16, 31, 47, 63], [31, 39, 47, 55, 63], [45, 49, 54, 58, 63],
                  [0, 7, 14, 21, 28], [14, 17, 21, 24, 28], [20, 22, 24, 26, 28],
                  [0, 4, 8, 12, 16], [8, 10, 12, 14, 16], [11, 12, 13, 15, 16]):
        lo, hi = steps[0], steps[4]
        ring = []
        for k in range(5):            # blue -> magenta
            ring.append((steps[k], lo, hi))
        for k in range(3, -1, -1):    # magenta -> red
            ring.append((hi, lo, steps[k]))
        for k in range(1, 5):         # red -> yellow
            ring.append((hi, steps[k], lo))
        for k in range(3, -1, -1):    # yellow -> green
            ring.append((steps[k], hi, lo))
        for k in range(1, 5):         # green -> cyan
            ring.append((lo, hi, steps[k]))
        for k in range(3, 0, -1):     # cyan -> blue
            ring.append((lo, steps[k], hi))
        out += ring
    out += [(0, 0, 0)] * 8
    assert len(out) == 256
    return bytearray(v for rgb in out for v in rgb)


class ProgramExit(Exception):
    def __init__(self, code):
        super().__init__("exit %d" % code)
        self.code = code


class Machine:
    def __init__(self, gamedata, exe_name="BOMB.EXE", font8x8=None, cmdline=b"", extra_files=None):
        self.gamedata = gamedata
        self.extra_files = {k.upper(): v for k, v in (extra_files or {}).items()}
        self.mem = bytearray(1 << 20)
        self.cpu = Cpu(self.mem, self)
        self.files = {}
        self.next_handle = 5
        self.dta = (PSP_SEG, 0x80)
        self.find_list = []
        self.stdout = bytearray()
        self.dac = bytearray(768)
        self.dac_w = 0
        self.dac_written = set()   # DAC entries written since the last reset (ports or INT 10h)
        self.dac_r = 0
        self.dac_sub_w = 0
        self.dac_sub_r = 0
        self.retrace = 0
        self.ports = {}
        self.next_tick = TICK_STEPS
        self.traps = {}            # linear address -> callable(machine) before executing there
        self.int_hooks = {}        # interrupt number -> callable(machine) -> True if handled
        self.log = []
        self._init_bios(font8x8)
        self._load(exe_name, cmdline)

    # ---- set up ----
    def _init_bios(self, font8x8):
        m = self.mem
        for n in range(256):
            struct.pack_into("<HH", m, n * 4, 0x100 + n, TRAP_SEG)
            m[(TRAP_SEG << 4) + 0x100 + n] = 0xCF  # IRET
        m[(TRAP_SEG << 4) + RET_TRAP] = 0xF4       # HLT, never executed (trapped)
        if font8x8 is not None:
            m[(TRAP_SEG << 4) + FONT_OFS:(TRAP_SEG << 4) + FONT_OFS + 1024] = font8x8[:1024]
            m[(TRAP_SEG << 4) + FONT_HI_OFS:(TRAP_SEG << 4) + FONT_HI_OFS + 1024] = font8x8[1024:2048]
        struct.pack_into("<HH", m, 0x1F * 4, FONT_HI_OFS, TRAP_SEG)
        bda = 0x400
        struct.pack_into("<H", m, bda + 0x10, 0x0026)     # equipment: colour 80x25
        struct.pack_into("<H", m, bda + 0x13, 640)        # memory in KB
        m[bda + 0x49] = 3
        struct.pack_into("<H", m, bda + 0x4A, 80)
        struct.pack_into("<H", m, bda + 0x63, 0x3D4)
        m[bda + 0x84] = 24
        m[bda + 0x85] = 16
        struct.pack_into("<HH", m, bda + 0x1A, 0x1E, 0x1E)  # keyboard buffer empty
        struct.pack_into("<HH", m, bda + 0x80, 0x1E, 0x3E)

    def _load(self, exe_name, cmdline):
        exe = self.read_file(exe_name)
        (sig, lastp, pages, nrel, hdrpar, minalloc, maxalloc, ss, sp, csum, ip, cs, relo, ovl) = \
            struct.unpack("<2s13H", exe[:28])
        hdr = hdrpar * 16
        size = (pages - 1) * 512 + lastp if lastp else pages * 512
        img = exe[hdr:size]
        base = LOAD_SEG << 4
        self.mem[base:base + len(img)] = img
        for i in range(nrel):
            o, s = struct.unpack_from("<HH", exe, relo + 4 * i)
            a = base + s * 16 + o
            struct.pack_into("<H", self.mem, a, (struct.unpack_from("<H", self.mem, a)[0] + LOAD_SEG) & 0xFFFF)
        env = b"PATH=C:\\\x00COMSPEC=C:\\COMMAND.COM\x00\x00\x01\x00C:\\BOMB\\" + exe_name.encode() + b"\x00"
        self.mem[ENV_SEG << 4:(ENV_SEG << 4) + len(env)] = env
        p = PSP_SEG << 4
        self.mem[p:p + 2] = b"\xcd\x20"
        struct.pack_into("<H", self.mem, p + 2, MEM_TOP)
        struct.pack_into("<H", self.mem, p + 0x2C, ENV_SEG)
        cl = cmdline[:126]
        self.mem[p + 0x80] = len(cl)
        self.mem[p + 0x81:p + 0x81 + len(cl)] = cl
        self.mem[p + 0x81 + len(cl)] = 0x0D
        c = self.cpu
        c.S[CS], c.ip = (cs + LOAD_SEG) & 0xFFFF, ip
        c.S[SS], c.R[SP] = (ss + LOAD_SEG) & 0xFFFF, sp
        c.S[DS] = c.S[ES] = PSP_SEG
        self.image_size = len(img)

    # ---- files (read only, from the game data folder) ----
    def find_file(self, name):
        want = name.replace("/", "\\").split("\\")[-1].upper()
        if want in self.extra_files:
            return self.extra_files[want]
        for f in os.listdir(self.gamedata):
            if f.upper() == want:
                return os.path.join(self.gamedata, f)
        return None

    def file_data(self, name):
        """Contents of a file (game data folder or an extra file given as path or bytes), or None."""
        path = self.find_file(name)
        if path is None:
            return None
        if isinstance(path, (bytes, bytearray)):
            return bytes(path)
        with open(path, "rb") as fh:
            return fh.read()

    def read_file(self, name):
        data = self.file_data(name)
        if data is None:
            raise FileNotFoundError(name)
        return data

    # ---- helpers ----
    def lin(self, seg, ofs):
        return ((seg << 4) + ofs) & 0xFFFFF

    def asciiz(self, seg, ofs):
        a = self.lin(seg, ofs)
        e = self.mem.index(0, a)
        return bytes(self.mem[a:e]).decode("latin-1")

    def set_ret_flag(self, bit, value):
        """Set a flag in the FLAGS image an IRET will restore (interrupt handlers)."""
        c = self.cpu
        a = self.lin(c.S[SS], c.R[SP] + 4)
        f = self.mem[a] | (self.mem[a + 1] << 8)
        f = f | bit if value else f & ~bit
        self.mem[a] = f & 0xFF
        self.mem[a + 1] = (f >> 8) & 0xFF

    def ret_cf(self, value):
        self.set_ret_flag(1, value)

    def ret_zf(self, value):
        self.set_ret_flag(0x40, value)

    # ---- run ----
    def run(self, stop=None, limit=None):
        """Run until stop(machine) is true at an instruction boundary."""
        c = self.cpu
        traps = self.traps
        mem = self.mem
        while True:
            a = ((c.S[CS] << 4) + c.ip) & 0xFFFFF
            if a in traps:
                if traps[a](self):
                    return
                a = ((c.S[CS] << 4) + c.ip) & 0xFFFFF
            if c.S[CS] == TRAP_SEG:
                if c.ip == RET_TRAP:
                    return
                if 0x100 <= c.ip < 0x200:
                    self.service(c.ip - 0x100)
            if stop is not None and stop(self):
                return
            if limit is not None and c.steps >= limit:
                raise EmuError("step limit at %04X:%04X" % (c.S[CS], c.ip))
            c.step()
            if c.steps >= self.next_tick:
                self.next_tick += TICK_STEPS
                t = struct.unpack_from("<I", mem, 0x46C)[0] + 1
                struct.pack_into("<I", mem, 0x46C, t & 0xFFFFFFFF)

    def far_call(self, seg, ofs, args=(), limit=None):
        """Call seg:ofs with Pascal arguments (pushed left to right) and run until it returns."""
        c = self.cpu
        for v in args:
            c.push(v & 0xFFFF)
        c.push(TRAP_SEG)
        c.push(RET_TRAP)
        c.S[CS], c.ip = seg, ofs
        self.run(limit=limit)

    # ---- interrupt services ----
    def service(self, n):
        hook = self.int_hooks.get(n)
        if hook is not None and hook(self):
            return
        if n == 0x21:
            self.int21()
        elif n == 0x10:
            self.int10()
        elif n == 0x16:
            self.int16()
        elif n == 0x1A:
            c = self.cpu
            if (c.R[AX] >> 8) == 0:
                t = struct.unpack_from("<I", self.mem, 0x46C)[0]
                c.R[CX], c.R[DX] = t >> 16, t & 0xFFFF
                c.s8(0, 0)
        elif n == 0x11:
            self.cpu.R[AX] = struct.unpack_from("<H", self.mem, 0x410)[0]
        elif n == 0x12:
            self.cpu.R[AX] = 640
        elif n == 0x33:
            if self.cpu.R[AX] == 0:
                self.cpu.R[AX] = 0  # no mouse
        elif n == 0x15:
            self.ret_cf(True)
        elif n == 0x2F:
            self.cpu.s8(0, 0)
        elif n == 0x00:
            raise EmuError("divide error without handler")
        elif n in (0x08, 0x09, 0x1B, 0x1C, 0x23, 0x24, 0x28, 0x0A, 0x67):
            pass
        else:
            self.log.append("int %02X ax=%04X unhandled" % (n, self.cpu.R[AX]))

    def int16(self):
        c = self.cpu
        ah = c.R[AX] >> 8
        if ah in (0x00, 0x10):
            raise EmuError("program waits for a key (INT 16h)")
        if ah in (0x01, 0x11):
            self.ret_zf(True)
        elif ah in (0x02, 0x12):
            c.s8(0, 0)

    def int10(self):
        c = self.cpu
        ax = c.R[AX]
        ah, al = ax >> 8, ax & 0xFF
        m = self.mem
        if ah == 0x00:
            mode = al & 0x7F
            m[0x449] = mode
            if mode == 0x13:
                struct.pack_into("<H", m, 0x44A, 40)
                self.dac[:] = vga_default_dac()
                if not al & 0x80:
                    m[0xA0000:0xAFA00] = bytes(64000)
            else:
                struct.pack_into("<H", m, 0x44A, 80)
        elif ah == 0x0F:
            c.R[AX] = (struct.unpack_from("<H", m, 0x44A)[0] << 8) | m[0x449]
            c.s8(7, 0)  # BH page
        elif ah == 0x1A and al == 0x00:
            c.s8(0, 0x1A)
            c.R[BX] = 0x0008
        elif ah == 0x12 and (c.R[BX] & 0xFF) == 0x10:
            c.R[BX] = 0x0003
            c.R[CX] = 0x0009
        elif ah == 0x10:
            bx = c.R[BX]
            if al == 0x10:
                i = (bx & 0xFF) * 3
                self.dac[i:i + 3] = bytes([(c.R[DX] >> 8) & 0x3F, (c.R[CX] >> 8) & 0x3F, c.R[CX] & 0x3F])
                self.dac_written.add(bx & 0xFF)
            elif al == 0x12:
                src = self.lin(c.S[ES], c.R[DX])
                for k in range(c.R[CX]):
                    j = ((bx + k) & 0xFF) * 3
                    self.dac[j:j + 3] = bytes(v & 0x3F for v in m[src + 3 * k:src + 3 * k + 3])
                    self.dac_written.add((bx + k) & 0xFF)
            elif al == 0x15:
                i = (bx & 0xFF) * 3
                r, g, b = self.dac[i:i + 3]
                c.R[DX] = (c.R[DX] & 0xFF) | (r << 8)
                c.R[CX] = (g << 8) | b
            elif al == 0x17:
                dst = self.lin(c.S[ES], c.R[DX])
                for k in range(c.R[CX]):
                    j = ((bx + k) & 0xFF) * 3
                    m[dst + 3 * k:dst + 3 * k + 3] = self.dac[j:j + 3]
            else:
                self.log.append("int 10 ax=%04X" % ax)
        elif ah == 0x11 and al == 0x30:
            bh = c.R[BX] >> 8
            c.S[ES] = TRAP_SEG
            c.R[BP] = FONT_HI_OFS if bh == 4 else FONT_OFS
            c.R[CX] = 8
            c.R[DX] = (c.R[DX] & 0xFF00) | 24
        elif ah == 0x0C:
            if m[0x449] == 0x13 and c.R[CX] < 320 and c.R[DX] < 200:
                m[0xA0000 + c.R[DX] * 320 + c.R[CX]] = al
        elif ah == 0x0D:
            if m[0x449] == 0x13 and c.R[CX] < 320 and c.R[DX] < 200:
                c.s8(0, m[0xA0000 + c.R[DX] * 320 + c.R[CX]])
        elif ah in (0x01, 0x02, 0x03, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0B, 0x0E):
            pass
        else:
            self.log.append("int 10 ax=%04X" % ax)

    def int21(self):
        c = self.cpu
        R = c.R
        ax = R[AX]
        ah, al = ax >> 8, ax & 0xFF
        m = self.mem
        ok = True
        if ah == 0x30:
            R[AX] = 0x0005
            R[BX] = 0
            R[CX] = 0
        elif ah == 0x25:
            struct.pack_into("<HH", m, al * 4, R[DX], c.S[DS])
        elif ah == 0x35:
            R[BX], c.S[ES] = struct.unpack_from("<HH", m, al * 4)
        elif ah == 0x4A:
            pass
        elif ah == 0x48:
            R[AX] = 8
            R[BX] = 0
            ok = False
        elif ah == 0x49:
            pass
        elif ah == 0x4C:
            raise ProgramExit(al)
        elif ah == 0x62 or ah == 0x51:
            R[BX] = PSP_SEG
        elif ah == 0x2C:
            R[CX] = 0x0C00
            R[DX] = 0x0000
        elif ah == 0x2A:
            R[CX] = 1992
            R[DX] = 0x0101
            c.s8(0, 3)
        elif ah == 0x19:
            c.s8(0, 2)
        elif ah == 0x0E:
            c.s8(0, 26)
        elif ah == 0x47:
            a = self.lin(c.S[DS], R[SI])
            m[a:a + 5] = b"BOMB\x00"
        elif ah == 0x3B:
            pass
        elif ah == 0x1A:
            self.dta = (c.S[DS], R[DX])
        elif ah == 0x2F:
            c.S[ES], R[BX] = self.dta
        elif ah == 0x33:
            R[DX] = R[DX] & 0xFF00
        elif ah == 0x3D:
            name = self.asciiz(c.S[DS], R[DX])
            data = self.file_data(name)
            if data is None:
                R[AX] = 2
                ok = False
            else:
                h = self.next_handle
                self.next_handle += 1
                self.files[h] = [bytearray(data), 0, name]  # writes stay in memory
                R[AX] = h
                self.log.append("open %s -> %d" % (name, h))
        elif ah == 0x3C or ah == 0x41 or ah == 0x56 or ah == 0x5B:
            R[AX] = 5
            ok = False
        elif ah == 0x3E:
            self.files.pop(R[BX], None)
        elif ah == 0x3F:
            h = R[BX]
            if h in self.files:
                f = self.files[h]
                n = min(R[CX], len(f[0]) - f[1])
                a = self.lin(c.S[DS], R[DX])
                m[a:a + n] = f[0][f[1]:f[1] + n]
                f[1] += n
                R[AX] = n
            elif h <= 4:
                R[AX] = 0
            else:
                R[AX] = 6
                ok = False
        elif ah == 0x40:
            h = R[BX]
            a = self.lin(c.S[DS], R[DX])
            if h <= 4:
                self.stdout += m[a:a + R[CX]]
                R[AX] = R[CX]
            elif h in self.files:
                f = self.files[h]
                f[0][f[1]:f[1] + R[CX]] = m[a:a + R[CX]]
                f[1] += R[CX]
                R[AX] = R[CX]
            else:
                R[AX] = 5
                ok = False
        elif ah == 0x42:
            h = R[BX]
            if h not in self.files:
                R[AX] = 6
                ok = False
            else:
                f = self.files[h]
                d = (R[CX] << 16) | R[DX]
                if d & 0x80000000:
                    d -= 1 << 32
                pos = [d, f[1] + d, len(f[0]) + d][al]
                f[1] = pos
                R[DX], R[AX] = pos >> 16, pos & 0xFFFF
        elif ah == 0x44:
            h = R[BX]
            if al == 0x00:
                R[DX] = 0x80D3 if h <= 4 else 0x0002
            elif al == 0x01:
                pass
            else:
                R[AX] = 1
                ok = False
        elif ah == 0x4E:
            pattern = self.asciiz(c.S[DS], R[DX])
            self.find_list = self.match_files(pattern)
            ok = self.find_next()
        elif ah == 0x4F:
            ok = self.find_next()
        elif ah == 0x0B:
            c.s8(0, 0)
        elif ah == 0x06 and (R[DX] & 0xFF) == 0xFF:
            self.ret_zf(True)
            c.s8(0, 0)
        elif ah in (0x02, 0x06):
            self.stdout.append(R[DX] & 0xFF)
        elif ah == 0x09:
            a = self.lin(c.S[DS], R[DX])
            e = m.index(ord("$"), a)
            self.stdout += m[a:e]
        else:
            self.log.append("int 21 ax=%04X unhandled" % ax)
            R[AX] = 1
            ok = False
        self.ret_cf(not ok)

    def match_files(self, pattern):
        import fnmatch
        pat = pattern.replace("/", "\\").split("\\")[-1].upper()
        if pat.endswith(".*"):
            pat = pat[:-2] + "*"  # DOS: NAME.* also matches names without extension
        names = sorted(f for f in os.listdir(self.gamedata) if fnmatch.fnmatchcase(f.upper(), pat)
                       and os.path.isfile(os.path.join(self.gamedata, f)))
        names = sorted(set(names) | {k for k in self.extra_files if fnmatch.fnmatchcase(k, pat)})
        self.log.append("find %s -> %s" % (pattern, names))
        return names

    def find_next(self):
        c = self.cpu
        if not self.find_list:
            c.R[AX] = 0x12
            return False
        name = self.find_list.pop(0)
        size = len(self.file_data(name))
        a = self.lin(*self.dta)
        rec = bytearray(43)
        rec[0x15] = 0x20
        struct.pack_into("<HHI", rec, 0x16, 0, 0x1821, size)
        nm = name.upper().encode()[:12]
        rec[0x1E:0x1E + len(nm)] = nm
        self.mem[a:a + 43] = rec
        c.R[AX] = 0
        return True

    # ---- ports ----
    def port_in(self, port, w):
        if port == 0x3DA:
            # Alternate display enable and vertical retrace so that wait loops end.
            self.retrace = (self.retrace + 1) & 3
            return 0x09 if self.retrace >= 2 else 0x00
        if port == 0x3C9:
            v = self.dac[self.dac_r * 3 + self.dac_sub_r]
            self.dac_sub_r += 1
            if self.dac_sub_r == 3:
                self.dac_sub_r = 0
                self.dac_r = (self.dac_r + 1) & 0xFF
            return v
        if port == 0x3C7:
            return 0
        if port == 0x3C8:
            return self.dac_w
        return self.ports.get(port, 0)

    def port_out(self, port, v, w):
        if w:
            self.port_out(port, v & 0xFF, 0)
            self.port_out(port + 1, (v >> 8) & 0xFF, 0)
            return
        if port == 0x3C8:
            self.dac_w = v
            self.dac_sub_w = 0
        elif port == 0x3C7:
            self.dac_r = v
            self.dac_sub_r = 0
        elif port == 0x3C9:
            self.dac[self.dac_w * 3 + self.dac_sub_w] = v & 0x3F
            self.dac_written.add(self.dac_w)
            self.dac_sub_w += 1
            if self.dac_sub_w == 3:
                self.dac_sub_w = 0
                self.dac_w = (self.dac_w + 1) & 0xFF
        else:
            self.ports[port] = v
