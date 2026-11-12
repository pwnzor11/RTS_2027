"""Authority Flak Emplacement: a sandbagged ring round a quick-firing twin gun pointed at the sky (1x1 cell). Original design.
Contract: see auth_refinery.py; built by tools/art/structures/auth_flak_build.py (the head rotates, like auth_turret.py).

The head ROTATES: frames are indexed like vehicles (facing 0 = north, clockwise, 22.5 deg steps).  The base (pad, three courses of
sandbags, ammunition crates, pedestal) is identical in every frame; only the head, its house plates and the barrels turn.  The
barrels are elevated about 58 degrees (anti-air only: targets = air).  The head is true 3D (not ground-stretched).
House colour: the front shield, both cheek plates and the rear magazine casing, so it reads from every facing.
STATES here are flat frame counts for build_structure.render(); auth_flak_build.py writes the real manifest:
  idle     16 facings x 1   frame = first + facing
  fire     16 facings x 2   frame = first + facing * 2 + k   (k 0 = muzzle flash and recoil, k 1 = flash dying)
  damaged  16 facings x 1   (structural: one barrel snapped, shield torn, magazine split, sandbags spilled)
  build    1 facing  x 6    stage 5 = the finished emplacement facing north = idle facing 0
Not a producer; DOCK is the cell below.
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

ID = "auth_flak"
TITLE = "Authority Flak Emplacement"
FOOTPRINT = (1, 1)
CANVAS = (80, 112)
ORIGIN = (8, 40)        # footprint top-left corner inside the frame: 8 px margin each side, 40 px above (the elevated barrels)
DOCK = (0, 1)
CAP_MATERIAL = "concrete2"
FACINGS = 16
STATES = [("idle", 16, 1), ("fire", 32, 1), ("damaged", 16, 1), ("build", 6, 5)]
MANIFEST_STATES = [("idle", 16, 1, 1), ("fire", 16, 2, 2), ("damaged", 16, 1, 1), ("build", 1, 6, 5)]   # (name, facings, frames, ticks)


def px(x, y, z):
    return round(ORIGIN[0] + 32 + x * 18.8), round(ORIGIN[1] + 32 - y * 18.8 * math.sin(math.radians(40)) - z * 14.4)


_hx, _hy = px(0.0, 0.0, 1.9)
FX = [
    dict(state="damaged", kind="fire_small", x=_hx, y=_hy, phase=0),
    dict(state="damaged", kind="smoke_plume", x=_hx, y=_hy - 2, phase=2),
]

MATERIALS = dict(_ref.MATERIALS)
MATERIALS.update({
    "steel2": ("#979EA3", dict(rough=0.4, metal=0.5)),
    "gunmetal": ("#2A2E31", dict(rough=0.35, metal=0.7)),
    "flash": ("#FFF2B0", dict(rough=0.3, emit="#FFE9A0")),
    "flash2": ("#FFD36A", dict(rough=0.3, emit="#FFC850")),
    "sandbag": ("#A39574", dict(rough=1.0)),
    "sandbag2": ("#877B5C", dict(rough=1.0)),
    "sandbag_s": ("#6E664F", dict(rough=1.0)),
    "brass": ("#B38F3C", dict(rough=0.4, metal=0.5)),
})
SOOT_SKIN = dict(_ref.SOOT_SKIN)
SOOT_SKIN.update({"sandbag": "sandbag_s", "sandbag2": "sandbag_s"})
PRIMER_SKIN = dict(_ref.PRIMER_SKIN)
PRIMER_SKIN.update({"steel2": "primer", "gunmetal": "dark", "sandbag": "primer", "sandbag2": "primer", "brass": "primer"})
beam, post = _ref.beam, _ref.post

ZH = 0.82            # top of the turntable
ELEV = 58.0          # barrel elevation, degrees
ELR = math.radians(ELEV)


def part(S, name, size, c, m, th, bevel=0.03, pitch=0.0, nocast=False):
    """Box in head-local coordinates: c = (right, forward, z); th = head facing in degrees clockwise from north."""
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    return S.box(name, size, (wx, wy, c[2]), m, bevel, rot=(pitch, 0, -th), stretch=False, nocast=nocast)


def cpart(S, name, r, h, c, m, th, verts=16):
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    return S.cyl(name, r, h, (wx, wy, c[2]), m, axis="Z", verts=verts, stretch=False)


def base(S, dmg=False):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.12, "pad", 0.03)
    n = 15
    for course in range(3):
        for k in range(n):
            if dmg and ((k * 5 + course * 3) % 7 == 0 and course > 0):
                continue
            a = 2 * math.pi * (k + 0.5 * (course % 2)) / n
            r = 1.32
            m = "sandbag" if (k + course) % 2 == 0 else "sandbag2"
            S.box(f"sbag{course}_{k}", (0.6, 0.34, 0.21), (r * math.sin(a), r * math.cos(a), 0.12 + 0.2 * course + 0.105), m, 0.05,
                  rot=(0, 0, -math.degrees(a)), stretch=False)
    S.cyl("floor", 1.12, 0.08, (0, 0, 0.16), "concrete2", verts=28, stretch=False)
    S.cyl("ped", 0.62, ZH - 0.2, (0, 0, 0.2 + (ZH - 0.2) / 2), "concrete", verts=20, stretch=False)
    S.cyl("track", 0.78, 0.1, (0, 0, ZH - 0.02), "steel", verts=24, stretch=False)
    # ammunition crates and a cable duct on the pad corners, off the barrel's sweep
    S.bb("ammo", -1.55, -1.0, -1.55, -1.1, 0.12, 0.5, "crate", 0.03)
    S.bb("ammo2", -1.5, -1.05, -1.5, -1.15, 0.5, 0.75, "crate", 0.03, rot=(0, 0, 14))
    S.bb("ammol", -1.5, -1.05, -1.57, -1.55, 0.25, 0.4, "dark", 0.0, nocast=True)
    S.bb("duct", 1.0, 1.55, -1.55, -1.4, 0.12, 0.22, "steel", 0.02)
    for k in range(4):     # spent brass beside the ring
        S.cyl(f"brass{k}", 0.05, 0.2, (1.0 + 0.14 * k, -1.2 + 0.05 * (k % 2), 0.22), "brass", verts=6, stretch=False, nocast=True)
    if dmg:
        S.box("crackc", (0.5, 0.06, 0.4), (0.2, -1.4, 0.4), "dark", 0.0, rot=(0, 0, 20), stretch=False, nocast=True)
        S.box("spill", (0.55, 0.34, 0.2), (0.9, -1.55, 0.2), "sandbag_s", 0.04, rot=(0, 0, 30), stretch=False)
        S.box("spill2", (0.55, 0.34, 0.2), (-0.5, -1.6, 0.18), "sandbag_s", 0.04, rot=(0, 0, -20), stretch=False)
        S.bb("scorchb", 0.5, 1.4, -1.6, -1.58, 0.0, 0.4, "char", 0.0, nocast=True)


def head(S, th, dmg=False, flash=0):
    """Head at facing th. flash: 0 none, 1 = full muzzle flash + recoil, 2 = dying flash."""
    z0 = ZH + 0.06
    cpart(S, "deck", 1.0, 0.12, (0, 0, z0 + 0.06), "steel", th, verts=24)
    cpart(S, "deckrim", 1.02, 0.05, (0, 0, z0 + 0.02), "dark", th, verts=24)
    # cheek plates (house) and cradle frame
    for sx in (-1, 1):
        if dmg and sx > 0:
            part(S, "cheekR", (0.12, 0.6, 0.55), (0.55, -0.1, z0 + 0.5), "hull2", th, 0.03)
            part(S, "houseR", (0.04, 0.45, 0.4), (0.62, -0.05, z0 + 0.5), "house", th, 0.0)
            continue
        part(S, f"cheek{sx}", (0.14, 1.1, 0.95), (sx * 0.55, -0.1, z0 + 0.58), "hull", th, 0.04)
        part(S, f"house{sx}", (0.04, 0.85, 0.7), (sx * 0.64, -0.1, z0 + 0.6), "house", th, 0.0)
    part(S, "cross", (1.0, 0.4, 0.3), (0, -0.1, z0 + 0.3), "hull2", th, 0.04)
    # rear magazine casing (house top) and seat
    if not dmg:
        part(S, "mag", (0.9, 0.55, 0.5), (0, -0.78, z0 + 0.35), "hull", th, 0.05)
        part(S, "housemag", (0.7, 0.4, 0.04), (0, -0.78, z0 + 0.62), "house2", th, 0.0)
        part(S, "housemagb", (0.7, 0.04, 0.3), (0, -1.07, z0 + 0.38), "house", th, 0.0)
    else:
        part(S, "mag", (0.9, 0.55, 0.3), (0, -0.78, z0 + 0.25), "hull2", th, 0.05)
        part(S, "housemag", (0.4, 0.4, 0.04), (0.2, -0.78, z0 + 0.42), "house2", th, 0.0)
        part(S, "housemagb", (0.4, 0.04, 0.22), (0.2, -1.07, z0 + 0.3), "house", th, 0.0)
    part(S, "seat", (0.3, 0.3, 0.06), (-0.55, -0.85, z0 + 0.62), "dark", th, 0.0, nocast=True) if False else None
    # front shield: sloped plate in house colour, with a slot for the guns
    if not dmg:
        part(S, "shield", (1.25, 0.1, 0.75), (0, 0.5, z0 + 0.7), "hull", th, 0.03, pitch=-14)
        part(S, "shieldhouse", (0.9, 0.04, 0.5), (0, 0.555, z0 + 0.7), "house", th, 0.0, pitch=-14)
    else:
        part(S, "shield", (0.7, 0.1, 0.75), (-0.28, 0.5, z0 + 0.7), "hull", th, 0.03, pitch=-14)
        part(S, "shieldhouse", (0.4, 0.04, 0.45), (-0.3, 0.555, z0 + 0.7), "house", th, 0.0, pitch=-14)
        part(S, "gash", (0.5, 0.05, 0.3), (0.35, 0.45, z0 + 0.55), "dark", th, 0.0, nocast=True)
    # trunnion and twin barrels elevated ELEV degrees
    part(S, "trun", (1.0, 0.16, 0.16), (0, 0.15, z0 + 0.95), "gunmetal", th, 0.02)
    py_, pz = 0.15, z0 + 0.95
    rec = 0.16 if flash == 1 else (0.07 if flash == 2 else 0.0)
    dy, dz = math.cos(ELR), math.sin(ELR)
    for k, sx in enumerate((-0.22, 0.22)):
        gone = dmg and k == 1
        bent = dmg and k == 0
        L0 = 0.9 if gone else 1.75
        s0 = -0.25 - rec               # start of the barrel behind the trunnion
        cen = s0 + L0 / 2
        part(S, f"barrel{k}", (0.12, L0, 0.12), (sx, py_ + dy * cen, pz + dz * cen), "gunmetal", th, 0.0, pitch=ELEV + (-22 if bent else 0))
        if not gone:
            sh = s0 + 0.5
            part(S, f"shroud{k}", (0.2, 0.7, 0.2), (sx, py_ + dy * sh, pz + dz * sh), "steel2", th, 0.02, pitch=ELEV + (-22 if bent else 0))
            tip = s0 + L0
            if not bent:
                part(S, f"brake{k}", (0.17, 0.16, 0.17), (sx, py_ + dy * (tip - 0.04), pz + dz * (tip - 0.04)), "dark", th, 0.0, pitch=ELEV)
            if flash and not dmg:
                size = 0.65 if flash == 1 else 0.35
                fc = tip + size / 2
                part(S, f"fl{k}", (0.22 if flash == 1 else 0.12, size, 0.22 if flash == 1 else 0.12), (sx, py_ + dy * fc, pz + dz * fc), "flash", th, 0.0,
                     pitch=ELEV, nocast=True)
                if flash == 1:
                    part(S, f"fl2{k}", (0.4, 0.16, 0.4), (sx, py_ + dy * (tip + 0.05), pz + dz * (tip + 0.05)), "flash2", th, 0.0, pitch=ELEV, nocast=True)
    # sight arm on the left cheek
    part(S, "sight", (0.1, 0.4, 0.14), (-0.75, 0.25, z0 + 0.95), "steel", th, 0.02)
    part(S, "lens", (0.06, 0.04, 0.1), (-0.75, 0.46, z0 + 0.95), "lamp" if not dmg else "dark", th, 0.0, nocast=True)
    if dmg:
        part(S, "dent", (0.5, 0.4, 0.05), (0.3, -0.1, z0 + 0.62), "char", th, 0.0, nocast=True)
        part(S, "strut", (0.05, 0.05, 0.5), (0.5, 0.0, z0 + 0.9), "rebar", th, 0.0, pitch=18)


# ====================================================================================================== build stages
def build_site(S, i):
    if i < 3:
        S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.12, "pad", 0.03)
    if i == 0:
        S.cyl("pit", 1.4, 0.05, (0, 0, 0.14), "soil", verts=28, stretch=False)
        S.cyl("pit2", 1.15, 0.07, (0, 0, 0.15), "soil2", verts=28, stretch=False)
        S.cyl("pit3", 0.85, 0.08, (0, 0, 0.16), "soil", verts=28, stretch=False)
        S.bb("mound", 0.9, 1.6, 0.6, 1.5, 0.12, 0.6, "soil", 0.2)
        for (sx, sy) in ((-1.5, -1.5), (1.5, -1.5), (-1.5, 1.5), (1.5, 1.5)):
            post(S, "stake", sx, sy, 0.12, 0.8, 0.09, "timber")
            post(S, "staket", sx, sy, 0.62, 0.8, 0.11, "string")
        S.bb("line", -1.5, 1.5, -1.52, -1.48, 0.7, 0.72, "string", 0.0, nocast=True)
        S.bb("line", -1.5, 1.5, 1.48, 1.52, 0.7, 0.72, "string", 0.0, nocast=True)
        S.bb("sign", -1.0, -0.2, -1.65, -1.6, 0.4, 0.95, "yellow", 0.0)
    elif i == 1:
        S.cyl("slabC", 1.45, 0.2, (0, 0, 0.22), "raw", verts=28, stretch=False)
        for k in range(10):
            a = math.radians(k * 36)
            post(S, "rb", 1.0 * math.sin(a), 1.0 * math.cos(a), 0.32, 1.1, 0.05, "rebar")
        S.cyl("mixer", 0.3, 0.6, (-1.25, -1.25, 0.5), "primer", verts=14, stretch=False)
        S.bb("mixer", -1.55, -0.95, -1.5, -1.0, 0.12, 0.3, "girder", 0.0)
        S.bb("sandpile", 0.9, 1.6, 0.8, 1.5, 0.12, 0.5, "soil", 0.25)
    elif i == 2:     # floor poured, pedestal formwork, first sandbag course half laid, bags piled
        S.cyl("slabC", 1.45, 0.2, (0, 0, 0.22), "raw", verts=28, stretch=False)
        S.cyl("ped", 0.62, 0.6, (0, 0, 0.5), "raw", verts=20, stretch=False)
        for k in range(8):
            a = math.radians(k * 45 + 5)
            S.box(f"fb{k}", (0.5, 0.06, 0.45), (0.7 * math.sin(a), 0.7 * math.cos(a), 0.5), "timber", 0.0, rot=(0, 0, -math.degrees(a)), stretch=False)
        for k in range(7):
            a = 2 * math.pi * (k + 0.5) / 15 + 3.0
            S.box(f"sb{k}", (0.6, 0.34, 0.21), (1.32 * math.sin(a), 1.32 * math.cos(a), 0.225), "sandbag" if k % 2 else "sandbag2", 0.05,
                  rot=(0, 0, -math.degrees(a)), stretch=False)
        S.bb("sbpile", 0.6, 1.4, -1.5, -1.0, 0.12, 0.45, "sandbag", 0.1)
    elif i == 3:     # ring finished, turntable set, head frame: girders and a lifting derrick
        base(S)
        for sx in (-0.6, 0.6):
            for sy in (-0.5, 0.5):
                post(S, "hp", sx, sy, ZH + 0.06, ZH + 1.1, 0.12, "girder")
        for z in (ZH + 0.6, ZH + 1.1):
            for sy in (-0.5, 0.5):
                beam(S, "hx", (-0.6, sy, z), (0.6, sy, z), 0.1, "girder")
            for sx in (-0.6, 0.6):
                beam(S, "hy", (sx, -0.5, z), (sx, 0.5, z), 0.1, "girder")
        beam(S, "hb", (-0.6, -0.5, ZH + 0.1), (0.6, -0.5, ZH + 0.6), 0.06, "girder")
        S.bb("pl1", -1.5, -0.8, 0.6, 1.5, 0.12, 0.4, "timber", 0.0)
        _ref.spark(S, 0.6, -0.5, ZH + 1.1, 1, 3)
    elif i == 4:     # head hull plated in primer, scaffold on one side, guns not yet mounted
        base(S)
        head(S, 0.0)
        for sx in (-1.55, -1.1):
            post(S, "sp", sx, -1.3, 0.12, 2.5, 0.07, "timber")
            post(S, "sp", sx, -0.4, 0.12, 2.5, 0.07, "timber")
        for z in (0.9, 1.7):
            S.bb("deck2", -1.62, -1.02, -1.4, -0.3, z, z + 0.06, "timber", 0.0)
        _ref.spark(S, 0.0, -0.9, ZH + 1.0, 1, 5)


def build(S, state, i, n):
    if state == "idle":
        S.skin.clear()
        base(S)
        head(S, i * 22.5)
    elif state == "fire":
        f, k = divmod(i, 2)
        base(S)
        head(S, f * 22.5, flash=1 + k)
    elif state == "damaged":
        S.skin.update(SOOT_SKIN)
        base(S, dmg=True)
        head(S, i * 22.5, dmg=True)
    else:
        if i < 5:
            if i == 4:
                S.hide = lambda nm: nm.startswith(("barrel", "shroud", "brake", "house", "lens", "sight", "trun"))
                S.skin.update(PRIMER_SKIN)
            build_site(S, i)
            return
        base(S)
        head(S, 0.0)
