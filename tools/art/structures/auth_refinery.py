"""Authority Refinery: a fixed centrifuge refinery (3x3 cells). Original design, no franchise references.

Contract for a structure module (read by tools/art/build_structure.py):
  ID, TITLE, FOOTPRINT (cells), CANVAS (frame w,h px), ORIGIN (footprint top-left in the frame, px), DOCK (cell,
  footprint-relative), STATES [(name, frames, ticks_per_frame)], MATERIALS {name: (hex, {principled kwargs})},
  build(S, state, i, n)  -> add primitives to S for one frame; set S.clip_z to cut the building off at a height.

Authoring units: 3.4 per cell, x east, y NORTH, z up, footprint centred on (0, 0): x, y in [-5.1, 5.1].
The camera looks north from the south, so the south face (y = -5.1 side) and the roofs are what you see.
Screen size of things: 1 unit of x = 18.8 px; 1 unit of z = 14.4 px (cos 40 deg); 1 unit of y = 18.8 px of ground.
"""
import math

ID = "auth_refinery"
TITLE = "Authority Refinery"
FOOTPRINT = (3, 3)
CANVAS = (192, 224)
ORIGIN = (0, 32)        # footprint top-left corner inside the frame (32 px of headroom for the drum and the stack)
DOCK = (1, 3)           # harvester parks in the cell just below the middle of the footprint
CAP_MATERIAL = "concrete2"   # material of the faces made by the build-state cut
STATES = [("idle", 4, 5), ("damaged", 4, 5), ("build", 8, 5)]

MATERIALS = {
    "pad": ("#686C63", dict(rough=0.95)),
    "concrete": ("#8F9389", dict(rough=0.95)),
    "concrete2": ("#6E7269", dict(rough=0.95)),
    "roof": ("#4C5A44", dict(rough=0.85)),
    "hull": ("#566448", dict(rough=0.7)),
    "hull2": ("#4A5840", dict(rough=0.7)),
    "steel": ("#7B8187", dict(rough=0.45, metal=0.5)),
    "rust": ("#85502F", dict(rough=0.95)),
    "dark": ("#15170F", dict(rough=0.9)),
    "white": ("#EDEDE2", dict(rough=0.8)),
    "yellow": ("#C9A640", dict(rough=0.8)),
    "lamp": ("#FFE9A0", dict(rough=0.3, emit="#FFE9A0")),
    "smoke": ("#A3AA9B", dict(rough=1.0)),
    "smoke2": ("#6F766A", dict(rough=1.0)),
    "char": ("#3A4034", dict(rough=1.0)),
    "fire": ("#FFD36A", dict(rough=0.5, emit="#FFC850")),
}

HALL_X = 4.1
HALL_Y0, HALL_Y1 = -2.6, 2.2     # south wall / north wall (ground units)
WALL_H = 3.6                     # roof slab bottom
ROOF_T = 0.22
DRUM = (-1.9, 0.7)               # drum centre (x, y)
DRUM_R = 1.45
STACK = (3.3, 0.7)
Z_BUILD = [0.3, 1.1, 2.0, 2.9, 3.85, 4.8, 5.9]   # clip heights for the build frames 0..6; frame 7 = complete


def frame_params(state, i):
    return dict(theta=i * 22.5, damaged=(state == "damaged"))


def hazard(S, y0, y1, z=0.15):
    """Yellow / dark hazard stripes across the dock apron."""
    x0, w = -4.8, 0.6
    for k in range(16):
        S.bb(f"haz{k}", x0 + k * w, x0 + (k + 1) * w, y0, y1, 0.14, z, "yellow" if k % 2 == 0 else "dark", 0.0)


def scorch(S, name, x, y, z, w, h, d=0.03, plane="S"):
    """Irregular scorch patch made of a few dark boxes, on the south wall (plane S) or the roof (plane T)."""
    spec = [(0, 0, 1.0, 1.0), (-0.35, 0.3, 0.5, 0.6), (0.4, 0.35, 0.45, 0.5), (0.1, -0.4, 0.7, 0.35)]
    for k, (dx, dz, sw, sh) in enumerate(spec):
        if plane == "S":
            S.box(f"{name}{k}", (w * sw, d, h * sh), (x + dx * w * 0.5, y, z + dz * h * 0.5), "char", 0.0, nocast=True)
        else:
            S.box(f"{name}{k}", (w * sw, h * sh, d), (x + dx * w * 0.5, y + dz * h * 0.5, z), "char", 0.0, nocast=True)


def build(S, state, i, n):
    if state == "build":
        i_anim = 0          # the completed building (build frame 7) is exactly idle frame 0
    else:
        i_anim = i
    P = frame_params(state, i_anim)
    dmg = P["damaged"]
    th = P["theta"]
    YS = S.YS
    building = state == "build"
    if building:
        S.clip_z = Z_BUILD[i] if i < 7 else None

    # ------------------------------------------------------------------ pad, apron, hazard stripes
    S.bb("pad", -5.0, 5.0, -5.0, 5.0, 0.0, 0.14, "pad", 0.02)
    S.bb("padlip", -5.0, 5.0, -5.0, -4.9, 0.14, 0.2, "concrete2", 0.0)
    hazard(S, -4.88, -4.4)
    for sx in (-1.45, 1.45):
        S.bb("guide", sx - 0.07, sx + 0.07, -4.4, -2.9, 0.14, 0.17, "yellow", 0.0)

    # ------------------------------------------------------------------ hall: walls, pilasters, roof
    S.bb("hall", -HALL_X, HALL_X, HALL_Y0, HALL_Y1, 0.14, WALL_H, "concrete", 0.05)
    S.bb("plinth", -HALL_X - 0.1, HALL_X + 0.1, HALL_Y0 - 0.1, HALL_Y1 + 0.1, 0.14, 0.55, "concrete2", 0.04)
    for px in (-HALL_X, -2.55, 2.55, HALL_X):
        S.bb(f"pil{px}", px - 0.22, px + 0.22, HALL_Y0 - 0.14, HALL_Y0 + 0.1, 0.14, WALL_H + 0.05, "concrete2", 0.04)
    S.bb("roof", -HALL_X - 0.25, HALL_X + 0.25, HALL_Y0 - 0.2, HALL_Y1 + 0.2, WALL_H, WALL_H + ROOF_T, "roof", 0.06)
    S.bb("parapet", -HALL_X - 0.25, HALL_X + 0.25, HALL_Y0 - 0.2, HALL_Y0 - 0.02, WALL_H + ROOF_T, WALL_H + ROOF_T + 0.22, "concrete2", 0.03)
    for sx in (-1, 1):
        S.bb("parapetS", sx * (HALL_X + 0.07) - 0.07, sx * (HALL_X + 0.07) + 0.07, HALL_Y0 - 0.2, HALL_Y1 + 0.2, WALL_H + ROOF_T, WALL_H + ROOF_T + 0.18, "concrete2", 0.02)

    # ------------------------------------------------------------------ south face: bay, house panels, stencils
    yw = HALL_Y0 - 0.02
    S.bb("bay", -1.25, 1.25, yw - 0.06, yw + 0.1, 0.14, 2.5, "dark", 0.0)
    S.bb("lintel", -1.55, 1.55, yw - 0.16, yw + 0.1, 2.5, 2.95, "concrete2", 0.04)
    S.bb("lintelstripe", -1.5, 1.5, yw - 0.18, yw - 0.14, 2.62, 2.82, "yellow", 0.0)
    for sx in (-1, 1):
        S.bb("jamb", sx * 1.42 - 0.17, sx * 1.42 + 0.17, yw - 0.14, yw + 0.1, 0.14, 2.55, "concrete2", 0.03)
        # house panels: big plaque on each side of the bay, framed (23 px x 20 px at 1x)
        cx = sx * 2.9
        S.bb("pframe", cx - 1.2, cx + 1.2, yw - 0.08, yw + 0.02, 0.62, 3.1, "dark", 0.0)
        broken = dmg and sx > 0
        if not broken:
            S.bb("panel", cx - 1.1, cx + 1.1, yw - 0.14, yw - 0.06, 0.72, 3.0, "house", 0.0)
        # stencil bars on the plinth band
        for k in range(3):
            S.bb("sten", cx - 0.55 + k * 0.5, cx - 0.3 + k * 0.5, yw - 0.1, yw - 0.06, 0.22, 0.5, "white", 0.0, nocast=True)
    # rust streaks under the parapet
    for k, (rx, rw, rh) in enumerate(((-2.0, 0.18, 0.9), (0.2, 0.14, 0.5), (1.9, 0.2, 0.8), (-4.0, 0.16, 0.6))):
        S.bb(f"streak{k}", rx, rx + rw, yw - 0.12, yw - 0.1, 3.6 - rh, 3.6, "rust", 0.0, nocast=True)
    # hopper chute: funnel out of the bay toward the dock, sloped steel with concrete cheeks
    run, rise = 1.9 * YS, 1.05
    ang = -math.degrees(math.atan2(rise, run))   # tilt about X: top end toward the wall
    S.box("chute", (2.3, math.hypot(run, rise) / YS, 0.12), (0, yw - 0.95 - 0.35, 0.85), "steel", 0.03, rot=(ang, 0, 0))
    for sx in (-1, 1):
        S.box("cheek", (0.22, math.hypot(run, rise) / YS, 0.5), (sx * 1.3, yw - 0.95 - 0.35, 0.92), "concrete2", 0.03, rot=(ang, 0, 0))
    S.bb("hopperlip", -1.3, 1.3, yw - 2.25, yw - 2.05, 0.2, 0.55, "hull2", 0.04)
    # gantry arch over the bay with a stencilled plate
    for sx in (-1, 1):
        S.bb("gpost", sx * 1.7 - 0.1, sx * 1.7 + 0.1, yw - 1.05, yw - 0.85, 0.14, 3.3, "steel", 0.02)
    S.bb("gbeam", -1.8, 1.8, yw - 1.05, yw - 0.85, 3.2, 3.45, "yellow", 0.03)
    S.bb("gplate", -0.5, 0.5, yw - 1.08, yw - 1.04, 3.22, 3.43, "dark", 0.0)
    for k in range(4):
        S.bb("gnum", -0.38 + k * 0.22, -0.26 + k * 0.22, yw - 1.1, yw - 1.07, 3.25, 3.4, "white", 0.0, nocast=True)

    # ------------------------------------------------------------------ roof detail: house marking, tube rack, hatches
    zr = WALL_H + ROOF_T
    S.bb("roofhouse", 1.1, 3.3, -2.1, -1.2, zr, zr + 0.04, "house", 0.0)
    S.bb("roofframe", 1.0, 3.4, -2.2, -1.1, zr, zr + 0.02, "dark", 0.0)
    S.bb("hatch", -3.8, -2.4, -2.1, -0.9, zr, zr + 0.18, "concrete2", 0.04)
    # roof duct block with grille slots, between the hatch and the house marking
    S.bb("duct", -2.0, -0.2, -2.1, -1.0, zr, zr + 0.42, "concrete2", 0.04)
    for k in range(5):
        S.bb(f"grille{k}", -1.85 + k * 0.33, -1.67 + k * 0.33, -2.12, -1.0, zr + 0.42, zr + 0.5, "dark", 0.0)
    S.bb("hatchd", -3.65, -2.55, -1.95, -1.05, zr + 0.18, zr + 0.22, "steel", 0.02)
    S.bb("rack", 0.3, 2.5, -0.5, 0.3, zr, zr + 0.25, "steel", 0.03)
    for k in range(6):
        x = 0.55 + k * 0.36
        S.cyl(f"tube{k}", 0.15, 0.7, (x, -0.1, zr + 0.6), "steel", verts=12)
        lit = (k + i_anim) % 2 == 0
        S.cyl(f"tubecap{k}", 0.115, 0.14, (x, -0.1, zr + 1.01), "lamp" if (lit and not dmg) or (dmg and k in (1, 4)) else "dark", verts=10)

    # ------------------------------------------------------------------ centrifuge drum (rotates: plates, ribs, spokes)
    dx, dy = DRUM
    zb = zr
    S.cyl("plinthD", DRUM_R + 0.25, 0.45, (dx, dy, zb + 0.22), "concrete2", verts=32)
    S.cyl("drum", DRUM_R, 2.25, (dx, dy, zb + 0.45 + 1.12), "hull", verts=32)
    for k, zz in enumerate((zb + 0.83, zb + 1.63)):
        S.cyl(f"ring{k}", DRUM_R + 0.05, 0.1, (dx, dy, zz), "steel", verts=32)
    for k in range(4):
        ph = math.radians(th + 90 * k + 45)
        # vertical ribs (steel) and house plates between them, both ride on the drum
        S.box(f"rib{k}", (0.16, 0.12, 1.5), (dx, dy, zb + 1.23), "steel", 0.02, wo=(math.cos(ph) * (DRUM_R + 0.04), math.sin(ph) * (DRUM_R + 0.04)),
              rot=(0, 0, math.degrees(ph) - 90), stretch=False)
        ph2 = math.radians(th + 90 * k)
        if not (dmg and k == 1):
            S.box(f"plate{k}", (1.0, 0.07, 0.62), (dx, dy, zb + 2.03), "house", 0.0, wo=(math.cos(ph2) * (DRUM_R + 0.03), math.sin(ph2) * (DRUM_R + 0.03)),
                  rot=(0, 0, math.degrees(ph2) - 90), stretch=False)
        S.box(f"bolt{k}", (0.5, 0.08, 0.16), (dx, dy, zb + 0.53), "rust", 0.0, wo=(math.cos(ph2) * (DRUM_R + 0.02), math.sin(ph2) * (DRUM_R + 0.02)),
              rot=(0, 0, math.degrees(ph2) - 90), stretch=False)
    S.cyl("drumtop", DRUM_R + 0.08, 0.14, (dx, dy, zb + 2.5), "hull2", verts=32)
    S.cyl("drumcap", 1.0, 0.1, (dx, dy, zb + 2.62), "concrete2", verts=32)
    for k in range(2):
        S.box(f"spoke{k}", (1.9, 0.18, 0.1), (dx, dy, zb + 2.74), "steel", 0.02, rot=(0, 0, th + 90 * k), stretch=False)
    S.cyl("hub", 0.3, 0.2, (dx, dy, zb + 2.8), "rust", verts=14)
    S.cyl("hubtop", 0.1, 0.2, (dx, dy, zb + 2.96), "dark", verts=8)

    # ------------------------------------------------------------------ stack with rust bands, vent cap
    sx_, sy_ = STACK
    stack_top = zr + 2.2
    if dmg:
        stack_top = zr + 1.5
    S.cyl("stack", 0.38, stack_top - zr, (sx_, sy_, (stack_top + zr) / 2), "concrete", verts=16)
    for k, zz in enumerate((zr + 0.5, zr + 1.2, zr + 1.9)):
        if zz < stack_top - 0.1:
            S.cyl(f"sband{k}", 0.42, 0.14, (sx_, sy_, zz), "rust", verts=16)
    if not dmg:
        S.cyl("scollar", 0.5, 0.16, (sx_, sy_, stack_top + 0.05), "steel", verts=16)
        S.cyl("sdark", 0.3, 0.08, (sx_, sy_, stack_top + 0.15), "dark", verts=12)
    else:
        S.box("sjag1", (0.3, 0.3, 0.4), (sx_ - 0.12, sy_, stack_top + 0.15), "concrete2", 0.0, rot=(0, 20, 10), stretch=False)
        S.box("sjag2", (0.2, 0.25, 0.3), (sx_ + 0.2, sy_, stack_top + 0.08), "rust", 0.0, rot=(10, -15, 0), stretch=False)
    # ladder rungs on the stack
    S.bb("ladder", sx_ - 0.04 - 0.3, sx_ + 0.04 - 0.3, sy_ - 0.5, sy_ - 0.42, zr, stack_top - 0.2, "dark", 0.0)

    # ------------------------------------------------------------------ pipes
    zp = zr + 1.4
    S.cyl("pipeH", 0.17, 3.9, (1.4, dy, zp), "steel", axis="X", verts=12)
    for k, xx in enumerate((-0.2, 1.4, 2.8)):
        S.cyl(f"fl{k}", 0.24, 0.1, (xx, dy, zp), "rust", axis="X", verts=12)
    S.cyl("pipeV", 0.17, zp - zr, (STACK[0] - 0.1, dy, (zp + zr) / 2), "steel", verts=12)
    # side tank and riser on the pad strips
    if dmg:
        S.cyl("tankE", 0.6, 4.2, (4.62, 0.3, 0.85), "steel", axis="Y", verts=18)
    else:
        S.cyl("tankE", 0.6, 4.6, (4.62, 0.3, 0.85), "steel", axis="Y", verts=18)
    for k, yy in enumerate((-1.0, 0.3, 1.6)):
        S.cyl(f"tband{k}", 0.66, 0.12, (4.62, yy, 0.85), "rust", axis="Y", verts=18)
    S.bb("tsaddle", 4.35, 4.9, -1.3, 1.9, 0.14, 0.3, "concrete2", 0.02)
    S.cyl("riser", 0.13, 3.8, (-4.5, dy, 2.05), "steel", verts=10)
    S.cyl("valve", 0.24, 0.08, (-4.5, dy - 0.2, 1.1), "rust", axis="X", verts=10)
    # drum feed pipe down the hall's west side and across the roof
    S.cyl("feed", 0.14, 1.1, (-3.95, dy, zr + 0.14), "steel", axis="X", verts=10)

    # ------------------------------------------------------------------ damage: scorch, holes, fire, smoke
    if dmg:
        scorch(S, "scW", -3.7, yw - 0.16, 2.5, 1.0, 0.8)
        scorch(S, "scB", 0.6, yw - 0.12, 3.25, 1.6, 0.4)
        scorch(S, "scR", 2.6, yw - 0.14, 0.3, 1.4, 0.4)
        scorch(S, "scT", 1.9, -1.6, zr + 0.06, 2.6, 1.2, 0.04, plane="T")
        # gaping hole in the roof beside the rack + broken panel stubs on the right panel
        S.bb("hole", 1.3, 2.5, -2.2, -1.3, zr, zr + 0.06, "dark", 0.0)
        S.box("shard1", (1.2, 0.07, 0.5), (2.9, yw - 0.14, 1.0), "house", 0.0, rot=(0, 0, 14))
        S.box("shard2", (0.5, 0.07, 0.4), (3.7, yw - 0.14, 2.1), "house", 0.0, rot=(0, 0, -25))
        S.bb("pgap", 2.1, 3.7, yw - 0.07, yw - 0.03, 1.5, 2.7, "char", 0.0)
        # gash in the drum
        S.box("gash", (0.9, 0.1, 0.5), (dx, dy, zb + 1.2), "char", 0.0, wo=(-0.3, -DRUM_R * 0.98), stretch=False, rot=(0, 0, 0))
        S.bb("rubble1", -0.9, -0.3, -4.6, -4.1, 0.14, 0.4, "concrete2", 0.04)
        S.bb("rubble2", 3.0, 3.5, -4.5, -4.0, 0.14, 0.32, "rust", 0.04)
        # flames at the roof hole (flicker), smoke columns from the hole and from the stack stump
        for k in range(3):
            ph = (i + k * 0.7) % 4 / 4
            h = 0.55 + 0.4 * math.sin(math.pi * 2 * ph + k)
            S.cone(f"flame{k}", 0.3 - 0.04 * k, 0.04, h, (1.55 + 0.4 * k, -1.75 + 0.2 * (k % 2), zr + 0.05 + h / 2), "fire")
        for src, (px, py, pz) in enumerate(((1.9, -1.75, zr + 0.5), (sx_, sy_, stack_top + 0.25))):
            for k in range(3):
                ph = ((i / 4) + k / 3 + src * 0.25) % 1
                r = 0.08 + 0.38 * math.sin(math.pi * ph) ** 0.8
                sway = 0.25 * math.sin(2 * math.pi * (ph + k / 3))
                S.sphere(f"smk{src}{k}", r, (px + ph * 0.9 + sway, py + ph * 0.2, pz + ph * 1.5 + r * 0.3), "smoke2", squash=0.85)
    else:
        if not building or i == 7:
            for k in range(2):
                ph = ((i_anim / 4) + k / 2) % 1
                r = 0.06 + 0.34 * math.sin(math.pi * ph) ** 0.7
                S.sphere(f"puff{k}", r, (sx_ + ph * 0.5, sy_, stack_top + 0.35 + ph * 1.4 + r * 0.3), "smoke", squash=0.85)

    # ------------------------------------------------------------------ build state: scaffold poles at the corners + crane lamp
    if building and i < 7:
        zc = Z_BUILD[i]
        for sx in (-1, 1):
            for yy in (-2.9, 2.5):
                S.bb("scaf", sx * 4.55 - 0.05, sx * 4.55 + 0.05, yy - 0.05, yy + 0.05, 0.14, min(zc + 0.8, 5.0), "steel", 0.0, noclip=True)
        S.bb("scrailS", -4.6, 4.6, -2.95, -2.85, min(zc + 0.7, 4.9), min(zc + 0.78, 4.98), "yellow", 0.0, noclip=True)
