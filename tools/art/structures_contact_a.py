"""Contact sheet for fan-out A structures: python3 tools/art/structures_contact_a.py  ->  art/pilot/structures_a_contact.png
Per building: idle at 1x/2x, damaged 2x, idle animation strip, build strip (1x and 2x), damaged strip; then all four beside the
refinery and a tank on ash at 1x, then the house remap (amber / red / blue, exact-match, asserted to touch only ramp pixels)."""
import os
import numpy as np
from PIL import Image, ImageDraw
import common as C
from contact_sheet import ash_patch, hcat, vcat, BG, FG
import remap as R
import build_structure as BS

IDS = ["auth_conyard", "auth_power", "auth_barracks", "auth_silo"]
GROUND = (74, 66, 58)
HOUSES = {
    "amber #E0A020": C.make_ramp(C.hx("#2A1A04"), C.hx("#E0A020"), C.hx("#FFE9A8")),
    "red #B02A1A": C.make_ramp(C.hx("#2A0D08"), C.hx("#B02A1A"), C.hx("#F0A08A")),
    "blue #2E5A8B": C.make_ramp(C.hx("#0C1B2E"), C.hx("#2E5A8B"), C.hx("#9CC6EE")),
}


def strip(frames, z, gap=4):
    cw, ch = frames[0].width * z, frames[0].height * z
    out = Image.new("RGB", (len(frames) * (cw + gap) - gap, ch), GROUND)
    for i, f in enumerate(frames):
        big = f.resize((cw, ch), Image.NEAREST) if z > 1 else f
        out.paste(big, (i * (cw + gap), 0), big)
    return out


def load(sid):
    mod = BS.load_module(sid)
    sheet = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", f"{sid}.png")).convert("RGBA")
    fw, fh = mod.CANVAS
    fr = C.split_sheet(sheet, sheet.width // fw, sheet.height // fh)
    st, k = {}, 0
    for name, n, _t in mod.STATES:
        st[name] = fr[k:k + n]
        k += n
    return mod, st


def main():
    data = {sid: load(sid) for sid in IDS}
    refm, refst = load("auth_refinery")
    blocks = []
    for sid in IDS:
        mod, st = data[sid]
        blocks.append((f"{sid}: IDLE 1x | IDLE 2x | DAMAGED 2x | idle loop 1x (4 frames) | damaged loop 1x",
                       hcat([strip([st["idle"][0]], 1), strip([st["idle"][0]], 2), strip([st["damaged"][0]], 2), strip(st["idle"], 1), strip(st["damaged"], 1)], 20)))
        blocks.append((f"{sid}: BUILD, 8 stages at 1x (plot, foundation, first steel, full frame, half clad, mostly clad, primer, finished)", strip(st["build"], 1)))
        blocks.append((f"{sid}: BUILD at 2x (stages 0-3 | 4-7 on next row)", strip(st["build"][:4], 2)))
        blocks.append(("", strip(st["build"][4:], 2)))
        blocks.append((f"{sid}: DAMAGED at 2x (4 frames; structural damage only, effects come from FX anchors)", strip(st["damaged"], 2)))
    # in context: 14x8 cells on ash at 1x
    cell = 64
    img = ash_patch(cell, 2027).convert("RGBA").resize((14 * cell, 8 * cell))
    arr = np.asarray(img).copy()
    rng = np.random.default_rng(5)
    img = Image.fromarray(np.clip(arr.astype(int) + rng.normal(0, 3, arr.shape).astype(int), 0, 255).astype(np.uint8), "RGBA")
    place = [("auth_refinery", (0, 1), refm, refst), ("auth_conyard", (4, 1)), ("auth_power", (8, 1)), ("auth_barracks", (11, 1)), ("auth_silo", (8, 4))]
    for p in place:
        if len(p) == 2:
            mod, st = data[p[0]]
        else:
            mod, st = p[2], p[3]
        fx, fy = p[1]
        img.alpha_composite(st["idle"][0], (fx * cell - mod.ORIGIN[0], fy * cell - mod.ORIGIN[1]))
    tank_path = os.path.join(C.ROOT, "data", "sprites", "vehicles", "tank_v2.png")
    tank = C.split_sheet(Image.open(tank_path).convert("RGBA"), 4, 4)
    ax, ay = C.anchor_px(64)
    for (cx, cy), t in (((1, 4), 0), ((5, 4), 3), ((12, 4), 5), ((10, 6), 12), ((3, 6), 9), ((6, 6), 1)):
        img.alpha_composite(tank[t], (int((cx + 0.5) * cell - ax), int((cy + 0.5) * cell - ay)))
    d = ImageDraw.Draw(img)
    for gx in range(0, img.width, cell):
        for gy in range(0, img.height, cell):
            d.point((gx, gy), fill=(100, 94, 88))
    if os.path.isdir(os.path.join(C.ROOT, "scratch")):
        img.convert("RGB").save(os.path.join(C.ROOT, "scratch", "ctx_a.png"))
    blocks.append(("IN CONTEXT 1x on ash (14x8 cells, 64 px): refinery 3x3, conyard 3x3, power 2x2, barracks 2x2, silo 2x2 and the tank", img.convert("RGB")))
    # remap
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    for sid in IDS:
        mod, st = data[sid]
        base = st["idle"][0]
        variants = [base]
        for n, rp in HOUSES.items():
            out, cnt = R.remap(base, rp)
            a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
            ch = (a0 != a1).any(axis=1)
            assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
            assert (a0[:, 3] == a1[:, 3]).all()
            print(sid, n, "pixels changed:", int(ch.sum()))
            variants.append(out)
        blocks.append((f"{sid}: HOUSE REMAP at 2x: magenta original | amber | red | blue", strip(variants, 2, 8)))
    W = max(b[1].width for b in blocks) + 20
    total = 10 + sum(b[1].height + (26 if b[0] else 6) for b in blocks)
    out = Image.new("RGB", (W, total), BG)
    dr = ImageDraw.Draw(out)
    y = 10
    for label, im in blocks:
        if label:
            dr.text((10, y), label, fill=FG)
            y += 16
        out.paste(im, (10, y))
        y += im.height + (10 if label else 6)
    out.save(os.path.join(C.PILOT, "structures_a_contact.png"), optimize=True)
    print("wrote structures_a_contact.png", out.size)


if __name__ == "__main__":
    main()
