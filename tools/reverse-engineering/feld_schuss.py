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
  feld_schuss.py [...] post [--only I ...]           shot, then all tanks, landslide O7:03C1 and falling O7:0020 like
                                                     O4:49CB -> analyse/feld/schuss-post-hashes.tsv

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
O4_LASER = 0x3426
O4_MIRV = 0x13D4
O4_MIRV_PATTERN_LOOP = 0x15BE    # keyboard loop of the pattern choice, selection [bp-102h]
O4_MIRV_DETONATION_LOOP = 0x18E8  # keyboard loop of the detonation choice, selection [bp-108h]
O4_MIRV_AFTER_DIALOG = 0x1AD0    # screen restored from DS:E5BC, flight setup follows
O10_DAMAGE = 0x0000
O10_DAMAGE_RET = 0x0147          # retf 8 of O10:0000, AX = lost energy
KEY_ENTER_FLAG = 0x4563          # one of the three "accept" flags polled by the MIRV dialog
INPUT_MODE = 0xE5A6              # word per player: 1 keyboard, 2 mouse

REGIONS = {
    "O4 Schuss 01B7": ("ovl", 4, 0x01B7, 0x1379),
    "O6 Einschlag": ("ovl", 6, 0x0000, 0x18CC),
    "0431 Punkte und Erdrutsch": ("seg", 0x0431, 0x03C3, 0x05ED),
    "0CC8 PutPixel/GetPixel": ("seg", 0x0CC8, 0x0BB6, 0x0C1F),
    "O7 Fallen, Rahmen, Erdrutsch": ("ovl", 7, 0x0000, 0x040C),
    "O4 nach dem Schuss": ("ovl", 4, 0x4960, 0x4A0B),
    "O4 MIRV 13D4": ("ovl", 4, 0x1379, 0x3426),
    "O4 Laser 3426": ("ovl", 4, 0x3426, 0x44A7),
    "O10 Schaden und Punkte": ("ovl", 10, 0x0000, 0x02A4),
}
REGION_HASHES = {
    "O4 Schuss 01B7": "2351a60888e61870751f6c9802b0d33eb970d4a9b87dc26fd8256fc29ac26ffd",
    "O6 Einschlag": "d58b9aecd1635788f76a91d32cff5b0cd2d8e8be2be5f163d0ea508211f6d6fb",
    "0431 Punkte und Erdrutsch": "ccad557dbf333b6b505f99e89b8ba528e8f70abb8f06507e65571496cff621e0",
    "0CC8 PutPixel/GetPixel": "a4a9bcc22bfa4ca3ca362ee7f3e41b32f74fac3595909c20b110d9d988161b84",
    "O7 Fallen, Rahmen, Erdrutsch": "55fc0dfa00df9a64ee3436241519c124de7dc5397cac54ac65f09815508d12bf",
    "O4 nach dem Schuss": "a32d2990cddcea807769594c4076fa03c7f3b16f95a20f97053da861a1f4e585",
    "O4 MIRV 13D4": "2563cd4976f97f0d7a411400e999f0a5e2c549b08bc839ee60913c8cbe9991f5",
    "O4 Laser 3426": "a9c660d647a06ce798256057fa74d37af60a319b5e0edff0b62cf79d89b1eb66",
    "O10 Schaden und Punkte": "9fbb21488f10be560df87d927775a021e4f2ad45c4b47d93d4e53262089cf7ae",
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


def snapshot(p, m, case, counts, n0, r0):
    bg_ofs, bg_seg = struct.unpack_from("<HH", m.mem, feld.lin(p.ds(), 0xE5C0))
    b = feld.lin(bg_seg, bg_ofs)
    records = []
    raw = b""
    for i in range(1, case["players"] + 1):
        r = feld.RECORD + feld.RECORD_SIZE * i
        raw += bytes(m.mem[feld.lin(p.ds(), r):feld.lin(p.ds(), r) + feld.RECORD_SIZE])
        records.append({k: p.rds(m, r + o, n, sg) for k, o, n, sg in FIELDS})
    return {
        "screen": bytes(m.mem[0xA0000:0xA0000 + 64000]),
        "background": bytes(m.mem[b:b + 64000]),
        "dac_set": b"".join(bytes([i]) + bytes(m.dac[3 * i:3 * i + 3]) for i in sorted(m.dac_written)),
        "records_raw": raw,
        "records": records,
        "randseed": p.rds(m, feld.RANDSEED, 4),
        "random_n": counts["random_n"] - n0,
        "random_real": counts["random_real"] - r0,
    }


def run_shot(p, case, shot, post=None):
    """post: {"crumble": percent} continues like O4:49CB after the weapon: all tanks U431:07BA, Crumble > 0 -> O7:03C1,
    FallDown -> O7:0020 (runde.md 5.8, 5.9 K11)."""
    out = {}

    def after(m, counts, snaps):
        c = m.cpu
        rec = feld.RECORD + feld.RECORD_SIZE * shot["shooter"]
        guidance = shot.get("guidance", 0)
        for ofs, value in ((0x1D, shot["angle"]), (0x1B, shot["power"]), (0x24, shot["weapon"]), (0x26, guidance)):
            p.wds(m, rec + ofs, value)
        stock = rec + 0x2E + 2 * shot["weapon"]
        if shot.get("stock") is not None:
            p.wds(m, stock, shot["stock"])
        elif shot["weapon"] == 5:
            # the laser stock is energy: O4:3426 caps the power at +38h and subtracts it (runde.md 5.10)
            p.wds(m, stock, max(shot["power"], p.rds(m, stock)))
        else:
            p.wds(m, stock, max(1, p.rds(m, stock)))
        if guidance:
            # target player (+28h) and at least one Herb (+2Ah) / Vick (+2Ch) for the decrement in O4:01B7 (runde.md 5.1)
            p.wds(m, rec + 0x28, shot.get("target", 2))
            for bit, ofs in ((1, 0x2A), (2, 0x2C)):
                if guidance & bit:
                    p.wds(m, rec + ofs, max(1, p.rds(m, rec + ofs)))
        for player, (shield, inertia, repulser) in sorted((shot.get("defense") or {}).items()):
            r = feld.RECORD + feld.RECORD_SIZE * player
            for ofs, value in ((0x52, shield), (0x56, inertia), (0x5A, repulser)):
                p.wds(m, r + ofs, value, 4)
        if shot["weapon"] == 7:
            p.wds(m, INPUT_MODE + 2 * shot["shooter"], 1)  # the MIRV dialog is answered through the keyboard flags
        p.wds(m, 0xE8B2, 0, 1)  # fly sound off (it only plays tones)
        p.wds(m, 0xE8B3, 1 if shot.get("sound") else 0, 1)  # sound: Random calls in the explosion picture (runde.md 5.6)
        p.wds(m, 0xE8B5, 1 if shot["trail"] else 0, 1)
        if shot["ss"] is not None:
            p.wds(m, 0xE8CA, shot["ss"])
        if shot["wind_value"] is not None:
            p.wds(m, 0xE8A2, shot["wind_value"])
        if shot.get("crumble") is not None:
            p.wds(m, 0xE8A4, shot["crumble"])  # Crumble percent: Xplosive Dirt decides the landslide (runde.md 5.7 K8)
        if shot.get("randseed") is not None:
            p.wds(m, feld.RANDSEED, shot["randseed"], 4)
        out["before"] = {"randseed": p.rds(m, feld.RANDSEED, 4), "random_n": counts["random_n"],
                         "random_real": counts["random_real"], "ss": p.rds(m, 0xE8CA),
                         "wind": p.rds(m, 0xE8A2, signed=True), "crumble": p.rds(m, 0xE8A4)}
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

        damage = []

        def on_damage(mm):
            st = feld.lin(mm.cpu.S[SS], mm.cpu.R[SP])
            ptr, _seg, kind, amount = struct.unpack_from("<HHHh", mm.mem, st + 4)
            damage.append([amount, kind & 0xFF, (ptr - feld.RECORD) // feld.RECORD_SIZE, None])
            return False

        def on_damage_ret(mm):
            v = mm.cpu.R[0]
            damage[-1][3] = v - 65536 if v & 0x8000 else v
            return False

        def mirv_choice(local, value):
            def f(mm):
                mm.cpu.ww(mm.cpu.S[SS], (mm.cpu.R[BP] - local) & 0xFFFF, value)
                p.wds(mm, KEY_ENTER_FLAG, 1, 1)
                return False
            return f

        def mirv_after_dialog(mm):
            # The dialog belongs to spur zug; its DAC writes (colours 130..134) are not part of the weapon result.
            mm.dac_written.clear()
            p.wds(mm, KEY_ENTER_FLAG, 0, 1)
            return False

        impacts = []

        def on_any_impact(mm):
            st = feld.lin(mm.cpu.S[SS], mm.cpu.R[SP])
            args = bytes(mm.mem[st + 4:st + 4 + 16])
            impacts.append("%d,%s,%s,%d" % (struct.unpack_from("<H", args, 0)[0], args[8:14].hex(), args[2:8].hex(),
                                            struct.unpack_from("<H", args, 14)[0]))
            if not out.get("impact"):
                on_impact(mm)
            return False

        o4 = p.ovseg[4]
        m.traps[feld.lin(o4, O4_LOOP)] = on_loop
        m.traps[feld.lin(o4, O4_AFTER_LOOP)] = on_end
        m.traps[feld.lin(p.ovseg[6], O6_IMPACT)] = on_any_impact
        m.traps[feld.lin(p.ovseg[10], O10_DAMAGE)] = on_damage
        m.traps[feld.lin(p.ovseg[10], O10_DAMAGE_RET)] = on_damage_ret
        entry = {5: O4_LASER, 7: O4_MIRV}.get(shot["weapon"], O4_SHOT)
        if entry == O4_MIRV:
            m.traps[feld.lin(o4, O4_MIRV_PATTERN_LOOP)] = mirv_choice(0x102, shot.get("pattern", 0))
            m.traps[feld.lin(o4, O4_MIRV_DETONATION_LOOP)] = mirv_choice(0x108, shot.get("detonation", 0))
            m.traps[feld.lin(o4, O4_MIRV_AFTER_DIALOG)] = mirv_after_dialog
        n0, r0 = counts["random_n"], counts["random_real"]
        m.far_call(o4, entry, args=(shot["shooter"],), limit=c.steps + 4_000_000_000)
        out["impacts"] = impacts
        out["damage"] = damage
        out["after"] = snapshot(p, m, case, counts, n0, r0)
        out["path"] = {"steps": steps["n"], "sha": path.hexdigest(), "first": steps["first"]}
        if post is not None:
            m.dac_written.clear()
            n1, r1 = counts["random_n"], counts["random_real"]
            p.wds(m, 0xE8A4, post["crumble"])
            for i in range(1, case["players"] + 1):
                m.far_call(load + 0x0431, 0x07BA, args=(i,), limit=c.steps + 50_000_000)
            if post["crumble"] > 0:
                m.far_call(p.ovseg[7], 0x03C1, limit=c.steps + 2_000_000_000)
            if not case["no_fall"]:
                m.far_call(p.ovseg[7], 0x0020, limit=c.steps + 2_000_000_000)
            out["post"] = snapshot(p, m, case, counts, n1, r1)

    result, snaps = p.run_case(case, after=after)
    return result, out


def summary(result, out):
    a = out["after"]
    return {
        "before": out["before"], "end": out.get("end"), "impact": out.get("impact"), "path": out["path"],
        "impacts": out["impacts"], "damage": out["damage"],
        "after": {"randseed": "%08X" % a["randseed"], "random_n": a["random_n"], "random_real": a["random_real"],
                  "screen": sha(a["screen"]), "background": sha(a["background"]), "dac_set": sha(a["dac_set"]),
                  "dac_entries": len(a["dac_set"]) // 4, "records": a["records"]},
        "wall_round": result["wall_round"], "wind": result["wind"], "tanks": result["tanks"],
    }


def parse_shot(args):
    return {"shooter": args.shooter, "angle": args.angle, "power": args.power, "weapon": args.weapon,
            "ss": args.ss, "trail": args.trail, "wind_value": args.wind_value,
            "randseed": None if args.shot_seed is None else int(args.shot_seed, 16), "sound": args.sound,
            "guidance": args.guidance, "target": args.target, "pattern": args.pattern, "detonation": args.detonation,
            "defense": parse_defense(args.defense)}


def parse_defense(items):
    """["2:100,0,3"] -> {2: (shield, inertia, repulser)}"""
    d = {}
    for item in items or ():
        player, values = item.split(":")
        d[int(player)] = tuple(int(v) for v in values.split(","))
    return d


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
    (A, (1, 88, 943, 1, None, False, 0)),             # 23 power = M through the terrain, explodes at the bottom (y 191)
    (A, (1, 88, 943, 3, None, False, 0)),             # 24 the same with weapon 3 (radius 30)
    # Review #116 P3-2 (issue #118): the left wall for every wall kind, the wall 0 rules with wind 0, the tick limit.
    (wall(1), (1, 135, 400, 0, None, False, None)),   # 25 Sticky, left wall
    (wall(2), (1, 135, 400, 0, None, False, None)),   # 26 Elastic, left wall
    (wall(3), (1, 135, 400, 1, None, False, None)),   # 27 Accelerate, left wall, Baby Missile
    (wall(4), (1, 135, 400, 0, None, False, None)),   # 28 Warp, left wall
    (A, (1, 135, 400, 0, None, False, 0)),            # 29 wall 0 left with wind 0: no y := 300
    (A, (1, 135, 400, 0, None, False, -100)),         # 30 wall 0 left with wind < 0: y := 300
    (A, (1, 45, 400, 0, None, False, 0)),             # 31 wall 0 right with wind 0: no y := 300
    (wall(2), (2, 15, 943, 0, None, False, 0)),       # 32 Elastic, power = M: bounces until the tick limit (ticks > 1000)
]


# After-shot cases (O4:49CB): shot number from SHOTS, crumble percent, FallDown (False = case option no_fall).
POST = [
    (3, 0, True),     # 0 Baby Missile direct hit: crater under tank 2, it falls (Random(3) + 1 per row on inertia)
    (3, 100, True),   # 1 the same with Crumble 100: landslide first
    (2, 100, True),   # 2 crater next to tank 2, landslide
    (18, 50, True),   # 3 four players, Crumble 50 (Random:Real decides)
    (3, 100, False),  # 4 landslide without falling
    (13, 100, True),  # 5 trail pixels (90..99) count as air for the landslide
    (23, 100, True),  # 6 hole below the surface: the landslide moves terrain
    (24, 100, True),  # 7 large hole: terrain slides, tank 2 falls
    (24, 0, True),    # 8 the same without landslide
]


# All weapons (spur waffen, runde.md section 5): feld.py case as in SHOTS, then a dict of shot fields. Keys besides the
# SHOTS fields: guidance (+26h), target (+28h), pattern / detonation (MIRV dialog), defense {player: (shield, inertia,
# repulser)}, stock (+2Eh + 2 * weapon), crumble (option, percent), post (crumble percent for O7:03C1, then falling).
def weapon_shots():
    t = []

    def add(case, **kw):
        spec = {"shooter": 1, "angle": 45, "power": 262, "weapon": 0, "ss": None, "trail": False, "wind_value": None}
        spec.update(kw)
        t.append((case, spec))

    for w in range(18):
        add(A, weapon=w)                         # terrain next to tank 2 (laser: up to the right)
        add(A, weapon=w, power=270)              # direct hit on tank 2 (Lead Ball path)
        for k in (1, 2, 3, 4):
            add(wall(k), weapon=w, power=400)    # right wall: Sticky, Elastic, Accelerate, Warp
        add(A, weapon=w, sound=True)             # sound on: Random calls of the pictures
    # laser (runde.md 5.10): through the terrain, against tank 2 without and with shield, shield not used up, walls
    add(A, weapon=5, angle=3, power=500)
    add(A, weapon=5, angle=4, power=500)
    add(A, weapon=5, angle=4, power=500, defense={2: (50, 0, 0)})
    add(A, weapon=5, angle=4, power=500, defense={2: (2000, 0, 0)})
    add(A, weapon=5, angle=4, power=500, sound=True)
    add(A, weapon=5, angle=4, power=500, stock=120)  # stock below the power: power capped
    for k in (1, 2, 3, 4):
        add(wall(k), weapon=5, angle=30, power=900)
    add(A, weapon=5, angle=135, power=300)           # wall 0 to the left
    # MIRV (runde.md 5.11): every pattern and detonation
    for pattern in range(5):
        for detonation in range(3):
            add(A, weapon=7, angle=60, power=300, pattern=pattern, detonation=detonation)
    add(A, weapon=7, angle=60, power=300, pattern=0, detonation=0, sound=True)
    # guidance (runde.md 5.12 G1): Herb, Vick, both; wall 0 rule with guidance
    add(A, angle=45, power=240, guidance=1)
    add(A, angle=30, power=280, guidance=1)
    add(A, angle=55, power=320, guidance=2)
    add(A, angle=65, power=330, guidance=2)
    add(A, angle=60, power=300, guidance=3)
    add(A, angle=60, power=320, guidance=3, weapon=3)
    add(A, angle=45, power=400, guidance=1, wind_value=200)
    add(A, angle=45, power=400, guidance=2, wind_value=200)
    add(A, angle=55, power=320, guidance=2, target=1)  # target is the shooter
    # defense (Q11, Q5): shield partly and fully used, inertia through falling, repulser deflects and wears out
    add(A, weapon=1, power=270, defense={2: (50, 0, 0)})
    add(A, weapon=1, power=270, defense={2: (1000, 0, 0)})
    add(A, weapon=4, power=262, defense={2: (100, 0, 0), 1: (30, 0, 0)})
    add(A, weapon=1, power=270, defense={2: (0, 40, 0)}, post=0)
    add(A, weapon=4, power=270, defense={2: (0, 5, 0)}, post=0)
    add(A, weapon=0, power=270, defense={2: (0, 0, 3)})
    add(A, weapon=1, power=270, defense={2: (0, 0, 3)})
    add(A, weapon=0, power=262, defense={2: (0, 0, 1)})
    add(A, weapon=0, power=262, defense={1: (0, 0, 2)})
    add(A, weapon=0, angle=89, power=100, defense={1: (0, 0, 2)})
    add(A, weapon=4, power=270, defense={2: (10, 0, 2)})   # deflected back onto the shooter
    add(A, weapon=1, angle=89, power=100, defense={1: (0, 0, 2)})   # area damage on a tank with repulser: Random:Real
    add(A, weapon=4, angle=89, power=100, defense={1: (20, 0, 1)})
    add(A, weapon=3, angle=89, power=100, defense={1: (0, 0, 5)}, randseed=0)
    # landslide options of Sonic Blaster and Xplosive Dirt
    for w in (12, 17):
        for crumble in (0, 10, 100):
            add(A, weapon=w, crumble=crumble)
    # appended (spur p3-runde-3, #164): MIRV pattern 4 with warheads that land apart, so the sign of vy of part 3 shows
    add(A, weapon=7, angle=60, power=250, pattern=4, detonation=0)
    add(A, weapon=7, angle=75, power=300, pattern=4, detonation=0)
    # appended (#163): Herb from above (shooter 2 stands higher than its target 1), so the bound dy <= 4 decides the path
    add(A, shooter=2, angle=135, power=250, guidance=1, target=1)
    add(A, shooter=2, angle=120, power=300, guidance=1, target=1)
    return t


WEAPON_SHOTS = weapon_shots()


def weapon_row(p, i):
    (seed, n, wall_kind, bg, mask, windopt, inv, nofall), spec = WEAPON_SHOTS[i]
    case = {"seed": seed, "players": n, "wall": wall_kind, "background": bg, "mask": mask, "wind": windopt,
            "invisible": inv, "no_fall": nofall}
    shot = dict(spec)
    post = None if shot.get("post") is None else {"crumble": shot["post"]}
    result, out = run_shot(p, case, shot, post=post)
    a = out["after"]
    defense = " ".join("%d:%d,%d,%d" % ((k,) + v) for k, v in sorted((shot.get("defense") or {}).items())) or "-"
    recs = " ".join("%d,%d,%d,%d,%d,%d" % (r["energy"], r["score"], r["shield"], r["inertia"], r["repulser"], r["hits"])
                    for r in a["records"])
    cols = [i, "%08X" % seed, n, wall_kind, bg, windopt, shot["shooter"], shot["angle"], shot["power"], shot["weapon"],
            shot.get("guidance", 0), shot.get("target", 2) if shot.get("guidance") else 0, shot.get("pattern", 0),
            shot.get("detonation", 0), 1 if shot.get("sound") else 0, out["before"]["crumble"],
            defense, "-" if shot.get("stock") is None else shot["stock"], out["before"]["ss"], out["before"]["wind"],
            "%08X" % out["before"]["randseed"], out["path"]["steps"], out["path"]["sha"],
            " ".join(out["impacts"]) or "-",
            " ".join("%d/%d/%d/%d" % tuple(d) for d in out["damage"]) or "-",
            "%08X" % a["randseed"], a["random_n"], a["random_real"], sha(field_crop(a["screen"])), sha(a["background"]),
            sha(a["dac_set"]), a["dac_set"].hex(), sha(a["records_raw"]), recs]
    if post is None:
        cols += ["-"] * 6
    else:
        q = out["post"]
        cols += ["%08X" % q["randseed"], q["random_n"], q["random_real"], sha(field_crop(q["screen"])), sha(q["background"]),
                 sha(q["records_raw"])]
    cols.append(stock_after(a["records_raw"], n, shot["weapon"]))
    return "\t".join(str(c) for c in cols)


def stock_after(raw, players, weapon):
    """Power +1Bh, Herb +2Ah, Vick +2Ch and the stock of the fired weapon +2Eh + 2w of every record after the shot (signed
    words, #163): "power,herb,vick,stock" per player."""
    out = []
    for i in range(players):
        r = raw[i * feld.RECORD_SIZE:(i + 1) * feld.RECORD_SIZE]
        out.append("%d,%d,%d,%d" % tuple(struct.unpack_from("<h", r, o)[0] for o in (0x1B, 0x2A, 0x2C, 0x2E + 2 * weapon)))
    return " ".join(out)


WEAPON_HEADER = "nr\tseed\tplayers\twall\tbackground\twind_opt\tshooter\tangle\tpower\tweapon\tguidance\ttarget\t" \
    "pattern\tdetonation\tsound\tcrumble\tdefense\tstock\tss\twind\tseed_before\tsteps\tpath\timpacts\tdamage\t" \
    "randseed\trandom_n\trandom_real\tscreen_field\tbackground_buffer\tdac_set\tdac_set_hex\trecords_raw\trecords\t" \
    "post_randseed\tpost_random_n\tpost_random_real\tpost_screen_field\tpost_background\tpost_records_raw\tstock_after"

_worker = {}


def _weapon_job(job):
    gamedata, font, i = job
    if "p" not in _worker:
        _worker["p"] = feld.Probe(gamedata, font)
    return weapon_row(_worker["p"], i)


def cmd_waffen(p, args):
    selected = list(range(len(WEAPON_SHOTS))) if args.only is None else args.only
    print(WEAPON_HEADER)
    rows = []
    if args.jobs > 1:
        import multiprocessing
        with multiprocessing.Pool(args.jobs) as pool:
            for row in pool.imap(_weapon_job, [(args.gamedata, p.font, i) for i in selected]):
                rows.append(row)
                print(row, flush=True)
    else:
        for i in selected:
            row = weapon_row(p, i)
            rows.append(row)
            print(row, flush=True)
    if args.only is None:
        os.makedirs(args.root_out, exist_ok=True)
        with open(os.path.join(args.root_out, "waffen-hashes.tsv"), "w") as f:
            f.write(WEAPON_HEADER + "\n" + "\n".join(rows) + "\n")
    return 0


def cmd_waffen_cs(p, args):
    """analyse/feld/waffen-hashes.tsv -> tests/BombRedone.Core.Tests/Shot/WeaponProbeData.cs (hashes and numbers only)."""
    import csv
    root = os.path.normpath(os.path.join(HERE, "..", ".."))
    with open(os.path.join(args.root_out, "waffen-hashes.tsv")) as f:
        rows = list(csv.DictReader(f, delimiter="\t"))

    def q(v):
        return "null" if v == "-" else '"%s"' % v

    def n(v):
        return "null" if v == "-" else v

    lines = []
    for r in rows:
        lines.append("        new(%s)," % ", ".join([
            r["nr"], q(r["seed"]), r["players"], r["wall"], r["background"], r["wind_opt"], r["shooter"], r["angle"],
            r["power"], r["weapon"], r["guidance"], r["target"], r["pattern"], r["detonation"],
            "true" if r["sound"] == "1" else "false", n(r["crumble"]), q(r["defense"]), n(r["stock"]), r["ss"], r["wind"],
            q(r["seed_before"]), r["steps"], q(r["path"]), q(r["impacts"]), q(r["damage"]), q(r["randseed"]),
            r["random_n"], r["random_real"], q(r["screen_field"]), q(r["background_buffer"]), q(r["dac_set"]),
            q(r["records"]), q(r["post_randseed"]), q(r["post_screen_field"]), q(r["post_background"]),
            "false" if WEAPON_SHOTS[int(r["nr"])][1].get("randseed") is None else "true"]))
    stock = "\n".join('        "%s",' % r["stock_after"] for r in rows)
    text = """namespace BombRedone.Core.Tests.Shot;

/// <summary>
/// Generated from analyse/feld/waffen-hashes.tsv (<c>python3 -I tools/reverse-engineering/feld_schuss.py waffen</c>, then
/// <c>waffen-cs</c>); the shots are listed with comments in weapon_shots() of that script. Hashes and numbers only, no bytes
/// of the originals.
/// </summary>
internal static class WeaponProbeData
{
    public static readonly WeaponProbeTests.WeaponCase[] All =
    [
%s
    ];

    /// <summary>Per case (index = nr): power, Herb, Vick and the stock of the fired weapon of every record after the shot.</summary>
    public static readonly string[] StockAfter =
    [
%s
    ];
}
""" % ("\n".join(lines), stock)
    out = os.path.join(root, "tests", "BombRedone.Core.Tests", "Shot", "WeaponProbeData.cs")
    with open(out, "w") as f:
        f.write(text)
    print(out, len(rows))
    return 0


def cmd_post(p, args):
    header = "nr\tshot\tcrumble\tfall\trandseed\trandom_n\trandom_real\tscreen_field\tbackground_buffer\tdac_set\trecords"
    rows = []
    print(header)
    selected = range(len(POST)) if args.only is None else args.only
    for k in selected:
        i, crumble, fall = POST[k]
        case_t, spec = SHOTS[i]
        (seed, n, wall, bg, mask, windopt, inv, nofall) = case_t
        case = {"seed": seed, "players": n, "wall": wall, "background": bg, "mask": mask, "wind": windopt,
                "invisible": inv, "no_fall": not fall}
        shot = {"shooter": spec[0], "angle": spec[1], "power": spec[2], "weapon": spec[3], "ss": spec[4], "trail": spec[5],
                "wind_value": spec[6], "randseed": spec[7] if len(spec) > 7 else None,
                "sound": spec[8] if len(spec) > 8 else False}
        result, out = run_shot(p, case, shot, post={"crumble": crumble})
        a = out["post"]
        recs = " ".join("%d,%d,%d,%d" % (r["energy"], r["inertia"], r["x"], r["y"]) for r in a["records"])
        row = "%d\t%d\t%d\t%d\t%08X\t%d\t%d\t%s\t%s\t%s\t%s" % (
            k, i, crumble, 1 if fall else 0, a["randseed"], a["random_n"], a["random_real"], sha(field_crop(a["screen"])),
            sha(a["background"]), sha(a["dac_set"]), recs)
        rows.append(row)
        print(row, flush=True)
    os.makedirs(args.root_out, exist_ok=True)
    with open(os.path.join(args.root_out, "schuss-post-hashes.tsv"), "w") as f:
        f.write(header + "\n" + "\n".join(rows) + "\n")
    return 0


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
    r.add_argument("--guidance", type=int, default=0, help="+26h: 0 none, 1 Herb, 2 Vick, 3 both")
    r.add_argument("--target", type=int, default=2, help="+28h: target player of the guidance")
    r.add_argument("--pattern", type=int, default=0, help="MIRV pattern 0..4")
    r.add_argument("--detonation", type=int, default=0, help="MIRV detonation 0 impact, 1 simultaneous, 2 none")
    r.add_argument("--defense", nargs="*", help="player:shield,inertia,repulser (LongInt +52h, +56h, +5Ah)")
    r.add_argument("--out")
    h = sub.add_parser("hashes")
    h.add_argument("--only", type=int, nargs="*")
    q = sub.add_parser("post")
    q.add_argument("--only", type=int, nargs="*")
    wa = sub.add_parser("waffen", help="all weapons -> analyse/feld/waffen-hashes.tsv (spur waffen)")
    wa.add_argument("--only", type=int, nargs="*")
    wa.add_argument("--jobs", type=int, default=1)
    sub.add_parser("waffen-cs", help="waffen-hashes.tsv -> tests/.../Shot/WeaponProbeData.cs")
    args = ap.parse_args()
    font = None
    if os.path.exists(args.font):
        with open(args.font, "rb") as f:
            font = f.read()
    p = feld.Probe(args.gamedata, font)
    return {"befunde": cmd_befunde, "run": cmd_run, "hashes": cmd_hashes, "post": cmd_post,
            "waffen": cmd_waffen, "waffen-cs": cmd_waffen_cs}[args.cmd](p, args)


if __name__ == "__main__":
    sys.exit(main())
