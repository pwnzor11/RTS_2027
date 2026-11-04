"""Contact sheets for the Directorate pilot assets (M3-ART-PILOT), each next to the matching Authority asset.
  python3 tools/art/dir_contact.py conscript   -> art/pilot/dir_conscript_contact.png
  python3 tools/art/dir_contact.py vent        -> art/pilot/dir_smelter_vent_contact.png
Everything is nearest-neighbour; 1x is true game size."""
import os, sys
import numpy as np
from PIL import Image, ImageDraw
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, "infantry"))
import common as C
import remap as R
import build_infantry as BI
import build_structure as BS

BG, FG, ASH = (24, 24, 30), (225, 225, 230), (74, 66, 58)
N8 = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"]


def zoom(im, z):
    return im.resize((im.width * z, im.height * z), Image.NEAREST) if z > 1 else im


def tile(frames_rows, z, gap=2, bg=ASH):
    """frames_rows: list of rows of RGBA frames -> RGB image."""
    w, h = frames_rows[0][0].size
    cols, rows = max(len(r) for r in frames_rows), len(frames_rows)
    out = Image.new("RGB", (cols * (w * z + gap) - gap, rows * (h * z + gap) - gap), bg)
    for r, row in enumerate(frames_rows):
        for c, f in enumerate(row):
            g = zoom(f, z)
            out.paste(g, (c * (w * z + gap), r * (h * z + gap)), g)
    return out


def hcat(ims, gap=24):
    out = Image.new("RGB", (sum(i.width for i in ims) + gap * (len(ims) - 1), max(i.height for i in ims)), BG)
    x = 0
    for i in ims:
        out.paste(i, (x, 0))
        x += i.width + gap
    return out


def stack(blocks, width=None):
    W = width or max(b[1].width for b in blocks) + 20
    total = 10 + sum(b[1].height + 26 for b in blocks)
    out = Image.new("RGB", (W, total), BG)
    d = ImageDraw.Draw(out)
    y = 10
    for label, im in blocks:
        d.text((10, y), label, fill=FG)
        out.paste(im, (10, y + 16))
        y += im.height + 26
    return out


def inf_frames(sid):
    mod = BI.load_module(sid)
    w, h = mod.CANVAS
    sh = Image.open(os.path.join(C.ROOT, "data", "sprites", "infantry", f"{sid}.png")).convert("RGBA")
    fr = C.split_sheet(sh, BI.COLS, sh.height // h)
    _f, firsts = BI.frame_list(mod)
    S = {name: (nf, n, firsts[name]) for name, nf, n, _t in mod.STATES}

    def get(state, facing, step=0):
        nf, n, first = S[state]
        return fr[first + (facing if nf > 1 else 0) * n + step]
    return mod, get


def conscript():
    mod, D = inf_frames("dir_conscript")
    _m, A = inf_frames("auth_rifleman")
    blocks = []
    row = lambda G, st, k=0: [G(st, f, k) for f in range(8)]
    blocks.append(("IDLE, 8 facings (N NE E SE S SW W NW).  Rows: Directorate conscript / Authority rifleman.  Left 1x (true size), right 2x",
                   hcat([tile([row(D, "idle"), row(A, "idle")], 1), tile([row(D, "idle"), row(A, "idle")], 2)])))
    blocks.append(("FIRE frame 2 (muzzle flash), 8 facings.  1x then 2x.  Rows: conscript / rifleman",
                   hcat([tile([row(D, "fire", 1), row(A, "fire", 1)], 1), tile([row(D, "fire", 1), row(A, "fire", 1)], 2)])))
    wk = lambda G: [[G("walk", f, k) for f in range(8)] for k in range(6)]
    blocks.append(("WALK 6 frames x 8 facings at 1x.  Left Directorate conscript, right Authority rifleman",
                   hcat([tile(wk(D), 1), tile(wk(A), 1)], 40)))
    w2 = lambda G, f: [[G("walk", f, k) for k in range(6)]]
    blocks.append(("WALK at 2x, facings E (top) and S (bottom).  Left conscript, right rifleman",
                   hcat([tile(w2(D, 2) + w2(D, 4), 2), tile(w2(A, 2) + w2(A, 4), 2)], 30)))
    dd = lambda G: [[G("die", 0, k) for k in range(6)]]
    blocks.append(("DIE 6 frames (fall to the east), 1x and 2x.  Conscript then rifleman",
                   hcat([tile(dd(D), 1), tile(dd(D), 2), tile(dd(A), 1), tile(dd(A), 2)], 24)))
    # squad on ash: 5 slots per cell, conscripts on the left cell, riflemen on the right
    cell, SL = 64, [(32, 36), (15, 25), (49, 25), (15, 53), (49, 53)]
    ox, oy = mod.ORIGIN
    face = [0, 2, 4, 6, 3]
    img = Image.new("RGB", (cell * 4, cell), ASH)
    rng = np.random.default_rng(7)
    arr = np.asarray(img).astype(float) + rng.normal(0, 3, (cell, cell * 4, 1))
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    for k, (sx, sy) in enumerate(SL):
        for G, cx in ((D, 0), (A, 2)):
            f = G("idle", face[k])
            img.paste(f, (cx * cell + sx - ox, sy - oy), f)
    blocks.append(("IN CONTEXT 1x and 3x: five conscripts share a cell (left), five riflemen (right)",
                   hcat([img, zoom(img, 3).crop((0, 0, cell * 4 * 3 // 1, cell * 3))], 20)))
    # house remap
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    base = img.convert("RGBA")
    base = Image.new("RGBA", img.size, ASH + (255,))
    for k, (sx, sy) in enumerate(SL):
        f = D("idle", face[k])
        base.alpha_composite(f, (sx - ox, sy - oy))
    base = base.crop((0, 0, cell, cell))
    vs = [("magenta", base)]
    for n, rp in (("amber", C.make_ramp(C.hx("#3A2A06"), C.hx("#E0A020"), C.hx("#FFE9A0"))),
                  ("red", C.make_ramp(C.hx("#2A0D08"), C.hx("#B02A1A"), C.hx("#F0A090"))),
                  ("blue", C.make_ramp(C.hx("#0C1B2E"), C.hx("#2E5A8B"), C.hx("#9CC6EE")))):
        out, _c = R.remap(base, rp)
        a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
        ch = (a0 != a1).any(axis=1)
        assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
        vs.append((n, out))
    blocks.append(("HOUSE REMAP at 3x (exact-match magenta ramp): magenta, amber, red, blue",
                   tile([[v for _n, v in vs]], 3, 6)))
    out = stack(blocks)
    return out, "dir_conscript_contact.png"


def infantry(sid):
    """Generic Directorate infantry sheet (M3-ART-INF): asset rows above the conscript rows, same layout as conscript()."""
    mod, D = inf_frames(sid)
    _m, K = inf_frames("dir_conscript")
    _m2, A = inf_frames("auth_rifleman")
    ox, oy = mod.ORIGIN
    row = lambda G, st, k=0: [G(st, f, k) for f in range(8)]
    both = lambda st, k=0: [row(D, st, k), row(K, st, k)]
    blocks = []
    blocks.append((f"IDLE, 8 facings (N NE E SE S SW W NW).  Rows: {mod.TITLE} / Directorate conscript (scale reference).  Left 1x (true size), right 2x",
                   hcat([tile(both("idle"), 1), tile(both("idle"), 2)])))
    blocks.append(("FIRE frame 1 and 2 at 1x then 2x (frame 2 = muzzle flash / shot), 8 facings.  Rows: asset f1, conscript f1, asset f2, conscript f2",
                   hcat([tile(both("fire", 0) + both("fire", 1), 1), tile(both("fire", 1), 2)])))
    wk = lambda G: [[G("walk", f, k) for f in range(8)] for k in range(6)]
    blocks.append(("WALK 6 frames x 8 facings at 1x.  Left this asset, right Directorate conscript",
                   hcat([tile(wk(D), 1), tile(wk(K), 1)], 40)))
    w2 = lambda G, f: [[G("walk", f, k) for k in range(6)]]
    blocks.append(("WALK at 2x, facings E (top) and S (bottom).  Left this asset, right conscript",
                   hcat([tile(w2(D, 2) + w2(D, 4), 2), tile(w2(K, 2) + w2(K, 4), 2)], 30)))
    dd = lambda G: [[G("die", 0, k) for k in range(6)]]
    blocks.append(("DIE 6 frames (fall to the east), 1x and 2x.  This asset then conscript",
                   hcat([tile(dd(D), 1), tile(dd(D), 2), tile(dd(K), 1), tile(dd(K), 2)], 24)))
    cell, SL = 64, [(32, 36), (15, 25), (49, 25), (15, 53), (49, 53)]
    face = [0, 2, 4, 6, 3]
    img = Image.new("RGB", (cell * 6, cell), ASH)
    rng = np.random.default_rng(7)
    arr = np.asarray(img).astype(float) + rng.normal(0, 3, (cell, cell * 6, 1))
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    for k, (sx, sy) in enumerate(SL):
        for G, cx in ((D, 0), (K, 2), (A, 4)):
            f = G("idle", face[k])
            img.paste(f, (cx * cell + sx - ox, sy - oy), f)
    blocks.append(("IN CONTEXT 1x and 3x: five of this asset (left), five conscripts (middle), five Authority riflemen (right)",
                   hcat([img, zoom(img, 3).crop((0, 0, cell * 6 * 3 // 2, cell * 3))], 20)))
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    base = Image.new("RGBA", (cell, cell), ASH + (255,))
    for k, (sx, sy) in enumerate(SL):
        base.alpha_composite(D("idle", face[k]), (sx - ox, sy - oy))
    vs = [("magenta", base)]
    for n, rp in (("amber", C.make_ramp(C.hx("#3A2A06"), C.hx("#E0A020"), C.hx("#FFE9A0"))),
                  ("red", C.make_ramp(C.hx("#2A0D08"), C.hx("#B02A1A"), C.hx("#F0A090"))),
                  ("blue", C.make_ramp(C.hx("#0C1B2E"), C.hx("#2E5A8B"), C.hx("#9CC6EE")))):
        out, _c = R.remap(base, rp)
        a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(out).reshape(-1, 4)
        ch = (a0 != a1).any(axis=1)
        assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
        vs.append((n, out))
    blocks.append(("HOUSE REMAP at 3x (exact-match magenta ramp): magenta, amber, red, blue",
                   tile([[v for _n, v in vs]], 3, 6)))
    return stack(blocks), f"{sid}_contact.png"


def struct_states(sid):
    mod = BS.load_module(sid)
    sh = Image.open(os.path.join(C.ROOT, "data", "sprites", "buildings", f"{sid}.png")).convert("RGBA")
    fr = C.split_sheet(sh, 4, sh.height // mod.CANVAS[1])
    st, k = {}, 0
    for name, n, _t in mod.STATES:
        st[name] = fr[k:k + n]
        k += n
    return mod, st


def strip(frames, z, gap=4):
    return tile([frames], z, gap)


def vent():
    mod, D = struct_states("dir_smelter_vent")
    amod, A = struct_states("auth_refinery")
    blocks = []
    blocks.append(("IDLE and DAMAGED frame 0 at 2x.  Left to right: Directorate smelter vent (3x2) idle, damaged | Authority refinery (3x3) idle, damaged",
                   hcat([strip([D["idle"][0]], 2), strip([D["damaged"][0]], 2), strip([A["idle"][0]], 2), strip([A["damaged"][0]], 2)], 24)))
    blocks.append(("IDLE loop, 4 frames at 1x (true size): Directorate (left) | Authority (right).  Ash puffs rise, furnace slits flicker",
                   hcat([strip(D["idle"], 1), strip(A["idle"], 1)], 40)))
    blocks.append(("DAMAGED, 4 frames at 1x: Directorate (left) | Authority (right).  Structural damage only; fire / smoke are FX anchors in the manifest",
                   hcat([strip(D["damaged"], 1), strip(A["damaged"], 1)], 40)))
    # in context on ash with infantry for scale
    cell = 64
    img = Image.new("RGB", (10 * cell, 5 * cell), ASH)
    rng = np.random.default_rng(11)
    arr = np.asarray(img).astype(float) + rng.normal(0, 3, (5 * cell, 10 * cell, 1))
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).convert("RGBA")
    top = 100
    img.alpha_composite(D["idle"][0], (16 - mod.ORIGIN[0], top - mod.ORIGIN[1]))
    img.alpha_composite(A["idle"][0], (330 - amod.ORIGIN[0], top - amod.ORIGIN[1]))
    imod, DI = inf_frames("dir_conscript")
    _m, AI = inf_frames("auth_rifleman")
    for k, (G, x, y) in enumerate([(DI, 120, 300), (DI, 152, 292), (DI, 184, 303), (AI, 400, 306), (AI, 432, 297), (AI, 464, 308)]):
        f = G("idle", [4, 0, 2, 4, 0, 6][k])
        img.alpha_composite(f, (x - imod.ORIGIN[0], y - imod.ORIGIN[1]))
    d = ImageDraw.Draw(img)
    for gx in range(0, img.width, cell):
        for gy in range(0, img.height, cell):
            d.point((gx, gy), fill=(100, 94, 88))
    x0, y0 = 16 + mod.DOCK[0] * cell, top + mod.DOCK[1] * cell
    for px, py in ((x0, y0), (x0 + cell - 1, y0), (x0, y0 + cell - 1), (x0 + cell - 1, y0 + cell - 1)):
        d.point((px, py), fill=(230, 200, 60))
    blocks.append(("IN CONTEXT 1x on ash: vent (3x2, dock cell marked with yellow dots) left, Authority refinery (3x3) right; conscripts and riflemen for scale.  Cell ticks every 64 px",
                   img.convert("RGB")))
    out = stack(blocks)

    b2 = []
    b2.append(("BUILD, 8 frames at 1x: Directorate vent (0 plot, 1 foundation, 2-3 steel, 4-5 clad to a rising height, 6 primer + scaffold, 7 finished)",
               strip(D["build"], 1)))
    b2.append(("BUILD, 8 frames at 1x: Authority refinery for comparison", strip(A["build"], 1)))
    b2.append(("BUILD frames 3, 4, 5, 6 at 2x: Directorate vent", strip([D["build"][k] for k in (3, 4, 5, 6)], 2)))
    R.TOL = 0
    ramp_set = {tuple(c) for c in C.HOUSE}
    base = D["idle"][0]
    vs = [base]
    for rp in (C.make_ramp(C.hx("#3A2A06"), C.hx("#E0A020"), C.hx("#FFE9A0")), C.make_ramp(C.hx("#2A0D08"), C.hx("#B02A1A"), C.hx("#F0A090")),
               C.make_ramp(C.hx("#0C1B2E"), C.hx("#2E5A8B"), C.hx("#9CC6EE"))):
        o, _c = R.remap(base, rp)
        a0, a1 = np.asarray(base).reshape(-1, 4), np.asarray(o).reshape(-1, 4)
        ch = (a0 != a1).any(axis=1)
        assert all(tuple(p[:3]) in ramp_set for p in a0[ch]), "remap touched a non-ramp pixel"
        vs.append(o)
    b2.append(("HOUSE REMAP idle frame 0 at 2x (exact-match magenta ramp; asserts only ramp pixels change): magenta, amber, red, blue",
               strip(vs, 2, 8)))
    return out, "dir_smelter_vent_contact.png", stack(b2), "dir_smelter_vent_contact_build.png"


if __name__ == "__main__":
    which = sys.argv[1]
    res = conscript() if which == "conscript" else infantry(which) if which.startswith("dir_") else vent()
    for im, name in zip(res[0::2], res[1::2]):
        p = os.path.join(C.PILOT, name)
        im.save(p, optimize=True)
        print("wrote", p, im.size)
