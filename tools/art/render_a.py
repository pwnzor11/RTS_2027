"""Arm A: smooth downscale of the 4x renders (area filter) to 48 and 64 px cells.
Soft shadow kept as partial alpha; house pixels snapped to the exact magenta ramp."""
import os
import numpy as np
from PIL import Image
import common as C


def frame_a(cell, f):
    L = C.load_layers(cell, f)
    base, A, hfrac, hidx, sh = C.downscale_layers(L, C.canvas(cell))
    rgb = base.copy()
    ramp = np.array(C.HOUSE, dtype=float)
    isH = hfrac >= 0.5
    rgb[isH] = ramp[hidx[isH]]
    rgb[A < 0.01] = 0
    shadow_a = sh * 0.5
    out_a = A + shadow_a * (1 - A)
    out = np.dstack([np.clip(rgb, 0, 255), np.clip(out_a, 0, 1) * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def main():
    for cell in C.CELL_SIZES:
        cv = C.canvas(cell)
        sheet = C.pack_sheet([frame_a(cell, f) for f in range(16)], 4, cv, cv)
        sheet.save(os.path.join(C.PILOT, f"A_render_{cell}.png"))
        print("wrote A_render", cell, sheet.size)


if __name__ == "__main__":
    main()
