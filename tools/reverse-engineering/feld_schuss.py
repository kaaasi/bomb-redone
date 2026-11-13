#!/usr/bin/env python3
"""Probe for the shot of the ballistic weapons (spur schuss, R2-D7 = A): continues a feld.py case at O4:479A
(start player chosen) and far calls the original O4:01B7 (flight, walls, hit test, impact O6:00E0) for one shooter.

Docs: docs/analyse/feld.md section 5. No bytes of the originals in this script (AGENTS.md section 2.1); outputs only
to analyse/feld/ (protected, git-ignored).

Usage:
  feld_schuss.py [--gamedata DIR] befunde [--print]   check the code regions (SHA-256)
  feld_schuss.py [...] run --seed HEX [feld.py case options] --shooter N --angle A --power P [--weapon W]
                     [--ss S] [--trail] [--wind-value V] [--shot-seed HEX] [--sound] [--out DIR]
                                                     one shot; writes dumps and result.json
  feld_schuss.py [...] hashes [--only I ...]         fixed shot list -> TSV (analyse/feld/schuss-hashes.tsv); the C#
                                                     tests (tests/BombRedone.Core.Tests/Shot) compare against these

What is recorded (all original code runs; the probe only sets the record fields of the shooter):
  - the shot state x, y, vx, vy (DS:4796, 479C, 47A2, 47A8, Real48) at every arrival at the loop head O4:060A, with
    the sub-step counter DS:4740 and the tick counter [bp-2Eh] -> one SHA-256 over all steps ("path")
  - the end of the flight at O4:1343: x, y, vx, vy, hit colour DS:47DE, explode DS:4750, end DS:4751
  - the arguments of O6:00E0 (impact) if it is called
  - after the return of O4:01B7: screen A000h, background buffer DS:E5C0, DAC entries written during the shot,
    RandSeed and the number of Random(n) / Random:Real calls during the shot, all records 1..N
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
from feld_cpu import BP, SP, SS  # noqa: E402

O4_SHOT = 0x01B7
O4_LOOP = 0x060A
O4_AFTER_LOOP = 0x1343
O6_IMPACT = 0x00E0
SHOT_STATE = 0x4796  # x, y, vx, vy: 4 x Real48
SUBSTEP = 0x4740
SUBSTEPS = 0x4744

REGIONS = {
    "O4 Schuss 01B7": ("ovl", 4, 0x01B7, 0x1379),
    "O6 Einschlag": ("ovl", 6, 0x0000, 0x18CC),
    "0431 Punkte und Erdrutsch": ("seg", 0x0431, 0x03C3, 0x05ED),
    "0CC8 PutPixel/GetPixel": ("seg", 0x0CC8, 0x0BB6, 0x0C1F),
}
REGION_HASHES = {
    "O4 Schuss 01B7": "2351a60888e61870751f6c9802b0d33eb970d4a9b87dc26fd8256fc29ac26ffd",
    "O6 Einschlag": "d58b9aecd1635788f76a91d32cff5b0cd2d8e8be2be5f163d0ea508211f6d6fb",
    "0431 Punkte und Erdrutsch": "ccad557dbf333b6b505f99e89b8ba528e8f70abb8f06507e65571496cff621e0",
    "0CC8 PutPixel/GetPixel": "a4a9bcc22bfa4ca3ca362ee7f3e41b32f74fac3595909c20b110d9d988161b84",
}

FIELDS = (("energy", 0x21, 2, True), ("score", 0x13, 4, True), ("shield", 0x52, 4, True),
          ("inertia", 0x56, 4, True), ("repulser", 0x5A, 4, True), ("hits", 0x6C, 2, True),
          ("kills", 0x6E, 2, True), ("alive", 0x23, 1, False), ("x", 0x0F, 2, True), ("y", 0x11, 2, True))


def sha(b):
    return hashlib.sha256(b).hexdigest()


def field_crop(screen):
    """Frame and field (x 8..312, y 28..192): what Core/Playfield and the shot draw. The header, the weapon bar and the
    wall label are BGI output of the UI (feld.md section 2) and lie outside."""
    return b"".join(screen[y * 320 + 8:y * 320 + 313] for y in range(28, 193))


def run_shot(p, case, shot):
    out = {}

    def after(m, counts, snaps):
        c = m.cpu
        rec = feld.RECORD + feld.RECORD_SIZE * shot["shooter"]
        for ofs, value in ((0x1D, shot["angle"]), (0x1B, shot["power"]), (0x24, shot["weapon"]), (0x26, 0)):
            p.wds(m, rec + ofs, value)
        p.wds(m, rec + 0x2E + 2 * shot["weapon"], max(1, p.rds(m, rec + 0x2E + 2 * shot["weapon"])))
        p.wds(m, 0xE8B2, 0, 1)  # fly sound off (it only plays tones)
        p.wds(m, 0xE8B3, 1 if shot.get("sound") else 0, 1)  # sound: Random calls in the explosion picture (runde.md 5.6)
        p.wds(m, 0xE8B5, 1 if shot["trail"] else 0, 1)
        if shot["ss"] is not None:
            p.wds(m, 0xE8CA, shot["ss"])
        if shot["wind_value"] is not None:
            p.wds(m, 0xE8A2, shot["wind_value"])
        if shot.get("randseed") is not None:
            p.wds(m, feld.RANDSEED, shot["randseed"], 4)
        out["before"] = {"randseed": p.rds(m, feld.RANDSEED, 4), "random_n": counts["random_n"],
                         "random_real": counts["random_real"], "ss": p.rds(m, 0xE8CA),
                         "wind": p.rds(m, 0xE8A2, signed=True)}
        m.dac_written.clear()
        path = hashlib.sha256()
        steps = {"n": 0, "first": None}
        load = fm.LOAD_SEG

        def state(mm):
            a = feld.lin(p.ds(), SHOT_STATE)
            return bytes(mm.mem[a:a + 24])

        def on_loop(mm):
            ticks = struct.unpack("<i", bytes(mm.mem[feld.lin(mm.cpu.S[SS], mm.cpu.R[BP] - 0x2E):][:4]))[0]
            rec_bytes = state(mm) + struct.pack("<hi", p.rds(mm, SUBSTEP, signed=True), ticks)
            path.update(rec_bytes)
            if steps["first"] is None:
                steps["first"] = rec_bytes.hex()
            steps["n"] += 1
            return False

        def on_end(mm):
            s = state(mm)
            out["end"] = {"x": s[0:6].hex(), "y": s[6:12].hex(), "vx": s[12:18].hex(), "vy": s[18:24].hex(),
                          "hit": p.rds(mm, 0x47DE), "explode": p.rds(mm, 0x4750, 1), "stop": p.rds(mm, 0x4751, 1),
                          "ix": mm.cpu.rw(mm.cpu.S[SS], (mm.cpu.R[BP] - 0x18) & 0xFFFF),
                          "iy": mm.cpu.rw(mm.cpu.S[SS], (mm.cpu.R[BP] - 0x1A) & 0xFFFF),
                          "randseed": p.rds(mm, feld.RANDSEED, 4)}
            return False

        def on_impact(mm):
            st = feld.lin(mm.cpu.S[SS], mm.cpu.R[SP])
            args = bytes(mm.mem[st + 4:st + 4 + 16])  # return address, then color, y (6), x (6), shooter
            out["impact"] = {"color": struct.unpack_from("<H", args, 0)[0], "y": args[2:8].hex(),
                             "x": args[8:14].hex(), "shooter": struct.unpack_from("<H", args, 14)[0]}
            return False

        m.traps[feld.lin(p.ovseg[4], O4_LOOP)] = on_loop
        m.traps[feld.lin(p.ovseg[4], O4_AFTER_LOOP)] = on_end
        m.traps[feld.lin(p.ovseg[6], O6_IMPACT)] = on_impact
        n0, r0 = counts["random_n"], counts["random_real"]
        m.far_call(p.ovseg[4], O4_SHOT, args=(shot["shooter"],), limit=c.steps + 2_000_000_000)
        bg_ofs, bg_seg = struct.unpack_from("<HH", m.mem, feld.lin(p.ds(), 0xE5C0))
        b = feld.lin(bg_seg, bg_ofs)
        n_players = case["players"]
        records = []
        raw = b""
        for i in range(1, n_players + 1):
            r = feld.RECORD + feld.RECORD_SIZE * i
            raw += bytes(m.mem[feld.lin(p.ds(), r):feld.lin(p.ds(), r) + feld.RECORD_SIZE])
            records.append({k: p.rds(m, r + o, n, sg) for k, o, n, sg in FIELDS})
        out["after"] = {
            "screen": bytes(m.mem[0xA0000:0xA0000 + 64000]),
            "background": bytes(m.mem[b:b + 64000]),
            "dac_set": b"".join(bytes([i]) + bytes(m.dac[3 * i:3 * i + 3]) for i in sorted(m.dac_written)),
            "records_raw": raw,
            "records": records,
            "randseed": p.rds(m, feld.RANDSEED, 4),
            "random_n": counts["random_n"] - n0,
            "random_real": counts["random_real"] - r0,
        }
        out["path"] = {"steps": steps["n"], "sha": path.hexdigest(), "first": steps["first"]}

    result, snaps = p.run_case(case, after=after)
    return result, out


def summary(result, out):
    a = out["after"]
    return {
        "before": out["before"], "end": out.get("end"), "impact": out.get("impact"), "path": out["path"],
        "after": {"randseed": "%08X" % a["randseed"], "random_n": a["random_n"], "random_real": a["random_real"],
                  "screen": sha(a["screen"]), "background": sha(a["background"]), "dac_set": sha(a["dac_set"]),
                  "dac_entries": len(a["dac_set"]) // 4, "records": a["records"]},
        "wall_round": result["wall_round"], "wind": result["wind"], "tanks": result["tanks"],
    }


def parse_shot(args):
    return {"shooter": args.shooter, "angle": args.angle, "power": args.power, "weapon": args.weapon,
            "ss": args.ss, "trail": args.trail, "wind_value": args.wind_value,
            "randseed": None if args.shot_seed is None else int(args.shot_seed, 16), "sound": args.sound}


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


def cmd_run(p, args):
    case = feld.parse_case(args)
    shot = parse_shot(args)
    result, out = run_shot(p, case, shot)
    d = args.out or os.path.join(args.root_out, "schuss-%08X-n%d-w%d-s%d-a%d-p%d-w%d" % (
        case["seed"], case["players"], case["wall"], shot["shooter"], shot["angle"], shot["power"], shot["weapon"]))
    os.makedirs(d, exist_ok=True)
    for k in ("screen", "background", "dac_set"):
        with open(os.path.join(d, "shot-%s.bin" % k), "wb") as f:
            f.write(out["after"][k])
    s = summary(result, out)
    with open(os.path.join(d, "result.json"), "w") as f:
        json.dump(s, f, indent=1)
    print(json.dumps(s, indent=1))
    print(d)
    return 0


# Fixed shots for the C# comparison: feld.py case (seed, players, wall, background, mask, wind option, invisible,
# no_fall) and shot (shooter, angle, power, weapon, ss, trail, wind value override or None).
A = (0x12345678, 2, 0, 0, 0xFFFF, 2, False, False)  # tanks (33, 123) and (300, 107), wind -44, SS 10


def wall(w, base=A):
    return base[:2] + (w,) + base[3:]


SHOTS = [
    (A, (1, 45, 270, 0, None, False, None)),          # 0 Lead Ball hits tank 2 (5 damage, 50 points)
    (A, (1, 45, 240, 0, None, False, None)),          # 1 Lead Ball into terrain
    (A, (1, 45, 262, 1, None, False, None)),          # 2 Baby Missile into terrain next to tank 2 (crater, area damage)
    (A, (1, 45, 270, 1, None, False, None)),          # 3 Baby Missile direct hit on tank 2
    (A, (1, 45, 400, 0, None, False, None)),          # 4 wall 0: out to the right, ends at y >= 190
    (wall(1), (1, 45, 400, 0, None, False, None)),    # 5 Sticky
    (wall(2), (1, 45, 400, 0, None, False, None)),    # 6 Elastic
    (wall(3), (1, 45, 400, 0, None, False, None)),    # 7 Accelerate
    (wall(4), (1, 45, 400, 0, None, False, None)),    # 8 Warp
    (A, (1, 45, 400, 0, None, False, 200)),           # 9 wall 0 right with wind > 0: y := 300
    (A, (2, 45, 400, 0, None, False, -200)),          # 10 wall 0 left with wind < 0 (vx < 1 rule)
    (A, (1, 20, 943, 0, None, False, None)),          # 11 power = 3 * energy + 643: flies through terrain
    (A, (1, 20, 1000, 0, None, False, None)),         # 12 power above: ends in the last sub-step of a tick
    (A, (1, 45, 240, 0, None, True, None)),           # 13 trail
    (A[:6] + (True, False), (1, 45, 255, 0, None, True, None)),  # 14 invisible shot with trail
    (A, (1, 89, 100, 1, None, False, None)),          # 15 Baby Missile back onto the shooter
    (A, (1, 45, 262, 0, 1, False, None)),             # 16 SS = 1
    (A, (1, 45, 262, 0, 20, False, None)),            # 17 SS = 20
    ((0x9E3779B9, 4, 2, 2, 0xFFFF, 1, False, False), (3, 60, 300, 1, None, False, None)),   # 18 four players, background 2
    ((0xDEADBEEF, 5, 3, 3, 0xFFFF, 3, False, False), (2, 50, 350, 0, None, True, None)),    # 19 five players, circles
    (A, (1, 45, 265, 1, None, False, None)),          # 20 Baby Missile close to tank 2: area damage only
    (A, (1, 45, 240, 0, None, False, None, 0)),       # 21 RandSeed 0 before the shot: Random:Real < 0.2, 1 damage
    (A, (1, 45, 265, 1, None, False, None, None, True)),  # 22 Baby Missile with sound: Random(500)/Random(250) per step
]


def cmd_hashes(p, args):
    header = "nr\tseed\tplayers\twall\tbackground\twind_opt\tshooter\tangle\tpower\tweapon\tss\ttrail\twind\t" \
             "steps\tpath\tend_x\tend_y\tend_vx\tend_vy\tix\tiy\thit\texplode\timpact\trandseed\trandom_n\trandom_real\t" \
             "screen\tbackground_buffer\tdac_set\trecords\tscreen_field"
    rows = []
    print(header)
    selected = range(len(SHOTS)) if args.only is None else args.only
    for i in selected:
        (seed, n, wall, bg, mask, windopt, inv, nofall), spec = SHOTS[i]
        shooter, angle, power, weapon, ss, trail, wind = spec[:7]
        randseed = spec[7] if len(spec) > 7 else None
        sound = spec[8] if len(spec) > 8 else False
        case = {"seed": seed, "players": n, "wall": wall, "background": bg, "mask": mask, "wind": windopt,
                "invisible": inv, "no_fall": nofall}
        shot = {"shooter": shooter, "angle": angle, "power": power, "weapon": weapon, "ss": ss, "trail": trail,
                "wind_value": wind, "randseed": randseed, "sound": sound}
        result, out = run_shot(p, case, shot)
        e, a = out["end"], out["after"]
        imp = out.get("impact")
        recs = " ".join("%d,%d,%d,%d" % (r["energy"], r["score"], r["shield"], r["hits"]) for r in a["records"])
        row = "%d\t%08X\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%d\t%s\t%s\t%s\t%s\t%s\t%d\t%d\t%d\t%d\t%s\t%08X\t%d\t%d\t%s\t%s\t%s\t%s\t%s" % (
            i, seed, n, wall, bg, windopt, shooter, angle, power, weapon, out["before"]["ss"], trail,
            out["before"]["wind"], out["path"]["steps"], out["path"]["sha"], e["x"], e["y"], e["vx"], e["vy"],
            e["ix"] - 65536 if e["ix"] & 0x8000 else e["ix"], e["iy"] - 65536 if e["iy"] & 0x8000 else e["iy"],
            e["hit"], e["explode"], "-" if imp is None else "%d,%s,%s" % (imp["color"], imp["x"], imp["y"]),
            a["randseed"], a["random_n"], a["random_real"], sha(a["screen"]), sha(a["background"]), sha(a["dac_set"]),
            recs, sha(field_crop(a["screen"])))
        rows.append(row)
        print(row, flush=True)
    os.makedirs(args.root_out, exist_ok=True)
    with open(os.path.join(args.root_out, "schuss-hashes.tsv"), "w") as f:
        f.write(header + "\n" + "\n".join(rows) + "\n")
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
    r.add_argument("--shooter", type=int, default=1)
    r.add_argument("--angle", type=int, default=45)
    r.add_argument("--power", type=int, default=500)
    r.add_argument("--weapon", type=int, default=0)
    r.add_argument("--ss", type=int)
    r.add_argument("--trail", action="store_true")
    r.add_argument("--wind-value", type=int)
    r.add_argument("--shot-seed", help="RandSeed (hex) set right before O4:01B7")
    r.add_argument("--sound", action="store_true", help="sound effects on (fly sound stays off)")
    r.add_argument("--out")
    h = sub.add_parser("hashes")
    h.add_argument("--only", type=int, nargs="*")
    args = ap.parse_args()
    font = None
    if os.path.exists(args.font):
        with open(args.font, "rb") as f:
            font = f.read()
    p = feld.Probe(args.gamedata, font)
    return {"befunde": cmd_befunde, "run": cmd_run, "hashes": cmd_hashes}[args.cmd](p, args)


if __name__ == "__main__":
    sys.exit(main())
