"""Motion pass contact sheet + driving GIF.  Usage: python3 tools/art/mot_contact.py --before <dir> --after <dir> --png out.png --gif out.gif
<dir> holds <id>.png + <id>.sprite.toml for auth_tank and auth_jeep (before = the old idle-only sheets).  Pure PIL; no Blender.
Sheet: true size (1x = in-game pixels, since the unit sprite is drawn 1:1) and 3x nearest, on rustbelt clear and ash ground.
GIF: both vehicles drive a loop through all facings at game speed (20 ticks/s); top lane old idle sprites, bottom lane new move frames.
Frame choice mirrors SpriteStates.UnitFrame: step = travel_leptons * px_per_cell / 256 / 1.25 / step_px.
"""
import argparse, math, os, tomllib
from PIL import Image, ImageDraw

CLEAR, ASH = (0x8E, 0x83, 0x71), (0x42, 0x39, 0x4D)
SPEED = {"auth_tank": 20, "auth_jeep": 36}   # leptons per tick (data/rules/vehicles.toml)
IDS = ("auth_tank", "auth_jeep")


class Sheet:
    def __init__(self, d, i):
        self.t = tomllib.load(open(os.path.join(d, i + ".sprite.toml"), "rb"))
        self.img = Image.open(os.path.join(d, i + ".png")).convert("RGBA")
        self.fw, self.fh = self.t["frame_w"], self.t["frame_h"]
        self.cols = self.img.width // self.fw
        self.ox, self.oy = self.t["origin_x"], self.t["origin_y"]
        self.states = {s["name"]: s for s in self.t["state"]}

    def frame(self, state, facing32, step=0):
        s = self.states[state]
        n = s["first"] + facing32 * s["frames"] + step % s["frames"]
        x, y = (n % self.cols) * self.fw, (n // self.cols) * self.fh
        return self.img.crop((x, y, x + self.fw, y + self.fh))

    def move_step(self, travel_lep):
        sp = self.states["move"].get("step_px", 0)
        return int(travel_lep * self.t.get("px_per_cell", 64) / 256 / 1.25 / sp) if sp else 0


def paste(canvas, fr, ox_, oy_, sh, scale=1):
    if scale != 1:
        fr = fr.resize((fr.width * scale, fr.height * scale), Image.NEAREST)
    canvas.alpha_composite(fr, (int(ox_ - sh.ox * scale), int(oy_ - sh.oy * scale)))


def contact(before, after, path):
    blocks = []
    for ground in (CLEAR, ASH):
        rows = []
        for i in IDS:
            b, a = before[i], after[i]
            f = b.fw
            facs = [0, 4, 8, 12, 16, 20, 24, 28]
            w = f * len(facs)
            im = Image.new("RGBA", (w, f * 5), ground + (255,))
            for c, fc in enumerate(facs):
                im.alpha_composite(b.frame("idle", fc), (c * f, 0))
                for k in range(4):
                    im.alpha_composite(a.frame("move", fc, k), (c * f, f * (1 + k)))
            rows.append(im)
        blocks.append(rows)
    # 1x: before row then 4 move frames, per vehicle, clear (left) and ash (right) stacked
    gap = 8
    w1 = max(r.width for r in blocks[0])
    one = Image.new("RGBA", (w1 * 2 + gap, sum(r.height for r in blocks[0]) + gap), (20, 20, 20, 255))
    y = 0
    for r0, r1 in zip(*blocks):
        one.alpha_composite(r0, (0, y)); one.alpha_composite(r1, (w1 + gap, y)); y += r0.height + gap
    # 3x: before | k0 k1 k2 k3 for two facings per vehicle on clear ground, ash beside
    cells = []
    for ground in (CLEAR, ASH):
        col = []
        for i in IDS:
            b, a = before[i], after[i]
            f = b.fw * 3
            for fc in (6, 22):
                im = Image.new("RGBA", (f * 5, f), ground + (255,))
                im.alpha_composite(b.frame("idle", fc).resize((f, f), Image.NEAREST), (0, 0))
                for k in range(4):
                    im.alpha_composite(a.frame("move", fc, k).resize((f, f), Image.NEAREST), (f * (1 + k), 0))
                col.append(im)
        cells.append(col)
    three = Image.new("RGBA", (cells[0][0].width, sum(c.height for c in cells[0]) + sum(c.height for c in cells[1]) + gap * 8), (20, 20, 20, 255))
    y = 0
    for col in cells:
        for im in col:
            three.alpha_composite(im, (0, y)); y += im.height + gap
    W = max(one.width, three.width)
    out = Image.new("RGBA", (W, one.height + three.height + 3 * gap), (20, 20, 20, 255))
    out.alpha_composite(one, (0, 0))
    out.alpha_composite(three, (0, one.height + 2 * gap))
    out.convert("RGB").save(path)
    print("wrote", path, out.size)


def _track(w, h, r):
    """Dense closed polyline of a rounded rectangle (clockwise on screen), as [(x, y, cumulative_length)]."""
    pts = []
    for cx, cy, a0 in ((w - r, r, -90), (w - r, h - r, 0), (r, h - r, 90), (r, r, 180)):
        for j in range(0, 31):
            a = math.radians(a0 + 3 * j)
            pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    pts.append(pts[0])
    out, acc = [], 0.0
    for j, (x, y) in enumerate(pts):
        if j:
            acc += math.hypot(x - pts[j - 1][0], y - pts[j - 1][1])
        out.append((x, y, acc))
    return out


def lane_path(w, h, t, _cache={}):
    """Position and heading (deg, 0 = north, clockwise) after travelling t px round the rounded-rectangle track."""
    tr = _cache.setdefault((w, h), _track(w, h, h * 0.4))
    t %= tr[-1][2]
    j = next(k for k in range(1, len(tr)) if tr[k][2] >= t)
    (x0, y0, d0), (x1, y1, d1) = tr[j - 1], tr[j]
    u = (t - d0) / max(d1 - d0, 1e-9)
    hd = math.degrees(math.atan2(x1 - x0, -(y1 - y0))) % 360
    return x0 + (x1 - x0) * u, y0 + (y1 - y0) * u, hd


def gif(before, after, path, ticks=200):
    W, H = 640, 360
    frames = []
    lanes = [("BEFORE (idle only)", 0), ("AFTER (move frames)", H // 2)]
    for tick in range(ticks):
        im = Image.new("RGBA", (W, H), CLEAR + (255,))
        d = ImageDraw.Draw(im)
        for gx in range(0, W, 64):          # 64 px world cell dots so travel is visible against the ground
            for gy in range(0, H, 64):
                d.rectangle((gx, gy, gx + 1, gy + 1), fill=(0x7D, 0x73, 0x63))
        for name, y0 in lanes:
            d.text((6, y0 + 4), name, fill=(255, 255, 255))
            d.line((0, y0 + H // 2 - 1, W, y0 + H // 2 - 1), fill=(0x6A, 0x60, 0x52))
        for lane, (name, y0) in enumerate(lanes):
            for n, i in enumerate(IDS):
                sh = (before if lane == 0 else after)[i]
                px_tick = SPEED[i] * 64 / 256            # screen px per tick at zoom 1
                aw, ah = 250, 64
                ax = 20 + n * 300
                s = tick * px_tick
                x, y, hd = lane_path(aw, ah, s + n * 150)
                facing = round(hd / 11.25) % 32
                travel = tick * SPEED[i]
                if lane == 0:
                    fr = sh.frame("idle", facing)
                else:
                    fr = sh.frame("move", facing, sh.move_step(travel))
                paste(im, fr, ax + x + 50, y0 + 70 + y, sh)
        frames.append(im.convert("RGB"))
    frames[0].save(path, save_all=True, append_images=frames[1:], duration=50, loop=0, optimize=True)
    print("wrote", path, len(frames), "frames")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--before", required=True)
    ap.add_argument("--after", required=True)
    ap.add_argument("--png")
    ap.add_argument("--gif")
    a = ap.parse_args()
    before = {i: Sheet(a.before, i) for i in IDS}
    after = {i: Sheet(a.after, i) for i in IDS}
    if a.png: contact(before, after, a.png)
    if a.gif: gif(before, after, a.gif)


if __name__ == "__main__":
    main()
