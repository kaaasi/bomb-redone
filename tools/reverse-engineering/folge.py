#!/usr/bin/env python3
"""Probe for the game sequence (spur spielfolge, runde 3): runs the original code of the game sequence O3 of
BOMB.OVR in the emulated PC of feld_machine.py on prepared player records and records what it changes.

Docs: docs/analyse/runde.md section 1, docs/anforderungen/wertung-einkauf.md. The script holds no bytes of the
originals (AGENTS.md section 2.1): code regions are checked by SHA-256 only; outputs go to analyse/folge/
(protected, git-ignored). Only feld.py (boot, overlays) and feld_machine.py are imported, nothing there is changed.

Usage:
  folge.py [--gamedata DIR] befunde [--print]     check the code regions the probe relies on
  folge.py [...] cases [--only I ...]             fixed case list -> analyse/folge/cases.json and one line per case
                                                  on stdout; the C# tests compare against these values
  folge.py [...] csharp                           C# test data (BattleProbeData.cs) from cases.json on stdout

Case kinds (every case starts from a fresh boot, O2:0216(n) for n = 1..10 and the inputs of the case):
  swap      far call O3:0000(a, b)                                    (S4)
  shuffle   RandSeed := seed, far call O3:2417                        (S3)
  rating    far call O3:5AEF (S5; screen hash at the key wait)
  team      far call O3:6696 with Team Play (team rating; sorted slots [bp-126h] and screen hash at the key wait)
  bonus     O3:7847 entered at O3:7885 with DS:47E0 = winner team, stopped at O3:796B   (S2)
  shop      far call O3:0E1F (order of the shops, record at every key wait, random calls)
  battle    far call O3:7847 over all games; the round O4[0] is a test double that only sets the result of
            the round (alive +23h, score +13h, wins +6Ah, winner team DS:47E0); snapshot of all records after
            every phase   (S1)

Records are identified by their name (+01h, "Player i" from O2:0216(i)); the intelligence pointers DS:3BE8 + 4i
point to dummy objects outside the program memory (segment E800h + i) so that O3:0000 can write their player number
(Self+10h, 0129:01BF). Keyboard: INT 16h is answered per wait from the case's key list or, by default, with one key per procedure
(Enter in the ratings, Esc in the shop); an empty list at a wait is an error.
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
from feld_cpu import AX, CS, SS, BP  # noqa: E402

RECORD, RECORD_SIZE = feld.RECORD, feld.RECORD_SIZE
TEAM, TEAM_SIZE = 0x4838, 0x35
CONTROL, INTEL = 0xE5A6, 0x3BE8
NUM_PLAYERS, WINNER_TEAM, GAME_NO, GAMES = 0x47DA, 0x47E0, 0x47E2, 0xE8A6
ALL_COMPUTER, SAVE_DIALOG, LOADED, AUTOSAVE, TEAM_PLAY = 0x4812, 0x4816, 0x4817, 0x4818, 0xE8BC
INTEL_SEG = 0xE800
O3_SWAP, O3_SHUFFLE, O3_SHOP, O3_RATING, O3_TEAM, O3_SEQUENCE = 0x0000, 0x2417, 0x0E1F, 0x5AEF, 0x6696, 0x7847
O3_SEQ_SHUFFLE, O3_SEQ_BONUS, O3_SEQ_AFTER_BONUS, O3_SEQ_END = 0x7872, 0x7885, 0x796B, 0x7A0D
O3_SHOP_FRAME = 0x0E22   # after mov bp,sp of O3:0E1F; the player loop counter is [bp-0Ch]
O3_TEAM_FRAME = 0x6699   # after mov bp,sp of O3:6696; team slots in sorted order [bp-126h + 2i], i = 1..10
O4_ROUND = 0x44A7
KEY_ENTER, KEY_ESC, KEY_SPACE = 0x1C0D, 0x011B, 0x3920
DEFAULT_KEYS = {"rating": KEY_ENTER, "team": KEY_ENTER, "shop": KEY_ESC}

REGIONS = {
    "O3 Tausch": (3, 0x0000, 0x012E),
    "O3 Einkauf Spielerschleife": (3, 0x0E1F, 0x0F65),
    "O3 Mischen": (3, 0x2417, 0x2477),
    "O3 Wertung Sortierung": (3, 0x5AEF, 0x5C70),
    "O3 Teamwertung Summen und Sortierung": (3, 0x6696, 0x6CD2),
    "O3 Spielfolge": (3, 0x7847, 0x7A0D),
}
REGION_HASHES = {
    "O3 Tausch":
        "7376e5d5e3f980fa27250d0e685adbfed3527f5f2cc7ed1e6c5974bf191455b1",
    "O3 Einkauf Spielerschleife":
        "db56b7e0b3cc00b19222a1cdd55352727f3ca039dd728c03cadc38d5db50d1ce",
    "O3 Mischen":
        "6e85cd5fa25fb8a508a512e28373e50b32ff8095c0807519592399026ba7218a",
    "O3 Wertung Sortierung":
        "3a481e4cdb0030ba7233a595c62f15dd61791ac893185552775dec71bfc15d5a",
    "O3 Teamwertung Summen und Sortierung":
        "b31351eccc4705d084d3a113f324452601ef7960d9a88f3139082c6eecb686fa",
    "O3 Spielfolge":
        "8119b91b5b60c2ecae7860ae5dd9ba9f43c11ba30eef2496f0fc177727c921d7",
}


def sha(b):
    return hashlib.sha256(b).hexdigest()


class Folge:
    def __init__(self, probe):
        self.p = probe

    # ---- memory helpers ----
    def rec(self, i, ofs):
        return RECORD + RECORD_SIZE * i + ofs

    def wds(self, m, ofs, value, size=2):
        self.p.wds(m, ofs, value, size)

    def rds(self, m, ofs, size=2, signed=False):
        return self.p.rds(m, ofs, size, signed)

    def name(self, m, i):
        a = feld.lin(self.p.ds(), self.rec(i, 0x01))
        n = m.mem[a]
        return bytes(m.mem[a + 1:a + 1 + n]).decode("latin-1")

    # ---- set up ----
    def prepare(self, case):
        p = self.p
        m = p.boot()
        c = m.cpu
        for n in range(1, 11):
            m.far_call(p.ovseg[2], feld.O2_DEFAULT_PLAYER, args=(n,), limit=c.steps + 2_000_000)
        n = case["players"]
        self.wds(m, NUM_PLAYERS, n)
        self.wds(m, GAME_NO, case.get("game", 1))
        self.wds(m, GAMES, case.get("games", 3))
        for ofs in (ALL_COMPUTER, SAVE_DIALOG, LOADED, AUTOSAVE):
            self.wds(m, ofs, 0, 1)
        self.wds(m, TEAM_PLAY, 1 if case.get("teams") else 0, 1)
        controls = case.get("controls") or [1] * n
        for i in range(1, 11):
            self.wds(m, INTEL + 4 * i, (INTEL_SEG + i) << 16, 4)
            m.mem[feld.lin(INTEL_SEG + i, 0):feld.lin(INTEL_SEG + i, 0x20)] = bytes(0x20)
            self.wds(m, CONTROL + 2 * i, controls[i - 1] if i <= n else 1)
        for key, ofs, size in (("score", 0x13, 4), ("spent", 0x17, 4), ("wins", 0x6A, 2), ("alive", 0x23, 1)):
            for i, v in enumerate(case.get(key) or [], 1):
                self.wds(m, self.rec(i, ofs), v, size)
        if case.get("teams"):
            for i, t in enumerate(case["teams"], 1):
                self.wds(m, self.rec(i, 0x66), t)
            for t in range(1, 11):
                members = sum(1 for x in case["teams"] if x == t)
                a = feld.lin(p.ds(), TEAM + TEAM_SIZE * t)
                text = ("Team %d" % t).encode("latin-1")
                m.mem[a:a + 14] = bytes([len(text)]) + text + bytes(13 - len(text))
                self.wds(m, TEAM + TEAM_SIZE * t + 0x0E, 0, 1)
                self.wds(m, TEAM + TEAM_SIZE * t + 0x0F, members)
        return m

    def records(self, m, n):
        """State of records 1..n: name, number +00h, control, intelligence pointer and its Self+10h, score, wins."""
        out = []
        for i in range(1, n + 1):
            ptr = self.rds(m, INTEL + 4 * i, 4)
            seg = ptr >> 16
            out.append({
                "name": self.name(m, i), "number": m.mem[feld.lin(self.p.ds(), self.rec(i, 0))],
                "control": self.rds(m, CONTROL + 2 * i), "intel": seg - INTEL_SEG,
                "intel_number": m.cpu.rw(seg, 0x10), "team": self.rds(m, self.rec(i, 0x66)),
                "score": self.rds(m, self.rec(i, 0x13), 4, signed=True),
                "spent": self.rds(m, self.rec(i, 0x17), 4, signed=True),
                "wins": self.rds(m, self.rec(i, 0x6A)), "alive": m.mem[feld.lin(self.p.ds(), self.rec(i, 0x23))],
                "stock": sha(bytes(m.mem[feld.lin(self.p.ds(), self.rec(i, 0x2A)):
                                         feld.lin(self.p.ds(), self.rec(i, 0x5E))]))[:16],
            })
        return out

    def instrument(self, m, keys, events):
        """Counts Random calls, answers INT 16h from keys and logs every wait with the active O3 procedure."""
        p = self.p
        c = m.cpu
        load = fm.LOAD_SEG
        state = {"random_n": 0, "random_real": 0, "proc": None, "shop_bp": None, "waiting": False}

        def count(key):
            def f(mm):
                state[key] += 1
                return False
            return f

        m.traps[feld.lin(load + feld.RANDOM_N[0], feld.RANDOM_N[1])] = count("random_n")
        m.traps[feld.lin(load + feld.RANDOM_REAL[0], feld.RANDOM_REAL[1])] = count("random_real")
        names = {O3_SHUFFLE: "shuffle", O3_RATING: "rating", O3_TEAM: "team", O3_SHOP: "shop"}
        for ofs, name in names.items():
            def enter(mm, name=name):
                state["proc"] = name
                events.append({"event": "enter", "proc": name, "game": self.rds(mm, GAME_NO),
                               "randseed": "%08X" % self.rds(mm, feld.RANDSEED, 4),
                               "random_n": state["random_n"], "random_real": state["random_real"]})
                return False
            m.traps[feld.lin(p.ovseg[3], ofs)] = enter

        def shop_frame(mm):
            state["shop_bp"] = mm.cpu.R[BP]
            return False

        m.traps[feld.lin(p.ovseg[3], O3_SHOP_FRAME)] = shop_frame

        def team_frame(mm):
            state["team_bp"] = mm.cpu.R[BP]
            return False

        m.traps[feld.lin(p.ovseg[3], O3_TEAM_FRAME)] = team_frame

        def int16(mm):
            ah = c.R[AX] >> 8
            if ah not in (0x00, 0x01, 0x10, 0x11):
                return False
            if not state["waiting"]:
                state["waiting"] = True
                ev = {"event": "wait", "proc": state["proc"], "game": self.rds(mm, GAME_NO),
                      "screen": sha(bytes(mm.mem[0xA0000:0xA0000 + 64000])), "dac": sha(bytes(mm.dac)),
                      "random_n": state["random_n"], "random_real": state["random_real"]}
                if state["proc"] == "shop" and state["shop_bp"] is not None:
                    ev["record"] = c.rw(c.S[SS], (state["shop_bp"] - 0x0C) & 0xFFFF)
                    ev["name"] = self.name(mm, ev["record"])
                if state["proc"] == "team" and state.get("team_bp") is not None:
                    bp = state["team_bp"]
                    ev["team_order"] = [c.rw(c.S[SS], (bp - 0x126 + 2 * i) & 0xFFFF) for i in range(1, 11)]
                    ev["team_counted"] = c.rw(c.S[SS], (bp - 0x128) & 0xFFFF)
                events.append(ev)
                if isinstance(keys, dict):
                    state["key"] = keys[state["proc"]]
                elif keys:
                    state["key"] = keys.pop(0)
                else:
                    raise RuntimeError("wait without a key in %s" % state["proc"])
                ev["key"] = "%04X" % state["key"]
            if ah in (0x01, 0x11):
                c.R[AX] = state["key"]
                mm.ret_zf(False)
            else:
                c.R[AX] = state["key"]
                state["waiting"] = False
            return True

        m.int_hooks[0x16] = int16
        return state

    # ---- cases ----
    def run(self, case):
        p = self.p
        m = self.prepare(case)
        c = m.cpu
        n = case["players"]
        kind = case["kind"]
        events = []
        keys = case.get("keys", DEFAULT_KEYS)
        keys = dict(keys) if isinstance(keys, dict) else list(keys)
        state = self.instrument(m, keys, events)
        result = {"case": case}
        o3 = p.ovseg[3]
        limit = c.steps + 400_000_000
        if kind == "swap":
            m.far_call(o3, O3_SWAP, args=(case["a"], case["b"]), limit=limit)
        elif kind == "shuffle":
            self.wds(m, feld.RANDSEED, case["seed"], 4)
            m.far_call(o3, O3_SHUFFLE, limit=limit)
        elif kind in ("rating", "team", "shop"):
            if "seed" in case:
                self.wds(m, feld.RANDSEED, case["seed"], 4)
            entry = {"rating": O3_RATING, "team": O3_TEAM, "shop": O3_SHOP}[kind]
            m.far_call(o3, entry, limit=limit)
        elif kind == "bonus":
            def to_bonus(mm):
                self.wds(mm, WINNER_TEAM, case["winner"])
                mm.cpu.ip = O3_SEQ_BONUS
                return False

            m.traps[feld.lin(o3, O3_SEQ_SHUFFLE)] = to_bonus
            m.traps[feld.lin(o3, O3_SEQ_AFTER_BONUS)] = lambda mm: True
            m.far_call(fm.LOAD_SEG + p.game.overlays[2]["stub"], 0x20, limit=limit)
            if c.ip != O3_SEQ_AFTER_BONUS:
                raise RuntimeError("O3:796B not reached")
        elif kind == "battle":
            self.wds(m, feld.RANDSEED, case["seed"], 4)
            results = list(case["rounds"])
            snaps = []

            def snap(label):
                snaps.append({"after": label, "game": self.rds(m, GAME_NO),
                              "randseed": "%08X" % self.rds(m, feld.RANDSEED, 4),
                              "random_n": state["random_n"], "random_real": state["random_real"],
                              "screen": sha(bytes(m.mem[0xA0000:0xA0000 + 64000])),
                              "records": self.records(m, n)})

            def round_stub(mm):
                # Test double for O4[0]: the result of the round, applied to the records by name.
                snap("shuffle")
                r = results.pop(0)
                by_name = {self.name(mm, i): i for i in range(1, n + 1)}
                for who, (alive, score, wins) in r["players"].items():
                    i = by_name[who]
                    self.wds(mm, self.rec(i, 0x23), alive, 1)
                    self.wds(mm, self.rec(i, 0x13), self.rds(mm, self.rec(i, 0x13), 4) + score, 4)
                    self.wds(mm, self.rec(i, 0x6A), self.rds(mm, self.rec(i, 0x6A)) + wins)
                self.wds(mm, WINNER_TEAM, r["winner"])
                ip = mm.cpu.pop()
                cs = mm.cpu.pop()
                mm.cpu.ip, mm.cpu.S[CS] = ip, cs
                return False

            def phase(label):
                def f(mm):
                    snap(label)
                    return False
                return f

            m.traps[feld.lin(p.ovseg[4], O4_ROUND)] = round_stub
            m.traps[feld.lin(o3, O3_SEQ_AFTER_BONUS)] = phase("bonus")
            m.traps[feld.lin(o3, 0x7984)] = phase("rating")       # return from O3:5AEF
            m.traps[feld.lin(o3, 0x79A5)] = phase("team")         # after the team rating and its dialog
            m.traps[feld.lin(o3, 0x79B2)] = phase("shop")         # after O3:0E1F (or skipped)
            m.traps[feld.lin(o3, 0x79FD)] = phase("wipe")         # screen wiped from the middle, before GameNo + 1
            m.traps[feld.lin(o3, O3_SEQ_END)] = lambda mm: True
            m.far_call(fm.LOAD_SEG + p.game.overlays[2]["stub"], 0x20, limit=limit)
            if c.ip != O3_SEQ_END:
                raise RuntimeError("O3:7A0D not reached")
            result["snapshots"] = snaps
        else:
            raise ValueError(kind)
        result["keys_left"] = 0 if isinstance(keys, dict) else len(keys)
        result["events"] = events
        result["records"] = self.records(m, n)
        result["randseed"] = "%08X" % self.rds(m, feld.RANDSEED, 4)
        result["random_n"] = state["random_n"]
        result["random_real"] = state["random_real"]
        result["log"] = [x for x in m.log if not x.startswith(("open ", "find ", "int 10 ax=10"))]
        return result


# Fixed case list. Scores and wins are inputs per record 1..N; teams are the team words +66h.
CASES = [
    {"kind": "swap", "players": 2, "a": 1, "b": 2, "controls": [1, 2]},
    {"kind": "swap", "players": 5, "a": 2, "b": 4, "controls": [1, 2, 3, 1, 2], "score": [10, 20, 30, 40, 50]},
    {"kind": "swap", "players": 10, "a": 10, "b": 1, "controls": [1, 2, 3, 4, 5, 6, 7, 3, 2, 6]},
    {"kind": "swap", "players": 3, "a": 2, "b": 2, "controls": [1, 2, 3]},
    {"kind": "shuffle", "players": 2, "seed": 0x12345678, "controls": [1, 3]},
    {"kind": "shuffle", "players": 2, "seed": 0x00000000, "controls": [1, 3]},
    {"kind": "shuffle", "players": 5, "seed": 0x12345678, "controls": [1, 2, 3, 1, 3]},
    {"kind": "shuffle", "players": 5, "seed": 0xDEADBEEF, "controls": [3, 1, 2, 3, 1]},
    {"kind": "shuffle", "players": 10, "seed": 0x9E3779B9, "controls": [1, 2, 3, 4, 5, 6, 7, 1, 2, 3]},
    {"kind": "shuffle", "players": 10, "seed": 0xFFFFFFFF, "controls": [7, 6, 5, 4, 3, 2, 1, 1, 1, 1]},
    {"kind": "rating", "players": 2, "score": [1000, 2500], "wins": [1, 0]},
    {"kind": "rating", "players": 5, "score": [300, -200, 300, 5000, -200],
     "wins": [1, 2, 1, 0, 2]},
    {"kind": "rating", "players": 10, "keys": [KEY_ENTER],
     "score": [0, 100, -100, 100, 7000, 0, -32768, 65536, 100, 1], "wins": [0, 1, 1, 1, 0, 3, 3, 0, 1, 0]},
    {"kind": "rating", "players": 3, "game": 3, "games": 3, "score": [5, 5, 5],
     "wins": [1, 1, 1]},
    {"kind": "team", "players": 6, "teams": [1, 2, 3, 1, 2, 3],
     "score": [1000, 200, 300, 0, 900, 300], "wins": [1, 0, 1, 0, 1, 1]},
    {"kind": "team", "players": 7, "teams": [2, 2, 5, 5, 5, 7, 7],
     "score": [100, 100, 0, 50, 50, 300, -100], "wins": [1, 0, 0, 1, 1, 1, 0]},
    {"kind": "team", "players": 9, "teams": [3, 1, 3, 1, 2, 2, 3, 2, 1],
     "score": [10, 20, 30, 40, 50, 60, 70, 80, 90], "wins": [1, 1, 0, 1, 2, 0, 1, 1, 0]},
    {"kind": "bonus", "players": 3, "winner": 2, "alive": [0, 1, 0], "score": [100, 200, 300], "wins": [0, 0, 0]},
    {"kind": "bonus", "players": 6, "winner": 3, "teams": [1, 3, 2, 3, 3, 1], "alive": [0, 1, 0, 0, 1, 1],
     "score": [0, 0, 0, 0, 0, 0], "wins": [0, 0, 0, 0, 0, 0]},
    {"kind": "bonus", "players": 10, "winner": 1, "teams": [1, 1, 1, 2, 2, 2, 3, 3, 3, 1], "alive": [1] * 10,
     "score": [-5] * 10, "wins": [7] * 10},
    {"kind": "bonus", "players": 4, "winner": 0, "alive": [1, 0, 0, 0], "score": [1, 2, 3, 4], "wins": [0] * 4},
    {"kind": "shop", "players": 3, "seed": 0x12345678, "score": [0, 5000, 100000]},
    {"kind": "shop", "players": 5, "seed": 0x12345678,
     "score": [20000, 20000, 20000, 20000, 20000], "spent": [0, 5000, 19000, 20000, 0]},
    {"kind": "shop", "players": 2, "seed": 0x12345678, "keys": [KEY_SPACE, KEY_SPACE, KEY_ESC], "score": [100000, 0]},
    {"kind": "battle", "players": 3, "games": 3, "seed": 0x12345678, "score": [0, 0, 0], "wins": [0, 0, 0],
     "rounds": [
         {"winner": 2, "players": {"Player 1": [0, 1500, 0], "Player 2": [1, 3000, 0], "Player 3": [0, -1000, 0]}},
         {"winner": 3, "players": {"Player 1": [0, 0, 0], "Player 2": [0, 1500, 0], "Player 3": [1, 1500, 0]}},
         {"winner": 3, "players": {"Player 1": [0, 500, 0], "Player 2": [0, 0, 0], "Player 3": [1, 1500, 0]}},
     ]},
    {"kind": "battle", "players": 5, "games": 2, "seed": 0xDEADBEEF, "teams": [1, 2, 1, 2, 3],
     "score": [0] * 5, "wins": [0] * 5,
     "rounds": [
         {"winner": 1, "players": {"Player 1": [1, 1500, 0], "Player 3": [0, 0, 0], "Player 2": [0, -1000, 0]}},
         {"winner": 3, "players": {"Player 5": [1, 4500, 0]}},
     ]},
]


def cmd_befunde(p, args):
    ok = True
    for key, (ovl, s, e) in REGIONS.items():
        got = sha(p.region_bytes("ovl", ovl, s, e))
        if args.print:
            print(key, "O%d %04X-%04X" % (ovl, s, e), got)
            continue
        good = got == REGION_HASHES[key]
        ok &= good
        print(("ok    " if good else "FEHLT ") + key)
    return 0 if ok else 1


def summary(i, r):
    case = r["case"]
    order = ",".join(x["name"].replace("Player ", "") for x in r["records"])
    waits = sum(1 for e in r["events"] if e["event"] == "wait")
    return "%d\t%s\t%d\t%s\t%s\t%d\t%d\t%d\t%d" % (i, case["kind"], case["players"], order, r["randseed"],
                                                   r["random_n"], r["random_real"], waits, r["keys_left"])


def cmd_cases(p, args):
    f = Folge(p)
    out = []
    print("nr\tart\tspieler\treihenfolge\trandseed\trandom_n\trandom_real\twarten\ttasten_uebrig")
    for i, case in enumerate(CASES):
        if args.only and i not in args.only:
            continue
        r = f.run(case)
        r["index"] = i
        out.append(r)
        print(summary(i, r))
    os.makedirs(args.root_out, exist_ok=True)
    path = os.path.join(args.root_out, "cases.json")
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(out, fh, indent=1, sort_keys=True)
    print("%s: %d Fälle" % (path, len(out)), file=sys.stderr)
    return 0


def csv(values):
    return ",".join(str(v) for v in values)


def cmd_csharp(p, args):
    """C# test data of the record cases (swap, shuffle, rating, bonus) and of the team ratings from
    analyse/folge/cases.json, on stdout."""
    with open(os.path.join(args.root_out, "cases.json"), encoding="utf-8") as fh:
        results = json.load(fh)
    print("namespace BombRedone.Core.Tests.Battle;")
    print()
    print("/// <summary>")
    print("/// Generated from analyse/folge/cases.json (<c>python3 -I tools/reverse-engineering/folge.py cases</c>, then")
    print("/// <c>folge.py csharp</c>); the cases are listed in CASES of that script. Values only, no bytes of the originals.")
    print("/// </summary>")
    print("internal static class BattleProbeData")
    print("{")
    print("    public static readonly BattleRecordsProbeTests.RecordCase[] All =")
    print("    [")
    for r in results:
        c = r["case"]
        if c["kind"] not in ("swap", "shuffle", "rating", "bonus"):
            continue
        n = c["players"]
        recs = r["records"]
        print('        new(%d, "%s", %d, %d, %d, "%08X", "%s", "%s", "%s", "%s", "%s", %d, "%s", "%s", "%s", "%s", "%s", "%s"),' % (
            r["index"], c["kind"], n, c.get("a", 0), c.get("b", 0), c.get("seed", 0), csv(c.get("controls") or [1] * n),
            csv(c.get("score") or [0] * n), csv(c.get("wins") or [0] * n), csv(c.get("teams") or range(1, n + 1)),
            csv(c.get("alive") or [0] * n), c.get("winner", 0), csv(int(x["name"].split()[1]) for x in recs),
            csv(x["control"] for x in recs), csv(x["intel"] for x in recs), csv(x["score"] for x in recs),
            csv(x["wins"] for x in recs), r["randseed"]))
    print("    ];")
    print()
    print("    public static readonly BattleRecordsProbeTests.TeamCase[] Teams =")
    print("    [")
    for r in results:
        c = r["case"]
        if c["kind"] != "team":
            continue
        ev = next(e for e in r["events"] if "team_order" in e)
        order = [t if t in c["teams"] else 0 for t in ev["team_order"]]
        print('        new(%d, "%s", "%s", "%s", "%s", %d),' % (
            r["index"], csv(c["teams"]), csv(c["score"]), csv(c["wins"]), csv(order), ev["team_counted"]))
    print("    ];")
    print("}")
    return 0


def main():
    root = os.path.normpath(os.path.join(HERE, "..", ".."))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--gamedata", default=os.path.join(root, "gamedata"))
    ap.add_argument("--font", default=os.path.expanduser("~/referenz/schriften/dosbox-0.74-3-vga-8x8.bin"),
                    help="8x8 VGA font (2048 bytes) for the BIOS font area; default from the reference clone")
    ap.add_argument("--root-out", default=os.path.join(root, "analyse", "folge"))
    sub = ap.add_subparsers(dest="cmd", required=True)
    b = sub.add_parser("befunde")
    b.add_argument("--print", action="store_true")
    k = sub.add_parser("cases")
    k.add_argument("--only", type=int, nargs="*", help="indices into the case list")
    sub.add_parser("csharp")
    args = ap.parse_args()
    font = None
    if os.path.exists(args.font):
        with open(args.font, "rb") as fh:
            font = fh.read()
        if len(font) != 2048:
            sys.exit("font must have 2048 bytes")
    elif args.cmd not in ("befunde", "csharp"):
        print("Warnung: keine 8x8-Schrift (%s), Text mit DefaultFont bleibt leer" % args.font, file=sys.stderr)
    p = feld.Probe(args.gamedata, font)
    return {"befunde": cmd_befunde, "cases": cmd_cases, "csharp": cmd_csharp}[args.cmd](p, args)


if __name__ == "__main__":
    sys.exit(main())
