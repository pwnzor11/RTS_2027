"""House-colour audit (D-184 / art guidance D4): count house-ramp pixels per facing in sprite sheets.

Usage:
  python3 tools/art/house_audit.py [paths ...] [--min 10] [--state idle] [--all-states] [--csv out.csv] [--report out.txt]

paths: sprite manifests (*.sprite.toml), sheets (*.png next to a manifest) or directories (searched recursively).
Default: data/sprites/infantry and data/sprites/vehicles.

A pixel is a house pixel when its RGB exactly equals one of the 8 shades of the manifest's [house] ramp (alpha 255).
For each asset the tool reports, over the 8/16 facings of the audited state (default idle; the first frame of each facing),
the minimum and median house-pixel count, plus the minimum over every frame of every state (walk/fire/die) as a second column.
An asset is FLAGGED when its audited minimum is under --min (default 10 px per facing at 1x).  With --all-states the flag
uses the minimum over all frames instead.  Exit code 0 always, unless --fail is given and something is flagged.
"""
import argparse, os, statistics, sys
import numpy as np
from PIL import Image
try:
    import tomllib
except ImportError:      # pragma: no cover
    import tomli as tomllib

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DEFAULTS = [os.path.join(ROOT, "data", "sprites", "infantry"), os.path.join(ROOT, "data", "sprites", "vehicles")]


def find_manifests(paths):
    out = []
    for p in paths:
        if os.path.isdir(p):
            for dp, _dn, fn in os.walk(p):
                out += [os.path.join(dp, f) for f in sorted(fn) if f.endswith(".sprite.toml")]
        elif p.endswith(".png"):
            out.append(p[:-4] + ".sprite.toml")
        else:
            out.append(p)
    return sorted(set(out))


def hex_rgb(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


def frame_counts(manifest):
    """Return (name, {state: [count per frame index in state order]}, facings per state, frames per state, frame size) or None."""
    with open(manifest, "rb") as fh:
        m = tomllib.load(fh)
    ramp = [hex_rgb(c) for c in m.get("house", {}).get("ramp", [])]
    if not ramp:
        return None
    png = os.path.join(os.path.dirname(manifest), m["file"])
    img = np.asarray(Image.open(png).convert("RGBA"))
    fw, fh_ = m["frame_w"], m["frame_h"]
    cols = img.shape[1] // fw
    rgb = img[..., :3].astype(int)
    key = rgb[..., 0] * 65536 + rgb[..., 1] * 256 + rgb[..., 2]
    keys = {r * 65536 + g * 256 + b for r, g, b in ramp}
    house = np.isin(key, list(keys)) & (img[..., 3] == 255)
    res = {}
    for st in m.get("state", []):
        nf, n, first = st["facings"], st["frames"], st["first"]
        per = []
        for f in range(nf):
            row = []
            for k in range(n):
                i = first + f * n + k
                cx, cy = (i % cols) * fw, (i // cols) * fh_
                row.append(int(house[cy:cy + fh_, cx:cx + fw].sum()))
            per.append(row)
        res[st["name"]] = per
    area = None
    return os.path.basename(manifest)[:-len(".sprite.toml")], res, (fw, fh_)


def audit(manifests, state, min_px, all_states):
    rows = []
    for mf in manifests:
        r = frame_counts(mf)
        if r is None:
            continue
        name, res, size = r
        if name.endswith("_shadow"):       # air-unit shadow sheets carry no house paint by design
            continue
        st = state if state in res else next(iter(res))
        idle = [row[0] for row in res[st]]
        allf = [c for per in res.values() for row in per for c in row]
        nz = [x for x in allf if True]
        key = min(allf) if all_states else min(idle)
        rows.append(dict(name=name, kind=os.path.basename(os.path.dirname(mf)) + ("-turret" if name.endswith("_turret") else ""), size=size, state=st, facings=len(idle),
                         idle_min=min(idle), idle_med=statistics.median(idle), idle_max=max(idle), all_min=min(allf),
                         all_med=statistics.median(allf), flagged=key < min_px, idle=idle))
    return rows


def render(rows, min_px, all_states):
    L = [f"House-colour audit: threshold {min_px} px per facing at 1x ({'all frames' if all_states else 'audited state'}); "
         f"FLAG = minimum under threshold", ""]
    L.append(f"{'asset':<28}{'kind':<17}{'frame':>8}{'fac':>4}{'min':>6}{'median':>8}{'max':>6}{'allmin':>8}{'allmed':>8}  flag")
    for r in rows:
        L.append(f"{r['name']:<28}{r['kind']:<17}{str(r['size'][0]) + 'x' + str(r['size'][1]):>8}{r['facings']:>4}{r['idle_min']:>6}"
                 f"{r['idle_med']:>8.1f}{r['idle_max']:>6}{r['all_min']:>8}{r['all_med']:>8.1f}  {'FLAG' if r['flagged'] else ''}")
    n = len(rows)
    fl = sum(r["flagged"] for r in rows)
    L += ["", f"{fl} of {n} assets under the threshold."]
    for kind in sorted({r["kind"] for r in rows}):
        sub = [r for r in rows if r["kind"] == kind]
        L.append(f"  {kind}: {len(sub)} assets, {sum(r['flagged'] for r in sub)} flagged, median of per-asset idle medians "
                 f"{statistics.median([r['idle_med'] for r in sub]):.1f} px, lowest idle min {min(r['idle_min'] for r in sub)}")
    return "\n".join(L)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("paths", nargs="*")
    ap.add_argument("--min", type=int, default=10, help="minimum house pixels per facing at 1x (default 10)")
    ap.add_argument("--state", default="idle")
    ap.add_argument("--all-states", action="store_true", help="flag on the minimum over every frame, not the audited state")
    ap.add_argument("--csv")
    ap.add_argument("--report")
    ap.add_argument("--fail", action="store_true", help="exit 1 when any asset is flagged")
    a = ap.parse_args()
    mfs = find_manifests(a.paths or DEFAULTS)
    rows = audit(mfs, a.state, a.min, a.all_states)
    txt = render(rows, a.min, a.all_states)
    print(txt)
    if a.report:
        open(a.report, "w").write(txt + "\n")
    if a.csv:
        with open(a.csv, "w") as fh:
            fh.write("asset,kind,frame_w,frame_h,facings,min,median,max,all_min,all_median,flagged,per_facing\n")
            for r in rows:
                fh.write(f"{r['name']},{r['kind']},{r['size'][0]},{r['size'][1]},{r['facings']},{r['idle_min']},{r['idle_med']},"
                         f"{r['idle_max']},{r['all_min']},{r['all_med']},{int(r['flagged'])},{' '.join(map(str, r['idle']))}\n")
    if a.fail and any(r["flagged"] for r in rows):
        sys.exit(1)


if __name__ == "__main__":
    main()
