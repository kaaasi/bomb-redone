#!/usr/bin/env python3
"""Probe for the round loop after the start player (spur runde, R3-D2 = A): continues a feld.py case at O4:479A and
lets the original O4:44A7 run to its end: turn start Z1, the weapon (O4:01B7, O4:3426, O4:13D4), tanks, crumble and
falling after the shot (K11), shot log DS:4A80, the death loop K12 with the death explosions O5 (K15, K16), the
lightning O6:14C5 (K13), the round end K14 and the return of O4:44A7.

The turn O7:040C itself (spur zug, docs/analyse/zug.md) is not run: at its call O4:4960 the probe writes the turn
result of the script (angle +1Dh, power +1Bh, weapon +24h, guidance +26h/+28h) into the record of the player to move
and continues at O4:4965. Crt.Delay is skipped and its argument summed (the clock of the pictures).

Docs: docs/analyse/feld.md section 9. No bytes of the originals in this script (AGENTS.md section 2.1): code regions
are checked by SHA-256 only; outputs go to analyse/feld/ (protected, git-ignored).

Usage:
  tod.py [--gamedata DIR] befunde [--print]          check the code regions the probe relies on
  tod.py [...] run --seed HEX [feld.py case options] --turns "TURN;TURN;..." [--set [P:]FIELD=V ...]
                   [--sound] [--crumble C] [--pass V] [--death-seed HEX,...] [--lightning-seed HEX,...] [--out DIR]
                                                     one round; writes result.json and the dumps of every checkpoint
  tod.py [...] runden [--only I ...] [--jobs N]       fixed round list -> analyse/feld/runden-hashes.tsv (one line per
                                                     checkpoint); the C# tests (tests/BombRedone.Core.Tests/Round)
                                                     compare against these

A TURN is "angle,power[,weapon[,guidance,target[,pattern,detonation]]]" (pattern/detonation for the MIRV dialog,
answered through the keyboard flags like feld_schuss.py). Without a turn left the run stops ("exhausted").
Before Z1 of the first turn (at O4:479A) the probe writes the --set fields, the options (sound, fly sound off,
Crumble) and the pass counter DS:47EA (--pass). The weapon's stock is raised to at least 1 (laser: the power) at
O4:4960, like feld_schuss.py; the remake gets the same stock.

Checkpoints (one snapshot each: RandSeed, number of Random(n)/Random:Real calls since O4:479A, frame and field of
the screen, background buffer DS:E5C0, DAC entries written since O4:479A, all records, delays):
  turn    O4:4965  after the turn (record of the shooter as written)
  post    O4:4A0B  after the weapon, all tanks, Crumble O7:03C1 and falling O7:0020
  death   O4:4D50  after one death: Random(7) choice, O5 picture, points, frame, O7:03C1, falling
  bolt    O4:4D9F  after the lightning O6:14C5
  end     O4:4EF2  return of O4:44A7 (after the round end K14)
  next    O4:4E68  the round goes on (jump back to O4:4770)
"""
import argparse
import hashlib
import json
import os
import struct
import sys
from concurrent.futures import ProcessPoolExecutor

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import feld  # noqa: E402
import feld_machine as fm  # noqa: E402
from feld_cpu import AX, BP, SP, SS, CS  # noqa: E402

O4_CALL_TURN = 0x4960
O4_AFTER_TURN = 0x4965
O4_AFTER_POST = 0x4A0B
O4_DEATH_RANDOM = 0x4C10      # call of Random(7) choosing the death picture
O4_DEATH_CHOICE = 0x4C15      # AX = Random(7)
O4_DEATH_DONE = 0x4D50        # after the points, frame, O7:03C1 and falling of one death
O4_LIGHTNING_CALL = 0x4D9A    # call of O6:14C5(0)
O4_LIGHTNING_DONE = 0x4D9F
O4_ROUND_END = 0x4E6B         # round over: 0431:0000, round sum +68h
O4_NOT_OVER = 0x4E68          # jmp O4:4770: the round goes on with the next turn
O4_RETURN = 0x4EF2            # retf of O4:44A7
O4_MIRV_PATTERN_LOOP = 0x15BE
O4_MIRV_DETONATION_LOOP = 0x18E8
O4_MIRV_AFTER_DIALOG = 0x1AD0
O6_IMPACT = 0x00E0
O10_DAMAGE = 0x0000
O10_DAMAGE_RET = 0x0147
CRT_DELAY = (0x11F8, 0x029C)
KEY_ENTER_FLAG = 0x4563
INPUT_MODE = 0xE5A6
SHOT_LOG = 0x4A80              # DS:4A80 + 7DAh * record + 10 * (+5Eh): barrel, power, x, impact x, impact y
SHOT_LOG_SIZE = 0x7DA
DEATHS = {0: "O5:001F", 1: "O5:0DA0(0)", 2: "O5:1BDE", 3: "O5:0DA0(1)", 4: "O5:2662", 5: "O5:28DE", 6: "O5:29A0"}

REGIONS = {
    "O4 nach dem Schuss bis Rundenende": ("ovl", 4, 0x4A0B, 0x4EF3),
    "O4 Zugfolge und Waffenwahl": ("ovl", 4, 0x4704, 0x4A0B),
    "O5 Todesexplosionen": ("ovl", 5, 0x0000, 0x2B60),
    "O6 Einschlag und Blitz": ("ovl", 6, 0x0000, 0x18CC),
    "O7 Fallen, Rahmen, Erdrutsch": ("ovl", 7, 0x0000, 0x040C),
    "O10 Schaden und Punkte": ("ovl", 10, 0x0000, 0x02A4),
    "0431 Rundenende": ("seg", 0x0431, 0x0000, 0x000C),
}
REGION_HASHES = {
    "O4 nach dem Schuss bis Rundenende": "7c32c30655ab23e69ecca8a1ad995ce72a7972ca5a0077ae520d14cb004dc09a",
    "O4 Zugfolge und Waffenwahl": "5495c1a8a0103a6fd195813ca036124e8442dfbb33b1723516ea1b413c18913e",
    "O5 Todesexplosionen": "986f91e15ffab866347a15555f2c112b94254105b60be4cb25cd57ef61f4f37b",
    "O6 Einschlag und Blitz": "d58b9aecd1635788f76a91d32cff5b0cd2d8e8be2be5f163d0ea508211f6d6fb",
    "O7 Fallen, Rahmen, Erdrutsch": "55fc0dfa00df9a64ee3436241519c124de7dc5397cac54ac65f09815508d12bf",
    "O10 Schaden und Punkte": "9fbb21488f10be560df87d927775a021e4f2ad45c4b47d93d4e53262089cf7ae",
    "0431 Rundenende": "90f091b3fa20352d71cb4e0222bfdbd02635141d4431b73273f9fcee5b373709",
}

# Record fields (offset, size, signed): read back at every checkpoint, writable with --set.
FIELDS = {"x": (0x0F, 2, True), "y": (0x11, 2, True), "score": (0x13, 4, True), "power": (0x1B, 2, True),
          "angle": (0x1D, 2, True), "dir": (0x1F, 2, True), "energy": (0x21, 2, True), "alive": (0x23, 1, False),
          "weapon": (0x24, 2, True), "guidance": (0x26, 2, True), "target": (0x28, 2, True),
          "herb": (0x2A, 2, True), "vick": (0x2C, 2, True), "shield": (0x52, 4, True), "inertia": (0x56, 4, True),
          "repulser": (0x5A, 4, True), "shots": (0x5E, 2, True), "team": (0x66, 2, True),
          "roundsum": (0x68, 2, True), "hits": (0x6C, 2, True), "kills": (0x6E, 2, True), "turns": (0x70, 2, True),
          "starts": (0x72, 2, True)}
for _w in range(18):
    FIELDS["stock%d" % _w] = (0x2E + 2 * _w, 2, True)
SHORT = ("x", "y", "energy", "alive", "score", "shield", "inertia", "repulser", "shots", "hits", "kills", "turns",
         "roundsum", "weapon")


class Exhausted(Exception):
    pass


def sha(b):
    return hashlib.sha256(b).hexdigest()


def field_crop(screen):
    """Frame and field (x 8..312, y 28..192), as feld_schuss.py: the header is BGI text of the UI (feld.md section 2)."""
    return b"".join(screen[y * 320 + 8:y * 320 + 313] for y in range(28, 193))


def parse_turns(text):
    turns = []
    for raw in (text or "").split(";"):
        raw = raw.strip()
        if not raw:
            continue
        v = [int(x, 0) for x in raw.split(",")]
        t = {"angle": v[0], "power": v[1], "weapon": v[2] if len(v) > 2 else 0}
        if len(v) > 4:
            t["guidance"], t["target"] = v[3], v[4]
        if len(v) > 6:
            t["pattern"], t["detonation"] = v[5], v[6]
        turns.append(t)
    return turns


def parse_sets(items):
    out = []
    for item in items or ():
        k, v = item.split("=")
        p_, k = k.split(":") if ":" in k else (None, k)
        if k not in FIELDS:
            raise SystemExit("unknown field %s" % k)
        out.append((None if p_ is None else int(p_), k, int(v, 0)))
    return out


def parse_seeds(text):
    return [int(x, 16) for x in text.split(",")] if text else []


def run_round(p, case, rnd):
    """rnd: turns, sets [(player or None = start player, field, value)], sound, crumble, pass, death_seeds,
    lightning_seeds, keep (also keep the dumps of every checkpoint)."""
    out = {"checkpoints": [], "turns": [], "deaths": [], "bolts": [], "impacts": [], "damage": [], "status": "ok"}

    def after(m, counts, snaps):
        c = m.cpu
        ds = p.ds()
        n = case["players"]
        start = p.rds(m, 0x47EE, 1)
        for who, k, v in rnd["sets"]:
            off, size, _ = FIELDS[k]
            p.wds(m, feld.RECORD + feld.RECORD_SIZE * (who or start) + off, v, size)
        p.wds(m, 0xE8B2, 0, 1)                                # fly sound off (tones only)
        p.wds(m, 0xE8B3, 1 if rnd.get("sound") else 0, 1)     # sound: Random calls in the pictures (runde.md 5.6)
        if rnd.get("crumble") is not None:
            p.wds(m, 0xE8A4, rnd["crumble"])
        if rnd.get("pass") is not None:
            p.wds(m, 0x47EA, rnd["pass"])
        m.dac_written.clear()
        n0, r0 = counts["random_n"], counts["random_real"]
        turns = list(rnd["turns"])
        delays = {"n": 0, "ms": 0}
        death_seeds = list(rnd.get("death_seeds") or ())
        bolt_seeds = list(rnd.get("lightning_seeds") or ())
        state = {"turn": -1, "deaths": 0}

        def records():
            raw = b""
            short = []
            for i in range(1, n + 1):
                a = feld.lin(ds, feld.RECORD + feld.RECORD_SIZE * i)
                raw += bytes(m.mem[a:a + feld.RECORD_SIZE])
                short.append({k: p.rds(m, feld.RECORD + feld.RECORD_SIZE * i + FIELDS[k][0], FIELDS[k][1],
                                       FIELDS[k][2]) for k in FIELDS})
            return raw, short

        def checkpoint(kind, **extra):
            screen = bytes(m.mem[0xA0000:0xA0000 + 64000])
            bg_ofs, bg_seg = struct.unpack_from("<HH", m.mem, feld.lin(ds, 0xE5C0))
            b = feld.lin(bg_seg, bg_ofs)
            background = bytes(m.mem[b:b + 64000])
            dac_set = b"".join(bytes([i]) + bytes(m.dac[3 * i:3 * i + 3]) for i in sorted(m.dac_written))
            raw, short = records()
            log = feld.lin(ds, SHOT_LOG + SHOT_LOG_SIZE)
            cp = {"kind": kind, "turn": state["turn"], "randseed": p.rds(m, feld.RANDSEED, 4),
                  "random_n": counts["random_n"] - n0, "random_real": counts["random_real"] - r0,
                  "field": sha(field_crop(screen)), "background": sha(background), "dac_set": sha(dac_set),
                  "dac_entries": len(dac_set) // 4, "records_raw": sha(raw), "records": short,
                  "shot_log": sha(bytes(m.mem[log:log + SHOT_LOG_SIZE * n])),
                  "pass": p.rds(m, 0x47EA), "to_move": p.rds(m, 0x47EE, 1), "winner": p.rds(m, 0x47E0, signed=True),
                  "delays": delays["n"], "delay_ms": delays["ms"]}
            cp.update(extra)
            if rnd.get("keep"):
                cp["dumps"] = {"screen": screen, "background": background, "dac_set": dac_set, "records": raw}
            out["checkpoints"].append(cp)

        def on_call_turn(mm):
            if not turns:
                raise Exhausted("turn %d" % (state["turn"] + 1))
            t = turns.pop(0)
            state["turn"] += 1
            player = mm.cpu.pop()  # argument of O7:040C (the procedure is skipped)
            rec = feld.RECORD + feld.RECORD_SIZE * player
            for k in ("angle", "power", "weapon"):
                p.wds(mm, rec + FIELDS[k][0], t[k])
            p.wds(mm, rec + 0x26, t.get("guidance", 0))
            if t.get("guidance"):
                p.wds(mm, rec + 0x28, t["target"])
                for bit, ofs in ((1, 0x2A), (2, 0x2C)):
                    if t["guidance"] & bit:
                        p.wds(mm, rec + ofs, max(1, p.rds(mm, rec + ofs, signed=True)))
            stock = rec + 0x2E + 2 * t["weapon"]
            need = t["power"] if t["weapon"] == 5 else 1
            p.wds(mm, stock, max(need, p.rds(mm, stock, signed=True)))
            if t["weapon"] == 7:
                p.wds(mm, INPUT_MODE + 2 * player, 1)
                state["mirv"] = (t.get("pattern", 0), t.get("detonation", 0))
            out["turns"].append({"player": player, "pass": p.rds(mm, 0x47EA), **t})
            mm.cpu.ip = O4_AFTER_TURN
            checkpoint("turn")
            return False

        def on_post(mm):
            checkpoint("post")
            return False

        def on_death_random(mm):
            j = state["deaths"]
            if j < len(death_seeds):
                p.wds(mm, feld.RANDSEED, death_seeds[j], 4)
            state["victim"] = p.rds(mm, 0x4740)
            state["seed_before"] = p.rds(mm, feld.RANDSEED, 4)
            return False

        def on_death_choice(mm):
            k = mm.cpu.R[AX]
            out["deaths"].append({"turn": state["turn"], "victim": state["victim"], "choice": k,
                                  "picture": DEATHS.get(k), "seed_before": "%08X" % state["seed_before"]})
            state["deaths"] += 1
            return False

        def on_death_done(mm):
            d = out["deaths"][-1]
            checkpoint("death", victim=d["victim"], choice=d["choice"])
            return False

        def on_lightning(mm):
            j = len(out["bolts"])
            if j < len(bolt_seeds):
                p.wds(mm, feld.RANDSEED, bolt_seeds[j], 4)
            out["bolts"].append({"turn": state["turn"], "pass": p.rds(mm, 0x47EA),
                                 "seed_before": "%08X" % p.rds(mm, feld.RANDSEED, 4)})
            return False

        def on_lightning_done(mm):
            checkpoint("bolt")
            return False

        def on_round_end(mm):
            out["round_end"] = {"turn": state["turn"], "winner": p.rds(mm, 0x47E0, signed=True)}
            return False

        def on_next(mm):
            checkpoint("next")
            return False

        def on_return(mm):
            checkpoint("end")
            return True

        def on_impact(mm):
            st = feld.lin(mm.cpu.S[SS], mm.cpu.R[SP])
            a = bytes(mm.mem[st + 4:st + 4 + 16])
            out["impacts"].append("%d:%d,%s,%s,%d" % (state["turn"], struct.unpack_from("<H", a, 0)[0], a[8:14].hex(),
                                                       a[2:8].hex(), struct.unpack_from("<H", a, 14)[0]))
            return False

        def on_damage(mm):
            st = feld.lin(mm.cpu.S[SS], mm.cpu.R[SP])
            ptr, _seg, kind, amount = struct.unpack_from("<HHHh", mm.mem, st + 4)
            out["damage"].append([state["turn"], amount, kind & 0xFF, (ptr - feld.RECORD) // feld.RECORD_SIZE, None])
            return False

        def on_damage_ret(mm):
            v = mm.cpu.R[AX]
            out["damage"][-1][4] = v - 65536 if v & 0x8000 else v
            return False

        def on_delay(mm):
            cc = mm.cpu
            ip, cs, ms = cc.pop(), cc.pop(), cc.pop()
            delays["n"] += 1
            delays["ms"] += ms
            cc.S[CS], cc.ip = cs, ip
            return False

        def mirv_choice(local, which):
            def f(mm):
                mm.cpu.ww(mm.cpu.S[SS], (mm.cpu.R[BP] - local) & 0xFFFF, state["mirv"][which])
                p.wds(mm, KEY_ENTER_FLAG, 1, 1)
                return False
            return f

        def mirv_after(mm):
            p.wds(mm, KEY_ENTER_FLAG, 0, 1)
            return False

        o4 = p.ovseg[4]
        for ofs, f in ((O4_CALL_TURN, on_call_turn), (O4_AFTER_POST, on_post),
                       (O4_DEATH_RANDOM, on_death_random), (O4_DEATH_CHOICE, on_death_choice),
                       (O4_DEATH_DONE, on_death_done), (O4_LIGHTNING_CALL, on_lightning),
                       (O4_LIGHTNING_DONE, on_lightning_done), (O4_ROUND_END, on_round_end),
                       (O4_NOT_OVER, on_next), (O4_RETURN, on_return),
                       (O4_MIRV_PATTERN_LOOP, mirv_choice(0x102, 0)), (O4_MIRV_DETONATION_LOOP, mirv_choice(0x108, 1)),
                       (O4_MIRV_AFTER_DIALOG, mirv_after)):
            m.traps[feld.lin(o4, ofs)] = f
        m.traps[feld.lin(p.ovseg[6], O6_IMPACT)] = on_impact
        m.traps[feld.lin(p.ovseg[10], O10_DAMAGE)] = on_damage
        m.traps[feld.lin(p.ovseg[10], O10_DAMAGE_RET)] = on_damage_ret
        m.traps[feld.lin(fm.LOAD_SEG + CRT_DELAY[0], CRT_DELAY[1])] = on_delay
        out["start"] = {"player": start, "randseed": "%08X" % p.rds(m, feld.RANDSEED, 4),
                        "wind": p.rds(m, 0xE8A2, signed=True), "crumble": p.rds(m, 0xE8A4),
                        "pass": p.rds(m, 0x47EA), "ss": p.rds(m, 0xE8CA), "trail": p.rds(m, 0xE8B5, 1)}
        try:
            m.run(limit=c.steps + rnd.get("limit", 20_000_000_000))
        except Exhausted as e:
            out["status"] = "exhausted at %s" % e.args[0]
            state["turn"] += 1  # the turn that has no script entry
            checkpoint("exhausted")

    result, _ = p.run_case(case, after=after)
    out["case"] = {"wall_round": result["wall_round"], "wind": result["wind"], "tanks": result["tanks"]}
    return out


def summary(out):
    s = {k: v for k, v in out.items() if k != "checkpoints"}
    s["checkpoints"] = [{k: v for k, v in cp.items() if k != "dumps"} for cp in out["checkpoints"]]
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


def round_from_args(args):
    return {"turns": parse_turns(args.turns), "sets": parse_sets(args.set), "sound": args.sound,
            "crumble": args.crumble, "pass": args.pass_, "death_seeds": parse_seeds(args.death_seed),
            "lightning_seeds": parse_seeds(args.lightning_seed)}


def cmd_run(p, args):
    case = feld.parse_case(args)
    rnd = round_from_args(args)
    rnd["keep"] = True
    out = run_round(p, case, rnd)
    d = args.out or os.path.join(args.root_out, "runde-%08X-n%d-w%d" % (case["seed"], case["players"], case["wall"]))
    os.makedirs(d, exist_ok=True)
    for i, cp in enumerate(out["checkpoints"]):
        for k, b in cp["dumps"].items():
            with open(os.path.join(d, "%02d-%s-%s.bin" % (i, cp["kind"], k)), "wb") as f:
                f.write(b)
    s = summary(out)
    with open(os.path.join(d, "result.json"), "w") as f:
        json.dump(s, f, indent=1)
    if args.json:
        print(json.dumps(s, indent=1))
    else:
        print_round(s)
    print(d)
    return 0 if out["status"] == "ok" else 2


def print_round(s):
    print("status", s["status"], "start", s["start"], "tanks", s["case"]["tanks"])
    for cp in s["checkpoints"]:
        alive = "".join("x" if r["alive"] else "-" for r in cp["records"])
        en = ",".join(str(r["energy"]) for r in cp["records"])
        extra = " victim %d choice %d" % (cp["victim"], cp["choice"]) if cp["kind"] == "death" else ""
        print("%-9s t%-2d pass %-3d seed %08X n %-5d r %-3d %s [%s] winner %d%s" % (
            cp["kind"], cp["turn"], cp["pass"], cp["randseed"], cp["random_n"], cp["random_real"], alive, en,
            cp["winner"], extra))
    print("deaths", s["deaths"])
    print("bolts", s["bolts"])


def seed_for(n, k, start=0):
    """First RandSeed >= start for which the next Random(n) returns k (TP: seed * 8088405h + 1, high word mod n)."""
    s = start
    while ((((s * 0x08088405 + 1) & 0xFFFFFFFF) >> 16) % n) != k:
        s += 1
    return s


# Fixed round list for the C# comparison. Case: (seed, players, wall, background, mask, wind option, invisible,
# no_fall); round: dict(turns, sets, sound, crumble, pass, death_seeds, lightning_seeds).
A = (0x12345678, 2, 0, 0, 0xFFFF, 2, False, False)    # tanks (33, 123) and (300, 107), wind -44, start player 2
B = (0x9E3779B9, 4, 0, 0, 0xFFFF, 2, False, False)    # tanks (33, 129), (115, 158), (197, 152), (279, 171), start 3
KILL = "45,230,1"                                     # player 2 hits next to player 1 (Baby Missile)
DEATH_SEEDS = [seed_for(7, k, 0x1000 * (k + 1)) for k in range(7)]
NEXT = "45,100,1"                                      # player 3 (case B) shoots past its neighbour
NEIGHBOUR = ["2:energy=0", "3:x=125", "3:y=158"]      # record 3 next to record 2, which dies in the first death loop


def R(case, turns, sets=(), **kw):
    d = {"turns": parse_turns(turns), "sets": parse_sets(sets), "turns_text": turns, "sets_text": " ".join(sets)}
    d.update(kw)
    return case, d


ROUNDS = [
    R(A, KILL, ["1:energy=5"]),                                                    # 0 kill, natural choice
] + [
    R(A, KILL, ["1:energy=5"], death_seeds=[DEATH_SEEDS[k]]) for k in range(7)     # 1..7 every death picture
] + [
    R(A, KILL, ["1:energy=5"], death_seeds=[DEATH_SEEDS[k]], sound=True) for k in (0, 1, 2, 5, 6)  # 8..12 sound
] + [
    R(A, "89,100,1", ["2:energy=5"]),                                              # 13 suicide: -1000, winner 1
    R(A, KILL, ["1:energy=5", "1:team=1", "2:team=1"]),                            # 14 same team: -1000, winner team
    R(A, "45,260,1;45,262,1;45,200,1;45,270,1;45,265,1;45,250,1;45,240,1;45,230,1;45,255,1;45,245,1",
      ["1:energy=40", "2:energy=40"]),                                             # 15 several turns until a kill
    R(A, "45,260,0;45,240,0;45,260,0;45,240,0", ["1:energy=20", "2:energy=20"], pass_=40),  # 16 lightning
    R(A, "45,260,0;45,240,0;45,260,0", ["1:energy=20", "2:energy=20"], pass_=40, sound=True),  # 17 with sound
    R(A, "45,260,0;45,240,0;45,260,0;45,240,0", ["1:energy=3", "2:energy=3"], pass_=60),  # 18 lightning kills
    R(A, "45,260,0;45,240,0;45,260,0;45,240,0;45,260,0;45,240,0;45,260,0;45,240,0;45,260,0;45,240,0;45,260,0;"
         "45,240,0;45,260,0;45,240,0", pass_=9),                                   # 19 passes 10, 11, ...: Random(100)
    R(B, "90,100,4", ["2:energy=5", "3:energy=5", "1:alive=0", "4:alive=0"]),      # 20 two deaths, nobody left: winner 0
    R(B, "90,100,4", ["2:energy=5", "3:energy=200"]),                              # 21 kill by a nuke, round goes on
    R(B, "90,100,4;75,200,1;45,200,1;60,250,1", ["2:energy=5", "2:team=1", "3:team=1", "1:team=2", "4:team=2"]),
    #                                                                                22 teams of two: team kill
    R(B, "90,100,4", ["2:energy=1", "3:energy=1", "4:energy=1", "1:energy=1"], death_seeds=[DEATH_SEEDS[0]] * 4),
    #                                                                                23 splinters may kill more
    R(A[:7] + (True,), KILL, ["1:energy=5"], crumble=0),                           # 24 no falling, Crumble 0
    R(A, "45,230,5;45,262,7,0,0,2,0;45,230,8", ["1:energy=60", "2:energy=60"]),    # 25 laser, MIRV, CRI in a round
] + [
    # Review P2-2 (#173): the pictures damage living neighbours. Record 2 starts dead (energy 0), record 3 stands next to it
    # (x 125 instead of 197) and shoots first; the shot only grazes it.
    R(B, NEXT, NEIGHBOUR + extra, death_seeds=[DEATH_SEEDS[k]]) for k, extra in (
        (0, []),                                                                   # 26 splinters: neighbour 91 -> 22
        (1, []),                                                                   # 27 particles: 91 -> 29
        (2, []),                                                                   # 28 splinters at once: also record 1
        (3, []),                                                                   # 29 sparks: neighbour dies, splinters
        (0, ["3:energy=40"]),                                                      # 30 splinters kill the neighbour
        (1, ["3:shield=30"]),                                                      # 31 particles on a shield
    )
]


ROW_HEADER = ("nr\tseed\tplayers\twall\tbackground\twind_opt\tinvisible\tno_fall\tturns\tsets\tsound\tcrumble\tpass\t"
              "death_seeds\tstatus\tstart\tss\ttrail\twind\tcp\tkind\tturn\tvictim\tchoice\tpass_now\tto_move\twinner\trandseed\t"
              "random_n\trandom_real\tfield\tbackground_sha\tdac_set\tdac_entries\trecords_raw\tshot_log\tdelays\t"
              "delay_ms\trecords")


def short_records(recs):
    return " ".join("/".join(str(r[k]) for k in SHORT) for r in recs)


def round_rows(p, i):
    case_t, rnd = ROUNDS[i]
    case = dict(zip(("seed", "players", "wall", "background", "mask", "wind", "invisible", "no_fall"), case_t))
    rnd = dict(rnd)
    if "pass_" in rnd:
        rnd["pass"] = rnd.pop("pass_")
    out = run_round(p, case, rnd)
    head = [str(i), "%08X" % case["seed"], str(case["players"]), str(case["wall"]), str(case["background"]),
            str(case["wind"]), str(int(case["invisible"])), str(int(case["no_fall"])), rnd["turns_text"],
            rnd["sets_text"], str(int(bool(rnd.get("sound")))), "" if rnd.get("crumble") is None else str(rnd["crumble"]),
            "" if rnd.get("pass") is None else str(rnd["pass"]),
            ",".join("%08X" % v for v in rnd.get("death_seeds") or ()), out["status"],
            "%d/%s" % (out["start"]["player"], out["start"]["randseed"]), str(out["start"]["ss"]),
            str(out["start"]["trail"]), str(out["start"]["wind"])]
    rows = []
    for j, cp in enumerate(out["checkpoints"]):
        rows.append("\t".join(head + [str(j), cp["kind"], str(cp["turn"]), str(cp.get("victim", "")),
                                      str(cp.get("choice", "")), str(cp["pass"]), str(cp["to_move"]),
                                      str(cp["winner"]), "%08X" % cp["randseed"], str(cp["random_n"]),
                                      str(cp["random_real"]), cp["field"], cp["background"], cp["dac_set"],
                                      str(cp["dac_entries"]), cp["records_raw"], cp["shot_log"], str(cp["delays"]),
                                      str(cp["delay_ms"]), short_records(cp["records"])]))
    return rows, summary(out)


def _round_job(job):
    gamedata, font, i = job
    return i, round_rows(feld.Probe(gamedata, font), i)


def cmd_runden(p, args):
    idx = args.only if args.only else range(len(ROUNDS))
    font = p.font if hasattr(p, "font") else None
    jobs = [(args.gamedata, font, i) for i in idx]
    results = {}
    if args.jobs > 1:
        with ProcessPoolExecutor(args.jobs) as ex:
            for i, r in ex.map(_round_job, jobs):
                results[i] = r
                print("Runde %d: %s, %d Messpunkte" % (i, r[1]["status"], len(r[0])), flush=True)
    else:
        for job in jobs:
            i, r = _round_job(job)
            results[i] = r
            print("Runde %d: %s, %d Messpunkte" % (i, r[1]["status"], len(r[0])), flush=True)
    os.makedirs(args.root_out, exist_ok=True)
    name = "runden-hashes.tsv" if not args.only else "runden-hashes-teil.tsv"
    with open(os.path.join(args.root_out, name), "w") as f:
        f.write(ROW_HEADER + "\n")
        for i in sorted(results):
            for row in results[i][0]:
                f.write(row + "\n")
    with open(os.path.join(args.root_out, name.replace(".tsv", ".json")), "w") as f:
        json.dump({str(i): results[i][1] for i in sorted(results)}, f, indent=1)
    print(os.path.join(args.root_out, name))
    return 0 if all(results[i][1]["status"] == "ok" for i in results) else 2


def cmd_runden_cs(p, args):
    """analyse/feld/runden-hashes.tsv -> tests/BombRedone.Core.Tests/Round/RoundProbeData.cs (hashes and numbers only)."""
    import csv
    root = os.path.normpath(os.path.join(HERE, "..", ".."))
    with open(os.path.join(args.root_out, "runden-hashes.tsv")) as f:
        rows = list(csv.DictReader(f, delimiter="\t"))

    def q(v):
        return "null" if v == "" else '"%s"' % v

    def n(v):
        return "null" if v == "" else v

    cases, points = [], []
    for r in rows:
        if r["cp"] == "0":
            cases.append("        new(%s)," % ", ".join([
                r["nr"], q(r["seed"]), r["players"], r["wall"], r["background"], r["wind_opt"],
                "true" if r["invisible"] == "1" else "false", "true" if r["no_fall"] == "1" else "false",
                q(r["turns"]), q(r["sets"]), "true" if r["sound"] == "1" else "false", n(r["crumble"]), n(r["pass"]),
                q(r["death_seeds"]), q(r["status"]), q(r["start"]), r["ss"], "true" if r["trail"] == "1" else "false",
                r["wind"]]))
        points.append("        new(%s)," % ", ".join([
            r["nr"], r["cp"], q(r["kind"]), r["turn"], n(r["victim"]), n(r["choice"]), r["pass_now"], r["to_move"],
            r["winner"], q(r["randseed"]), q(r["field"]), q(r["background_sha"]), q(r["dac_set"]), q(r["shot_log"]),
            q(r["records"])]))
    text = """namespace BombRedone.Core.Tests.Round;

/// <summary>
/// Generated from analyse/feld/runden-hashes.tsv (<c>python3 -I tools/reverse-engineering/tod.py runden</c>, then
/// <c>runden-cs</c>); the rounds are listed with comments in ROUNDS of that script. Hashes and numbers only, no bytes of the
/// originals.
/// </summary>
internal static class RoundProbeData
{
    public static readonly RoundProbeTests.RoundCase[] Cases =
    [
%s
    ];

    public static readonly RoundProbeTests.ProbePoint[] Points =
    [
%s
    ];
}
""" % ("\n".join(cases), "\n".join(points))
    out = os.path.join(root, "tests", "BombRedone.Core.Tests", "Round", "RoundProbeData.cs")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w") as f:
        f.write(text)
    print(out, len(cases), len(points))
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
    r.add_argument("--turns", default="")
    r.add_argument("--set", nargs="*", help="FIELD=V or PLAYER:FIELD=V (fields: %s)" % " ".join(FIELDS))
    r.add_argument("--sound", action="store_true")
    r.add_argument("--crumble", type=int)
    r.add_argument("--pass", dest="pass_", type=int)
    r.add_argument("--death-seed")
    r.add_argument("--lightning-seed")
    r.add_argument("--json", action="store_true")
    r.add_argument("--out")
    h = sub.add_parser("runden")
    h.add_argument("--only", type=int, nargs="*")
    h.add_argument("--jobs", type=int, default=1)
    sub.add_parser("runden-cs")
    args = ap.parse_args()
    font = None
    if os.path.exists(args.font):
        with open(args.font, "rb") as f:
            font = f.read()
    p = feld.Probe(args.gamedata, font)
    p.font = font
    return {"befunde": cmd_befunde, "run": cmd_run, "runden": cmd_runden, "runden-cs": cmd_runden_cs}[args.cmd](p, args)


if __name__ == "__main__":
    sys.exit(main())
