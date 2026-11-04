"""Contact sheet for one layered vehicle (turret / air shadow aware): python3 tools/art/veh_contact.py <id> [--ref auth_tank] [--out art/pilot/<id>_contact.png]
1x (true size) all 16 facings composited (turret at hull facing, shadow under air units), 2x close-ups, independent turret
traverse, size comparison with a reference unit (and a 2x2-cell outline for the land-train), exact house remap.
Composite rule: turret frame origin = hull origin + pivot rotated by the hull facing, projected at 40 deg, 64 px per cell."""
import argparse, math, os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import common as C
import remap as R
from contact_sheet import ash_patch, hcat, vcat, BG, FG

VEH = os.path.join(C.ROOT, "data", "sprites", "vehicles")
GROUND = (74, 66, 58)
CELLPX, CELLW = 64, 3.4


def manifest(name):
    d = {}
    for ln in open(os.path.join(VEH, name + ".sprite.toml")):
        ln = ln.strip()
        if "=" in ln and not ln.startswith(("#", "[", "ramp")):
            k, v = [x.strip() for x in ln.split("=", 1)]
            d.setdefault(k, []).append(v.strip('"'))
    return d


class Sheet:
    def __init__(self, name):
        m = manifest(name)
        self.fw, self.fh = int(m["frame_w"][0]), int(m["frame_h"][0])
        self.ox, self.oy = int(m["origin_x"][0]), int(m["origin_y"][0])
        self.img = Image.open(os.path.join(VEH, name + ".png")).convert("RGBA")
        self.cols = self.img.width // self.fw
        self.m = m

    def frame(self, f):
        x, y = (f % self.cols) * self.fw, (f // self.cols) * self.fh
        return self.img.crop((x, y, x + self.fw, y + self.fh))


class Unit:
    def __init__(self, vid):
        self.body = Sheet(vid)
        self.turret = Sheet(vid + "_turret") if os.path.exists(os.path.join(VEH, vid + "_turret.png")) else None
        self.shadow = Sheet(vid + "_shadow") if os.path.exists(os.path.join(VEH, vid + "_shadow.png")) else None
        if self.turret:
            self.pivot = tuple(float(self.turret.m[k][0]) for k in ("pivot_x", "pivot_y"))

    def comp(self, hf, tf=None):
        """frame (RGBA, body canvas) for hull facing hf and turret facing tf (default = hull facing)."""
        b = self.body
        out = Image.new("RGBA", (b.fw, b.fh), (0, 0, 0, 0))
        if self.shadow:
            out.alpha_composite(self.shadow.frame(hf))
        out.alpha_composite(b.frame(hf))
        if self.turret:
            th = math.radians(22.5 * hf)
            x, y = self.pivot
            xr, yr = x * math.cos(th) + y * math.sin(th), -x * math.sin(th) + y * math.cos(th)
            dx, dy = xr * CELLPX, -yr * CELLPX * math.sin(math.radians(40))
            t = self.turret.frame(hf if tf is None else tf)
            out.alpha_composite(t, (round(dx), round(dy)))
        return out


def trim(frames, pad=2):
    """crop every frame to the union alpha bounding box of the set (keeps the common alignment)"""
    bb = None
    for f in frames:
        b = f.getchannel("A").getbbox()
        if b:
            bb = b if bb is None else (min(bb[0], b[0]), min(bb[1], b[1]), max(bb[2], b[2]), max(bb[3], b[3]))
    bb = (max(bb[0] - pad, 0), max(bb[1] - pad, 0), min(bb[2] + pad, frames[0].width), min(bb[3] + pad, frames[0].height))
    return [f.crop(bb) for f in frames]


def grid(frames, cols, z=1, gap=2):
    if z > 1:
        frames = trim(frames)
    w, h = frames[0].width * z, frames[0].height * z
    rows = (len(frames) + cols - 1) // cols
    out = Image.new("RGB", (cols * (w + gap) - gap, rows * (h + gap) - gap), GROUND)
    for i, f in enumerate(frames):
        im = f.resize((w, h), Image.NEAREST) if z > 1 else f
        out.paste(im, ((i % cols) * (w + gap), (i // cols) * (h + gap)), im)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id")
    ap.add_argument("--ref", default="auth_tank")
    ap.add_argument("--out", default=None)
    ap.add_argument("--close", type=int, nargs="*", default=[0, 2, 5, 11])
    a = ap.parse_args()
    U = Unit(a.id)
    ref = Unit(a.ref)
    blocks = []
    fr = [U.comp(f) for f in range(16)]
    cols = 8 if U.body.fw > 120 else 16
    layers = "body" + (" + turret layer" if U.turret else "") + (" + separate ground-shadow sheet" if U.shadow else "")
    blocks.append((f"{a.id}: all 16 facings at 1x, true size (frame 0 = N, clockwise), {U.body.fw} px frames; {layers}", grid(trim(fr, 1) if U.body.fw > 140 else fr, cols, 1)))
    blocks.append((f"2x nearest close-up: facings {' | '.join(map(str, a.close))}", hcat([grid([fr[f]], 1, 2) for f in a.close], 8)))
    if U.turret:
        hf = 2
        blocks.append((f"TURRET traverse (hull facing {hf} fixed, turret facings 0,3,6,9,12,15 at 2x)",
                       hcat([grid([U.comp(hf, t)], 1, 2) for t in (0, 3, 6, 9, 12, 15)], 8)))
    if U.shadow:
        sh = U.shadow.frame(2)
        blocks.append(("AIR layers at 2x, facing 2: shadow sheet alone | body sheet alone (floats above its origin) | composited | origin cross (red = ground origin)",
                       hcat([grid([sh], 1, 2), grid([U.body.frame(2)], 1, 2), grid([fr[2]], 1, 2), _cross(U, 2)], 8)))
    # size comparison on the ground: reference at facing 2 and 6, subject at facing 2 and 6, 64 px cell ticks
    cmp_w = 8 * CELLPX
    air = 1 if U.shadow else 0
    img = ash_patch(CELLPX, 2027).crop((0, 0, cmp_w, (4 + air) * CELLPX)).convert("RGBA")
    d = ImageDraw.Draw(img)
    for gx in range(0, img.width, CELLPX):
        for gy in range(0, img.height, CELLPX):
            d.point((gx, gy), fill=(120, 112, 104))
    def put(u, f, cx, cy):
        fm = u.comp(f)
        img.alpha_composite(fm, (int(cx * CELLPX) - u.body.ox, int(cy * CELLPX) - u.body.oy))
    put(ref, 2, 1.0, 1.0 + air); put(U, 2, 3.5, 1.0 + air); put(ref, 6, 1.0, 3.0 + air); put(U, 6, 3.5, 3.0 + air)
    put(ref, 0, 6.0, 1.0 + air); put(U, 0, 7.0, 1.0 + air); put(ref, 4, 6.0, 3.0 + air); put(U, 4, 7.0, 3.0 + air)
    blocks.append((f"SIZE vs {a.ref} at 1x on ash (cell ticks every 64 px): {a.ref} left of each pair, {a.id} right; facings 2/6 and 0/4", img.convert("RGB")))
    if a.id == "dir_landtrain":
        cy = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", "auth_conyard.png")).convert("RGBA").crop((0, 0, 192, 248))
        img2 = ash_patch(CELLPX, 11).crop((0, 0, 8 * CELLPX, 4 * CELLPX)).convert("RGBA")
        img2.alpha_composite(cy, (0, 0 - 56 + 0))
        dd = ImageDraw.Draw(img2)
        x0, y0 = 4 * CELLPX, CELLPX
        dd.rectangle((x0, y0, x0 + 2 * CELLPX - 1, y0 + 2 * CELLPX - 1), outline=(240, 220, 90))
        for i, f in enumerate((0, 4)):
            fm = U.comp(f)
            img2.alpha_composite(fm, (x0 + CELLPX - U.body.ox, y0 + CELLPX - U.body.oy)) if i == 0 else None
        x1 = 6 * CELLPX
        dd.rectangle((x1, y0, x1 + 2 * CELLPX - 1, y0 + 2 * CELLPX - 1), outline=(240, 220, 90))
        img2.alpha_composite(U.comp(2), (x1 + CELLPX - U.body.ox, y0 + CELLPX - U.body.oy))
        blocks.append(("2x2-CELL FOOTPRINT check at 1x: yellow 128 px squares (cell ticks every 64 px), land-train at facing 0 and facing 2, Authority Construction Yard (3x3) at left", img2.convert("RGB")))
    # house remap at 2x, facing 2
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    base = U.comp(2)
    vs = [base]
    for n, rp in R.HOUSES.items():
        out, _c = R.remap(base, rp)
        a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
        ch = (a0 != a1).any(axis=1)
        assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
        print(a.id, n, "pixels changed:", int(ch.sum()))
        vs.append(out)
    blocks.append(("HOUSE REMAP at 2x, facing 2: magenta original | Authority green | Directorate rust #8B2E1A | blue ally #2E5A8B", grid(vs, 4, 2, 4)))
    W = max(b[1].width for b in blocks) + 20
    out = Image.new("RGB", (W, 10 + sum(b[1].height + 26 for b in blocks)), BG)
    dr = ImageDraw.Draw(out)
    y = 10
    for label, im in blocks:
        dr.text((10, y), label, fill=FG)
        out.paste(im, (10, y + 16))
        y += im.height + 26
    path = a.out or os.path.join(C.PILOT, f"{a.id}_contact.png")
    out.save(path, optimize=True)
    print("wrote", path, out.size)


def _cross(U, f):
    fm = U.comp(f)
    im = Image.new("RGB", fm.size, GROUND)
    im.paste(fm, (0, 0), fm)
    d = ImageDraw.Draw(im)
    ox, oy = U.body.ox, U.body.oy
    d.line((ox - 4, oy, ox + 4, oy), fill=(255, 40, 40))
    d.line((ox, oy - 4, ox, oy + 4), fill=(255, 40, 40))
    al = fm.getchannel("A").getbbox()
    box = (max(min(al[0], ox - 6) - 2, 0), max(min(al[1], oy - 6) - 2, 0), min(max(al[2], ox + 6) + 2, im.width), min(max(al[3], oy + 6) + 2, im.height))
    im = im.crop(box)
    return im.resize((im.width * 2, im.height * 2), Image.NEAREST)


if __name__ == "__main__":
    main()
