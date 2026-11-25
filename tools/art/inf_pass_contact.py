"""S43 infantry readability pass: contact sheets and GIF.
Usage: python3 tools/art/inf_pass_contact.py --var NAME=DIR [--var ...] --out PNG [--gif GIF --gif-var NAME]
Each DIR holds <id>.png + <id>.sprite.toml for auth_rifleman and dir_conscript.  The first --var is the 'before'."""
import argparse, os, sys, tomllib
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
CLEAR, ASH = (0x8E, 0x83, 0x71, 255), (0x42, 0x39, 0x4D, 255)     # rustbelt land.clear / land.ash base


class Sheet:
    def __init__(self, png, toml):
        self.im = Image.open(png).convert("RGBA")
        self.m = tomllib.load(open(toml, "rb"))
        self.w, self.h = self.m["frame_w"], self.m["frame_h"]
        self.o = (self.m["origin_x"], self.m["origin_y"])
        self.cols = self.im.width // self.w
        self.st = {s["name"]: s for s in self.m["state"]}

    def n(self, state):
        return self.st[state]["frames"]

    def frame(self, state, facing16, step=0):
        s = self.st[state]
        f = facing16 * s["facings"] // 16 if s["facings"] != 1 else 0
        i = s["first"] + f * s["frames"] + step
        c, r = i % self.cols, i // self.cols
        return self.im.crop((c * self.w, r * self.h, (c + 1) * self.w, (r + 1) * self.h))


def put(canvas, sh, state, facing, step, foot):
    fr = sh.frame(state, facing, step)
    canvas.alpha_composite(fr, (foot[0] - sh.o[0], foot[1] - sh.o[1]))


def load(d, sid):
    return Sheet(os.path.join(d, f"{sid}.png"), os.path.join(d, f"{sid}.sprite.toml"))


def zoom(im, z=3):
    return im.resize((im.width * z, im.height * z), Image.NEAREST)


def label(im, text, xy=(3, 2)):
    ImageDraw.Draw(im).text(xy, text, fill=(255, 255, 255, 255))


def variant_block(vars_, ground):
    """rows = variants; per unit: idle facings 0,3,6,9,12 (N, ..), walk step 2 east, fire step 1 east"""
    cell = 60
    units = ("auth_rifleman", "dir_conscript")
    cols_per_unit = 8
    W = cell * cols_per_unit * len(units) + 70
    H = cell * len(vars_)
    cv = Image.new("RGBA", (W, H), ground)
    for r, (name, d) in enumerate(vars_):
        label(cv, name, (3, r * cell + 4))
        for u, sid in enumerate(units):
            sh = load(d, sid)
            x0 = 70 + u * cell * cols_per_unit
            seq = [("idle", 0, 0), ("idle", 3, 0), ("idle", 6, 0), ("idle", 9, 0), ("idle", 12, 0), ("walk", 4, sh.n("walk") // 3), ("fire", 4, 0), ("fire", 4, 1)]
            for k, (stt, fa, stp) in enumerate(seq):
                put(cv, sh, stt, fa, stp, (x0 + k * cell + cell // 2, r * cell + 48))
    return cv


def crowd(d, ground, jeep_dir):
    """10 mixed infantry (5 of each faction, varied facings and states) beside an auth_jeep frame, true size."""
    a, c = load(d, "auth_rifleman"), load(d, "dir_conscript")
    jeep = Sheet(os.path.join(jeep_dir, "auth_jeep.png"), os.path.join(jeep_dir, "auth_jeep.sprite.toml"))
    cv = Image.new("RGBA", (330, 150), ground)
    spots = [(30, 40), (62, 48), (96, 38), (128, 50), (160, 42), (36, 84), (70, 96), (104, 86), (140, 98), (172, 88)]
    order = [(a, "idle", 4, 0), (c, "idle", 4, 0), (a, "walk", 5, 1), (c, "walk", 5, 2), (a, "fire", 4, 1),
             (c, "fire", 6, 0), (a, "idle", 12, 0), (c, "walk", 3, 4), (a, "walk", 7, 3), (c, "idle", 8, 0)]
    for (x, y), (sh, stt, fa, stp) in zip(spots, order):
        put(cv, sh, stt, fa, stp, (x, y))
    jf = jeep.frame("idle", 0, 0) if False else None
    s = jeep.st["idle"]
    f = 8 * s["facings"] // 32
    c0, r0 = f % jeep.cols, f // jeep.cols
    fr = jeep.im.crop((c0 * jeep.w, r0 * jeep.h, (c0 + 1) * jeep.w, (r0 + 1) * jeep.h))
    cv.alpha_composite(fr, (205 - jeep.o[0] + 55, 75 - jeep.o[1]))
    return cv


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--var", action="append", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--crowd-vars", nargs="*", default=None)
    ap.add_argument("--gif", default=None)
    ap.add_argument("--gif-var", default=None)
    ap.add_argument("--jeep-dir", default=os.path.join(ROOT, "data", "sprites", "vehicles"))
    a = ap.parse_args()
    vars_ = [tuple(v.split("=", 1)) for v in a.var]
    d = dict(vars_)
    names = a.crowd_vars or [vars_[0][0], vars_[-1][0]]
    parts = []
    for gname, g in (("clear", CLEAR), ("ash", ASH)):
        parts.append(variant_block(vars_, g))
    for n in names:
        row = Image.new("RGBA", (660, 150), CLEAR)
        row.alpha_composite(crowd(d[n], CLEAR, a.jeep_dir), (0, 0))
        row.alpha_composite(crowd(d[n], ASH, a.jeep_dir), (330, 0))
        label(row, "crowd " + n, (3, 2))
        parts.append(row)
    W = max(p.width for p in parts)
    true = Image.new("RGBA", (W, sum(p.height + 6 for p in parts)), (20, 20, 20, 255))
    y = 0
    for p in parts:
        true.alpha_composite(p, (0, y)); y += p.height + 6
    # phone sheet: true size on top, 3x below (cropped halves so it stays readable)
    z = zoom(true, 3)
    base = a.out
    true.save(base.replace(".png", "_1x.png"))
    z.save(base.replace(".png", "_3x.png"))
    print("wrote", base.replace(".png", "_1x.png"), true.size, base.replace(".png", "_3x.png"), z.size)
    if a.gif:
        make_gif(d[a.gif_var], a.gif, d[vars_[0][0]])


def make_gif(dirv, out, dirb):
    """Walk (8 frames) then fire x3, east and south-east, both units, on clear ground, 3x; game speed = 3 ticks/frame at 30 Hz."""
    frames = []
    a, c = load(dirv, "auth_rifleman"), load(dirv, "dir_conscript")
    plan = [("walk", k) for k in range(a.n("walk"))] * 2 + [("fire", k) for k in (0, 1, 0, 1, 0, 1)]
    for stt, k in plan:
        cv = Image.new("RGBA", (60 * 4, 60), CLEAR)
        for j, (sh, fa) in enumerate(((a, 4), (a, 6), (c, 4), (c, 6))):
            put(cv, sh, stt, fa, min(k, sh.n(stt) - 1), (j * 60 + 30, 52))
        frames.append(zoom(cv, 3).convert("P", palette=Image.ADAPTIVE))
    tpf = a.st["walk"]["ticks_per_frame"]
    ms = int(round(1000 * tpf / 30))
    frames[0].save(out, save_all=True, append_images=frames[1:], duration=[ms] * 16 + [150] * 6, loop=0, disposal=2)
    print("wrote", out)


if __name__ == "__main__":
    main()
