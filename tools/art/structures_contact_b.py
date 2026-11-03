"""Owner contact sheet for structure fan-out B: python3 tools/art/structures_contact_b.py [--out structures_b_contact.png]
Factory (idle / damaged / active / build strips), wall (idle, damaged, build, a 4-wall line), turret (16 facings, fire,
damaged, build), then an in-context 1x ash patch with the refinery and the tank, and the house remap amber / red / blue
(exact-match ramp replacement, asserted to touch only ramp pixels)."""
import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

import common as C
from contact_sheet import hcat, vcat, BG, FG
import remap as R
import build_structure as BS

BLD = os.path.join(C.ROOT, "data", "sprites", "buildings")
TANK = os.path.join(C.ROOT, "data", "sprites", "vehicles", "tank_v2.png")
GROUND = (74, 66, 58)
HOUSES = {   # D-068 house palette: amber, red, blue (dark, mid, light)
    "amber": C.make_ramp(C.hx("#2E1F04"), C.hx("#C98A1A"), C.hx("#FFE08A")),
    "red": C.make_ramp(C.hx("#2A0D08"), C.hx("#B3261E"), C.hx("#F0958A")),
    "blue": C.make_ramp(C.hx("#0C1B2E"), C.hx("#2E5A8B"), C.hx("#9CC6EE")),
}


class Sheet:
    """A building sheet and its states, read from the module (frame order) and the PNG."""

    def __init__(self, sid):
        self.mod = BS.load_module(sid)
        self.id = sid
        img = Image.open(os.path.join(BLD, f"{sid}.png")).convert("RGBA")
        self.fw, self.fh = self.mod.CANVAS
        self.ox, self.oy = self.mod.ORIGIN
        cols = img.width // self.fw
        rows = img.height // self.fh
        self.frames = C.split_sheet(img, cols, rows)
        self.first = {}
        k = 0
        for name, n, _t in self.mod.STATES:
            self.first[name] = (k, n)
            k += n

    def st(self, name, a=0, b=None):
        f, n = self.first[name]
        return self.frames[f + a: f + (n if b is None else b)]


def strip(frames, z, gap=4):
    cw, ch = frames[0].width * z, frames[0].height * z
    out = Image.new("RGB", (len(frames) * (cw + gap) - gap, ch), GROUND)
    for i, f in enumerate(frames):
        big = f.resize((cw, ch), Image.NEAREST) if z > 1 else f
        out.paste(big, (i * (cw + gap), 0), big)
    return out


def wall_line(w, n, z, state="idle", idx=0):
    f = w.st(state)[idx]
    out = Image.new("RGBA", (64 * n, f.height), GROUND + (255,))
    for k in range(n):
        out.alpha_composite(f, (k * 64, 0))
    out = out.convert("RGB")
    return out.resize((out.width * z, out.height * z), Image.NEAREST) if z > 1 else out


def ash(w, h, seed):
    rng = np.random.default_rng(seed)
    base = np.array([52, 47, 44], dtype=float)
    n = rng.normal(0, 3.0, (h, w, 1))
    blotch = np.asarray(Image.fromarray(rng.integers(0, 255, (h // 8 + 1, w // 8 + 1), dtype=np.uint8)).resize((w, h), Image.BILINEAR), dtype=float)[..., None]
    return Image.fromarray(np.clip(base + n + (blotch - 128) * 0.04, 0, 255).astype(np.uint8), "RGB")


def put(img, spr, cx, cy, ox, oy):
    img.alpha_composite(spr, (cx * 64 - ox, cy * 64 - oy))


def label(txt):
    im = Image.new("RGB", (max(400, 7 * len(txt)), 14), BG)
    ImageDraw.Draw(im).text((0, 1), txt, fill=FG)
    return im


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="structures_b_contact.png")
    a = ap.parse_args()
    F, W, T = Sheet("auth_factory"), Sheet("auth_wall"), Sheet("auth_turret")
    RF = Sheet("auth_refinery")
    blocks = []
    blocks.append(("FACTORY idle frame 0 at 1x and 2x | damaged frame 0 at 2x | active frame 0 at 2x",
                   hcat([strip(F.st("idle", 0, 1), 1), strip(F.st("idle", 0, 1), 2), strip(F.st("damaged", 0, 1), 2), strip(F.st("active", 0, 1), 2)], 16)))
    blocks.append(("FACTORY at 1x, true size: idle 0-3 (door lamps, crane trolley) | damaged 0-3 (structural; fire and smoke come from [[fx]]) | active 0-3 (door open, work lights, beacons)",
                   hcat([strip(F.st("idle"), 1), strip(F.st("damaged"), 1), strip(F.st("active"), 1)], 16)))
    blocks.append(("FACTORY BUILD, 8 stages at 1x: 0 plot, 1 foundation, 2 first steel, 3 frame + door header, 4 sawtooth rafters + panels, 5 cladding + roof decking, 6 primer, 7 finished",
                   strip(F.st("build"), 1)))
    blocks.append(("FACTORY BUILD at 2x (frames 0-3)", strip(F.st("build", 0, 4), 2)))
    blocks.append(("FACTORY BUILD at 2x (frames 4-7)", strip(F.st("build", 4, 8), 2)))
    blocks.append(("FACTORY DAMAGED at 2x (frames 0-3: swaying hook, flickering lamp)", strip(F.st("damaged"), 2)))
    blocks.append(("FACTORY ACTIVE at 2x", strip(F.st("active"), 2)))
    blocks.append(("WALL at 2x: idle | damaged | build 0-3  (single cells)",
                   hcat([strip(W.st("idle"), 2), strip(W.st("damaged"), 2), strip(W.st("build"), 2)], 16)))
    blocks.append(("WALL LINE: 4 idle blocks at 1x and 2x (edges and cap must run on) | 2 idle + damaged + 2 idle at 1x",
                   hcat([wall_line(W, 4, 1), wall_line(W, 4, 2)], 24)))
    mix = Image.new("RGBA", (64 * 5, W.fh), GROUND + (255,))
    for k, (stt) in enumerate(("idle", "idle", "damaged", "idle", "idle")):
        mix.alpha_composite(W.st(stt)[0], (k * 64, 0))
    blocks.append(("WALL mixed line (damaged block third) at 2x", mix.convert("RGB").resize((mix.width * 2, mix.height * 2), Image.NEAREST)))
    idle = T.st("idle")
    blocks.append(("TURRET idle, 16 facings at 1x (0 north, clockwise)", strip(idle, 1, 0)))
    blocks.append(("TURRET idle facings 0-7 at 2x", strip(idle[:8], 2)))
    blocks.append(("TURRET idle facings 8-15 at 2x", strip(idle[8:], 2)))
    fire = T.st("fire")
    sel = [fire[f * 2 + k] for f in (0, 3, 4, 8, 12, 13) for k in (0, 1)]
    blocks.append(("TURRET fire (flash, dying flash) for facings 0, 3, 4, 8, 12, 13 at 2x", strip(sel, 2)))
    blocks.append(("TURRET damaged, 16 facings at 1x", strip(T.st("damaged"), 1, 0)))
    blocks.append(("TURRET damaged facings 0, 2, 4, 8, 12 at 2x | build stages 0-5 at 2x",
                   hcat([strip([T.st("damaged")[k] for k in (0, 2, 4, 8, 12)], 2), strip(T.st("build"), 2)], 16)))
    # in context 1x: 14 x 7 ash patch
    cw, ch = 14, 7
    img = ash(cw * 64, ch * 64, 2027).convert("RGBA")
    put(img, F.st("idle")[0], 5, 2, F.ox, F.oy)
    put(img, RF.st("idle")[0], 1, 1, RF.ox, RF.oy)
    for k in range(4):
        put(img, W.st("idle")[0], 10 + k, 1, W.ox, W.oy)
    for k in range(3):
        put(img, W.st("idle")[0], 10, 2 + k, W.ox, W.oy)
    put(img, W.st("damaged")[0], 13, 1, W.ox, W.oy)
    put(img, T.st("idle")[3], 9, 1, T.ox, T.oy)
    put(img, T.st("fire")[2 * 6], 11, 3, T.ox, T.oy)
    put(img, T.st("damaged")[10], 12, 5, T.ox, T.oy)
    tank = C.split_sheet(Image.open(TANK).convert("RGBA"), 4, 4)
    ax, ay = C.anchor_px(64)
    for (cx, cy), t in (((6, 5), 0), ((8, 4), 3), ((3, 5), 5), ((11, 5), 12)):
        img.alpha_composite(tank[t], (int((cx + 0.5) * 64 - ax), int((cy + 0.5) * 64 - ay)))
    blocks.append(("IN CONTEXT 1x on ash (14x7 cells): refinery (left), factory (middle, exit cell below its middle), tanks, an L of walls with the turrets (idle facing 3, fire, damaged) beside them",
                   img.convert("RGB")))
    # house remap
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    rows = []
    for nm, sheet_, spr in (("factory", F, F.st("idle")[0]), ("wall", W, W.st("idle")[0]), ("turret", T, T.st("idle")[4]), ("turret fire", T, T.st("fire")[8])):
        vs = [spr]
        for n, rp in HOUSES.items():
            out, cnt = R.remap(spr, rp)
            a0, a1 = np.asarray(spr).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
            ch_ = (a0 != a1).any(axis=1)
            assert all(tuple(p[:3]) in ramp_set for p in a0[ch_]), "remap touched a non-ramp pixel"
            assert (a0[:, 3] == a1[:, 3]).all()
            print(nm, n, "pixels changed:", int(ch_.sum()))
            vs.append(out)
        rows.append(strip(vs, 2 if nm != "factory" else 1, 8))
    blocks.append(("HOUSE REMAP (magenta original, amber, red, blue; exact-match ramp replacement): factory at 1x, wall, turret, turret firing at 2x", vcat(rows, 8)))
    W_ = max(b[1].width for b in blocks) + 20
    ims = []
    for t, im in blocks:
        ims += [label(t), im]
    sheet = vcat(ims, 6)
    canvas = Image.new("RGB", (max(W_, sheet.width + 20), sheet.height + 20), BG)
    canvas.paste(sheet, (10, 10))
    outp = os.path.join(C.PILOT, a.out)
    canvas.save(outp)
    print("wrote", outp, canvas.size)


if __name__ == "__main__":
    main()
