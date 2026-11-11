#!/usr/bin/env python3
"""Saved games (*.TWS) of Tank Wars 3.2: format evidence from BOMB.EXE / BOMB.OVR.

Every statement of docs/formate/spielstand.md is checked here against the original
files in gamedata/. The script derives the file layout from the machine code of the
save routine (O3:3B54) and the load routine (O3:3FE4); it contains no bytes of the
originals, only addresses, sizes and short texts (see tools/reverse-engineering/README.md).
Temporary disassembly goes to analyse/tws/ (protected, git-ignored folder).

Usage:
  tws.py [--gamedata DIR] [--out DIR] befunde          check all claims (exit code != 0 on mismatch)
  tws.py ... aufbau [--computer 1,3]                   print the layout with file offsets
  tws.py ... satz                                      accesses to the 0x74-byte player records
  tws.py ... xref ADDR [ADDR ...]                      accesses to DS variables (hex), all code
  tws.py ... lesen FILE.TWS                            decode a saved game (output: stdout only)
  tws.py selbsttest                                    synthetic round trip of 'lesen' (no game data)
"""
import argparse
import collections
import os
import re
import struct
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

# Runtime routines in Borland System (segment 12CE); each identity is checked in 'befunde' (T1).
SYS = 0x12CE
ASSIGN, RESET, REWRITE, CLOSE = 0x1C74, 0x1CA2, 0x1CAB, 0x1D23
BLOCKREAD, BLOCKWRITE = 0x1D8D, 0x1D94
STR_LOAD, STR_STORE, STR_CONCAT, STR_CMP = 0x0B4D, 0x0B67, 0x0BDA, 0x0C52
FILLCHAR, HALT = 0x1ED4, 0x00E9

# Procedures (overlay O3 = index 3, unit U129 = segment 0129)
SAVE, LOAD, SAVE_DIALOG, LOAD_DIALOG = 0x3B54, 0x3FE4, 0x46DE, 0x4D97
AI_SAVE, AI_LOAD, AI_SET_PLAYER, AI_INIT = 0x00C6, 0x0135, 0x01BF, 0x0000
# Constructors of the seven computer intelligences in U129, in the order of the kind word 1..7
AI_CTORS = [0x050A, 0x06A8, 0x0E9C, 0x1466, 0x1612, 0x2192, 0x1B12]

# Expected layout (own description, not original bytes): (symbol, size, field, type).
# 'i' = player number 1..DS:47DA. Meaning and evidence: docs/formate/spielstand.md.
GLOBALS = [
    ("DS:47D8", 2, "unbelegt (O6)", "Integer"),
    ("DS:47DA", 2, "Spielerzahl", "Integer"),
    ("DS:47E2", 2, "laufendes Spiel", "Integer"),
    ("DS:47E4", 2, "Wind dieser Runde", "Integer"),
    ("DS:47E6", 2, "Spielernummer, Hilfswert der Runde (O4)", "Integer"),
    ("DS:47E8", 2, "unbelegt", "Integer"),
    ("DS:47EA", 2, "Durchgangszähler Runde (O4)", "Integer"),
    ("DS:47EC", 2, "Wände dieser Runde", "Integer"),
    ("DS:47EE", 1, "Spieler am Zug", "Byte"),
    ("DS:4812", 1, "alle Spieler Computer", "Boolean"),
    ("DS:4814", 1, "Wand hält/prallt (Wände 1..3)", "Boolean"),
    ("DS:4815", 1, "Wand Warp (Wände 4)", "Boolean"),
    ("DS:E8A2", 2, "Windwert", "Integer"),
    ("DS:E8A4", 2, "Crumble Percent", "Integer"),
    ("DS:E8A6", 2, "Spiele je Schlacht", "Integer"),
    ("DS:E8A8", 2, "Wind (Option)", "Integer"),
    ("DS:E8AA", 2, "Wände (Option)", "Integer"),
    ("DS:E8AC", 2, "Hintergrund (Option)", "Integer"),
    ("DS:E8AE", 2, "Hintergrundmaske", "Word"),
    ("DS:E8B2", 1, "Fluggeräusch", "Boolean"),
    ("DS:E8B3", 1, "Toneffekte", "Boolean"),
    ("DS:E8B4", 1, "Schuss unsichtbar", "Boolean"),
    ("DS:E8B5", 1, "Spur sichtbar", "Boolean"),
    ("DS:E8B8", 1, "Panzer fallen", "Boolean"),
    ("DS:E8BA", 1, "Computer kaufen", "Boolean"),
    ("DS:E8BC", 1, "Team Play", "Boolean"),
]
PER_PLAYER = [
    ("DS:402E+74h*i", 0x74, "Spielersatz i", "Record"),
    ("DS:E5A6+2*i", 2, "Steuerart i (1 Tastatur, 2 Maus, 3 Computer)", "Integer"),
]
AI_RECORD = [  # only if control = 3; kind word written by AI_SAVE, read by LOAD into a local
    ("Self+00h", 2, "Art der Intelligenz 1..7", "Integer"),
    ("Self+0Bh", 1, "unbelegt", "Byte"),
    ("Self+0Eh", 2, "Zufallswahl beim Anlegen", "Integer"),
    ("Self+10h", 2, "Spielernummer", "Integer"),
]
TAIL = [("DS:4838", 0x247, "Teamtabelle 11 x 53 Byte", "Record[0..10]")]
# Names of the intelligences (DS:007C + 256*k, k = 1..7), compared by O2:0078
AI_NAME_BASE = 0x007C
HEADER_TEXT = b"Tank Wars Saved Game\r\nV3.2\x00\r\n\x1a"   # expected value, checked in T4


class Code:
    """Instruction list of one routine (ndisasm), up to the first RETF."""

    def __init__(self, ofs, mnem, text):
        self.ofs, self.mnem, self.text = ofs, mnem, text


def disasm(data, start, outdir, stop_at_ret=True, end=None):
    os.makedirs(outdir, exist_ok=True)
    with tempfile.NamedTemporaryFile(dir=outdir, suffix=".bin", delete=False) as fh:
        fh.write(data)
        path = fh.name
    try:
        out = subprocess.run(["ndisasm", "-b16", "-e", str(start), "-o", str(start), path],
                             capture_output=True, text=True, check=True).stdout
    finally:
        os.unlink(path)
    res = []
    for line in out.splitlines():
        parts = line.split(None, 2)
        if len(parts) < 3:
            continue
        ofs = int(parts[0], 16)
        if end is not None and ofs >= end:
            break
        ins = Code(ofs, parts[2].split()[0], parts[2])
        res.append(ins)
        if stop_at_ret and ins.mnem == "retf":
            break
    return res


def far_target(ins):
    m = re.match(r"call 0x([0-9a-f]+):0x([0-9a-f]+)$", ins.text)
    return (int(m.group(1), 16), int(m.group(2), 16)) if m else None


def near_target(ins):
    m = re.match(r"call 0x([0-9a-f]+)$", ins.text)
    return int(m.group(1), 16) if m else None


def imm(text, reg):
    m = re.match(r"mov %s,0x([0-9a-f]+)$" % reg, text) or re.match(r"mov %s,(\d+)$" % reg, text)
    if not m:
        return None
    return int(m.group(1), 16) if "0x" in text else int(m.group(1))


def seq_at(code, ofs, texts):
    """True if the instructions starting exactly at 'ofs' have exactly the given texts (fixed-position check)."""
    k = next((n for n, i in enumerate(code) if i.ofs == ofs), None)
    return k is not None and [i.text for i in code[k:k + len(texts)]] == texts


def h(v):
    return "%X" % v


def io_sequence(code):
    """BlockRead/BlockWrite calls and calls of the AI routines with the buffer as a symbol.

    The buffer is the last address loaded into DI before the size; DI forms:
    'mov di,imm' (DS variable), 'mov ax,74h; mul word [x]; mov di,ax; add di,imm' (record i),
    'mov di,[x]; shl di,1; add di,imm' (word array i), 'lea di,[bp-x]' (local),
    'les di,[bp+6]' [+ 'add di,imm'] (Self of an object method)."""
    seq = []
    buf = size = None
    mul74 = shl = False
    for k, ins in enumerate(code):
        t = ins.text
        v = imm(t, "di")
        if v is not None:
            buf, mul74, shl = "DS:%04X" % v, False, False
        elif t.startswith("lea di,[bp"):
            buf = "lokal"
        elif t == "les di,[bp+0x6]":
            buf = "Self+00h"
        elif t == "mov di,ax" and k >= 2 and code[k - 1].text.startswith("mul word") and imm(code[k - 2].text, "ax") == 0x74:
            mul74 = True
        elif t.startswith("mov di,[bp-") and code[k + 1].text == "shl di,1":
            shl = True
        elif t.startswith("add di,0x"):
            a = int(t.split("0x")[1], 16)
            if buf == "Self+00h":
                buf = "Self+%02Xh" % a
            elif mul74:
                buf, mul74 = "DS:%04X+74h*i" % a, False
            elif shl:
                buf, shl = "DS:%04X+2*i" % a, False
        if imm(t, "ax") is not None:
            size = imm(t, "ax")
        if t.startswith("mov al,[bp-"):
            size = "len"
        tgt = far_target(ins)
        if tgt in ((SYS, BLOCKREAD), (SYS, BLOCKWRITE)):
            seq.append(("R" if tgt[1] == BLOCKREAD else "W", buf, size, ins.ofs))
        elif tgt and tgt[0] == 0x0129 and tgt[1] in (AI_SAVE, AI_LOAD):
            seq.append(("AI-W" if tgt[1] == AI_SAVE else "AI-R", buf, None, ins.ofs))
    return seq


def strings_between(code, consts_cs, consts_ds, a, b):
    """Concatenated string constants (StrLoad/StrConcat of cs:/ds: constants) between offsets a and b."""
    out = b""
    last = None
    for k, ins in enumerate(code):
        if not a <= ins.ofs < b:
            continue
        v = imm(ins.text, "di")
        if v is not None and k + 1 < len(code) and code[k + 1].text in ("push cs", "push ds"):
            last = (code[k + 1].text, v)
        tgt = far_target(ins)
        if tgt in ((SYS, STR_LOAD), (SYS, STR_CONCAT)) and last:
            seg, v = last
            src = consts_cs if seg == "push cs" else consts_ds
            out += src[v + 1:v + 1 + src[v]]
            last = None
    return out


def pascal(data, at):
    return data[at + 1:at + 1 + data[at]]


def set_members(data, at):
    return [i for i in range(256) if data[at + i // 8] >> (i % 8) & 1]


class Tws:
    def __init__(self, gamedata, out):
        import bombre
        self.bombre = bombre
        self.g = bombre.Game(gamedata)
        self.out = out
        self.o2 = self.g.overlays[1]
        self.o3 = self.g.overlays[2]
        self.ds = self.g.seg_bytes(0x14D5)
        self.u129 = self.g.seg_bytes(0x0129)
        self.sys = self.g.seg_bytes(SYS)

    def o3_code(self, start):
        return disasm(self.o3["code"], start, self.out)

    def o3_file(self, ofs):
        return "BOMB.OVR 0x%05X" % (self.o3["fileofs"] + ofs)

    def u129_file(self, ofs):
        return "BOMB.EXE 0x%05X" % self.g.exe_off(0x0129, ofs)


def expected_writes():
    seq = [("W", "lokal", "len")]
    seq += [("W", s, n) for s, n, _, _ in GLOBALS]
    seq += [("W", s, n) for s, n, _, _ in PER_PLAYER]
    seq += [("AI-W", "lokal", None)]
    seq += [("W", s, n) for s, n, _, _ in TAIL]
    return seq


def expected_reads():
    seq = [("R", "lokal", "len")]
    seq += [("R", s, n) for s, n, _, _ in GLOBALS]
    seq += [("R", s, n) for s, n, _, _ in PER_PLAYER]
    seq += [("R", "lokal", 2), ("AI-R", "lokal", None)]   # kind word into a local, then the object
    seq += [("R", s, n) for s, n, _, _ in TAIL]
    return seq


def cmd_befunde(t, args):
    ok = True
    results = []

    def check(fid, claim, cond, where):
        nonlocal ok
        ok &= bool(cond)
        results.append((fid, "OK  " if cond else "FAIL", claim, where))

    sysf = lambda ofs: "BOMB.EXE 0x%05X" % t.g.exe_off(SYS, ofs)

    # T1: identity of the runtime routines from their DOS calls
    def rt(ofs, n=40):
        return " ; ".join(i.text for i in disasm(t.sys, ofs, t.out, stop_at_ret=False)[:n])
    a = rt(ASSIGN, 20)
    check("T1a", "System+1C74 = Assign: Dateivariable fmClosed (D7B0h), Name höchstens 79 Zeichen",
          "mov ax,0xd7b0" in a and "cmp al,0x4f" in a, sysf(ASSIGN))
    check("T1b", "System+1CA2 = Reset (INT 21h AH=3Dh, FileMode), System+1CAB = Rewrite (AX=3C00h), Satzgröße als Argument",
          "mov ah,0x3d" in rt(RESET, 4) and "mov ax,0x3c00" in rt(REWRITE, 2) and "mov [es:di+0x4],ax" in rt(REWRITE, 40),
          sysf(RESET))
    check("T1c", "System+1D8D = BlockRead (AH=3Fh), System+1D94 = BlockWrite (AH=40h), Anzahl × Satzgröße, Ergebnis nil "
          "erlaubt; zu wenig übertragen und Ergebnis nil → InOutRes := 100/101",
          rt(BLOCKREAD, 2).startswith("mov bl,0x3f") and rt(BLOCKWRITE, 2).startswith("mov bl,0x40")
          and "mul word [es:di+0x4]" in rt(BLOCKWRITE, 20) and "or dx,di" in rt(BLOCKWRITE, 30)
          and "mov [0x3ad4],cx" in rt(BLOCKWRITE, 40), sysf(BLOCKREAD))
    check("T1d", "System+1D23 = Close (AH=3Eh), System+00E9 = Halt (ExitCode := AX)",
          "mov ah,0x3e" in rt(CLOSE, 10) and "mov [0x3aca],ax" in rt(HALT, 6), sysf(CLOSE))

    # T2: save routine
    w = t.o3_code(SAVE)
    wseq = io_sequence(w)
    calls = [far_target(i) for i in w if far_target(i)]
    check("T2a", "O3:3B54 = Sichern: Assign(f, Name + '.TWS'), Rewrite(f, 1), Close am Ende",
          (SYS, ASSIGN) in calls and (SYS, REWRITE) in calls and calls[-1] == (SYS, CLOSE)
          and pascal(t.o3["code"], 0x3B32) == b".TWS", t.o3_file(SAVE))
    got = [(k, b, n) for k, b, n, _ in wseq]
    check("T2b", "Sichern schreibt %d Blöcke in der Reihenfolge der Tabelle (Kopf, 26 Variablen, je Spieler Satz + Steuerart "
          "[+ Intelligenz], Teamtabelle)" % len(expected_writes()), got == expected_writes(), t.o3_file(wseq[0][3]))
    loop = [i for i in w if i.text in ("mov ax,[0x47da]", "cmp word [di-0x1a5a],byte +0x3")]
    check("T2c", "Schleife i = 1..DS:47DA; Intelligenz nur bei DS:E5A6+2i = 3, Self aus DS:3BE8+4i",
          len(loop) == 2 and any(i.text == "les di,[di+0x3be8]" for i in w), t.o3_file(loop[0].ofs if loop else SAVE))
    check("T2d", "kein E/A-Fehlertest ({$I-}): kein Aufruf zwischen den Blöcken prüft IOResult",
          all(c[0] == SYS and c[1] in (ASSIGN, REWRITE, BLOCKWRITE, CLOSE, STR_LOAD, STR_STORE, STR_CONCAT) or c[0] == 0x0129
              for c in calls), t.o3_file(SAVE))

    # T3: load routine
    r = t.o3_code(LOAD)
    rseq = io_sequence(r)
    rcalls = [far_target(i) for i in r if far_target(i)]
    check("T3a", "O3:3FE4 = Laden: Assign(f, Name + '.TWS'), Reset(f, 1), Close am Ende",
          (SYS, ASSIGN) in rcalls and (SYS, RESET) in rcalls and rcalls[-1] == (SYS, CLOSE)
          and pascal(t.o3["code"], 0x3F99) == b".TWS", t.o3_file(LOAD))
    gotr = [(k, b, n) for k, b, n, _ in rseq]
    check("T3b", "Laden liest dieselben Blöcke in derselben Reihenfolge; die Art der Intelligenz in eine lokale Variable",
          gotr == expected_reads(), t.o3_file(rseq[0][3]))
    fill = [k for k, i in enumerate(r) if far_target(i) == (SYS, FILLCHAR)]
    check("T3c", "Laden leert vorher die Tabelle DS:3BE8 (44 Byte = 11 Zeiger, Spieler 0..10)",
          fill and imm(r[fill[0] - 7].text, "di") == 0x3BE8 and any(imm(i.text, "ax") == 0x2C for i in r[fill[0] - 7:fill[0]]),
          t.o3_file(r[fill[0]].ofs) if fill else t.o3_file(LOAD))
    cmpk = [k for k, i in enumerate(r) if far_target(i) == (SYS, STR_CMP)]
    halt = [i for i in r if far_target(i) == (SYS, HALT)]
    check("T3d", "Kopf ungleich → „Save File is not valid!“, Warten auf Taste, Halt(0) (kein Rückweg ins Menü)",
          cmpk and halt and pascal(t.o3["code"], 0x3FBB) == b"Save File is not valid!", t.o3_file(halt[0].ofs if halt else LOAD))
    kinds = []
    for k, i in enumerate(r):
        tg = far_target(i)
        if tg and tg[0] == 0x0129 and tg[1] in AI_CTORS:
            kinds.append(AI_CTORS.index(tg[1]) + 1)
    cmps = [int(i.text.split(",")[1], 16) for i in r if i.text.startswith("cmp ax,0x") and len(i.text) <= 12]
    check("T3e", "Art 1..7 wählt den Konstruktor (U129:050A, 06A8, 0E9C, 1466, 1612, 2192, 1B12), danach Load U129:0135; "
          "andere Werte: Zeiger bleibt nil, Load wird trotzdem gerufen", kinds == list(range(1, 8)) and cmps == list(range(1, 8)),
          t.o3_file(LOAD))

    # T4: header text
    opened_w = next(i.ofs for i in w if far_target(i) == (SYS, REWRITE))
    opened_r = next(i.ofs for i in r if far_target(i) == (SYS, RESET))
    hdr_w = strings_between(w, t.o3["code"], t.ds, opened_w, wseq[0][3])
    hdr_r = strings_between(r, t.o3["code"], t.ds, opened_r, rseq[0][3])
    check("T4", "Kopf = 'Tank Wars Saved Game' CR LF 'V' + DS:0056 (string[4] '3.2'+NUL) CR LF ^Z, 30 Byte, ohne Längenbyte",
          hdr_w == HEADER_TEXT and hdr_r == HEADER_TEXT and len(HEADER_TEXT) == 30, "BOMB.EXE 0x%05X" % t.g.exe_off(0x14D5, 0x56))

    uses = []
    for name, f, base, ins in all_code(t):
        for k, i in enumerate(ins):
            if i.text == "mov di,0x56" and ins[k + 1].text == "push ds":
                nxt = next((far_target(x) for x in ins[k + 2:k + 10] if far_target(x)), None)
                uses.append(nxt)
    check("T4b", "DS:0056 wird nur gelesen (%d Stellen: Verkettung oder Vergleich, kein Speichern)" % len(uses),
          uses and all(u in ((SYS, STR_CONCAT), (SYS, STR_CMP), (SYS, STR_LOAD)) for u in uses),
          "alle Code-Blöcke")

    # T5: AI object
    sv = disasm(t.u129, AI_SAVE, t.out)
    ld = disasm(t.u129, AI_LOAD, t.out)
    sseq = [(b, n) for _, b, n, _ in io_sequence(sv)]
    lseq = [(b, n) for _, b, n, _ in io_sequence(ld)]
    check("T5a", "U129:00C6 schreibt Self+00h (2), +0Bh (1), +0Eh (2), +10h (2); U129:0135 liest +0Bh, +0Eh, +10h",
          sseq == [(s, n) for s, n, _, _ in AI_RECORD] and lseq == [(s, n) for s, n, _, _ in AI_RECORD[1:]], t.u129_file(AI_SAVE))
    ok_ctor = True
    sets = []
    for k, c in enumerate(AI_CTORS, 1):
        code = disasm(t.u129, c, t.out)
        texts = [i.text for i in code]
        ok_ctor &= "mov word [es:di],0x%x" % k in texts and "call 0x%x" % AI_INIT in texts and "call 0x%x" % AI_SET_PLAYER in texts
        ok_ctor &= "mov di,0x12" in texts   # VMT field at Self+12h
        setofs = [imm(x, "di") for x in texts if imm(x, "di") not in (None, 0x12)]
        sets.append(set_members(t.u129, setofs[0]) if setofs else None)
    check("T5b", "Konstruktor k: Init (U129:0000) mit Menge, Spielernummer (U129:01BF → +10h), Self+00h := k; VMT-Feld +12h",
          ok_ctor, t.u129_file(AI_CTORS[0]))
    init = [i.text for i in disasm(t.u129, AI_INIT, t.out)]
    check("T5c", "Init: +02h, +03h, +05h, +07h, +09h, +0Bh := 0, +0Ch := -1, +0Eh := Random(4)+1, wiederholt bis in der "
          "Menge (Mengen: Art 3 und 6 {1..4}, sonst {1,2,4})",
          "call 0x12ce:0x1675" in init and "mov [es:di+0xe],ax" in init
          and {"mov byte [es:di+0x2],0x0", "mov [es:di+0x3],ax", "mov [es:di+0x5],ax", "mov [es:di+0x7],ax",
               "mov [es:di+0x9],ax", "mov byte [es:di+0xb],0x0", "mov word [es:di+0xc],0xffff"} <= set(init)
          and sets == [[1, 2, 4], [1, 2, 4], [1, 2, 3, 4], [1, 2, 4], [1, 2, 4], [1, 2, 3, 4], [1, 2, 4]], t.u129_file(AI_INIT))
    names = [pascal(t.ds, AI_NAME_BASE + 256 * k).decode("ascii") for k in range(1, 8)]
    o2c = disasm(t.o2["code"], 0x0078, t.out)
    pairs = []
    cur = None
    for i in o2c:
        v = imm(i.text, "di")
        if v is not None and (v - AI_NAME_BASE) % 256 == 0 and v > AI_NAME_BASE:
            cur = (v - AI_NAME_BASE) // 256
        tg = far_target(i)
        if tg and tg[0] == 0x0129 and tg[1] in AI_CTORS and cur:
            pairs.append((cur, AI_CTORS.index(tg[1]) + 1))
    check("T5d", "Art k = Name DS:007C+256k: %s (O2:0078 vergleicht Satz +01h mit dem Namen)" % ", ".join(names),
          pairs == [(k, k) for k in range(1, 8)], "BOMB.OVR 0x%05X" % (t.o2["fileofs"] + 0x78))

    # T6: callers and when the game is saved or loaded
    o3all = disasm(t.o3["code"], 0x7900, t.out, stop_at_ret=False, end=0x79B2)
    seqcall = [(near_target(i), i.ofs) for i in o3all if near_target(i)]
    check("T6a", "nach jedem Spiel (O3:796B): DS:4817 := 0; bei Autosave (DS:4818) Sichern unter DS:4822, dann Wertung; "
          "auf Wunsch (DS:4816) Dialog O3:46DE nach Einzel- und Teamwertung; Einkauf O3:0E1F nur bei DS:47E2 ≠ DS:E8A6",
          [c for c, _ in seqcall if c in (SAVE, SAVE_DIALOG, 0x5AEF, 0x6696)] == [SAVE, 0x5AEF, SAVE_DIALOG, 0x6696, SAVE_DIALOG]
          and [i.text for i in o3all][-5:] == ["mov ax,[0x47e2]", "cmp ax,[0xe8a6]", "jz 0x79b2", "push cs", "call 0xe1f"]
          and "mov byte [0x4817],0x0" in [i.text for i in o3all] and "cmp byte [0x4818],0x0" in [i.text for i in o3all],
          t.o3_file(0x796B))
    dlg = disasm(t.o3["code"], SAVE_DIALOG, t.out)
    keys = [i.text for i in dlg if i.text.startswith("cmp byte [0x47ef],0x")]
    check("T6b", "Dialog: S Sichern, N neuer Name, C Abbruch, A Autosave umschalten, Q Sichern und Ende; Name String[20], "
          "Vorschlag GAMEn mit erstem freien n",
          [chr(int(k.split(",0x")[1], 16)) for k in keys] == list("QACNS")
          and pascal(t.o3["code"], 0x4651) == b"GAME" and pascal(t.o3["code"], 0x465B) == b"SAVE GAME"
          # key -> choice at fixed places: Q = 5 (O3:4AC9), S = 1 (O3:4AF9)
          and seq_at(dlg, 0x4AC2, ["cmp byte [0x47ef],0x51", "jnz 0x4ace", "mov word [bp-0x2],0x5"])
          and seq_at(dlg, 0x4AF2, ["cmp byte [0x47ef],0x53", "jnz 0x4afe", "mov word [bp-0x2],0x1"])
          # choice 1 (S): save under DS:4822; choice 5 (Q): save under DS:4822, then Halt(0)
          and seq_at(dlg, 0x4CBA, ["cmp word [bp-0x2],byte +0x1", "jnz 0x4cc9", "mov di,0x4822", "push ds", "push di",
                                   "push cs", "call 0x%x" % SAVE])
          and seq_at(dlg, 0x4CC9, ["cmp word [bp-0x2],byte +0x5", "jnz 0x4ce8", "mov di,0x4822", "push ds", "push di",
                                   "push cs", "call 0x%x" % SAVE, "mov ax,0x3", "push ax", "call 0x11f8:0x16b",
                                   "xor ax,ax", "call 0x%x:0x%x" % (SYS, HALT)])
          # name DS:4822 is String[20]: StrStore with maximum length 20 (O3:476E)
          and seq_at(dlg, 0x4769, ["mov di,0x4822", "push ds", "push di", "mov ax,0x14", "push ax",
                                   "call 0x%x:0x%x" % (SYS, STR_STORE)]), t.o3_file(SAVE_DIALOG))
    boss = disasm(t.g.seg_bytes(0x0C02), 0x01F9, t.out, stop_at_ret=False, end=0x02B0)
    bcalls = [far_target(i) for i in boss if far_target(i)]
    check("T6c", "Boss-Taste (UC02): Sichern unter <Verzeichnis>\\BOSS, dann Halt(0)",
          bcalls.count((0x0B08, 0x2F)) == 2 and pascal(t.g.seg_bytes(0x0C02), 0x0004) == b"\\BOSS",
          "BOMB.EXE 0x%05X" % t.g.exe_off(0x0C02, 0x0225))
    ld_dlg = disasm(t.o3["code"], LOAD_DIALOG, t.out)
    menu = [i.text for i in disasm(t.o2["code"], 0x3F03, t.out, stop_at_ret=False, end=0x3F2C)]
    check("T6d", "Laden nur aus dem Hauptmenü: bei DS:4817 (Load Game) O3[2] = O3:4D97 (FindFirst '*.TWS', Namen String[10]) "
          "→ O3:3FE4, danach ohne Spielerauswahl und Teams; keine Datei → „No Save Files Found“, Halt(0)",
          any(near_target(i) == LOAD for i in ld_dlg) and pascal(t.o3["code"], 0x4D4C) == b"*.TWS"
          and pascal(t.o3["code"], 0x4D54) == b"No Save Files Found" and any(far_target(i) == (SYS, HALT) for i in ld_dlg)
          and menu[:3] == ["cmp byte [0x4817],0x0", "jz 0x3f0f", "call 0xb08:0x2a"] and "call 0x52dd" in menu
          and "call 0x2749" in menu
          # name list: element i at bp-0x592+11*i, StrStore with maximum length 10 (String[10], O3:4E1D..4E30)
          and seq_at(ld_dlg, 0x4E1D, ["mov ax,0xb", "mul word [bp-0x594]", "mov di,ax", "lea di,[bp+di-0x592]", "push ss",
                                      "push di", "mov ax,0xa", "push ax", "call 0x%x:0x%x" % (SYS, STR_STORE)]),
          t.o3_file(LOAD_DIALOG))

    # T7: meaning of the global variables from other code
    o4 = t.g.overlays[3]["code"]
    rnd = [i.text for i in disasm(o4, 0x44CC, t.out, stop_at_ret=False, end=0x45F1)]
    check("T7a", "Rundenstart O4:44CC: DS:47E4 := Wind-Option (DS:E8A8), bei 5 Random(5); DS:E8A2 := Windwert, "
          "begrenzt auf ±30000", "mov [0x47e4],ax" in rnd and "mov [0xe8a2],ax" in rnd and "mov word [0xe8a2],0x7530" in rnd
          and "mov word [0xe8a2],0x8ad0" in rnd, "BOMB.OVR 0x%05X" % (t.g.overlays[3]["fileofs"] + 0x44CC))
    o8 = t.g.overlays[7]
    wl = [i.text for i in disasm(o8["code"], 0x09AA, t.out, stop_at_ret=False, end=0x09EF)]
    check("T7b", "O8:09AA: DS:47EC := Wände-Option, bei 5 Random(5); DS:4814 := Wände in {1,2,3}; DS:4815 := Wände = 4",
          "mov [0x47ec],ax" in wl and "mov byte [0x4814],0x1" in wl and "mov byte [0x4815],0x1" in wl
          and set_members(o8["code"], 0x0904) == [1, 2, 3], "BOMB.OVR 0x%05X" % (o8["fileofs"] + 0x09AA))
    tn = [i.text for i in disasm(o4, 0x4766, t.out, stop_at_ret=False, end=0x479A)]
    check("T7c", "O4:476A: DS:47EA := 1, +1 bei jedem Umlauf über Spieler DS:47DA; DS:47EE := Spieler am Zug (Satz +23h ≠ 0)",
          "mov word [0x47ea],0x1" in tn and "inc word [0x47ea]" in tn and "mov [0x47ee],al" in tn
          and "cmp byte [di+0x4051],0x0" in tn, "BOMB.OVR 0x%05X" % (t.g.overlays[3]["fileofs"] + 0x476A))
    hv = [i.text for i in disasm(o4, 0x0A49, t.out, stop_at_ret=False, end=0x0A6C)]
    check("T7f", "O4:0A51: DS:47E6 := Satz +28h des Spielers, danach Index eines Spielersatzes (mul 74h)",
          "mov ax,[di+0x4056]" in hv and "mov [0x47e6],ax" in hv and "mul word [0x47e6]" in hv,
          "BOMB.OVR 0x%05X" % (t.g.overlays[3]["fileofs"] + 0x0A51))
    cfg = disasm(t.o2["code"], 0x6611, t.out)
    cseq = [b for k, b, n, _ in io_sequence(cfg) if k == "R"]
    opts = [s for s, _, _, _ in GLOBALS if s.startswith("DS:E8") and s != "DS:E8A2"]
    check("T7d", "die Optionen DS:E8A4..E8BC sind dieselben Variablen, die O2:6611 aus BOMB.CFG liest (Bedeutung: konfiguration.md)",
          all(s in cseq for s in opts) and "DS:E8A2" not in cseq, "BOMB.OVR 0x%05X" % (t.o2["fileofs"] + 0x6611))
    check("T7e", "Teamtabelle DS:4838: 583 Byte = 11 Sätze zu 53 Byte (Team 0..10)", TAIL[0][1] == 11 * 53, t.o3_file(0x3F70))

    for fid, st, claim, where in results:
        print("%-4s %s %s  [%s]" % (fid, st, claim, where))
    print("Ergebnis: %d von %d Befunden bestätigt" % (sum(r[1] == "OK  " for r in results), len(results)))
    return 0 if ok else 1


def layout_rows(computers, players):
    """(offset, size, symbol, field, type) for a file with the given players; computers = set of player numbers."""
    rows = []
    pos = 0

    def add(sym, n, field, typ):
        nonlocal pos
        rows.append((pos, n, sym, field, typ))
        pos += n
    add("kopf", len(HEADER_TEXT), "Kopfzeile", "Text")
    for s, n, f, ty in GLOBALS:
        add(s, n, f, ty)
    for i in range(1, players + 1):
        for s, n, f, ty in PER_PLAYER:
            add(s.replace("*i", "*%d" % i), n, f.replace(" i", " %d" % i), ty)
        if i in computers:
            for s, n, f, ty in AI_RECORD:
                add("Intelligenz %d %s" % (i, s), n, f, ty)
    for s, n, f, ty in TAIL:
        add(s, n, f, ty)
    return rows, pos


def size_of(players, computers):
    g = len(HEADER_TEXT) + sum(n for _, n, _, _ in GLOBALS)
    return g + players * sum(n for _, n, _, _ in PER_PLAYER) + computers * sum(n for _, n, _, _ in AI_RECORD) + TAIL[0][1]


def cmd_aufbau(args):
    comp = {int(x) for x in args.computer.split(",") if x} if args.computer else set()
    rows, total = layout_rows(comp, args.spieler)
    print("Offset  Byte  Quelle                       Feld")
    for o, n, s, f, ty in rows:
        print("%6d  %4d  %-28s %s (%s)" % (o, n, s, f, ty))
    print("Länge: %d Byte = 30 + %d + %d × 118 + %d × 7 + 583" % (total, sum(n for _, n, _, _ in GLOBALS), args.spieler, len(comp)))
    return 0


def parse(data):
    """Decode a saved game; returns (fields, error). Fields: list of (offset, symbol, field, value)."""
    res = []
    if data[:30] != HEADER_TEXT:
        return res, "Kopf ungleich – das Original meldet „Save File is not valid!“ und beendet sich"
    pos = 30
    vals = {}

    def take(n, typ):
        nonlocal pos
        if pos + n > len(data):
            raise EOFError(pos)
        raw = data[pos:pos + n]
        pos += n
        if typ in ("Integer",):
            return struct.unpack("<h", raw)[0]
        if typ == "Word":
            return struct.unpack("<H", raw)[0]
        if n == 1:
            return raw[0]
        return raw
    try:
        res.append((0, "kopf", "Kopfzeile", HEADER_TEXT.decode("latin-1").encode("unicode_escape").decode()))
        for s, n, f, ty in GLOBALS:
            o = pos
            v = take(n, ty)
            vals[s] = v
            res.append((o, s, f, v))
        for i in range(1, vals["DS:47DA"] + 1):
            o = pos
            rec = take(0x74, "Record")
            name = rec[2:2 + min(rec[1], 13)].decode("latin-1")
            res.append((o, "DS:402E+74h*%d" % i, "Spielersatz %d" % i,
                        "Nr %d, Name %r, Punkte %d, ausgegeben %d" % (rec[0], name, *struct.unpack("<ii", rec[0x13:0x1B]))))
            o = pos
            ctrl = take(2, "Integer")
            res.append((o, "DS:E5A6+2*%d" % i, "Steuerart %d" % i, ctrl))
            if ctrl == 3:
                for s, n, f, ty in AI_RECORD:
                    o = pos
                    res.append((o, "Intelligenz %d %s" % (i, s), f, take(n, ty)))
        o = pos
        take(TAIL[0][1], "Record")
        res.append((o, "DS:4838", TAIL[0][2], "%d Byte" % TAIL[0][1]))
    except EOFError as e:
        return res, "Datei endet bei Offset %d (das Original liest ohne Prüfung weiter, InOutRes = 100)" % e.args[0]
    if pos != len(data):
        return res, "%d Byte hinter dem letzten Feld (das Original liest sie nicht)" % (len(data) - pos)
    return res, None


def cmd_lesen(args):
    with open(args.datei, "rb") as fh:
        data = fh.read()
    fields, err = parse(data)
    for o, s, f, v in fields:
        print("%6d  %-28s %-40s %s" % (o, s, f, v))
    print("Länge: %d Byte" % len(data))
    if err:
        print("Hinweis: " + err)
    return 1 if err and err.startswith("Kopf") else 0


def synthetic(players, computers):
    """Synthetic saved game built from the layout (no original data)."""
    out = bytearray(HEADER_TEXT)
    for s, n, _, _ in GLOBALS:
        v = players if s == "DS:47DA" else 0
        out += struct.pack("<h", v) if n == 2 else bytes([v])
    for i in range(1, players + 1):
        rec = bytearray(0x74)
        rec[0] = i
        name = ("P%d" % i).encode()
        rec[1] = len(name)
        rec[2:2 + len(name)] = name
        rec[0x13:0x17] = struct.pack("<i", 1000 * i)
        out += rec
        out += struct.pack("<h", 3 if i in computers else 1)
        if i in computers:
            out += struct.pack("<hBhh", 2, 0, 1, i)
    out += bytes(0x247)
    return bytes(out)


def cmd_selbsttest(args):
    ok = True
    for players, comp in ((2, set()), (3, {2}), (10, set(range(1, 11)))):
        data = synthetic(players, comp)
        fields, err = parse(data)
        good = err is None and len(data) == size_of(players, len(comp)) and len(data) == layout_rows(comp, players)[1]
        good &= sum(1 for f in fields if f[2].startswith("Spielersatz")) == players
        ok &= good
        print("%s %d Spieler, %d Computer: %d Byte" % ("OK  " if good else "FAIL", players, len(comp), len(data)))
    fields, err = parse(synthetic(2, set())[:-1])
    ok &= err is not None and "endet" in err
    fields, err = parse(b"x" + synthetic(2, set())[1:])
    ok &= err is not None and err.startswith("Kopf")
    print("Ergebnis: %s" % ("bestanden" if ok else "fehlgeschlagen"))
    return 0 if ok else 1


def all_code(t):
    """(block name, file, file offset base, instructions) for every code block, string constants skipped."""
    for name, data, f, base in t.g.code_blocks():
        skips = []
        for o, s in t.bombre.pascal_strings(data, 4):
            skips += ["-k", "%d,%d" % (o, len(s) + 1)]
        os.makedirs(t.out, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=t.out, suffix=".bin", delete=False) as fh:
            fh.write(data)
            path = fh.name
        try:
            out = subprocess.run(["ndisasm", "-b16"] + skips + [path], capture_output=True, text=True, check=True).stdout
        finally:
            os.unlink(path)
        ins = []
        for line in out.splitlines():
            p = line.split(None, 2)
            if len(p) == 3 and not p[2].startswith("skipping"):
                ins.append(Code(int(p[0], 16), p[2].split()[0], p[2]))
        yield name, f, base, ins


def cmd_satz(t, args):
    """Accesses [di+402Eh+k] behind 'mul' by 74h: offset k within the player record, operand width, sites."""
    acc = collections.defaultdict(list)
    for name, f, base, ins in all_code(t):
        for k, i in enumerate(ins):
            m = re.search(r"(byte |word |dword )?\[(?:di|bx|si)\+0x(40[0-9a-f]{2})\]", i.text)
            if not m:
                continue
            a = int(m.group(2), 16) - 0x402E
            if not 0 <= a < 0x74:
                continue
            back = " ".join(x.text for x in ins[max(0, k - 6):k])
            if "0x74" not in back or "mul" not in back:
                continue
            width = m.group(1).strip() if m.group(1) else ("byte" if re.search(r"\b(al|ah|bl|cl|dl)\b", i.text) else "word")
            acc[a].append((width, "%s:%04X" % (name, i.ofs)))
    print("Satz+  Breite        Stellen  Beispiele")
    for a in sorted(acc):
        widths = sorted({w for w, _ in acc[a]})
        print("%02Xh   %-12s %7d  %s" % (a, "/".join(widths), len(acc[a]), ", ".join(s for _, s in acc[a][:3])))
    return 0


def cmd_xref(t, args):
    want = {int(a, 16) for a in args.adressen}
    hits = collections.defaultdict(list)
    for name, f, base, ins in all_code(t):
        for i in ins:
            for m in re.finditer(r"\[0x([0-9a-f]{3,4})\]", i.text):
                a = int(m.group(1), 16)
                if a in want:
                    hits[a].append("%s:%04X %s" % (name, i.ofs, i.text))
    for a in sorted(want):
        print("DS:%04X: %d Stellen" % (a, len(hits[a])))
        for x in hits[a]:
            print("  " + x)
    return 0


def main():
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--gamedata", default=os.path.join(root, "gamedata"))
    ap.add_argument("--out", default=os.path.join(root, "analyse", "tws"))
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("befunde")
    p = sub.add_parser("aufbau")
    p.add_argument("--spieler", type=int, default=2)
    p.add_argument("--computer", default="")
    sub.add_parser("satz")
    p = sub.add_parser("xref")
    p.add_argument("adressen", nargs="+")
    p = sub.add_parser("lesen")
    p.add_argument("datei")
    sub.add_parser("selbsttest")
    args = ap.parse_args()
    if args.cmd == "aufbau":
        return cmd_aufbau(args)
    if args.cmd == "lesen":
        return cmd_lesen(args)
    if args.cmd == "selbsttest":
        return cmd_selbsttest(args)
    t = Tws(args.gamedata, args.out)
    return {"befunde": cmd_befunde, "satz": cmd_satz, "xref": cmd_xref}[args.cmd](t, args)


if __name__ == "__main__":
    sys.exit(main())
