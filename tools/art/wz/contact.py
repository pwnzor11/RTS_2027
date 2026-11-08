"""Contact sheets for the art-wz pilot: current sprite vs Warzone-based candidate, 2 house colours, 1x and 2x, on a ground-coloured backdrop.

Usage: python3 tools/art/wz/contact.py <out_dir>
Tank: 8 facings (every 2nd of the 16), turret layer composited with the pivot rule from the manifest.  Structure: idle frame 0.
"""
import math
import os
import re
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
SPR = os.path.join(ROOT, "data", "sprites")
BG = (92, 102, 78, 255)
RAMP = [(64, 0, 64), (91, 18, 91), (119, 37, 119), (146, 55, 146), (173, 73, 173), (200, 91, 200), (228, 110, 228), (255, 128, 255)]
HOUSES = {"red": (200, 40, 36), "blue": (50, 100, 220)}


def manifest(path):
    t = open(path).read()
    g = lambda k: float(re.search(rf"^{k}\s*=\s*(-?[\d.]+)", t, re.M).group(1))
    return dict(fw=int(g("frame_w")), fh=int(g("frame_h")), ox=int(g("origin_x")), oy=int(g("origin_y")),
                px=g("pivot_x") if "pivot_x" in t else 0.0, py=g("pivot_y") if "pivot_y" in t else 0.0)


def frame(sheet, mf, i):
    cols = sheet.width // mf["fw"]
    x, y = (i % cols) * mf["fw"], (i // cols) * mf["fh"]
    return sheet.crop((x, y, x + mf["fw"], y + mf["fh"]))


def recolour(im, col):
    a = im.copy()
    px = a.load()
    lut = {}
    for i, r in enumerate(RAMP):
        t = i / 7.0
        lut[r] = tuple(min(255, int(c * (0.3 + 0.7 * t) + 255 * max(0.0, t - 0.75) * 0.8)) for c in col)
    for y in range(a.height):
        for x in range(a.width):
            p = px[x, y]
            if p[3] and p[:3] in lut:
                px[x, y] = (*lut[p[:3]], 255)
    return a


def tank_img(vid, f, turret=True):
    body = Image.open(f"{SPR}/vehicles/{vid}.png").convert("RGBA")
    mb = manifest(f"{SPR}/vehicles/{vid}.sprite.toml")
    cv = Image.new("RGBA", (160, 160), (0, 0, 0, 0))
    ox, oy = 80, 100                   # ground point inside the common cell
    cv.alpha_composite(frame(body, mb, f), (ox - mb["ox"], oy - mb["oy"]))
    tp = f"{SPR}/vehicles/{vid}_turret.png"
    if turret and os.path.exists(tp):
        mt = manifest(f"{SPR}/vehicles/{vid}_turret.sprite.toml")
        th = math.radians(22.5 * f)
        x, y = mt["px"], mt["py"]
        xr, yr = x * math.cos(th) + y * math.sin(th), -x * math.sin(th) + y * math.cos(th)
        gx, gy = ox + xr * 64, oy - yr * 64 * math.sin(math.radians(40))
        cv.alpha_composite(frame(Image.open(tp).convert("RGBA"), mt, f), (round(gx - mt["ox"]), round(gy - mt["oy"])))
    return cv


def sheet_tank(out):
    facings = list(range(0, 16, 2))
    rows = []
    for hname, col in HOUSES.items():
        for vid in ("auth_tank", "auth_tank_wz"):
            rows.append([recolour(tank_img(vid, f), col) for f in facings])
    W = Image.new("RGBA", (8 * 320, 4 * 320 + 4 * 160 + 40), BG)
    for r, ims in enumerate(rows):
        for c, im in enumerate(ims):
            W.alpha_composite(im.resize((320, 320), Image.NEAREST), (c * 320, r * 320))
    for r, ims in enumerate(rows):
        for c, im in enumerate(ims):
            W.alpha_composite(im, (c * 320 + 80, 4 * 320 + 20 + r * 160))
    W.save(os.path.join(out, "tank_old_vs_wz.png"))


def sheet_struct(out):
    ims = []
    for sid in ("auth_factory", "auth_factory_wz"):
        sh = Image.open(f"{SPR}/buildings/{sid}.png").convert("RGBA")
        mf = manifest(f"{SPR}/buildings/{sid}.sprite.toml")
        ims.append(frame(sh, mf, 0))
    W = Image.new("RGBA", (4 * 384 + 40, 448 + 224 + 40), BG)
    k = 0
    for hname, col in HOUSES.items():
        for im in ims:
            im = recolour(im, col)
            W.alpha_composite(im.resize((384, 448), Image.NEAREST), (k * 384 + 10 * (k + 1) // 2, 0))
            W.alpha_composite(im, (k * 384 + 80, 460))
            k += 1
    W.save(os.path.join(out, "factory_old_vs_wz.png"))


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    sheet_tank(out)
    sheet_struct(out)
