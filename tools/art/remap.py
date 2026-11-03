"""House-colour remap test: replace exactly the magenta ramp pixels with a 3 house colour ramps.
Uses frame 2 (45 deg) of B_pixel_64 and A_render_64. Output art/pilot/remap_test.png (3x nearest zoom)."""
import os
import numpy as np
from PIL import Image
import common as C

HOUSES = {
    "Authority green": C.make_ramp(C.hx("#1E2A1C"), C.hx("#6E7F5E"), C.hx("#C3D4B2")),
    "Directorate rust-red": C.make_ramp(C.hx("#2A0D08"), C.hx("#8B2E1A"), C.hx("#E8957A")),
    "Blue ally": C.make_ramp(C.hx("#0C1B2E"), C.hx("#2E5A8B"), C.hx("#9CC6EE")),
}
FRAME = 2
TOL = 6  # max RGB distance to count as a ramp shade


def remap(img, ramp):
    a = np.asarray(img.convert("RGBA")).copy()
    rgb = a[..., :3].astype(int)
    src = np.array(C.HOUSE)
    d = np.abs(rgb[:, :, None, :] - src[None, None, :, :]).sum(axis=3)  # H,W,8
    idx, dist = d.argmin(axis=2), d.min(axis=2)
    sel = (dist <= TOL) & (a[..., 3] > 0)
    a[sel, :3] = np.array(ramp)[idx[sel]]
    return Image.fromarray(a, "RGBA"), int(sel.sum())


def get_frames():
    out = {}
    for arm, fn in (("A render", "A_render_64.png"), ("B pixel", "B_pixel_64.png")):
        out[arm] = C.split_sheet(Image.open(os.path.join(C.PILOT, fn)).convert("RGBA"), 4, 4)[FRAME]
    return out


def main():
    fr = get_frames()
    cv = fr["A render"].width
    Z = 3
    bg = (74, 66, 58, 255)
    sheet = Image.new("RGBA", (cv * Z * 4, cv * Z * 2), bg)
    for r, (arm, img) in enumerate(fr.items()):
        variants = [("original", img)] + [(n, remap(img, rp)[0]) for n, rp in HOUSES.items()]
        for c, (n, im) in enumerate(variants):
            big = im.resize((cv * Z, cv * Z), Image.NEAREST)
            sheet.alpha_composite(big, (c * cv * Z, r * cv * Z))
    sheet.convert("RGB").save(os.path.join(C.PILOT, "remap_test.png"))
    for arm, img in fr.items():
        print(arm, "remapped pixels:", remap(img, HOUSES["Blue ally"])[1])


if __name__ == "__main__":
    main()
