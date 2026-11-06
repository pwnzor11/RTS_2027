#!/usr/bin/env python3
"""Aggregate m7_income.sh logs into the 11 §4 tables. Usage: m7_income_agg.py <outdir> [--md]
Income marks are cumulative credits (harvested + shipments) at 3/5/10 min; the per-minute figure is mark/minutes. A game that ended
before a mark contributes nothing to that mark (for BOTH sides of the game), so each column is over the games that reached it."""
import glob, os, re, statistics as st, sys
d = sys.argv[1]
name = {"authority": "Authority", "directorate": "Directorate", "left_behind": "Left Behind"}
sizes = {"64": "small", "96": "medium", "128": "large"}
pat_inc = re.compile(r"income at 3/5/10 min.*?: A (\d+)/(\d+)/(\d+) \| B (\d+)/(\d+)/(\d+)")
pat_eco = re.compile(r"first refinery tick A (-?\d+) B (-?\d+) \| tenders sunk A (\d+) B (\d+) \| decay step at 10 min A (\d+) B (\d+) \| shipments 3/5/10 A (\d+)/(\d+)/(\d+)")
pat_end = re.compile(r"at tick (\d+)|: draw at tick (\d+)")
def mean(x): return sum(x) / len(x) if x else float("nan")
for sz, label in sizes.items():
    rows = {}   # faction -> dict of lists ; Expedition pooled
    exp = dict(sh=[[], [], []], m=[[], [], []], ref=[], sunk=[], dec=[], ship10=[], n=0, tot10=[], tot3=[], tot5=[])
    for f in name:
        p = os.path.join(d, f"s{sz}_{f}.txt")
        if not os.path.exists(p): continue
        r = dict(m=[[], [], []], ref=[], sunk=[], dec=[], n=0)
        lines = open(p).read().splitlines()
        for i, ln in enumerate(lines):
            m = pat_inc.search(ln)
            if not m: continue
            e = pat_eco.search(lines[i + 1])
            a = [int(x) for x in m.groups()[:3]]; b = [int(x) for x in m.groups()[3:]]
            ea = list(map(int, e.groups()[:6])); sh = list(map(int, e.groups()[6:9]))
            r["n"] += 1; exp["n"] += 1
            for k, mins in enumerate((3, 5, 10)):
                if a[k] > 0 and b[k] > 0:
                    exp["m"][k].append(a[k] / mins); r["m"][k].append(b[k] / mins); exp["sh"][k].append(sh[k] / mins)
            if ea[0] >= 0: exp["ref"].append(ea[0])
            if ea[1] >= 0: r["ref"].append(ea[1])
            exp["sunk"].append(ea[2]); r["sunk"].append(ea[3])
            if a[2] > 0: exp["dec"].append(ea[4])
            r["dec"].append(ea[5])
        rows[f] = r
    if not rows: continue
    print(f"\n### {label} map (size {sz}) -- 20 games per opponent cell")
    print("| Faction | 3 min /min | 5 min /min | 10 min /min | vs mean 3/5/10 | Shipment share % | Games (reached 10 min) | first refinery tick | tenders sunk /game | decay step at 10 min |")
    print("|---|---|---|---|---|---|---|---|---|---|")
    others = [[mean(rows[f]["m"][k]) for k in range(3)] for f in rows]
    mn = [mean([o[k] for o in others]) for k in range(3)]
    for f, r in rows.items():
        v = [mean(r["m"][k]) for k in range(3)]
        print(f"| {name[f]} | {v[0]:.0f} | {v[1]:.0f} | {v[2]:.0f} | n/a | n/a | {r['n']} ({len(r['m'][2])}) | {mean(r['ref']):.0f} | {mean(r['sunk']):.2f} | {mean(r['dec']):.1f} |")
    ev = [mean(exp["m"][k]) for k in range(3)]
    pct = [100 * (ev[k] / mn[k] - 1) for k in range(3)]
    print(f"| Expedition | {ev[0]:.0f} | {ev[1]:.0f} | {ev[2]:.0f} | {pct[0]:+.0f} % / {pct[1]:+.0f} % / {pct[2]:+.0f} % | {' / '.join(f'{100*mean(exp["sh"][k])/max(1,mean(exp["m"][k])):.0f}' for k in range(3))} | {exp['n']} ({len(exp['m'][2])}) | {mean(exp['ref']):.0f} | {mean(exp['sunk']):.2f} | {mean(exp['dec']):.1f} |")
    print(f"(mean of the other three: {mn[0]:.0f} / {mn[1]:.0f} / {mn[2]:.0f}; spread of the three at 3/5/10: " +
          " / ".join(f"{100*(max(o[k] for o in others)/min(o[k] for o in others)-1):.0f} %" for k in range(3)) + ")")
