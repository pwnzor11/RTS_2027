"""Authority Gun Turret: a concrete emplacement (1x1 cell) with a rotating armoured head and a twin machine gun.
Original design, no franchise references. Contract: see auth_refinery.py; built by tools/art/structures/turret_build.py.

The head ROTATES: frames are indexed like vehicles (facing 0 = north, clockwise, 22.5 deg steps). The base (pad, ring,
turntable) is identical in every frame; only the head, its house plates and the barrels turn. The head is true 3D
(not ground-stretched), like a vehicle, so it turns without shearing.
STATES here are flat frame counts for build_structure.render(); turret_build.py writes the real manifest:
  idle     16 facings x 1   frame = first + facing
  fire     16 facings x 2   frame = first + facing * 2 + k   (k 0 = muzzle flash and recoil, k 1 = flash dying)
  damaged  16 facings x 1   (structural: dented head, one barrel shot away, torn plate, cracked ring)
  build    1 facing  x 6    stage 5 = the finished turret facing north = idle facing 0
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

ID = "auth_turret"
TITLE = "Authority Gun Turret"
FOOTPRINT = (1, 1)
CANVAS = (80, 104)
ORIGIN = (8, 32)        # footprint top-left corner inside the frame: 8 px margin each side (barrel and flash), 32 px above
DOCK = (0, 1)
CAP_MATERIAL = "concrete2"
FACINGS = 16
# Flat frame counts for the renderer (see the docstring); the manifest states are in MANIFEST_STATES.
STATES = [("idle", 16, 1), ("fire", 32, 1), ("damaged", 16, 1), ("build", 6, 5)]
MANIFEST_STATES = [("idle", 16, 1, 1), ("fire", 16, 2, 2), ("damaged", 16, 1, 1), ("build", 1, 6, 5)]   # (name, facings, frames, ticks)


def px(x, y, z):
    """Frame pixel of a point in world units (true 3D: ground y is foreshortened by sin 40)."""
    return round(ORIGIN[0] + 32 + x * 18.8), round(ORIGIN[1] + 32 - y * 18.8 * math.sin(math.radians(40)) - z * 14.4)


# Effect anchors (D-069), pixels in frame space; the head stays at the centre in every facing, so one set covers all.
_hx, _hy = px(0.0, 0.0, 1.85)
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
})
SOOT_SKIN = dict(_ref.SOOT_SKIN)
PRIMER_SKIN = dict(_ref.PRIMER_SKIN)
PRIMER_SKIN.update({"steel2": "primer", "gunmetal": "dark"})
beam, post = _ref.beam, _ref.post

ZH = 0.82           # top of the base / turntable height


def part(S, name, size, c, m, th, bevel=0.03, pitch=0.0, nocast=False):
    """Box in head-local coordinates: c = (right, forward, z); th = head facing in degrees clockwise from north."""
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    return S.box(name, size, (wx, wy, c[2]), m, bevel, rot=(pitch, 0, -th), stretch=False, nocast=nocast)


def cpart(S, name, r, h, c, m, th, axis="Z", verts=16):
    """Cylinder in head-local coordinates (axis Z or Y = along the gun)."""
    t = math.radians(th)
    wx = c[0] * math.cos(t) + c[1] * math.sin(t)
    wy = -c[0] * math.sin(t) + c[1] * math.cos(t)
    o = S.cyl(name, r, h, (wx, wy, c[2]), m, axis="Z", verts=verts, stretch=False)
    if axis == "Y":
        o.rotation_euler = (math.radians(90), 0, -t)
    return o


def base(S, dmg=False, raw=False):
    S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.12, "pad", 0.03)
    S.cyl("step", 1.5, 0.28, (0, 0, 0.12 + 0.14), "concrete2", verts=32, stretch=False)
    S.cyl("ring", 1.28, 0.62, (0, 0, 0.12 + 0.31), "concrete", verts=32, stretch=False)
    for k in range(8):       # yellow / dark ticks on the step
        a = math.radians(k * 45 + 22.5)
        S.box(f"tick{k}", (0.3, 0.12, 0.04), (1.4 * math.sin(a), 1.4 * math.cos(a), 0.41), "yellow" if k % 2 == 0 else "dark", 0.0,
              rot=(0, 0, -math.degrees(a)), stretch=False, nocast=True)
    S.cyl("track", 1.1, 0.1, (0, 0, ZH - 0.05 + 0.03), "steel", verts=32, stretch=False)
    S.cyl("trackd", 1.0, 0.04, (0, 0, ZH + 0.02 + 0.03), "dark", verts=32, stretch=False)
    # ammunition box and a cable duct on the pad corners, off the barrel's sweep
    S.bb("ammo", -1.5, -0.95, -1.5, -1.05, 0.12, 0.5, "crate", 0.03)
    S.bb("ammol", -1.45, -1.0, -1.52, -1.5, 0.3, 0.4, "dark", 0.0, nocast=True)
    S.bb("duct", 1.0, 1.55, -1.45, -1.3, 0.12, 0.22, "steel", 0.02)
    if dmg:
        S.box("crackc", (0.5, 0.06, 0.62), (0.2, -1.27 * 1.0, 0.45), "dark", 0.0, rot=(0, 0, 20), stretch=False, nocast=True)
        S.box("chunkc", (0.5, 0.4, 0.25), (1.2, -0.95, 0.2), "concrete_s", 0.0, rot=(0, 0, 25), stretch=False)
        S.box("chunkc", (0.3, 0.26, 0.18), (-0.9, -1.2, 0.18), "concrete2_s", 0.0, rot=(0, 0, -30), stretch=False)
        S.bb("scorchb", 0.5, 1.4, -1.58, -1.56, 0.0, 0.4, "char", 0.0, nocast=True)


def head(S, th, dmg=False, flash=0, primer=False):
    """Head at facing th. flash: 0 none, 1 = full muzzle flash + recoil, 2 = dying flash."""
    z0 = ZH + 0.08
    cpart(S, "collar", 1.0, 0.2, (0, 0, z0 + 0.1), "steel", th, verts=24)
    # hull: body with a sloped front and rear
    part(S, "hull", (1.7, 1.55, 0.7), (0, -0.05, z0 + 0.55), "hull", th, 0.1)
    part(S, "glacis", (1.5, 0.5, 0.55), (0, 0.78, z0 + 0.5), "hull2", th, 0.08, pitch=-26)
    part(S, "rear", (1.5, 0.4, 0.5), (0, -0.88, z0 + 0.52), "hull2", th, 0.08, pitch=22)
    part(S, "roofp", (1.5, 1.3, 0.1), (0, -0.05, z0 + 0.93), "hull2", th, 0.05)
    # house plates: top plate (mid shade) and the two side plates
    if not dmg:
        part(S, "housetop", (0.95, 0.75, 0.04), (0, -0.2, z0 + 1.0), "house2", th, 0.0)
        part(S, "houseL", (0.04, 0.9, 0.42), (-0.87, -0.02, z0 + 0.58), "house", th, 0.0)
        part(S, "houseR", (0.04, 0.9, 0.42), (0.87, -0.02, z0 + 0.58), "house", th, 0.0)
    else:
        part(S, "housetop", (0.5, 0.75, 0.04), (0.22, -0.2, z0 + 1.0), "house2", th, 0.0)       # half of it blown off
        part(S, "houseL", (0.04, 0.9, 0.42), (-0.87, -0.02, z0 + 0.58), "house", th, 0.0)
    # sensor block and hatch on the roof (asymmetric: makes the facing legible)
    part(S, "sens", (0.34, 0.36, 0.28), (-0.5, -0.5, z0 + 1.12), "steel", th, 0.03)
    part(S, "lens", (0.2, 0.04, 0.12), (-0.5, -0.32, z0 + 1.14), "lamp" if not dmg else "dark", th, 0.0, nocast=True)
    cpart(S, "hatch", 0.26, 0.08, (0.5, -0.55, z0 + 1.0), "steel", th, verts=12)
    cpart(S, "antenna", 0.025, 0.9, (0.65, -0.78, z0 + 1.35), "dark", th, verts=6)
    # mantlet and twin MG
    part(S, "mantlet", (1.0, 0.3, 0.5), (0, 1.0, z0 + 0.52), "gunmetal", th, 0.05)
    rec = 0.14 if flash == 1 else (0.06 if flash == 2 else 0.0)
    for k, sx in enumerate((-0.27, 0.27)):
        bent = dmg and k == 1
        gone = dmg and k == 0
        L = 0.82 - rec * 0 - (0.5 if gone else 0.0)
        cy = 1.15 + L / 2 - rec
        if bent:
            part(S, f"barrel{k}", (0.12, L, 0.12), (sx, cy - 0.05, z0 + 0.6), "gunmetal", th, 0.0, pitch=24)
        else:
            part(S, f"barrel{k}", (0.12, L, 0.12), (sx, cy, z0 + 0.55), "gunmetal", th, 0.0)
        if not gone and not bent:
            part(S, f"shroud{k}", (0.2, 0.42, 0.2), (sx, 1.38 - rec, z0 + 0.55), "steel", th, 0.02)
            part(S, f"brake{k}", (0.17, 0.14, 0.17), (sx, 1.15 + L - 0.05 - rec, z0 + 0.55), "dark", th, 0.0)
        if flash and not dmg:
            tip = 1.15 + L - rec
            size = 0.55 if flash == 1 else 0.3
            part(S, f"fl{k}", (0.2 if flash == 1 else 0.12, size, 0.2 if flash == 1 else 0.12), (sx, tip + size / 2 + 0.02, z0 + 0.55), "flash", th, 0.0, nocast=True)
            if flash == 1:
                part(S, f"fl2{k}", (0.38, 0.14, 0.38), (sx, tip + 0.06, z0 + 0.55), "flash2", th, 0.0, nocast=True)
    if dmg:
        # dented glacis: dark gash, torn side armour, exposed rebar-like struts, a scorch on the roof
        part(S, "gash", (0.7, 0.05, 0.3), (-0.2, 1.05, z0 + 0.45), "dark", th, 0.0, nocast=True)
        part(S, "tear", (0.04, 0.55, 0.3), (0.88, 0.05, z0 + 0.6), "dark", th, 0.0, nocast=True)
        part(S, "dent", (0.5, 0.4, 0.05), (0.3, 0.3, z0 + 0.98), "char", th, 0.0, nocast=True)
        part(S, "strut", (0.05, 0.05, 0.5), (0.7, 0.2, z0 + 1.15), "rebar", th, 0.0, pitch=18)
        part(S, "strut2", (0.05, 0.05, 0.4), (0.5, 0.35, z0 + 1.12), "rebar", th, 0.0, pitch=-14)
        part(S, "hole", (0.45, 0.4, 0.06), (0.25, -0.35, z0 + 1.0), "dark", th, 0.0, nocast=True)


# ====================================================================================================== build stages
def build_site(S, i):
    if i < 3:        # stages 3-4 get their pad from base() (a second coplanar pad gives black shadow acne)
        S.bb("pad", -1.62, 1.62, -1.62, 1.62, 0.0, 0.12, "pad", 0.03)
    if i == 0:       # staked plot, ring trench dug, spoil
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
    elif i == 1:     # slab and rebar ring, a mixer
        S.cyl("slabC", 1.45, 0.2, (0, 0, 0.22), "raw", verts=28, stretch=False)
        for k in range(14):
            a = math.radians(k * 360 / 14)
            post(S, "rb", 1.1 * math.sin(a), 1.1 * math.cos(a), 0.32, 1.15, 0.05, "rebar")
        for k in range(8):
            a = math.radians(k * 45)
            post(S, "rb", 0.5 * math.sin(a), 0.5 * math.cos(a), 0.32, 0.9, 0.05, "rebar")
        S.cyl("mixer", 0.3, 0.6, (-1.25, -1.25, 0.5), "primer", verts=14, stretch=False)
        S.bb("mixer", -1.55, -0.95, -1.5, -1.0, 0.12, 0.3, "girder", 0.0)
    elif i == 2:     # ring cast in raw concrete, formwork half on, rebar stubs up, crate of parts
        S.cyl("step", 1.5, 0.28, (0, 0, 0.26), "raw", verts=32, stretch=False)
        S.cyl("ring", 1.28, 0.62, (0, 0, 0.43), "raw", verts=32, stretch=False)
        for k in range(12):
            a = math.radians(k * 30 + 5)
            S.box(f"fb{k}", (0.6, 0.06, 0.5), (1.36 * math.sin(a), 1.36 * math.cos(a), 0.4), "timber", 0.0, rot=(0, 0, -math.degrees(a)), stretch=False)
        for k in range(6):
            a = math.radians(k * 60)
            post(S, "rb", 0.9 * math.sin(a), 0.9 * math.cos(a), 0.74, 1.2, 0.05, "rebar")
        S.bb("crate", 1.0, 1.55, -1.5, -0.95, 0.12, 0.6, "crate", 0.03)
    elif i == 3:     # base finished, turntable ring set, head frame: girders and a lifting derrick
        base(S)
        S.cyl("collar", 1.0, 0.2, (0, 0, ZH + 0.18), "steel", verts=24, stretch=False)
        for sx in (-0.7, 0.7):
            for sy in (-0.6, 0.6):
                post(S, "hp", sx, sy, ZH + 0.28, ZH + 1.25, 0.12, "girder")
        for z in (ZH + 0.7, ZH + 1.25):
            for sy in (-0.6, 0.6):
                beam(S, "hx", (-0.7, sy, z), (0.7, sy, z), 0.1, "girder")
            for sx in (-0.7, 0.7):
                beam(S, "hy", (sx, -0.6, z), (sx, 0.6, z), 0.1, "girder")
        beam(S, "hb", (-0.7, -0.6, ZH + 0.3), (0.7, -0.6, ZH + 0.7), 0.06, "girder")
        beam(S, "hb", (0.7, -0.6, ZH + 0.3), (-0.7, -0.6, ZH + 0.7), 0.06, "girder")
        S.bb("pl1", -1.5, -0.8, 0.6, 1.5, 0.12, 0.4, "timber", 0.0)
        _ref.spark(S, 0.7, -0.6, ZH + 1.25, 1, 3)
    elif i == 4:     # head hull plated in primer, scaffold on one side, gun not yet mounted
        base(S)
        head(S, 0.0, primer=True)
        # strip off the guns for this stage: handled by S.hide in build()
        for sx in (-1.55, -1.1):
            post(S, "sp", sx, -1.3, 0.12, 2.5, 0.07, "timber")
            post(S, "sp", sx, -0.4, 0.12, 2.5, 0.07, "timber")
        for z in (0.9, 1.7):
            S.bb("deck", -1.62, -1.02, -1.4, -0.3, z, z + 0.06, "timber", 0.0)
        beam(S, "br", (-1.55, -1.3, 0.12), (-1.1, -1.3, 1.9), 0.05, "timber")
        _ref.spark(S, 0.0, -0.9, ZH + 1.1, 1, 5)


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
    else:           # build
        if i < 5:
            if i == 4:       # the hull in primer, no guns, plates or fittings yet (hide/skin must be set before the parts are made)
                S.hide = lambda nm: nm.startswith(("barrel", "shroud", "brake", "mantlet", "house", "lens", "sens", "hatch", "antenna", "tick"))
                S.skin.update(PRIMER_SKIN)
            build_site(S, i)
            return
        base(S)
        head(S, 0.0)
