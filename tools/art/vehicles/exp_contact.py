"""Expedition pilot contact sheet for a vehicle: python3 tools/art/vehicles/exp_contact.py <id_A> <id_B> <out.png> [--facings 0 2 4 6 9 12]
Rows: A on sand (team 1), B on sand (team 1), A on water (team 2), B on water (team 2), A on water (team 1), B on sand (team 2) at 2x,
each row on a sand/ash or water tile, then a 1x true-size strip next to the Authority tank for scale.  Hull+turret composite as veh_contact.py."""
import argparse, os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.dirname(HERE)
sys.path.insert(0, ART)
import common as C
import veh_contact as VC
import remap as R

TEAMS = {"Crimson": C.make_ramp(C.hx("#2A0A10"), C.hx("#B5202F"), C.hx("#F6A0A8")),
         "Lime": C.make_ramp(C.hx("#10260A"), C.hx("#5FA82A"), C.hx("#D8F59A"))}
ALT = os.path.join(C.PILOT, "exp_variant_b")


def unit(uid):
    """Unit whose sheets live in data/sprites/vehicles (variant A) or art/pilot/exp_variant_b (variant B)."""
    folder = ALT if os.path.exists(os.path.join(ALT, uid + ".png")) else VC.VEH
    u = VC.Unit.__new__(VC.Unit)
    u.body = VC.Sheet(uid, folder)
    tp = os.path.join(folder, uid + "_turret.png")
    u.turret = VC.Sheet(uid + "_turret", folder) if os.path.exists(tp) else None
    u.shadow = None
    if u.turret:
        u.pivot = tuple(float(u.turret.m[k][0]) for k in ("pivot_x", "pivot_y"))
    return u


def sand(w, h, seed):
    rng = np.random.default_rng(seed)
    base = np.array([150, 132, 98], dtype=float)
    n = rng.normal(0, 6, (h, w, 1))
    blot = np.asarray(Image.fromarray(rng.integers(0, 255, (h // 16 + 2, w // 16 + 2), dtype=np.uint8)).resize((w, h), Image.BICUBIC), dtype=float)[..., None]
    arr = np.clip(base + n + (blot - 128) * 0.12, 0, 255)
    ash = np.clip((blot - 150) / 100, 0, 1)[..., :1]                       # grey ash patches
    arr = arr * (1 - ash * 0.5) + np.array([88, 84, 80]) * ash * 0.5
    return Image.fromarray(arr.astype(np.uint8), "RGB")


def water(w, h, seed):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:h, 0:w]
    v = 0.5 + 0.5 * np.sin(xx / 17.0 + 2.2 * np.sin(yy / 23.0)) * np.sin(yy / 9.0 + xx / 41.0)
    base = np.array([34, 96, 128], dtype=float)
    arr = base + (v[..., None] - 0.5) * 26 + rng.normal(0, 3, (h, w, 1))
    crest = (v > 0.93)[..., None]
    arr = np.where(crest, arr + 40, arr)
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")


SKIRT = {"A": (2.5, 3.8), "B": (2.4, 3.5)}   # model skirt footprint (w, length) before the 0.86 model scale
KSCALE = 0.86


def spray(bg, u, lab, f, cx, cy, z):
    """Faint white spray ring just outside the skirt edge (water rows): a rounded-rectangle outline on the ground plane, rotated by the
    hull facing and projected at 40 deg, drawn as sparse 1-2 px dashes UNDER the unit."""
    import math
    w, ln = (v * KSCALE / 2 + 0.34 for v in SKIRT[lab])
    th = math.radians(22.5 * f)
    rng = np.random.default_rng(f * 7 + 1)
    pts = []
    for q in range(160):
        a = 2 * math.pi * q / 160
        c, s_ = math.cos(a), math.sin(a)
        e = 4.0                                                      # superellipse = rounded rectangle
        x = w * math.copysign(abs(c) ** (2 / e), c)
        y = ln * math.copysign(abs(s_) ** (2 / e), s_)
        xr, yr = x * math.cos(th) + y * math.sin(th), -x * math.sin(th) + y * math.cos(th)
        pts.append((cx + xr * 64 / 3.4 * z, cy - yr * 64 / 3.4 * math.sin(math.radians(40)) * z))
    px = bg.load()
    for q, (x, y) in enumerate(pts):
        if rng.random() < 0.75:
            for dx, dy in ((0, 0), (1, 0), (0, 1)):
                X, Y = int(x) + dx, int(y) + dy
                if 0 <= X < bg.width and 0 <= Y < bg.height:
                    r, g, b = px[X, Y]
                    px[X, Y] = (int(r + (235 - r) * 0.85), int(g + (245 - g) * 0.85), int(b + (250 - b) * 0.85))


def row(u, ground, team, facings, z, slot, rowh, seed, lab="A"):
    W = slot * len(facings)
    bg = (sand if ground == "sand" else water)(W, rowh, seed)
    d = ImageDraw.Draw(bg)
    for gx in range(0, W, 64 * z):                      # cell corner ticks
        for gy in range(0, rowh, 64 * z):
            d.point((gx, gy), fill=(40, 40, 40))
    for i, f in enumerate(facings):
        fr = R.remap(u.comp(f), TEAMS[team])[0]
        big = fr.resize((fr.width * z, fr.height * z), Image.NEAREST)
        cx, cy = i * slot + slot // 2, rowh // 2 + 30
        if ground == "water":
            spray(bg, u, lab, f, cx, cy, z)
        bg.paste(big, (cx - u.body.ox * z, cy - u.body.oy * z), big)
    return bg


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("a"); ap.add_argument("b"); ap.add_argument("out")   # b = "-" for an A-only sheet
    ap.add_argument("--facings", type=int, nargs="*", default=[0, 2, 4, 6, 9, 12])
    ap.add_argument("--zoom", type=int, default=2)
    ap.add_argument("--rowh", type=int, default=250)
    ap.add_argument("--ref", default="auth_tank")
    a = ap.parse_args()
    A = unit(a.a)
    B = None if a.b == "-" else unit(a.b)
    z, slot, rowh = a.zoom, 232, a.rowh
    plan = [(A, "sand", "Crimson", "A"), (A, "sand", "Lime", "A"), (A, "water", "Lime", "A"), (A, "water", "Crimson", "A")]
    if B:
        plan = [(A, "sand", "Crimson", "A"), (B, "sand", "Crimson", "B"), (A, "water", "Lime", "A"), (B, "water", "Lime", "B"),
                (A, "water", "Crimson", "A"), (B, "sand", "Lime", "B")]
    rows = []
    for i, (u, g, t, lab) in enumerate(plan):
        im = row(u, g, t, a.facings, z, slot, rowh, 10 + i, lab)
        ImageDraw.Draw(im).text((6, 4), f"variant {lab} | {g} | team {t}", fill=(255, 255, 255))
        rows.append(im)
    ref = VC.Unit(a.ref)
    W = rows[0].width
    strip = Image.new("RGB", (W, 120), (74, 66, 58))
    ImageDraw.Draw(strip).text((6, 4), "1x true size: Glide Tank (hull facing 2, 6) + Authority tank for scale", fill=(255, 255, 255))
    x = 10
    for u in [x for x in (A, B, ref) if x]:
        for f in (2, 6):
            fr = u.comp(f)
            bb = fr.getchannel("A").getbbox()
            fr = fr.crop(bb)
            strip.paste(fr, (x, 20 + (96 - fr.height) // 2), fr)
            x += fr.width + 14
        x += 30
    out = Image.new("RGB", (W, sum(r.height for r in rows) + strip.height), (30, 28, 34))
    y = 0
    for r in rows + [strip]:
        out.paste(r, (0, y)); y += r.height
    out.save(a.out)
    print("wrote", a.out, out.size)


if __name__ == "__main__":
    main()
