#!/usr/bin/env python3
"""Coverage overview of BOMB.EXE / BOMB.OVR in five colours (template docs/vorlagen/abdeckung-grafik.md).

Usage:
    python3 tools/reverse-engineering/abdeckung_grafik.py <gamedata dir> --out <dir> [--repo .] [--sample N]

Runs the function map (funktionskarte.py) and the marker list (markerliste.py), maps every hit of a function to
the marker it belongs to (file + nearest marker line, as in funktionskarte step 3), takes the class of that marker
from docs/analyse/restliste-v1-klassen.csv and colours each function:

  blue   a hit belongs to an [APPROX] of class E (wins over green and yellow)
  green  status "umgesetzt"
  yellow status "genähert" with an [APPROX] of class K or D (and none of class V or without class)
  grey   status "dokumentiert" or "unbekannt" in an area with a documented drop reason (GREY, with Beleg)
  red    everything else

Writes <out>/abdeckung.html (self-contained, no external scripts or fonts), <out>/farben.csv (one row per function:
address, size, colour, class, hits) and prints the totals and a random sample per colour for the manual check.
From the originals only addresses, sizes and short text snippets leave the script.
"""

from __future__ import annotations

import argparse
import csv
import datetime
import html
import json
import random
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import funktionskarte as fk  # noqa: E402
import markerliste as ml  # noqa: E402

COLOURS = ["grün", "gelb", "blau", "grau", "rot"]
CSS = {"grün": "#2e9d4f", "gelb": "#e3b505", "blau": "#2f6fd0", "grau": "#9a9a9a", "rot": "#d6453d"}
RULES = {
    "grün": "belegt umgesetzt: Status „umgesetzt“ der Funktionskarte, keine Fundstelle gehört zu einem [APPROX] der Klasse E",
    "gelb": "umgesetzt ohne Beleg: Status „genähert“ mit [APPROX] der Klasse K oder D (ohne Adresse nachgebaut: nur mit "
            "Nachweis; im Ziel keine solchen Fälle)",
    "blau": "anders umgesetzt: eine Fundstelle gehört zu einem [APPROX] der Klasse E (Nutzerentscheidung, korrigierter "
            "Fehler, Plattformersatz, Erweiterung) – Vorrang vor grün und gelb",
    "grau": "weggefallen: nur mit Beleg (Borland-Laufzeit, Overlay-Manager, Treiber für Grafik/Maus, Speicherprüfung)",
    "rot": "noch unbekannt: alles Übrige, besonders „unbekannt“ und „dokumentiert“ ohne Remake-Nachweis; im Zweifel rot",
}

# Areas whose functions count as dropped when the remake has no [ORIGINAL]/[APPROX] hit (segment -> reason, Beleg).
GREY = {
    0x12CE: ("Borland-Laufzeitbibliothek (System)", "docs/analyse/re-grundlagen.md §1: Copyright-Text der TP-6-Laufzeit "
             "„Portions Copyright (c) 1983,90 Borland“ (Befund A2), System-Init A3"),
    0x11F8: ("Borland-Laufzeitbibliothek (Crt)", "docs/analyse/re-grundlagen.md §1/§3: Borland Crt (Befunde C1–C3)"),
    0x11E6: ("Borland-Laufzeitbibliothek (Dos)", "docs/analyse/re-grundlagen.md §1/§3: Borland Dos (Befund C4)"),
    0x0E22: ("Borland-Laufzeitbibliothek (Printer)", "docs/analyse/re-grundlagen.md §1: Borland Printer (Befund C6)"),
    0x0E26: ("Borland-Laufzeitbibliothek (Graph, BGI-Kern)", "docs/analyse/re-grundlagen.md §1: Borland Graph (Befund C5)"),
    0x125A: ("Overlay-Manager", "docs/analyse/re-grundlagen.md §1/§2: Borland Overlay, EMS (Befund B3)"),
    0x0E11: ("DOS-Treiber Maus", "docs/analyse/re-grundlagen.md §1: Maus über INT 33h (Befund G6)"),
    0x0DBD: ("DOS-Treiber Grafik (Anmeldung)", "docs/analyse/re-grundlagen.md §1/§4: InstallUserDriver/InstallUserFont, "
             "Fehlermeldung, PIC (Befund F6)"),
    0x0059: ("Speicherprüfung", "docs/analyse/re-grundlagen.md §1: Speicherübersicht, EMS-/Overlay-Meldungen (Texte)"),
}
GREY_BLOCK = {"BGI-Treiber VGA256": ("DOS-Treiber Grafik", "docs/analyse/re-grundlagen.md §4: eingebundener VGA256-Treiber "
                                     "2.00 (Befund F1)")}


def classify(an, classes_by_line):
    rows = []
    for f in an.functions:
        hits = [(st, w) for st, ws in f.refs.items() for w in ws]
        cls = []
        for st, w in hits:
            m = f.markers.get(w, "")
            c = classes_by_line.get(m)
            if c is not None:
                cls.append((w, m, c[0], c[1]))
        kinds = {c[2] for c in cls}
        status = f.status
        grey = None
        if f.region.data in GREY_BLOCK:
            grey = GREY_BLOCK[f.region.data]
        elif not f.region.data and f.region.seg in GREY:
            grey = GREY[f.region.seg]
        if "E" in kinds:
            colour = "blau"
        elif status == "umgesetzt":
            colour = "grün"
        elif status == "genähert" and kinds & {"K", "D"} and not kinds - {"K", "D", "X"}:
            colour = "gelb"
        elif status in ("dokumentiert", "unbekannt") and grey:
            colour = "grau"
        else:
            colour = "rot"
        rows.append(dict(f=f, colour=colour, status=status, classes=cls, grey=grey if colour == "grau" else None,
                         hits=hits))
    return rows


def marker_classes(repo: Path):
    markers, _ = ml.build(repo)
    classes = ml.read_classes(repo)
    out = {}
    for m in markers:
        k = classes.get(m.ident, {}).get("Klasse", "")
        out[f"src/BombRedone.{m.path}:{m.line}"] = (k or "–", m.ident)
    return out


def region_title(r) -> str:
    return f"{r.name} – {fk.region_kind(r)} ({r.file} 0x{r.file_offset(r.start):05X}, {r.size} Byte)"


def render_html(an, rows, sha, today) -> str:
    total_b = sum(r["f"].size for r in rows)
    total_n = len(rows)
    by_b = {c: sum(r["f"].size for r in rows if r["colour"] == c) for c in COLOURS}
    by_n = {c: sum(1 for r in rows if r["colour"] == c) for c in COLOURS}
    data = []
    for r in rows:
        f = r["f"]
        data.append(dict(
            o=f.offset(), a=f.address(), s=f.size, c=r["colour"], st=r["status"], g=f.region.key,
            k=", ".join(sorted({f"{c[3]} {c[2]}" for c in r["classes"]})),
            h=fk.hint(f, an), n=len(r["hits"]),
            r=[w for _, w in r["hits"]][:6],
        ))
    regions = [dict(k=r.key, t=region_title(r)) for r in an.regions]

    def bar(by, tot):
        return "".join(f'<div class="seg" style="width:{100 * by[c] / tot:.3f}%;background:{CSS[c]}" '
                       f'title="{c}: {by[c]} ({100 * by[c] / tot:.1f} %)"></div>' for c in COLOURS if by[c])

    def tbl(colour, n=20):
        sel = sorted((r for r in rows if r["colour"] == colour), key=lambda r: -r["f"].size)[:n]
        body = "".join(f"<tr><td>{html.escape(r['f'].offset())}</td><td>{html.escape(r['f'].address())}</td>"
                       f"<td class=n>{r['f'].size}</td><td>{html.escape(r['f'].region.name)}</td>"
                       f"<td>{html.escape(fk.hint(r['f'], an))}</td></tr>" for r in sel)
        return ("<table><tr><th>Dateioffset</th><th>Adresse</th><th>Größe</th><th>Bereich</th><th>Hinweis</th></tr>"
                + body + "</table>")

    greys = sorted((r for r in rows if r["colour"] == "grau"), key=lambda r: r["f"].start)
    grey_rows = "".join(f"<tr><td>{html.escape(r['f'].offset())}</td><td>{html.escape(r['f'].address())}</td>"
                        f"<td class=n>{r['f'].size}</td><td>{html.escape(r['grey'][0])}</td>"
                        f"<td>{html.escape(r['grey'][1])}</td></tr>" for r in greys)
    legend = "".join(f'<li><span class="sw" style="background:{CSS[c]}"></span><b>{c}</b> – {html.escape(RULES[c])} '
                     f'<span class="pc">{by_b[c]} Byte ({100 * by_b[c] / total_b:.1f} %), {by_n[c]} Funktionen '
                     f'({100 * by_n[c] / total_n:.1f} %)</span></li>' for c in COLOURS)
    filters = "".join(f'<label><input type="checkbox" checked data-c="{c}"> {c}</label>' for c in COLOURS)
    return f"""<!DOCTYPE html>
<html lang="de"><head><meta charset="utf-8"><title>Abdeckung BOMB.EXE/BOMB.OVR</title>
<style>
body{{font:14px/1.4 system-ui,sans-serif;margin:16px;color:#222;background:#fff}}
h1{{font-size:20px;margin:0 0 4px}} h2{{font-size:16px;margin:20px 0 6px}} h3{{font-size:13px;margin:12px 0 4px;font-weight:600}}
.bar{{display:flex;height:22px;border:1px solid #ccc;margin:2px 0 8px}} .seg{{height:100%}}
ul.leg{{list-style:none;padding:0}} ul.leg li{{margin:3px 0}} .sw{{display:inline-block;width:12px;height:12px;margin-right:6px;vertical-align:-1px}}
.pc{{color:#555}} .map{{display:flex;flex-wrap:wrap;border:1px solid #ddd;margin-bottom:4px}}
.fn{{height:16px;box-sizing:border-box;border-right:1px solid #fff;border-bottom:1px solid #fff;cursor:default;min-width:2px}}
.fn.off{{opacity:.08}} #tip{{position:fixed;pointer-events:none;background:#fffbe6;border:1px solid #999;padding:6px 8px;font-size:12px;max-width:460px;display:none;z-index:9}}
table{{border-collapse:collapse;font-size:12px;margin-bottom:8px}} td,th{{border:1px solid #ddd;padding:2px 6px;text-align:left;vertical-align:top}} td.n{{text-align:right}}
.filter label{{margin-right:12px}} .note{{color:#555;font-size:12px}}
</style></head><body>
<h1>Abdeckung von BOMB.EXE / BOMB.OVR im Remake</h1>
<div class="note">Stand <code>main</code> {html.escape(sha)}, erzeugt {today} mit <code>tools/reverse-engineering/abdeckung_grafik.py</code>
(Vorlage <code>docs/vorlagen/abdeckung-grafik.md</code>). {total_n} Funktionen und Datenblöcke, {total_b} Byte. Je Funktion gewichtet nach Bytes;
schon eine Fundstelle zählt die ganze Funktion (große, dünn belegte Funktionen wirken dadurch besser, als sie sind).</div>
<ul class="leg">{legend}</ul>
<h2>Gesamt nach Bytes</h2><div class="bar">{bar(by_b, total_b)}</div>
<h2>Gesamt nach Anzahl Funktionen</h2><div class="bar">{bar(by_n, total_n)}</div>
<h2>Karte je Bereich (Adressreihenfolge, Fläche nach Größe)</h2>
<div class="filter">Filter: {filters}</div>
<div id="maps"></div>
<h2>Die 20 größten roten Funktionen</h2>{tbl("rot")}
<h2>Die 20 größten gelben Funktionen</h2>{tbl("gelb")}
<h2>Graue Funktionen mit Beleg</h2>
<table><tr><th>Dateioffset</th><th>Adresse</th><th>Größe</th><th>Grund</th><th>Beleg</th></tr>{grey_rows}</table>
<div id="tip"></div>
<script>
const D={json.dumps(data, ensure_ascii=False)};
const R={json.dumps(regions, ensure_ascii=False)};
const C={json.dumps(CSS)};
const maps=document.getElementById('maps'),tip=document.getElementById('tip');
const esc=s=>String(s).replace(/[&<>"]/g,c=>({{'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}}[c]));
for(const r of R){{
  const fs=D.filter(d=>d.g===r.k); if(!fs.length) continue;
  const tot=fs.reduce((a,d)=>a+d.s,0), rows=Math.max(1,Math.round(Math.sqrt(tot/900)));
  const h=document.createElement('h3'); h.textContent=r.t; maps.appendChild(h);
  const m=document.createElement('div'); m.className='map'; maps.appendChild(m);
  for(const d of fs){{
    const e=document.createElement('div'); e.className='fn'; e.dataset.c=d.c;
    e.style.width=(100*d.s*rows/tot)+'%'; e.style.background=C[d.c];
    e.onmousemove=ev=>{{tip.style.display='block';tip.style.left=Math.min(ev.clientX+12,innerWidth-480)+'px';tip.style.top=(ev.clientY+12)+'px';
      tip.innerHTML='<b>'+esc(d.o)+'</b> ('+esc(d.a)+')<br>Größe '+d.s+' Byte · Farbe <b>'+d.c+'</b> · Status '+esc(d.st)+
      (d.k?'<br>Klasse: '+esc(d.k):'')+'<br>Fundstellen ('+d.n+'): '+(d.r.length?esc(d.r.join(', '))+(d.n>d.r.length?' …':''):'–')+
      '<br>Hinweis: '+esc(d.h);}};
    e.onmouseleave=()=>tip.style.display='none';
    m.appendChild(e);
  }}
}}
document.querySelectorAll('.filter input').forEach(i=>i.onchange=()=>{{
  const on=new Set([...document.querySelectorAll('.filter input')].filter(x=>x.checked).map(x=>x.dataset.c));
  document.querySelectorAll('.fn').forEach(e=>e.classList.toggle('off',!on.has(e.dataset.c)));
}});
</script></body></html>
"""


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("gamedata", type=Path)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    ap.add_argument("--sample", type=int, default=10)
    args = ap.parse_args()
    repo = args.repo.resolve()
    an, _ = fk.analyse(args.gamedata, repo)
    rows = classify(an, marker_classes(repo))
    sha = subprocess.run(["git", "-C", str(repo), "rev-parse", "--short", "HEAD"], capture_output=True,
                         text=True).stdout.strip()
    args.out.mkdir(parents=True, exist_ok=True)
    today = datetime.date.today().isoformat()
    (args.out / "abdeckung.html").write_text(render_html(an, rows, sha, today), encoding="utf-8")
    with (args.out / "farben.csv").open("w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["datei", "adresse", "bereich", "groesse", "status", "farbe", "klassen", "grund", "fundstellen"])
        for r in rows:
            f = r["f"]
            w.writerow([f.offset(), f.address(), f.region.name, f.size, r["status"], r["colour"],
                        " ".join(sorted({f"{c[3]}={c[2]}" for c in r["classes"]})), r["grey"][0] if r["grey"] else "",
                        "; ".join(f"{wh}>{f.markers.get(wh, '')}" for _, wh in r["hits"])])
    total = sum(f.size for f in an.functions)
    print(f"Stand {sha}: {len(rows)} Funktionen, {total} Byte")
    for c in COLOURS:
        b = sum(r["f"].size for r in rows if r["colour"] == c)
        n = sum(1 for r in rows if r["colour"] == c)
        print(f"  {c:5} {b:7} Byte {100 * b / total:5.1f} %  {n:4} Funktionen")
    print("Probe: Bytes", sum(r["f"].size for r in rows) == total, "Anzahl", len(rows) == len(an.functions))
    rnd = random.Random(26112026)
    for c in COLOURS:
        sel = [r for r in rows if r["colour"] == c]
        print(f"\n== Stichprobe {c} ({min(args.sample, len(sel))} von {len(sel)})")
        for r in rnd.sample(sel, min(args.sample, len(sel))):
            f = r["f"]
            print(f"  {f.offset()} {f.address()} {f.size} {r['status']} kl={[(x[3], x[2]) for x in r['classes']][:3]} "
                  f"grau={r['grey'][0] if r['grey'] else ''} hits={[(s, w, f.markers.get(w, '')) for s, w in r['hits']][:2]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
