"""Contact sheets for the art-exp-a package: python3 tools/art/vehicles/exp_contact_a.py <out.png> <kind> <id> [<id> ...]
kind = inf: rows of 8 idle facings (team Crimson, sand) + 4 walk facings (team Lime, ash) at 2x; kind = air: rows of 3 facings on sand
(Crimson) and 3 on water (Lime) at 2x (--zoom to change).  Bottom: 1x true-size strip next to the Glide Tank (hull facing 2 and 6)."""
import argparse, os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
sys.path.insert(0, ART)
sys.path.insert(0, HERE)
import common as C
import veh_contact as VC
import remap as R
import exp_contact as EC

INF = os.path.join(C.ROOT, "data", "sprites", "infantry")


class Inf:
    def __init__(self, uid):
        self.s = VC.Sheet(uid, INF)
        self.ox, self.oy = self.s.ox, self.s.oy
        m = self.s.m
        self.first = {n: int(f) for n, f in zip(m["name"], m["first"])}

    def frame(self, state, facing, step=0, n=1):
        return self.s.frame(self.first[state] + facing * n + step)


def bg_for(g, w, h, seed):
    if g == "ash":
        rng = np.random.default_rng(seed)
        arr = np.array([74, 66, 58], dtype=float) + rng.normal(0, 4, (h, w, 1))
        return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")
    return (EC.sand if g == "sand" else EC.water)(w, h, seed)


def inf_row(u, rowh=88, slot=64):
    items = [("idle", f, 0, 1, "Crimson", "sand") for f in range(8)] + [("walk", f, 2, 6, "Lime", "ash") for f in (0, 2, 4, 6)]
    W = slot * len(items)
    im = Image.new("RGB", (W, rowh))
    for i, (st, f, step, n, team, g) in enumerate(items):
        tile = bg_for(g, slot, rowh, 5 + i)
        fr = R.remap(u.frame(st, f, step, n), EC.TEAMS[team])[0]
        big = fr.resize((fr.width * 2, fr.height * 2), Image.NEAREST)
        tile.paste(big, (slot // 2 - u.ox * 2, rowh - 18 - u.oy * 2), big)
        im.paste(tile, (i * slot, 0))
    return im


def air_row(u, facings, z, rowh):
    fr0 = u.comp(0)
    bbs = [u.comp(f).getchannel("A").getbbox() for f in range(16)]
    x0 = min(b[0] for b in bbs) - 4; x1 = max(b[2] for b in bbs) + 4
    y0 = min(b[1] for b in bbs) - 4; y1 = max(b[3] for b in bbs) + 4
    slot = (x1 - x0) * z
    h = (y1 - y0) * z
    items = [(f, "Crimson", "sand") for f in facings[:3]] + [(f, "Lime", "water") for f in facings[3:]]
    im = Image.new("RGB", (slot * len(items), h))
    for i, (f, team, g) in enumerate(items):
        tile = bg_for(g, slot, h, 20 + i)
        fr = R.remap(u.comp(f), EC.TEAMS[team])[0].crop((x0, y0, x1, y1))
        big = fr.resize((fr.width * z, fr.height * z), Image.NEAREST)
        tile.paste(big, (0, 0), big)
        im.paste(tile, (i * slot, 0))
    return im


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("out"); ap.add_argument("kind"); ap.add_argument("ids", nargs="+")
    ap.add_argument("--zoom", type=int, default=2)
    ap.add_argument("--facings", type=int, nargs=6, default=[0, 5, 10, 2, 8, 13])
    a = ap.parse_args()
    rows, strip_units = [], []
    for uid in a.ids:
        if a.kind == "inf":
            u = Inf(uid)
            rows.append(inf_row(u))
        else:
            u = VC.Unit(uid)
            rows.append(air_row(u, a.facings, a.zoom, 0))
        ImageDraw.Draw(rows[-1]).text((4, 3), uid, fill=(255, 255, 255))
        strip_units.append(u)
    W = max(r.width for r in rows)
    strip = Image.new("RGB", (W, 150), (74, 66, 58))
    ImageDraw.Draw(strip).text((4, 3), "1x true size, Crimson: Glide Tank (hull facing 2) first, then the assets", fill=(255, 255, 255))
    x = 10
    ref = EC.unit("exp_glide_tank")
    for kind, u in [("veh", ref)] + [(a.kind, s) for s in strip_units]:
        for f in ((2,) if kind == "veh" else ((2, 6) if kind == "inf" else (2,))):
            fr = u.comp(f) if kind != "inf" else u.frame("idle", f)
            fr = R.remap(fr, EC.TEAMS["Crimson"])[0]
            bb = fr.getchannel("A").getbbox()
            fr = fr.crop(bb)
            if x + fr.width > W - 4:
                break
            strip.paste(fr, (x, 20 + (120 - fr.height) // 2), fr)
            x += fr.width + 14
    out = Image.new("RGB", (W, sum(r.height for r in rows) + strip.height), (30, 28, 34))
    y = 0
    for r in rows + [strip]:
        out.paste(r, (0, y)); y += r.height
    out.save(a.out)
    print("wrote", a.out, out.size)


if __name__ == "__main__":
    main()
