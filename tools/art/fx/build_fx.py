"""Procedural pixel-art effects (D-069): fire, smoke, sparks, explosions. Pillow + numpy only, no Blender.

Usage:  python3 tools/art/fx/build_fx.py [--classic]  # writes data/sprites/fx/*.png + *.sprite.toml (--classic: data/sprites_classic/fx, all sheets)
Every pixel is hard (alpha 0 or 255): no blur, no antialiasing. Fire uses its own 5-step ramp (the 24-colour art
palette has no red/orange), smoke uses 5 brown-greys with checker-dithered edges.
Origins: flames/smoke = base-centre pixel (the anchor sits on the ground), sparks/explosions = centre.
"""
import math, os, random, sys
import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(ROOT, "data", "sprites", "fx")


def hx(s):
    s = s.lstrip("#")
    return tuple(int(s[i:i + 2], 16) for i in (0, 2, 4))


FIRE = [hx(c) for c in ("#FFF6C8", "#FFD23A", "#FF8A1E", "#D8331A", "#7A1A12")]   # white-hot .. dark red
SMOKE = [hx(c) for c in ("#26231F", "#423E39", "#615C55", "#847E74", "#A9A296")]   # darkest .. lightest
DEBRIS = [hx("#26231F"), hx("#5E3420"), hx("#85502F")]
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


def bayer(x, y):
    return BAYER[y % 4, x % 4]


def fire_colour(v):
    """Heat 0..1 -> ramp colour, or None when cold."""
    if v > 0.80: return FIRE[0]
    if v > 0.58: return FIRE[1]
    if v > 0.34: return FIRE[2]
    if v > 0.13: return FIRE[3]
    if v > 0.0: return FIRE[4]
    return None


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.a = np.zeros((h, w, 4), dtype=np.uint8)

    def put(self, x, y, c):
        x, y = int(round(x)), int(round(y))
        if 0 <= x < self.w and 0 <= y < self.h and c is not None:
            self.a[y, x] = (*c, 255)

    def image(self):
        return Image.fromarray(self.a, "RGBA")


# ------------------------------------------------------------------------------------------------ flames
def tongue_field(w, h, tongues, f, n):
    """Heat field (h, w) from pointed, swaying tongues. Each tongue: dict(cx, H, W, ph, sway, k)."""
    yy, xx = np.mgrid[0:h, 0:w]
    xs, ys = xx + 0.5, yy + 0.5
    base = h - 0.5
    field = np.zeros((h, w))
    for T in tongues:
        u = f / n
        hf = 1.0 + 0.16 * math.sin(2 * math.pi * (u * T["k"] + T["ph"])) + 0.07 * math.sin(2 * math.pi * (u * 3 + T["ph"] * 2))
        H = T["H"] * hf
        t = (base - ys) / H
        valid = (t > -0.02) & (t < 1.0)
        tt = np.clip(t, 0, 1)
        bend = T["sway"] * np.sin(2 * math.pi * (u * T["k"] + T["ph"] + tt * 0.55)) * tt ** 1.4
        lean = T.get("lean", 0.0) * tt ** 1.6
        c = T["cx"] + bend + lean
        hw = T["W"] * (1 - tt) ** 0.8 * (1.0 + 0.22 * np.sin(math.pi * np.clip(tt * 2.2, 0, 1)) * 0.0)
        hw = np.maximum(hw, 0.0001)
        d = np.abs(xs - c) / hw
        v = np.clip(1 - d, 0, 1) * (1 - 0.5 * tt)
        v = np.where(valid & (d < 1), v, 0.0)
        field = np.maximum(field, v)
    return field


def licks(cv, tongues, f, n):
    """Small detached flame particles that peel off the tips and rise."""
    for T in tongues:
        u = ((f / n) * T["k"] + T["ph"] * 1.3) % 1.0
        H = T["H"]
        y = cv.h - 0.5 - H * (0.78 + 0.5 * u)
        x = T["cx"] + T["sway"] * math.sin(2 * math.pi * (u + T["ph"])) * 1.4 + T.get("lean", 0) * 0.9
        col = FIRE[1] if u < 0.25 else FIRE[2] if u < 0.55 else FIRE[3] if u < 0.8 else None
        if col is None: continue
        cv.put(x, y, col)
        if u < 0.55:
            cv.put(x, y + 1, FIRE[2] if u < 0.25 else FIRE[3])


def flame_frames(w, h, tongues, n, lick=True):
    frames = []
    for f in range(n):
        cv = Canvas(w, h)
        fld = tongue_field(w, h, tongues, f, n)
        for y in range(h):
            for x in range(w):
                v = fld[y, x]
                # a light ordered dither only on the band borders keeps the gradient hard-pixel
                v2 = v + (bayer(x, y) - 0.5) * 0.07 if v > 0 else 0
                c = fire_colour(v2)
                if c is not None:
                    cv.a[y, x] = (*c, 255)
        if lick:
            licks(cv, tongues, f, n)
        # base glow row: the bottom row is always hot so the fire sits on its anchor
        frames.append(cv)
    return frames


def fire_small():
    w, h, n = 16, 24, 8
    T = [dict(cx=8, H=19, W=4.6, ph=0.00, sway=1.6, k=1, lean=0.6),
         dict(cx=4.6, H=12, W=3.0, ph=0.37, sway=1.3, k=2, lean=-1.2),
         dict(cx=11.4, H=14, W=3.2, ph=0.71, sway=1.4, k=1, lean=1.5)]
    return w, h, n, flame_frames(w, h, T, n)


def fire_large():
    w, h, n = 24, 40, 8
    T = [dict(cx=12, H=34, W=6.6, ph=0.00, sway=2.4, k=1, lean=0.8),
         dict(cx=6.8, H=24, W=4.6, ph=0.30, sway=2.0, k=2, lean=-2.0),
         dict(cx=17.4, H=27, W=4.8, ph=0.62, sway=2.1, k=1, lean=2.4),
         dict(cx=3.4, H=14, W=3.0, ph=0.85, sway=1.5, k=2, lean=-1.4),
         dict(cx=20.6, H=16, W=3.2, ph=0.15, sway=1.6, k=1, lean=1.6),
         dict(cx=12, H=19, W=4.2, ph=0.50, sway=1.8, k=2, lean=-0.6)]
    return w, h, n, flame_frames(w, h, T, n)


# ------------------------------------------------------------------------------------------------ smoke
def smoothstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)


def smoke_plume():
    w, h, n = 32, 64, 12
    rng = random.Random(2027)
    puffs = []
    for k in range(6):
        lobes = [(rng.uniform(-0.55, 0.55), rng.uniform(-0.45, 0.45), rng.uniform(0.55, 0.85)) for _ in range(3)]
        puffs.append(dict(off=k / 6, lobes=lobes, wob=rng.uniform(0, 6.28), dir=1))
    frames = []
    for f in range(n):
        cv = Canvas(w, h)
        order = []
        for P in puffs:
            a = (f / n + P["off"]) % 1.0
            order.append((a, P))
        order.sort(key=lambda t: -t[0])          # old (high, big) first, young on top
        for a, P in order:
            r = 4.5 + 7.5 * a ** 0.7
            cx = 15.5 + 7.0 * a + 2.2 * math.sin(2 * math.pi * a * 1.2 + P["wob"]) * a
            cy = 60.0 - 45.0 * a ** 0.9
            opacity = smoothstep(0.0, 0.14, a) * (1 - smoothstep(0.62, 1.0, a))
            base_shade = 0 + 3.2 * a                # young puffs are dark, old ones pale
            for y in range(max(0, int(cy - r * 1.7)), min(h, int(cy + r * 1.7) + 1)):
                for x in range(max(0, int(cx - r * 1.7)), min(w, int(cx + r * 1.7) + 1)):
                    best = None
                    for (lx, ly, lr) in P["lobes"]:
                        px, py, pr = cx + lx * r, cy + ly * r, r * lr
                        d = math.hypot(x + 0.5 - px, y + 0.5 - py) / pr
                        if d < 1.0 and (best is None or d < best[0]):
                            best = (d, px, py, pr)
                    if best is None:
                        continue
                    d, px, py, pr = best
                    if bayer(x, y) >= opacity and opacity < 0.999:
                        continue
                    # light from the upper left: shade falls off towards the lower right of the lobe
                    lx_ = (x + 0.5 - px) / pr
                    ly_ = (y + 0.5 - py) / pr
                    lit = -0.55 * lx_ - 0.65 * ly_            # -1..1
                    s = base_shade + 0.9 - 1.5 * (0.5 + 0.5 * lit) + (0.5 if d < 0.55 else 0.0) * 0
                    s += (bayer(x + 1, y + 2) - 0.5) * 0.6
                    idx = int(min(4, max(0, math.floor(s + 0.5))))
                    if d > 0.88:
                        idx = 0                            # 1 px dark outline
                    cv.put(x, y, SMOKE[idx])
        frames.append(cv)
    return w, h, n, frames


# ------------------------------------------------------------------------------------------------ sparks
def sparks():
    w, h, n = 16, 16, 6
    rng = random.Random(7)
    parts = []
    for k in range(9):
        ang = rng.uniform(-math.pi * 0.95, -math.pi * 0.05) if k < 7 else rng.uniform(0, math.pi)
        sp = rng.uniform(1.6, 3.4)
        parts.append((math.cos(ang) * sp, math.sin(ang) * sp * 0.85, rng.uniform(0.0, 0.15)))
    frames = []
    for f in range(n):
        cv = Canvas(w, h)
        cx, cy = 7.5, 10.5
        if f == 0:
            for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                cv.put(cx + dx, cy + dy, FIRE[0])
            for dx, dy in ((2, 0), (-2, 0), (0, -2), (0, 2)):
                cv.put(cx + dx, cy + dy, FIRE[1])
        elif f == 1:
            for dx, dy in ((0, 0), (1, 0), (0, -1)):
                cv.put(cx + dx, cy + dy, FIRE[1])
        for vx, vy, delay in parts:
            t = f + 0.3 - delay * 6
            if t < 0:
                continue
            x = cx + vx * t
            y = cy + vy * t + 0.32 * t * t
            life = f / (n - 1)
            col = FIRE[0] if life < 0.2 else FIRE[1] if life < 0.45 else FIRE[2] if life < 0.7 else FIRE[3] if life < 0.9 else None
            if col is None:
                continue
            cv.put(x, y, col)
            if f < 4:
                cv.put(x - vx * 0.45, y - (vy + 0.32 * t) * 0.45, FIRE[2] if life < 0.45 else FIRE[3])
        frames.append(cv)
    return w, h, n, frames


# ------------------------------------------------------------------------------------------------ explosions
def explosion(size, n, seed, nlobes, debris_n, ring):
    w = h = size
    R = size * 0.46
    rng = random.Random(seed)
    lobes = []
    lobes.append(dict(ang=0, dist=0.0, s=0.0, r=0.58, hot=1.0))
    for k in range(nlobes):
        a = 2 * math.pi * (k + rng.uniform(-0.3, 0.3)) / nlobes
        lobes.append(dict(ang=a, dist=rng.uniform(0.28, 0.62), s=rng.uniform(0.0, 0.32), r=rng.uniform(0.28, 0.46), hot=rng.uniform(0.8, 1.0)))
    deb = []
    for k in range(debris_n):
        a = rng.uniform(0, 2 * math.pi)
        sp = rng.uniform(0.55, 1.0) * R * 1.25
        deb.append((math.cos(a) * sp, math.sin(a) * sp * 0.8 - R * 0.35, rng.choice(DEBRIS)))
    frames = []
    for f in range(n):
        t = f / (n - 1)
        cv = Canvas(w, h)
        cx0, cy0 = w / 2, h / 2 + R * 0.12
        cool = 0.12 + 0.40 * smoothstep(0.42, 0.9, t)         # below this heat a pixel is smoke, not fire
        fade = smoothstep(0.78, 1.0, t)
        for y in range(h):
            for x in range(w):
                T = 0.0
                cover = False
                for L in lobes:
                    u = (t - L["s"]) / (1 - L["s"])
                    if u < 0:
                        continue
                    grow = math.sqrt(min(1.0, u * 2.4))
                    rr = L["r"] * R * (0.35 + 0.65 * grow) * (1.0 + 0.25 * u)
                    px = cx0 + math.cos(L["ang"]) * L["dist"] * R * min(1.0, 0.35 + 1.4 * u)
                    py = cy0 + math.sin(L["ang"]) * L["dist"] * R * 0.85 * min(1.0, 0.35 + 1.4 * u) - R * 0.5 * u * u
                    d = math.hypot(x + 0.5 - px, y + 0.5 - py)
                    if d < rr:
                        cover = True
                        heat = L["hot"] * max(0.0, 1 - 1.15 * u) ** 1.25
                        T = max(T, heat * (1 - d / rr) ** 0.75 + (0.35 * heat if d < rr * 0.45 else 0))
                if not cover:
                    continue
                if bayer(x, y) < fade:
                    continue
                T2 = T + (bayer(x, y) - 0.5) * 0.08
                if T2 > cool:
                    cv.a[y, x] = (*fire_colour(min(1.0, T2)), 255)
                elif t > 0.3:
                    s = 4 - int(min(4, max(0, (T2 / max(cool, 1e-6)) * 3.0 + (1 - t) * 1.2)))
                    s = max(0, min(4, s if T2 > 0.02 else 1))
                    cv.a[y, x] = (*SMOKE[min(4, s)], 255)
                else:
                    cv.a[y, x] = (*FIRE[4], 255)
        # shock ring (first third)
        if ring and t < 0.34:
            rr = R * (0.55 + 1.9 * t)
            for ang in range(0, 360, 1):
                x = cx0 + math.cos(math.radians(ang)) * rr
                y = cy0 + math.sin(math.radians(ang)) * rr * 0.8
                cv.put(x, y, FIRE[0] if t < 0.17 else FIRE[1])
        # debris
        for vx, vy, col in deb:
            tt = t * 1.0
            x = cx0 + vx * tt
            y = cy0 + vy * tt + R * 0.9 * tt * tt
            if tt > 0.22 and tt < 0.95:
                cv.put(x, y, col)
                if size >= 64:
                    cv.put(x + 1, y, col)
        frames.append(cv)
    return w, h, n, frames


def explosion_small():
    return explosion(32, 10, 11, 5, 5, False)


def explosion_large():
    return explosion(64, 12, 29, 8, 12, True)


# ------------------------------------------------------------------------------------------------ output
EFFECTS = {
    # name: (builder, ticks_per_frame, loop, origin (x, y))
    "fire_small": (fire_small, 3, True, "base"),
    "fire_large": (fire_large, 3, True, "base"),
    "smoke_plume": (smoke_plume, 3, True, "base"),
    "sparks": (sparks, 2, False, "centre"),
    "explosion_small": (explosion_small, 2, False, "centre"),
    "explosion_large": (explosion_large, 3, False, "centre"),
}


# Sheets replaced by art imported with import_scifi_fx.py (S15). `--classic` writes ALL the procedural sheets to data/sprites_classic/fx
# (the RTS_FX=classic comparison set) and leaves data/sprites/fx alone.
IMPORTED = {"explosion_small", "explosion_large", "fire_small", "fire_large"}
CLASSIC = "--classic" in sys.argv
if CLASSIC:
    OUT = os.path.join(ROOT, "data", "sprites_classic", "fx")


def build_all():
    os.makedirs(OUT, exist_ok=True)
    result = {}
    for name, (fn, tpf, loop, org) in EFFECTS.items():
        if name in IMPORTED and not CLASSIC:
            print("skip", name, "(now imported art: tools/art/fx/import_scifi_fx.py; pass --classic to rebuild the old sheet)")
            continue
        w, h, n, frames = fn()
        sheet = Image.new("RGBA", (w * n, h), (0, 0, 0, 0))
        for i, cv in enumerate(frames):
            sheet.paste(cv.image(), (i * w, 0))
        arr = np.asarray(sheet)
        assert set(np.unique(arr[..., 3])) <= {0, 255}, name
        sheet.save(os.path.join(OUT, f"{name}.png"))
        ox, oy = (w // 2, h - 1) if org == "base" else (w // 2, h // 2)
        lines = [f"# Effect sprite {name} (D-069). Generated by tools/art/fx/build_fx.py -- do not hand-edit.",
                 f"# One row of {n} frames. Origin = {'base-centre pixel: sits on the anchor' if org == 'base' else 'centre pixel'}.",
                 "", f'file = "{name}.png"', f"frame_w = {w}", f"frame_h = {h}", f"origin_x = {ox}", f"origin_y = {oy}", "",
                 "[[state]]", 'name = "play"', "facings = 1", f"frames = {n}", "first = 0", f"ticks_per_frame = {tpf}"]
        if not loop:
            lines.append("loop = false")
        open(os.path.join(OUT, f"{name}.sprite.toml"), "w").write("\n".join(lines) + "\n")
        result[name] = dict(w=w, h=h, n=n, tpf=tpf, loop=loop, origin=(ox, oy), frames=[cv.image() for cv in frames])
        print("wrote", name, w, h, n)
    return result


if __name__ == "__main__":
    build_all()
