"""Arm B: pixel-art post-process of the 4x renders.
Box downscale -> hard alpha threshold -> nearest of fixed 32-colour palette (24 faction + 8 exact house
magenta) -> 1-px dark outline on the alpha edge -> checker-dithered hard shadow. No partial alpha."""
import os
import numpy as np
from PIL import Image
import common as C

W = np.array([2.0, 3.0, 1.0])  # rough perceptual weights (R,G,B)


def quantise(rgb, pal, rad=False):
    """Nearest palette index.  The radiation-green entries (C.RAD_SET, D-099) are excluded unless rad=True, and then ONLY they are
    considered, so a non-radiation pixel can never come out green and a radiation pixel can never come out anything else."""
    p = np.array(pal, dtype=float)
    d = (((rgb[:, None, :] - p[None, :, :]) ** 2) * W).sum(axis=2)
    is_rad = np.array([tuple(c) in C.RAD_SET for c in pal])
    d[:, is_rad != rad] = np.inf
    return d.argmin(axis=1)


BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0 - 0.5 + 1 / 32


def family_of(pi):
    """Palette index -> ordered list of palette indices (dark to light) of the same colour family."""
    fams = [range(1, 9), range(9, 14), range(14, 18), range(18, 21)]
    for fam in fams:
        if pi in fam:
            return list(fam)
    return [pi]


def lum(c):
    return 0.3 * c[0] + 0.59 * c[1] + 0.11 * c[2]


def dither_pick(rgb, qi, x, y):
    """2-level ordered dither: blend between the two palette neighbours of the pixel's family by luminance."""
    fam = family_of(qi)
    if len(fam) < 2:
        return qi
    L = lum(rgb)
    ls = [lum(C.PAL[i]) for i in fam]
    j = int(np.searchsorted(ls, L))
    if j <= 0 or j >= len(fam):
        return fam[min(max(j, 0), len(fam) - 1)]
    t = (L - ls[j - 1]) / max(ls[j] - ls[j - 1], 1e-6)
    return fam[j] if t > BAYER4[y % 4, x % 4] + 0.5 else fam[j - 1]


def frame_b(cell, f, dither=False):
    L = C.load_layers(cell, f)
    cv = C.canvas(cell)
    base, A, hfrac, hidx, sh = C.downscale_layers(L, cv)
    # slight contrast lift so the box-averaged render keeps readable ramp steps
    base = np.clip((base - 128) * 1.12 + 128, 0, 255)
    solid = A >= 0.5
    out = np.zeros((cv, cv, 4), dtype=np.uint8)
    pal = np.array(C.PAL, dtype=float)
    ys, xs = np.nonzero(solid)
    q = quantise(base[ys, xs], C.PAL)
    for y, x, qi in zip(ys, xs, q):
        if hfrac[y, x] >= 0.5:
            out[y, x] = (*C.HOUSE[hidx[y, x]], 255)
        else:
            out[y, x] = (*C.PAL[dither_pick(base[y, x], qi, x, y) if dither else qi], 255)
    # 1-px outline outside the silhouette (4-neighbour)
    s = solid
    edge = np.zeros_like(s)
    edge[1:, :] |= s[:-1, :]
    edge[:-1, :] |= s[1:, :]
    edge[:, 1:] |= s[:, :-1]
    edge[:, :-1] |= s[:, 1:]
    edge &= ~s
    out[edge] = (*C.OUTLINE, 255)
    # hard dithered shadow where nothing else is drawn
    yy, xx = np.mgrid[0:cv, 0:cv]
    chk = ((xx + yy) % 2 == 0)
    shadow = (~s) & (~edge) & ((sh > 0.55) | ((sh > 0.25) & chk))
    out[shadow] = (*C.PAL[1], 255)
    return Image.fromarray(out, "RGBA")


def main_v2():
    """v2: 64 px cell only. B_pixel_64_v2.png (plain) and B_dither_64_v2.png (2-level ordered dither)."""
    cv = C.canvas(64)
    for name, dith in (("B_pixel_64_v2.png", False), ("B_dither_64_v2.png", True)):
        sheet = C.pack_sheet([frame_b(64, f, dith) for f in range(16)], 4, cv, cv)
        sheet.save(os.path.join(C.PILOT, name))
        arr = np.asarray(sheet).reshape(-1, 4)
        assert set(np.unique(arr[:, 3])) <= {0, 255}
        print("wrote", name, "colours used:", len({tuple(c[:3]) for c in arr if c[3]}))


def main():
    for cell in C.CELL_SIZES:
        cv = C.canvas(cell)
        sheet = C.pack_sheet([frame_b(cell, f) for f in range(16)], 4, cv, cv)
        sheet.save(os.path.join(C.PILOT, f"B_pixel_{cell}.png"))
        cols = {tuple(c[:3]) for c in np.asarray(sheet).reshape(-1, 4) if c[3]}
        print("wrote B_pixel", cell, "colours used:", len(cols))


if __name__ == "__main__":
    import sys
    main_v2() if "v2" in sys.argv[1:] else main()
