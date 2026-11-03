"""Arm C: hand-placed pixel art, 48 px cell (72 px canvas), four facings only: N, E, S, W.
Every part is an explicit list of rects / rows on the shared 32-colour palette. Diagonal facings
would be drawn by hand later (this pilot only shows that the approach reads well at cell size).
W is E drawn with mirrored rect positions but re-shaded for the upper-left sun (not a pixel flip),
and its stencil number is placed unmirrored so it still reads."""
import os
import numpy as np
from PIL import Image
import common as C

CV = 72
GREEN, STEEL, RUST = C.PAL[1:9], C.PAL[9:14], C.PAL[14:18]
HIL, LENS = C.PAL[18:21], C.PAL[21:23]
HOUSE = C.HOUSE
DIGITS = {  # 3x5 stencil digits
    "0": ["111", "101", "101", "101", "111"],
    "7": ["111", "001", "010", "010", "010"],
}


class Cv:
    def __init__(self, flip=False):
        self.a = np.zeros((CV, CV, 4), dtype=np.uint8)
        self.flip = flip

    def _x(self, x, w):
        return CV - x - w if self.flip else x

    def px(self, x, y, w, h, col):
        x = self._x(x, w)
        self.a[y:y + h, x:x + w] = (*col, 255)

    def box(self, x, y, w, h, ramp, lv):
        """Filled rect, lit top/left edge, shaded bottom/right edge (screen-space light: upper-left)."""
        x = self._x(x, w)
        hi, lo = ramp[min(lv + 1, len(ramp) - 1)], ramp[max(lv - 1, 0)]
        self.a[y:y + h, x:x + w] = (*ramp[lv], 255)
        if w >= 3 and h >= 3:
            self.a[y, x:x + w] = (*hi, 255)
            self.a[y:y + h, x] = (*hi, 255)
            self.a[y + h - 1, x:x + w] = (*lo, 255)
            self.a[y:y + h, x + w - 1] = (*lo, 255)

    def rows(self, y0, spans, col):
        for i, (x0, x1) in enumerate(spans):
            self.px(x0, y0 + i, x1 - x0 + 1, 1, col if not callable(col) else col(i))

    def text(self, x, y, s, col):  # always screen-space, never mirrored
        for k, ch in enumerate(s):
            for r, row in enumerate(DIGITS[ch]):
                for c, v in enumerate(row):
                    if v == "1":
                        self.a[y + r, x + k * 4 + c] = (*col, 255)

    def finish(self):
        a = self.a
        solid = a[..., 3] > 0
        edge = np.zeros_like(solid)
        edge[1:, :] |= solid[:-1, :]; edge[:-1, :] |= solid[1:, :]
        edge[:, 1:] |= solid[:, :-1]; edge[:, :-1] |= solid[:, 1:]
        edge &= ~solid
        a[edge] = (*C.OUTLINE, 255)
        # dithered hard shadow, offset down-right from the silhouette
        sh = np.zeros_like(solid)
        for dx, dy in ((2, 2), (3, 3), (4, 3), (5, 3)):
            sh[dy:, dx:] |= solid[:CV - dy, :CV - dx]
        yy, xx = np.mgrid[0:CV, 0:CV]
        sh &= ~solid & ~edge & ((xx + yy) % 2 == 0)
        a[sh] = (*C.PAL[1], 255)
        return Image.fromarray(a, "RGBA")


def reactor_ns(c, cy):
    """Cylinder seen from above: top ellipse then ribbed collar. cy = top row."""
    c.rows(cy, [(32, 39), (30, 41), (29, 42), (29, 42), (30, 41), (32, 39)], STEEL[3])
    c.px(33, cy + 2, 5, 2, STEEL[0])           # dark cap hatch
    c.px(31, cy + 1, 2, 1, STEEL[4])           # highlight
    for i, col in enumerate((STEEL[3], STEEL[0], STEEL[3], STEEL[0], STEEL[2])):
        c.px(28, cy + 6 + i, 16, 1, col)
    c.px(29, cy + 11, 14, 1, GREEN[6])
    c.px(29, cy + 12, 14, 1, RUST[1])


def tank_n(c):
    for x in (17, 45):  # tracks with tread ticks
        c.box(x, 24, 9, 37, STEEL, 0)
        for y in range(26, 59, 3):
            c.px(x + 1, y, 7, 1, STEEL[1])
    c.box(26, 54, 19, 7, GREEN, 2)             # rear plate
    c.box(26, 26, 19, 29, GREEN, 4)            # deck
    c.box(26, 22, 19, 5, GREEN, 5)             # glacis
    for x in (17, 45):                         # skirts + house stripe
        c.box(x, 24, 9, 33, GREEN, 3)
    c.px(20, 27, 1, 27, HOUSE[5]); c.px(21, 27, 1, 27, HOUSE[3])
    c.px(49, 27, 1, 27, HOUSE[5]); c.px(50, 27, 1, 27, HOUSE[3])
    for y in (51, 53, 55):
        c.px(28, y, 15, 1, STEEL[0])           # vents
    c.box(44, 43, 2, 9, STEEL, 2)              # shovel
    c.px(46, 27, 3, 3, STEEL[3]); c.px(47, 27, 1, 1, LENS[1])   # searchlight
    c.box(27, 32, 18, 15, GREEN, 4)            # turret
    c.box(30, 34, 12, 9, GREEN, 5)
    c.px(28, 36, 1, 7, HOUSE[5]); c.px(44, 36, 1, 7, HOUSE[3])
    c.box(30, 36, 4, 4, GREEN, 2); c.px(31, 37, 2, 2, STEEL[0])  # cupola
    c.box(38, 36, 3, 3, STEEL, 2)              # hatch
    c.box(31, 29, 8, 5, STEEL, 2)              # mantlet
    c.box(34, 8, 3, 22, STEEL, 0); c.px(34, 8, 1, 22, STEEL[1])  # gun
    c.box(33, 5, 5, 5, STEEL, 3); c.px(34, 6, 3, 1, STEEL[0]); c.px(34, 8, 3, 1, STEEL[0])  # brake
    reactor_ns(c, 44)


def tank_s(c):
    reactor_ns(c, 12)
    for x in (17, 45):
        c.box(x, 18, 9, 38, STEEL, 0)
        for y in range(20, 54, 3):
            c.px(x + 1, y, 7, 1, STEEL[1])
    c.box(26, 18, 19, 28, GREEN, 4)
    c.box(26, 44, 19, 8, GREEN, 5)             # glacis
    c.box(26, 52, 19, 5, GREEN, 2)             # front face
    for x in (17, 45):
        c.box(x, 18, 9, 36, GREEN, 3)
    c.px(20, 21, 1, 30, HOUSE[5]); c.px(21, 21, 1, 30, HOUSE[3])
    c.px(49, 21, 1, 30, HOUSE[5]); c.px(50, 21, 1, 30, HOUSE[3])
    reactor_ns(c, 12)
    c.px(42, 45, 3, 3, STEEL[3]); c.px(43, 47, 1, 1, LENS[1])  # searchlight
    c.box(27, 29, 18, 15, GREEN, 4)            # turret
    c.box(30, 31, 12, 8, GREEN, 5)
    c.box(27, 40, 18, 5, GREEN, 2)             # turret front face
    c.px(28, 41, 2, 3, HOUSE[4]); c.px(42, 41, 2, 3, HOUSE[2])
    c.box(30, 32, 4, 4, GREEN, 2); c.px(31, 33, 2, 2, STEEL[0])
    c.box(38, 32, 3, 3, STEEL, 2)
    c.box(31, 40, 8, 5, STEEL, 2)              # mantlet
    c.box(33, 44, 4, 14, STEEL, 1)             # barrel coming at the viewer
    c.box(32, 56, 6, 7, STEEL, 3); c.px(34, 59, 2, 2, STEEL[0])


def tank_e(c, text_x):
    c.box(13, 29, 42, 6, STEEL, 0)             # far track
    c.box(12, 33, 44, 17, GREEN, 4)            # deck
    c.px(52, 33, 4, 17, GREEN[5])
    c.box(12, 50, 44, 6, GREEN, 3)             # near skirt
    c.px(16, 52, 36, 1, HOUSE[5]); c.px(16, 53, 36, 1, HOUSE[3])
    c.box(11, 56, 46, 8, STEEL, 0)             # near track
    c.px(12, 56, 44, 1, STEEL[1])
    for i in range(6):
        c.box(14 + 7 * i, 57, 5, 5, STEEL, 2)
        c.px(16 + 7 * i, 59, 1, 1, STEEL[0])
    c.box(50, 57, 5, 5, RUST, 2)
    # reactor (rear = left)
    c.rows(21, [(15, 22), (14, 23), (13, 24), (13, 24), (14, 23), (15, 22)], STEEL[3])
    c.px(16, 23, 6, 2, STEEL[0]); c.px(15, 22, 2, 1, STEEL[4])
    for i, col in enumerate((STEEL[3], STEEL[0], STEEL[3], STEEL[0], STEEL[2])):
        c.px(12, 27 + i, 14, 1, col)
    c.px(13, 32, 12, 1, GREEN[6]); c.px(12, 33, 14, 1, RUST[1])
    c.box(47, 41, 5, 3, RUST, 2)               # toolbox
    c.px(48, 29, 3, 4, STEEL[3]); c.px(51, 30, 1, 2, LENS[1])   # searchlight
    # turret
    c.box(25, 28, 20, 9, GREEN, 5)             # roof
    c.box(24, 37, 22, 7, GREEN, 3)             # side face
    c.box(26, 37, 18, 7, HOUSE, 4)             # house panel (turret side)
    c.box(28, 25, 4, 3, GREEN, 2); c.px(29, 26, 2, 1, STEEL[0])  # cupola
    c.box(38, 26, 4, 2, STEEL, 2)
    c.box(44, 29, 3, 6, STEEL, 2)              # mantlet
    c.box(44, 30, 8, 4, STEEL, 1)              # sleeve
    c.box(51, 31, 14, 2, STEEL, 0); c.px(51, 31, 14, 1, STEEL[1])  # barrel
    c.box(63, 29, 4, 6, STEEL, 3); c.px(64, 29, 1, 6, STEEL[0]); c.px(66, 29, 1, 6, STEEL[0])
    c.text(text_x, 38, "07", HIL[2])


def main():
    frames = []
    for name in "NESW":
        flip = name == "W"
        c = Cv(flip)
        if name == "N":
            tank_n(c)
        elif name == "S":
            tank_s(c)
        else:
            # E text at x=32,y=38 on screen; W tank is mirrored so the panel moves to 72-26-18=28
            tank_e(c, 32 if not flip else 28 + 5)
        frames.append(c.finish())
    sheet = C.pack_sheet(frames, 4, CV, CV)
    sheet.save(os.path.join(C.PILOT, "C_hand_48.png"))
    print("wrote C_hand_48.png", sheet.size)


if __name__ == "__main__":
    main()
