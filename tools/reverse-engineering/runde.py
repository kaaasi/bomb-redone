#!/usr/bin/env python3
"""Reverse-engineering helpers for the round, playfield and shot code of Tank Wars 3.2 (spur re-runde).

Builds on bombre.py (load image, overlays, listings). Writes derived material only to analyse/re-runde/
(protected, git-ignored folder; stored in the reference repository by pull request, never in this repository).

Usage:
  runde.py [--gamedata DIR] [--out DIR] procs|randoms|reals|listings|befunde|all

  procs     procedure table of the round blocks (O3-O9, 0129, 0431, 0CC8) with callers and callees
  randoms   every call of System.Random with its argument, per procedure, in code order
  reals     every Real48 immediate (DX:BX:AX loaded before a Real routine) decoded
  listings  annotated listings: Real48 values, near-call targets, DS variable names, Graph names
  befunde   checks the claims of docs/analyse/runde.md (exit code != 0 on mismatch)

Like bombre.py this script contains no bytes of the originals: fixed places are checked by
SHA-256 of a region and by instruction patterns built from targets and operands.
"""
import argparse
import collections
import os
import re
import shutil
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bombre  # noqa: E402

# Borland Graph entry points (segment 0E26). [APPROX] Names follow from the argument size (retf n) and the
# argument patterns at the call sites, not from a symbol table; each one used in docs/analyse/runde.md is checked.
GRAPH = {
    0x1DC2: "Graph.SetColor", 0x1F12: "Graph.OutTextXY", 0x13F2: "Graph.SetFillStyle", 0x1454: "Graph.SetTextJustify",
    0x1FE4: "Graph.PutPixel", 0x1F9B: "Graph.GetPixel", 0x1C86: "Graph.Line?", 0x1CCE: "Graph.Bar?", 0x1496: "Graph.SetTextStyle",
    0x0F93: "Graph.SetViewPort", 0x1FFB: "Graph.GetImage", 0x131D: "Graph.PutImage", 0x1042: "Graph.MoveTo?",
}

# Data segment variables named in docs/analyse/runde.md (DS:offset -> name). Each name is the claim of a finding.
DSVARS = {
    0x3AD6: "RandSeed", 0xE8C4: "opt.TS", 0xE8C8: "opt.FS", 0xE8C6: "opt.ES", 0xE8CA: "opt.SS", 0xE8CC: "opt.LS",
    0xE8F6: "SetRGB(ptr)", 0xE8F2: "SetPal(ptr)", 0xE904: "VideoSeg", 0xE8C2: "opt.PaletteSpeed",
    0x47DA: "NumPlayers", 0x47E2: "GameNo", 0x47E4: "WindThisRound", 0x47E6: "Tmp47E6", 0x47EA: "PassCounter",
    0x47EC: "WallsThisRound", 0x47EE: "PlayerToMove", 0x4814: "WallsSEA", 0x4815: "WallsWarp",
    0xE8A2: "WindValue", 0xE8A4: "opt.Crumble", 0xE8A6: "opt.GamesToPlay", 0xE8A8: "opt.Wind", 0xE8AA: "opt.Walls",
    0xE8AC: "opt.Background", 0xE8AE: "opt.BackMask", 0xE8B2: "opt.FlySound", 0xE8B3: "opt.Sound", 0xE8B4: "opt.Inviso",
    0xE8B5: "opt.Trail", 0xE8B8: "opt.FallDown", 0xE8BA: "opt.CompuBuy", 0xE8BC: "opt.TeamPlay", 0xE5A6: "Control[i]",
    0x402E: "PlayerRec[0]",
}

BLOCKS = ("ovl_O1_", "ovl_O2_", "ovl_O3", "ovl_O4", "ovl_O5", "ovl_O6", "ovl_O7", "ovl_O8", "ovl_O9", "ovl_O10", "seg_0129", "seg_0431", "seg_0CC8",
          "seg_0000", "seg_0059", "seg_0B25", "seg_0C02", "seg_0C48", "seg_0C79", "seg_0DBD", "seg_0E11")

REAL_CALLS = {(0x12CE, o): n for (s, o), n in bombre.NAMES.items() if s == 0x12CE and n.startswith("System.@Real")}


def real48(ax, bx, dx):
    """Decode a Turbo Pascal Real48 held in DX:BX:AX (AL = exponent, bias 129; DX bit 15 = sign)."""
    e = ax & 0xFF
    if e == 0:
        return 0.0
    mant = ((dx & 0x7FFF) << 24) | (bx << 8) | (ax >> 8)
    v = (1 + mant / float(1 << 39)) * 2.0 ** (e - 129)
    return -v if dx & 0x8000 else v


def fmt_real(v):
    return ("%.10g" % v)


def blocks(g):
    for name, data, f, base in g.code_blocks():
        if name.startswith(BLOCKS):
            yield name, data, f, base


def short(name):
    """ovl_O8_0B1A -> O8, seg_0129 -> 0129."""
    return name.split("_")[1] if name.startswith("ovl") else name[4:]


def disasm(data, tmpdir, name):
    """ndisasm output as list of (offset, bytes hex, text); Pascal string constants are skipped."""
    binp = os.path.join(tmpdir, name + ".bin")
    with open(binp, "wb") as fh:
        fh.write(data)
    skips = []
    for o, s in bombre.pascal_strings(data, 4):
        skips += ["-k", "%d,%d" % (o, len(s) + 1)]
    syncs = []
    for o in sorted(set(bombre.proc_starts(data))):
        syncs += ["-s", "%d" % o]
    out = subprocess.run(["ndisasm", "-b16"] + skips + syncs + [binp], capture_output=True, text=True, check=True).stdout
    res = []
    for line in out.splitlines():
        m = re.match(r"([0-9A-F]{8})  (\S+)\s+(.*)", line)
        if m:
            res.append((int(m.group(1), 16), m.group(2), m.group(3)))
        else:
            res.append((None, "", line))
    return res


_DIS = {}


def dis(g, tmp):
    if not _DIS:
        os.makedirs(tmp, exist_ok=True)
        for name, data, f, base in blocks(g):
            _DIS[name] = (disasm(data, tmp, name), data, f, base)
    return _DIS


def far_target(text):
    m = re.search(r"call 0x([0-9a-f]+):0x([0-9a-f]+)", text)
    return (int(m.group(1), 16), int(m.group(2), 16)) if m else None


def stub_name(g, k):
    for i, o in enumerate(g.overlays, 1):
        if o["stub"] == k[0] and (k[1] - 0x20) % 5 == 0 and 0 <= (k[1] - 0x20) // 5 < len(o["entries"]):
            e = (k[1] - 0x20) // 5
            return "O%d[%d]=O%d:%04X" % (i, e, i, o["entries"][e])
    return None


def target_name(g, k):
    if k in bombre.NAMES:
        return bombre.NAMES[k]
    if k[0] == 0x0E26 and k[1] in GRAPH:
        return GRAPH[k[1]]
    s = stub_name(g, k)
    if s:
        return s
    return "%04X:%04X" % k


def proc_table(g, tmp):
    """Per block: procedure starts (push bp; mov bp,sp) and, per procedure, calls in code order."""
    res = {}
    for name, (lines, data, f, base) in dis(g, tmp).items():
        starts = sorted(set(bombre.proc_starts(data)))
        procs = collections.OrderedDict((s, dict(calls=[], near=[])) for s in starts)

        def owner(ofs):
            p = None
            for s in starts:
                if s <= ofs:
                    p = s
                else:
                    break
            return p
        for ofs, hexb, text in lines:
            if ofs is None:
                continue
            p = owner(ofs)
            if p is None:
                continue
            k = far_target(text)
            if k:
                procs[p]["calls"].append((ofs, target_name(g, k)))
            m = re.match(r"call 0x([0-9a-f]+)$", text)
            if m:
                t = int(m.group(1), 16)
                procs[p]["near"].append((ofs, t))
                procs[p]["calls"].append((ofs, "%s:%04X" % (short(name), t)))
        res[name] = (procs, f, base)
    return res


def cmd_procs(g, args):
    tab = proc_table(g, args.tmp)
    callers = collections.defaultdict(list)
    for name, (procs, f, base) in tab.items():
        for p, d in procs.items():
            for ofs, t in d["calls"]:
                callers[t].append("%s:%04X" % (short(name), p))
    out = ["block\tproc\tdatei\tdateioffset\taufrufer\taufrufe"]
    for name, (procs, f, base) in tab.items():
        for p, d in procs.items():
            key = "%s:%04X" % (short(name), p)
            alias = [key]
            for i, o in enumerate(g.overlays, 1):
                if short(name) == "O%d" % i and p in o["entries"]:
                    alias.append("O%d[%d]=%s" % (i, o["entries"].index(p), key))
            cl = sorted(set(sum((callers[a] for a in alias), [])))
            calls = [t for _, t in d["calls"] if not t.startswith(("Graph.", "System.@Real"))]
            out.append("%s\t%04X\t%s\t0x%05X\t%s\t%s" % (short(name), p, f, base + p, " ".join(cl), " ".join(calls)))
    write(args, "prozeduren.tsv", out)


def write(args, fname, lines):
    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, fname), "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")
    print("%s: %d Zeilen" % (os.path.join(args.out, fname), len(lines)))


def random_calls(g, tmp):
    """(block, proc, offset, argument text) for every Random(Word) call; argument = last push before the call."""
    tab = proc_table(g, tmp)
    res = []
    for name, (lines, data, f, base) in dis(g, tmp).items():
        procs = tab[name][0]
        starts = list(procs)
        prev = []
        for ofs, hexb, text in lines:
            if ofs is None:
                continue
            k = far_target(text)
            if k == (0x12CE, 0x1675) or k == (0x12CE, 0x168B):
                arg = "-"
                if k == (0x12CE, 0x1675):
                    for t in reversed(prev[-4:]):
                        m = re.match(r"mov ax,(0x[0-9a-f]+|\[.*\])$", t)
                        if m:
                            arg = m.group(1)
                            break
                        m = re.match(r"push word (.*)$", t)
                        if m:
                            arg = m.group(1)
                            break
                p = max([s for s in starts if s <= ofs], default=None)
                res.append((short(name), p, ofs, "Random(%s)" % arg if k[1] == 0x1675 else "Random:Real", f, base + ofs))
            prev.append(text)
    return res


def cmd_randoms(g, args):
    out = ["block\tproc\toffset\tdatei\tdateioffset\taufruf"]
    for b, p, o, call, f, fo in random_calls(g, args.tmp):
        out.append("%s\t%04X\t%04X\t%s\t0x%05X\t%s" % (b, p if p is not None else 0, o, f, fo, call))
    write(args, "random.tsv", out)


IMM = re.compile(r"(?:mov (ax|bx|dx|cx|si|di),0x([0-9a-f]+)|xor (ax|bx|dx|cx|si|di),\3)$")
STORE = re.compile(r"mov word \[0x([0-9a-f]+)\],0x([0-9a-f]+)$")


def real_immediates(lines):
    """Yield (offset, value) for Real48 immediates.

    Turbo Pascal 6 ($N-) loads the first operand into DX:BX:AX and the second into DI:SI:CX by immediate moves
    (or xor for zero words); typed variables get three word stores to consecutive addresses."""
    regs = {}
    stores = []
    for ofs, hexb, text in lines:
        if ofs is None:
            regs, stores = {}, []
            continue
        m = IMM.match(text)
        if m:
            r = m.group(1) or m.group(3)
            v = int(m.group(2), 16) if m.group(1) else 0
            regs[r] = (ofs, v)
            for lo, mid, hi in (("ax", "bx", "dx"), ("cx", "si", "di")):
                if r in (lo, mid, hi) and all(x in regs for x in (lo, mid, hi)):
                    first = min(regs[x][0] for x in (lo, mid, hi))
                    yield first, real48(regs[lo][1], regs[mid][1], regs[hi][1])
                    for x in (lo, mid, hi):
                        del regs[x]
            stores = []
            continue
        regs = {}
        m = STORE.match(text)
        if m:
            stores.append((ofs, int(m.group(1), 16), int(m.group(2), 16)))
            stores = stores[-3:]
            if len(stores) == 3 and [a for _, a, _ in stores] == [stores[0][1] + 2 * i for i in range(3)]:
                yield stores[0][0], real48(stores[0][2], stores[1][2], stores[2][2])
                stores = []
        else:
            stores = []


def cmd_reals(g, args):
    out = ["block\toffset\tdatei\tdateioffset\twert"]
    for name, (lines, data, f, base) in dis(g, args.tmp).items():
        for ofs, v in real_immediates(lines):
            out.append("%s\t%04X\t%s\t0x%05X\t%s" % (short(name), ofs, f, base + ofs, fmt_real(v)))
    write(args, "real48.tsv", out)


def cmd_listings(g, args):
    d = os.path.join(args.out, "listings")
    os.makedirs(d, exist_ok=True)
    tab = proc_table(g, args.tmp)
    for name, (lines, data, f, base) in dis(g, args.tmp).items():
        reals = dict(real_immediates(lines))
        starts = set(tab[name][0])
        body = ["; Tank Wars 3.2 - %s, Dateioffset von Offset 0: %s 0x%05X" % (name, f, base),
                "; Erzeugt mit tools/reverse-engineering/runde.py listings (ndisasm). Abgeleitet aus Originalcode:",
                "; nur im Referenz-Repo (analyse/re-runde/) ablegen, nie im Code-Repo.",
                "; Anmerkungen: Aufrufziele, Real48-Werte (R=), DS-Variablen aus docs/analyse/runde.md, Prozeduranfänge."]
        for ofs, hexb, text in lines:
            if ofs is None:
                body.append(text)
                continue
            note = []
            if ofs in starts:
                body.append(";---- proc %s:%04X (%s 0x%05X)" % (short(name), ofs, f, base + ofs))
            k = far_target(text)
            if k:
                note.append(target_name(g, k))
            m = re.match(r"call 0x([0-9a-f]+)$", text)
            if m:
                note.append("%s:%04X" % (short(name), int(m.group(1), 16)))
            if ofs in reals:
                note.append("R=%s" % fmt_real(reals[ofs]))
            for mm in re.finditer(r"\[(?:[a-z]{2}:)?0x([0-9a-f]{4})\]", text):
                v = int(mm.group(1), 16)
                if v in DSVARS and not text.startswith(("call", "jmp")) or (v in DSVARS and "far" in text):
                    note.append(DSVARS[v])
            line = "%08X  %-20s %s" % (ofs, hexb, text)
            body.append(line + ("  ; " + "; ".join(note) if note else ""))
        with open(os.path.join(d, name + ".asm"), "w", encoding="ascii", errors="replace") as fh:
            fh.write("\n".join(body) + "\n")
    print("%d Listings in %s" % (len(_DIS), d))


def block_lines(g, args, key):
    """Disassembly lines of block key ('O8', '0129', ...)."""
    for name, val in dis(g, args.tmp).items():
        if short(name) == key:
            return val
    raise KeyError(key)


def seq(g, args, key, start, end, patterns):
    """True if the regular expressions occur in this order in the instructions of key between start and end (hex
    offsets); returns (ok, location of the first match or of the failing pattern)."""
    lines, data, f, base = block_lines(g, args, key)
    body = [(o, t) for o, h, t in lines if o is not None and start <= o < end]
    i = 0
    first = None
    for pat in patterns:
        rx = re.compile(pat)
        while i < len(body) and not rx.search(body[i][1]):
            i += 1
        if i == len(body):
            return False, "%s:%04X–%04X: fehlt /%s/" % (key, start, end, pat)
        if first is None:
            first = body[i][0]
        i += 1
    return True, "%s:%04X, %s 0x%05X" % (key, first, f, base + first)


def rnd(n):
    """Pattern list for 'Random(n)' with an immediate argument."""
    return [r"mov ax,0x%x$" % n, r"call 0x12ce:0x1675$"]


RANDOM_REAL = r"call 0x12ce:0x168b$"
RCMP = r"call 0x12ce:0x11d8$"


def real_at(g, args, key, ofs, value):
    lines, data, f, base = block_lines(g, args, key)
    vals = dict(real_immediates(lines))
    v = vals.get(ofs)
    return v is not None and abs(v - value) <= abs(value) * 1e-6, "%s:%04X = %s, %s 0x%05X" % (
        key, ofs, fmt_real(v) if v is not None else "?", f, base + ofs)


def all_of(*results):
    ok = all(r[0] for r in results)
    bad = [r[1] for r in results if not r[0]]
    return ok, (bad[0] if bad else results[0][1])


def cmd_befunde(g, args):
    """Check every finding of docs/analyse/runde.md (defined in runde_befunde.py)."""
    import runde_befunde
    ok_all = True
    n_ok = 0
    for fid, claim, fn in runde_befunde.FINDINGS:
        try:
            ok, where = fn(sys.modules[__name__], g, args)
        except Exception as e:  # a crashed check is a failed check
            ok, where = False, "Fehler: %r" % e
        ok_all &= bool(ok)
        n_ok += bool(ok)
        print("%-4s %s %s  [%s]" % (fid, "OK  " if ok else "FAIL", claim, where))
    print("Ergebnis: %d von %d Befunden bestätigt" % (n_ok, len(runde_befunde.FINDINGS)))
    return 0 if ok_all else 1


def main():
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--gamedata", default=os.path.join(root, "gamedata"))
    ap.add_argument("--out", default=os.path.join(root, "analyse", "re-runde"))
    ap.add_argument("cmd", choices=["procs", "randoms", "reals", "listings", "befunde", "all"])
    args = ap.parse_args()
    args.tmp = os.path.join(args.out, ".tmp")
    g = bombre.Game(args.gamedata)
    cmds = {"procs": cmd_procs, "randoms": cmd_randoms, "reals": cmd_reals, "listings": cmd_listings, "befunde": cmd_befunde}
    rc = 0
    try:
        for c in (cmds if args.cmd == "all" else [args.cmd]):
            rc |= cmds[c](g, args) or 0
    finally:
        shutil.rmtree(args.tmp, ignore_errors=True)
    return rc


if __name__ == "__main__":
    sys.exit(main())
