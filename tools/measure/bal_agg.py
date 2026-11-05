#!/usr/bin/env python3
"""Aggregate BalProbe TSVs into per-faction SUP-6 / VET-7 rows. Usage: bal_agg.py <outdir>"""
import csv, glob, os, statistics as st, sys
d = sys.argv[1]
rows = {}   # (label, faction) -> list of row dicts ; games keyed per file
games = {}  # (label, pair) -> list of (seed, game, rowA, rowB)
for f in sorted(glob.glob(os.path.join(d, "*.tsv"))):
    label = os.path.basename(f).split("_")[0]
    pair = "-".join(os.path.basename(f)[:-4].split("_")[1:])
    with open(f) as fh:
        r = [x for x in csv.DictReader(fh, delimiter="\t")]
    for x in r: rows.setdefault((label, x["faction"]), []).append(x | {"pair": pair})
    games.setdefault((label, pair), []).extend(r)
def num(x, k):
    v = int(x[k]); return None if v < 0 else v
def med(xs):
    xs = [v for v in xs if v is not None]; return st.median(xs) if xs else float("nan")
def mean(xs):
    xs = [v for v in xs if v is not None]; return sum(xs) / len(xs) if xs else float("nan")
print("== win rate per pair (A-side faction wins / games), draws counted as 0.5")
for (label, pair), r in sorted(games.items()):
    a = [x for x in r if x["side"] == "A"]
    if not a: continue
    w = sum(x["winner"] == "win" for x in a); dr = sum(x["winner"] == "draw" for x in a)
    print(f"{label:8s} {pair:28s} n={len(a)} Awin={w} draw={dr} Bwin={len(a)-w-dr}  A%={100*(w+dr/2)/len(a):.1f} avgEnd={mean([int(x['endTick']) for x in a]):.0f}")
print("== per faction")
for (label, fac), r in sorted(rows.items()):
    n = len(r); w = sum(x["winner"] == "win" for x in r); dr = sum(x["winner"] == "draw" for x in r)
    capw = sum(int(x["capWindow"]) for x in r); caps = sum(int(x["capSamples"]) for x in r)
    # per-game capped share of minutes 5-15 (only games that lasted the window)
    sh = [int(x["capSamples"]) / int(x["capWindow"]) for x in r if int(x["capWindow"]) > 0]
    full = [x for x in r if int(x["capWindow"]) >= 1100]
    over10 = sum(1 for s in sh if s > 0.10)
    print(f"{label:8s} {fac:11s} n={n} win%={100*(w+dr/2)/n:.1f} (w{w} d{dr} l{n-w-dr}) cap%(pooled)={100*caps/max(capw,1):.1f} cap%(mean of games)={100*mean(sh):.1f} games>10%cap={over10}/{len(sh)} "
          f"army5={med([num(x,'army5') for x in r])} army10={med([num(x,'army10') for x in r])} sup_bld={mean([int(x['supBuildings']) for x in r]):.2f} "
          f"inc10={med([num(x,'harv10') for x in r])} inc8-10={med([num(x,'harv8to10') for x in r])} used10={med([num(x,'used10') for x in r])} cap10={med([num(x,'cap10') for x in r])} "
          f"mismatch={sum(int(x['capSimMismatch']) for x in r)} n10={sum(1 for x in r if num(x,'army10') is not None)}")
print("== veterancy per faction (games alive at the minute only)")
for (label, fac), r in sorted(rows.items()):
    n10 = [x for x in r if num(x, "r1_10") is not None]; n20 = [x for x in r if num(x, "r1_20") is not None]
    n15 = [x for x in r if num(x, "r3_15") is not None]
    f = lambda xs, k: mean([int(x[k]) for x in xs])
    a10 = sum(int(x["alive10"]) for x in n10); a20 = sum(int(x["alive20"]) for x in n20)
    s10 = 100 * sum(int(x["a3_10"]) for x in n10) / a10 if a10 else float("nan")
    s20 = 100 * sum(int(x["a3_20"]) for x in n20) / a20 if a20 else float("nan")
    a15 = sum(int(x["alive15"]) for x in n15); s15 = 100 * sum(int(x["a3_15"]) for x in n15) / a15 if a15 else float("nan")
    g3 = sum(1 for x in n20 if int(x["r3_20" if False else "r3_20"]) > 0)
    g3_15 = sum(1 for x in n15 if int(x["r3_15"]) > 0)
    print(f"{label:8s} {fac:11s} min10 n={len(n10)} r1/r2/r3 per game={f(n10,'r1_10'):.2f}/{f(n10,'r2_10'):.2f}/{f(n10,'r3_10'):.2f} | min20 n={len(n20)} {f(n20,'r1_20'):.2f}/{f(n20,'r2_20'):.2f}/{f(n20,'r3_20'):.2f} "
          f"| rank3 share alive: min10 {s10:.1f}% min15 {s15:.1f}% min20 {s20:.1f}% | games with a rank3 by min15 {g3_15}/{len(n15)}, by min20 {g3}/{len(n20)}")
