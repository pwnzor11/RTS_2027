"""Re-grade Warzone 2100 texture pages toward our palette.  Pure numpy/PIL (no Blender).

The Warzone pages are mostly neutral grey (the faction colour is added at run time by the tcmask), with a few teal, tan and red patches.
We keep the page's luminance structure (panel lines, rivets, grilles) and map it through a three-stop ramp, so every patch becomes the
same olive family as the Authority hull.  The tcmask is NOT touched here: the pipeline turns masked texels into the magenta house ramp.

Output: art/pilot/work/wz/<kind>_<page>.png (git-ignored work dir), regenerated on demand by graded().
"""
import os, sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, ".."))
import common as C

TEXPAGES = os.environ.get("WZ_TEXPAGES", "/home/claude/ref/wz2100-texpages")
OUT = os.path.join(C.WORK, "wz")

# kind -> (dark, mid, light) ramp stops and a contrast gain applied around the page's own median
RAMPS = {
    "hull": ((0x16, 0x1B, 0x12), (0x46, 0x53, 0x3A), (0x86, 0x95, 0x76), 1.3),     # Authority olive (hull #566448)
    "track": ((0x16, 0x17, 0x0F), (0x35, 0x38, 0x33), (0x80, 0x85, 0x8A), 1.1),      # rubber/steel, like the tank's track material
    "concrete": ((0x2C, 0x30, 0x2A), (0x7A, 0x80, 0x74), (0xC9, 0xCF, 0xC0), 1.15),  # for buildings: grey-green concrete
}


def graded(page, kind="hull"):
    """Path of the re-graded copy of texture page `page` (file name as in the .pie TEXTURE line)."""
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, f"{kind}_{page}")
    src = os.path.join(TEXPAGES, page)
    if os.path.exists(dst) and os.path.getmtime(dst) >= os.path.getmtime(__file__):
        return dst
    im = Image.open(src).convert("RGBA")
    a = np.asarray(im, dtype=float) / 255.0
    lum = a[..., 0] * 0.3 + a[..., 1] * 0.55 + a[..., 2] * 0.15      # green-heavy so teal/tan patches keep some structure
    opaque = a[..., 3] > 0.5
    used = opaque & (lum > 0.15)                    # the pages are padded with near-black: do not let it pull the median down
    med = np.median(lum[used]) if used.any() else 0.5
    d, m, l, gain = RAMPS[kind]
    t = np.clip((lum - med) * gain + 0.5, 0, 1)
    d, m, l = (np.array(x, dtype=float) for x in (d, m, l))
    lo = t < 0.5
    out = np.where(lo[..., None], d + (m - d) * (t * 2)[..., None], m + (l - m) * ((t - 0.5) * 2)[..., None])
    rgba = np.dstack([out, np.where(opaque, 255, 0)]).clip(0, 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(dst)
    return dst


def mask_path(page):
    """Path of the tcmask belonging to a texture page (page-14-droid-hubs.png -> page-14_tcmask.png), or None."""
    num = page.split("-")[1]
    p = os.path.join(TEXPAGES, f"page-{num}_tcmask.png")
    return p if os.path.exists(p) else None
