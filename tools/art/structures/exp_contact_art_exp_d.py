"""Contact sheet for art-exp-d structures: python3 tools/art/structures/exp_contact_art_exp_d.py <out.png> <id> [<id> ...]
Per asset a row-pair: 2x idle 0, idle 2, damaged 0, build 4 on sand (Crimson) and on ash/water (Lime), then a 1x strip with the Landing Depot."""
import os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
sys.path.insert(0, ART)
import common as C
import remap as R
BLD = os.path.join(C.ROOT, "data", "sprites", "buildings")
TEAMS = {"Crimson": C.make_ramp(C.hx("#2A0A10"), C.hx("#B5202F"), C.hx("#F6A0A8")),
         "Lime": C.make_ramp(C.hx("#10260A"), C.hx("#5FA82A"), C.hx("#D8F59A"))}


def frames(sid):
    m = {}
    for ln in open(os.path.join(BLD, sid + ".sprite.toml")):
        ln = ln.strip()
        if "=" in ln and not ln.startswith(("#", "[", "ramp")):
            k, v = [x.strip() for x in ln.split("=", 1)]
            m.setdefault(k, []).append(v.strip('"'))
    fw, fh = int(m["frame_w"][0]), int(m["frame_h"][0])
    img = Image.open(os.path.join(BLD, sid + ".png")).convert("RGBA")
    cols = img.width // fw
    first = {n: int(f) for n, f in zip(m["name"], m["first"])}
    def fr(state, i):
        k = first[state] + i
        return img.crop(((k % cols) * fw, (k // cols) * fh, (k % cols) * fw + fw, (k // cols) * fh + fh))
    return fr, (fw, fh)


def ground(kind, w, h, seed):
    rng = np.random.default_rng(seed)
    base = np.array({"sand": [150, 132, 98], "ash": [74, 68, 64], "water": [40, 92, 112]}[kind], dtype=float)
    arr = np.clip(base + rng.normal(0, 5, (h, w, 1)), 0, 255)
    return Image.fromarray(arr.astype(np.uint8), "RGB")


def main():
    out, ids = sys.argv[1], sys.argv[2:]
    picks = [("idle", 0), ("idle", 2), ("damaged", 0), ("build", 4)]
    rows = []
    for sid in ids:
        F, (fw, fh) = frames(sid)
        for kind, team in (("sand", "Crimson"), ("ash", "Lime")):
            if kind == "ash" and sid in ("exp_quay", "exp_anchorage"):
                kind = "water"
            w = (fw * 2 + 12) * len(picks)
            im = ground(kind, w, fh * 2 + 18, 3)
            d = ImageDraw.Draw(im)
            d.text((4, 2), f"{sid} | {kind} | {team}", fill=(255, 255, 255))
            for k, (st, n) in enumerate(picks):
                fr = R.remap(F(st, n), TEAMS[team])[0]
                fr = fr.resize((fw * 2, fh * 2), Image.NEAREST)
                im.paste(fr, (k * (fw * 2 + 12) + 6, 16), fr)
            rows.append(im)
    W = max(r.width for r in rows)
    ld, (lw, lh) = frames("exp_landing_depot")
    gt = Image.open(os.path.join(C.ROOT, "data", "sprites", "vehicles", "exp_glide_tank.png")).convert("RGBA")
    strip = Image.new("RGB", (W, 270), (74, 66, 58))
    ImageDraw.Draw(strip).text((4, 2), "1x: Landing Depot, then each asset", fill=(255, 255, 255))
    x = 6
    for sid in ["exp_landing_depot"] + ids:
        F, _ = frames(sid)
        im = R.remap(F("idle", 0), TEAMS["Crimson"])[0]
        strip.paste(im, (x, 16), im); x += im.width + 8
    H = sum(r.height for r in rows) + strip.height
    o = Image.new("RGB", (W, H), (30, 28, 34)); y = 0
    for r in rows + [strip]:
        o.paste(r, (0, y)); y += r.height
    o.save(out); print(out, o.size)


main()
