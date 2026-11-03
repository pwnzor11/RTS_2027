"""Arm B: pixel-art post-process of the 4x renders.
Box downscale -> hard alpha threshold -> nearest of fixed 32-colour palette (24 faction + 8 exact house
magenta) -> 1-px dark outline on the alpha edge -> checker-dithered hard shadow. No partial alpha."""
import os
import numpy as np
from PIL import Image
import common as C

W = np.array([2.0, 3.0, 1.0])  # rough perceptual weights (R,G,B)


def quantise(rgb, pal):
    p = np.array(pal, dtype=float)
    d = (((rgb[:, None, :] - p[None, :, :]) ** 2) * W).sum(axis=2)
    return d.argmin(axis=1)


def frame_b(cell, f):
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
            out[y, x] = (*C.PAL[qi], 255)
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


def main():
    for cell in C.CELL_SIZES:
        cv = C.canvas(cell)
        sheet = C.pack_sheet([frame_b(cell, f) for f in range(16)], 4, cv, cv)
        sheet.save(os.path.join(C.PILOT, f"B_pixel_{cell}.png"))
        cols = {tuple(c[:3]) for c in np.asarray(sheet).reshape(-1, 4) if c[3]}
        print("wrote B_pixel", cell, "colours used:", len(cols))


if __name__ == "__main__":
    main()
