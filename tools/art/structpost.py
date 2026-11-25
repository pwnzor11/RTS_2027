"""Arm B post-process for structures: same recipe as pixelize.frame_b, any canvas size, sheet + .sprite.toml writer.
4x layers -> box downscale -> alpha>=50% -> nearest of the fixed 32-colour palette (house pixels = exact magenta ramp)
-> 1 px outline -> checker-dithered 1-bit shadow.  No partial alpha anywhere."""
import os
import numpy as np
from PIL import Image, ImageFilter
import common as C
import pixelize as P

SS = 4


def is_h2(m):
    """house2 mask pixels are pure red (G much lower than R); house mask pixels are white."""
    return (m[..., 0] > 127) & (m[..., 1] < 0.5 * m[..., 0])


def calibrate(wd, nframes, h2_fixed=False, frames=None):
    """(lo, hi) luminance range of the house paint. With h2_fixed (module sets HOUSE2_MID = True) the range comes from
    plain `house` pixels only and a third value, the median luminance of the `house2` pixels, is returned too."""
    p = os.path.join(wd, "house_cal.txt")
    vals, v2 = [], []
    for f in (frames if frames is not None else range(nframes)):
        c = np.asarray(Image.open(os.path.join(wd, f"col_{f:02d}.png")).convert("RGBA"), dtype=float)
        m = np.asarray(Image.open(os.path.join(wd, f"mask_{f:02d}.png")).convert("RGBA"), dtype=float)
        sel = (m[..., 0] > 127) & (c[..., 3] > 250)
        if h2_fixed:
            h2 = sel & is_h2(m)
            sel = sel & ~h2
            v2.append((c[..., 0][h2] + c[..., 2][h2]) / 2)
        vals.append((c[..., 0][sel] + c[..., 2][sel]) / 2)
    v = np.concatenate(vals)
    lo, hi = np.percentile(v, 2), np.percentile(v, 98)
    if h2_fixed:
        v2 = np.concatenate(v2)
        med = float(np.median(v2)) if len(v2) else (lo + hi) / 2
        open(p, "w").write(f"{lo} {hi} {med}")
        return lo, hi, med
    open(p, "w").write(f"{lo} {hi}")
    return lo, hi


def load_layers(wd, f, cal):
    c = np.asarray(Image.open(os.path.join(wd, f"col_{f:02d}.png")).convert("RGBA"), dtype=float)
    m = np.asarray(Image.open(os.path.join(wd, f"mask_{f:02d}.png")).convert("RGBA"), dtype=float)
    s = np.asarray(Image.open(os.path.join(wd, f"shadow_{f:02d}.png")).convert("RGBA"), dtype=float)
    a = c[..., 3] / 255
    house = (m[..., 0] > 127) & (a > 0.5)
    v = (c[..., 0] + c[..., 2]) / 2
    idx = C.house_index(v, cal[0], cal[1])
    if len(cal) > 2:      # house2 -> pinned to the mid ramp shades (3 or 2..4 with the surface's own shading)
        h2 = house & is_h2(m)
        step = (cal[1] - cal[0]) / 7
        idx = np.where(h2, np.clip(np.rint(3.4 + (v - cal[2]) / (2 * step)), 2, 4).astype(int), idx)
    lum = s[..., :3].mean(axis=2) / 255
    lit = np.percentile(lum, 99.5)
    sh = np.clip(1 - lum / max(lit, 1e-6), 0, 1)
    sh = np.asarray(Image.fromarray((sh * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(SS * 0.8)), dtype=float) / 255
    sh = np.clip((sh - 0.05) / 0.8, 0, 1)
    rad = (m[..., 2] > 127) & (m[..., 0] < 127) & (a > 0.5)     # radiation material (blue-only mask, D-099)
    return dict(rgb=c[..., :3], a=a, house=house, idx=idx, shadow=sh, rad=rad & ~house)


def frame_b(wd, f, cal, w, h, pal=None, shadow=True, depth=None, style=None):
    pal = C.PAL if pal is None else pal
    L = load_layers(wd, f, cal)
    base, A, hfrac, hidx, sh = C.downscale_layers(L, h)   # k = (h*SS)//h = SS
    assert A.shape == (h, w)
    k = L["a"].shape[0] // h
    wr = L["a"] * L["rad"]
    Wr = C.block_mean(wr, k)
    radrgb = C.block_mean(L["rgb"] * wr[..., None], k) / np.maximum(Wr[..., None], 1e-6)
    radf = Wr / np.maximum(A, 1e-6)       # fraction of the pixel's coverage that is radiation material
    radrgb = np.clip((radrgb - 128) * 1.12 + 128, 0, 255)
    base = np.clip((base - 128) * 1.12 + 128, 0, 255)
    solid = A >= 0.5
    if depth:
        base, hidx, sh = depth_pass(base, hidx, sh, solid, depth)
    if style:        # S43 infantry readability pass (see infantry_style); default None leaves every other caller untouched
        base, hidx = infantry_style(base, hidx, hfrac, solid, style)
    out = np.zeros((h, w, 4), dtype=np.uint8)
    ys, xs = np.nonzero(solid)
    q = P.quantise(base[ys, xs], pal)
    qr = P.quantise(radrgb[ys, xs], pal, rad=True)   # only evaluated for radiation pixels; the green ramp is reachable ONLY here
    for y, x, qi, qri in zip(ys, xs, q, qr):
        if hfrac[y, x] >= 0.5:
            out[y, x] = (*C.HOUSE[hidx[y, x]], 255)
        elif radf[y, x] >= 0.5 and any(tuple(c) in C.RAD_SET for c in pal):
            out[y, x] = (*pal[qri], 255)
        else:
            out[y, x] = (*pal[qi], 255)
    edge = np.zeros_like(solid)
    edge[1:, :] |= solid[:-1, :]
    edge[:-1, :] |= solid[1:, :]
    edge[:, 1:] |= solid[:, :-1]
    edge[:, :-1] |= solid[:, 1:]
    edge &= ~solid
    out[edge] = (*C.OUTLINE, 255)
    yy, xx = np.mgrid[0:h, 0:w]
    chk = ((xx + yy) % 2 == 0)
    if shadow:
        shadowm = (~solid) & (~edge) & ((sh > 0.55) | ((sh > 0.25) & chk))
        out[shadowm] = (*pal[1], 255)
    if style and style.get("flash") is not None:
        stamp_flash(out, L, h, w, k, pal, style["flash"])
    return Image.fromarray(out, "RGBA")


def _nb(a, dy, dx, fill):
    """out[y, x] = a[y + dy, x + dx], outside = fill."""
    h, w = a.shape[:2]
    o = np.full_like(a, fill)
    ys0, ys1 = max(0, -dy), min(h, h - dy)
    xs0, xs1 = max(0, -dx), min(w, w - dx)
    o[ys0:ys1, xs0:xs1] = a[ys0 + dy:ys1 + dy, xs0 + dx:xs1 + dx]
    return o


def infantry_style(base, hidx, hfrac, solid, st):
    """S43 infantry options (all inside design 18 sec. 7: the sun stays upper-left, alpha stays 0/255).
    st["contrast"]  stretch of the body colours around mid grey (default 1.12 is already in frame_b; this is applied on top)
    st["keyline"]   dark 1 px line on the non-house pixels that touch a house pixel (the panels stop melting into the coat)
    st["rim"]       1 px lit rim on the upper-left edge of the silhouette (house pixels step up one ramp shade)"""
    base = base.copy()
    hidx = hidx.copy()
    hp = solid & (hfrac >= 0.5)
    if st.get("contrast"):
        base = np.clip((base - 128) * st["contrast"] + 128, 0, 255)
    if st.get("rim"):
        lit = solid & (~_nb(solid, -1, 0, False) | ~_nb(solid, 0, -1, False))
        base[lit & ~hp] = np.clip(base[lit & ~hp] * 1.35 + 26, 0, 255)
        hidx = np.where(lit & hp, np.minimum(hidx + 1, 7), hidx)
    if st.get("keyline"):
        near = np.zeros_like(solid)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            near |= _nb(hp, dy, dx, False)
        kl = near & solid & ~hp
        base[kl] = base[kl] * 0.42
    return base, hidx


def stamp_flash(out, L, h, w, k, pal, mode):
    """Muzzle flash pixel cluster: finds the rendered flash (bright emissive yellow) in the 4x colour layer, then draws a hard-edged
    star (white core, pale-yellow body, gold tips) around its centre.  The colours are the nearest palette entries."""
    rgb, a = L["rgb"], L["a"]
    fm = (rgb[..., 0] > 240) & (rgb[..., 1] > 225) & (a > 0.8)
    from scipy import ndimage
    lab, nl = ndimage.label(fm)
    if nl == 0:
        return
    big = 1 + int(np.argmax(ndimage.sum(fm, lab, range(1, nl + 1))))       # the flash is the biggest bright blob (goggle lenses are small)
    ys, xs = np.nonzero(lab == big)
    if len(ys) < 6:
        return
    cy, cx = int(round(ys.mean() / k)), int(round(xs.mean() / k))
    want = [(255, 255, 240), (255, 214, 90), (214, 120, 36)]
    ok = [c for c in pal if tuple(c) not in C.RAD_SET]          # the radiation greens must never show up in a muzzle flash
    pa = np.array(ok, dtype=float)
    col = [tuple(ok[int(((pa - np.array(c)) ** 2).sum(axis=1).argmin())]) for c in want]
    core, body, tip = col
    rays = [(1, 0, 5), (-1, 0, 5), (0, 1, 4), (0, -1, 4), (1, 1, 3), (-1, -1, 3), (1, -1, 3), (-1, 1, 3)]
    def put(y, x, c):
        if 0 <= y < h and 0 <= x < w:
            out[y, x] = (*c, 255)
    for y, x in zip(ys // k, xs // k):
        put(int(y), int(x), body)
    for dx, dy, ln in rays:
        ln += 2 if dx * dy == 0 else 1          # reach out past the rendered blob
        for i in range(1, ln + 1):
            put(cy + dy * i, cx + dx * i, body if i < ln else tip)
            if i <= 2 and dx * dy == 0:                 # thicken the four main rays near the core
                put(cy + dy * i + dx, cx + dx * i + dy, body)
                put(cy + dy * i - dx, cx + dx * i - dy, body)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            put(cy + dy, cx + dx, core if abs(dx) + abs(dy) < 2 else body)


def depth_pass(base, hidx, sh, solid, ground_y):
    """D-243 depth pass (only with --depth), second pilot.  All inside design 18 sec. 7 (sun unchanged, hard alpha, 1 px outline, dithered shadow):
    lit 1 px rim on the upper-left edge of every face, dark 1 px rim on the lower-right edge (silhouette or a step down to a darker face),
    a darker band where the body meets the ground, a cast shadow up to 3 px longer toward the lower right, and a solid (undithered)
    contact shadow row just under the south edge of the footprint."""
    h, w = solid.shape
    lum = base.mean(axis=2)


    def nb(a, dy, dx, fill):
        """out[y, x] = a[y + dy, x + dx] (neighbour down-right for dy=dx=1), outside = fill."""
        o = np.full_like(a, fill)
        ys0, ys1 = max(0, -dy), min(h, h - dy)
        xs0, xs1 = max(0, -dx), min(w, w - dx)
        o[ys0:ys1, xs0:xs1] = a[ys0 + dy:ys1 + dy, xs0 + dx:xs1 + dx]
        return o

    T = 22
    up_s, lf_s = nb(solid, -1, 0, False), nb(solid, 0, -1, False)
    dn_s, rt_s = nb(solid, 1, 0, False), nb(solid, 0, 1, False)
    up_l, lf_l = nb(lum, -1, 0, 0.0), nb(lum, 0, -1, 0.0)
    dn_l, rt_l = nb(lum, 1, 0, 255.0), nb(lum, 0, 1, 255.0)
    lit = solid & (~up_s | ~lf_s | ((lum - up_l) > T) | ((lum - lf_l) > T))
    dark = solid & (~dn_s | ~rt_s | ((lum - dn_l) > T) | ((lum - rt_l) > T)) & ~lit
    base = base.copy()
    base[lit] = np.clip(base[lit] * 1.3 + 12, 0, 255)
    base[dark] = base[dark] * 0.7
    hidx = np.where(lit, np.minimum(hidx + 1, 7), hidx)
    hidx = np.where(dark, np.maximum(hidx - 1, 0), hidx)
    rows = np.arange(h)[:, None] * np.ones((1, w))
    band = solid & (rows >= ground_y - 6) & (rows < ground_y + 3) & ~lit
    base[band] = base[band] * 0.7
    hidx = np.where(band, np.maximum(hidx - 1, 0), hidx)
    sh2 = sh.copy()
    for k in (1, 2, 3):          # longer cast shadow: same direction as the sun (right, slightly down), no new light
        d, e = (k + 1) // 2, k
        sh2[d:, e:] = np.maximum(sh2[d:, e:], sh[:h - d, :w - e])
    contact = (~solid) & nb(solid, -2, 0, False) & (rows >= ground_y - 3) & (rows < ground_y + 6)
    sh2[contact] = 1.0
    return base, hidx, sh2


def write_toml(path, mod, sheet_name):
    fw, fh = mod.CANVAS
    ox, oy = mod.ORIGIN
    lines = [
        f"# Sprite manifest for {mod.ID} ({mod.TITLE}). Generated by tools/art/build_structure.py -- do not hand-edit.",
        "# Frames are packed row-major, 4 per row. origin_x/origin_y = pixel of the footprint's top-left cell corner",
        f"# inside a frame; the footprint is {mod.FOOTPRINT[0]}x{mod.FOOTPRINT[1]} cells at 64 px/cell = {mod.FOOTPRINT[0] * 64}x{mod.FOOTPRINT[1] * 64} px.",
        f"# Dock cell (where the harvester parks) is at footprint-relative cell [{mod.DOCK[0]}, {mod.DOCK[1]}], just below the south edge.",
        "",
        f'file = "{sheet_name}"',
        f"frame_w = {fw}",
        f"frame_h = {fh}",
        f"origin_x = {ox}",
        f"origin_y = {oy}",
        "",
        "# House paint is the 8-shade magenta ramp, darkest first (HouseRamp.Standard). Exact RGB matches are house pixels.",
        "[house]",
        'ramp = ["#400040", "#5B125B", "#772577", "#923792", "#AD49AD", "#C85BC8", "#E46EE4", "#FF80FF"]',
        "",
    ]
    first = 0
    for name, n, tpf in mod.STATES:
        lines += ["[[state]]", f'name = "{name}"', "facings = 1", f"frames = {n}", f"first = {first}", f"ticks_per_frame = {tpf}", ""]
        first += n
    for fx in getattr(mod, "FX", []):   # optional effect anchors (D-069): drawn by the game, not baked; kept inside the frame (the game rejects others)
        lines += ["[[fx]]", f'state = "{fx["state"]}"', f'kind = "{fx["kind"]}"', f'x = {min(max(fx["x"], 0), fw)}', f'y = {min(max(fx["y"], 0), fh)}',
                  f'phase = {fx.get("phase", 0)}', ""]
    open(path, "w").write("\n".join(lines))


def run(sid, mod, wd, depth=False, outdir=None, only=None):
    fw, fh = mod.CANVAS
    n = sum(s[1] for s in mod.STATES)
    todo = sorted(only) if (depth and only) else list(range(n))     # a partial depth (smoke) run packs only the frames that were rendered
    cal = calibrate(wd, n, getattr(mod, "HOUSE2_MID", False), frames=todo)
    gy = (mod.ORIGIN[1] + mod.FOOTPRINT[1] * 64) if depth else None
    frames = [frame_b(wd, f, cal, fw, fh, getattr(mod, "PALETTE", None), depth=gy) for f in todo]
    n = len(todo)
    for f, im in enumerate(frames):
        a = np.asarray(im)
        if a[0, :, 3].any():
            print(f"WARNING frame {f}: art touches the top edge of the canvas (cropped?)")
        if a[-1, :, 3].any() or a[:, 0, 3].any() or a[:, -1, 3].any():
            print(f"WARNING frame {f}: art touches a side/bottom edge of the canvas")
    cols = 4
    sheet = C.pack_sheet(frames, cols, fw, fh)
    outdir = outdir or os.path.join(C.ROOT, "data", "sprites", "buildings")
    os.makedirs(outdir, exist_ok=True)
    png = os.path.join(outdir, f"{sid}.png")
    sheet.save(png)
    write_toml(os.path.join(outdir, f"{sid}.sprite.toml"), mod, f"{sid}.png")
    arr = np.asarray(sheet).reshape(-1, 4)
    assert set(np.unique(arr[:, 3])) <= {0, 255}
    print("wrote", png, "colours used:", len({tuple(c[:3]) for c in arr if c[3]}))
