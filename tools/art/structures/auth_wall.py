"""Authority Wall: reinforced concrete wall pieces (1x1 cell) with a steel cap rail, house band and dragon-tooth row.
Original design, no franchise references. Contract: see auth_refinery.py.

M2-20 (D-072): one sheet with the wall as STATES chosen by the neighbours (the game picks from a 4-neighbour mask):
  h (east-west run), v (north-south run), end_n/e/s/w (one neighbour, in that direction), corner_ne/nw/se/sw (two
  neighbours), t_n/e/s/w (three neighbours: the arm in the name is the stem, the one missing side is opposite),
  cross (four) and post (none); each with a `_damaged` twin.  `idle`/`damaged` stay (= h) for icons and fallbacks;
  `build` stays generic.  Naming rule: the letters name the DIRECTIONS THE WALL CONTINUES.

Tiling: arms, cap rails, bands and trims run to the cell edge (+-1.7) with no bevel and no outline at the canvas edge,
so rows and columns read as one continuous wall. Dragon teeth keep a 0.85 pitch (centres at +-0.425, +-1.275) on the
south side of east-west arms and the east side of north-south runs. Pieces that are not straight get a corner pillar.
The canvas is exactly 64 px wide: the shadow (falls right/down) is cropped at the cell edge.
"""
import importlib.util
import math
import os

_here = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

ID = "auth_wall"
TITLE = "Authority Wall"
FOOTPRINT = (1, 1)
CANVAS = (64, 100)
ORIGIN = (0, 32)        # footprint top-left corner inside the frame (32 px headroom above the cell, 4 px below it)
DOCK = (0, 1)           # unused (no dock); required by the contract
CAP_MATERIAL = "concrete2"
HOUSE2_MID = True       # the small house marks on the cap ridge face up: pin them to the mid ramp shades

# variant name -> arms (neighbour directions the wall continues in)
VARIANTS = {
    "h": "EW", "v": "NS",
    "end_n": "N", "end_e": "E", "end_s": "S", "end_w": "W",
    "corner_ne": "NE", "corner_nw": "NW", "corner_se": "SE", "corner_sw": "SW",
    "t_n": "NEW", "t_e": "NES", "t_s": "ESW", "t_w": "NSW",
    "cross": "NESW", "post": "",
}
STATES = [("idle", 1, 1), ("damaged", 1, 1)]
for _v in VARIANTS:
    STATES.append((_v, 1, 1))
    STATES.append((_v + "_damaged", 1, 1))
STATES.append(("build", 4, 5))
# Effect anchors (D-069), pixels in frame space. The wall has none: it is damaged by chipping and cracking only.
FX = []

MATERIALS = dict(_ref.MATERIALS)
MATERIALS.update({
    "steel2": ("#979EA3", dict(rough=0.4, metal=0.5)),
    # S22 detail pass: a second concrete grey for alternate panels, ground-in grime, chipped-paint bare concrete
    "panelA": ("#9DA196", dict(rough=0.95)),
    "panelB": ("#7C8077", dict(rough=0.95)),
    "grime": ("#4B4D43", dict(rough=1.0)),
    "bolt": ("#B4BABD", dict(rough=0.4, metal=0.6)),
})
SOOT_SKIN = dict(_ref.SOOT_SKIN)
SOOT_SKIN.update({"panelA": "concrete_s", "panelB": "concrete2_s", "grime": "soot", "bolt": "steel_s"})
PRIMER_SKIN = dict(_ref.PRIMER_SKIN)
PRIMER_SKIN.update({"panelA": "raw", "panelB": "raw", "grime": "raw", "bolt": "primer"})

HX = 1.7                 # half cell
WT = 0.5                 # half thickness of the wall
WZ = 2.0                 # top of the block
PW = 0.62                # half width of a corner pillar
PZ = 0.25                # pillar rises this much above the block top
beam, post = _ref.beam, _ref.post


def T(d, u, v):
    """Arm coordinates -> ground (x, y): u runs from the hub (0) out to the cell edge (HX) along arm d, v across it."""
    return {"E": (u, v), "W": (-u, v), "N": (v, u), "S": (v, -u)}[d]


def ab(S, name, d, u0, u1, v0, v1, z0, z1, mat, bevel=0.0, **kw):
    """Box in arm coordinates."""
    x0, y0 = T(d, u0, v0)
    x1, y1 = T(d, u1, v1)
    return S.bb(name, min(x0, x1), max(x0, x1), min(y0, y1), max(y0, y1), z0, z1, mat, bevel, **kw)


def is_pillar(arms):
    return not (arms in ("EW", "NS"))


def teeth(S, arms, snapped=()):
    """Dragon teeth on the pitch grid: south of east-west arms, east of north-south runs."""
    spots = []
    for k in range(4):
        c = -1.275 + 0.85 * k
        if ("E" in arms and c > 0) or ("W" in arms and c < 0):
            if not ("S" in arms and abs(c) < 0.9):
                spots.append((k, c, -1.25))
    if "E" not in arms and "W" not in arms:
        for k in range(4):
            c = -1.275 + 0.85 * k
            if ("N" in arms and c > 0) or ("S" in arms and c < 0):
                spots.append((k, 1.25, c))
    for k, x, y in spots:
        if k in snapped:
            S.cone(f"tooth{k}", 0.4, 0.3, 0.3, (x, y, 0.15), "concrete2", verts=4, rot=(0, 0, 45), nocast=False)
            S.cone(f"toothf{k}", 0.34, 0.1, 0.55, (x + 0.2, y - 0.2, 0.17), "concrete2", verts=4, rot=(0, 78, 25), nocast=False)
            continue
        S.cone(f"tooth{k}", 0.46, 0.06, 0.78, (x, y, 0.39), "concrete2", verts=4, rot=(0, 0, 45), nocast=False)


def detail(S, a, b, face, broken, pillar):
    """S22 detail pass on one south-facing span (x from a to b, face plane y = `face`): alternate grey panels, dark seams,
    bolt plates, a grime band at the foot, rust drips, chips in the house band and a warning sign.  All of it is flat
    cladding (no silhouette change) laid out on the 0.85 panel grid so neighbouring cells line up."""
    def runs(x0, x1):
        x0, x1 = max(x0, a), min(x1, b)
        if x1 - x0 < 0.02:
            return []
        if broken:
            return [(p0, p1) for (p0, p1) in ((x0, min(x1, broken[0])), (max(x0, broken[1]), x1)) if p1 - p0 > 0.02]
        return [(x0, x1)]

    def at(x, w=0.0):
        return a + 0.02 <= x - w and x + w <= b - 0.02 and not (broken and broken[0] - w < x < broken[1] + w)

    f = face
    # alternate panel greys, lower and upper strip
    for k in range(4):
        x0 = -1.7 + 0.85 * k
        m = "panelA" if k % 2 == 0 else "panelB"
        for (p0, p1) in runs(x0, x0 + 0.85):
            S.bb("pan", p0, p1, f - 0.012, f, 0.45, 0.88, m, 0.0, nocast=True)
            S.bb("pan", p0, p1, f - 0.012, f, 1.64, WZ - 0.02, m, 0.0, nocast=True)
    # seams: dark vertical joints, with a bolt plate on the band at each
    for gx in (-0.85, 0.0, 0.85):
        if at(gx, 0.04):
            S.bb("seam", gx - 0.03, gx + 0.03, f - 0.035, f, 0.45, 0.88, "dark", 0.0, nocast=True)
            S.bb("seam", gx - 0.03, gx + 0.03, f - 0.035, f, 1.64, WZ - 0.02, "dark", 0.0, nocast=True)
        if at(gx, 0.07):
            for z in (1.04, 1.44):
                S.bb("plate", gx - 0.07, gx + 0.07, face - 0.075, face - 0.05, z, z + 0.08, "steel2", 0.0, nocast=True)
    # bolt row along the cap lip (pitch 0.425, offset half a pitch so it repeats across cells)
    for k in range(8):
        x = -1.4875 + 0.425 * k
        if at(x, 0.05):
            S.bb("bolt", x - 0.05, x + 0.05, -WT - 0.17, -WT - 0.14, WZ + 0.05, WZ + 0.12, "bolt", 0.0, nocast=True)
    # joints across the cap top
    for gx in (-0.85, 0.0, 0.85):
        if at(gx, 0.03):
            S.bb("capj", gx - 0.025, gx + 0.025, -WT - 0.14, -0.14, WZ + 0.16, WZ + 0.175, "dark", 0.0, nocast=True)
    # ground-in grime at the foot of the block and on the plinth front (ragged top edge)
    for k, h in enumerate((0.17, 0.26, 0.12, 0.22, 0.14, 0.28, 0.16, 0.2, 0.13, 0.24, 0.18, 0.1)):
        x0 = -1.7 + k * 0.2833
        for (p0, p1) in runs(x0, x0 + 0.2833):
            S.bb("grime", p0, p1, f - 0.02, f, 0.42, 0.42 + h, "grime", 0.0, nocast=True)
            S.bb("grime", p0, p1, -WT - 0.23, -WT - 0.2, 0.0, 0.1 + h * 0.2, "grime", 0.0, nocast=True)
    # rust drips from the cap and below the band
    for (x, z1, z0) in ((-1.25, WZ - 0.02, 1.72), (-0.42, WZ - 0.02, 1.86), (0.55, WZ - 0.02, 1.68), (1.3, WZ - 0.02, 1.8),
                        (-1.45, 0.88, 0.62), (-0.3, 0.88, 0.7), (1.05, 0.88, 0.58)):
        if at(x, 0.05):
            S.bb("rust", x - 0.05, x + 0.05, f - 0.04, f, z0, z1, "rust", 0.0, nocast=True)
    # chips in the house band: bare concrete showing through the paint
    for (x, w, h, z) in ((-1.3, 0.16, 0.06, 1.49), (0.4, 0.2, 0.06, 1.49), (1.1, 0.12, 0.06, 0.99)):
        if at(x, w / 2) and not pillar:
            S.bb("chip", x - w / 2, x + w / 2, face - 0.072, face - 0.045, z, z + h, "concrete", 0.0, nocast=True)
    # warning sign on the lower strip (once per run)
    if not pillar and a <= -0.6 and b >= -0.2:
        sx = -0.425
        S.bb("sign", sx - 0.17, sx + 0.17, f - 0.05, f, 0.52, 0.83, "yellow", 0.0, nocast=True)
        S.bb("signbar", sx - 0.03, sx + 0.03, f - 0.07, f - 0.05, 0.62, 0.76, "dark", 0.0, nocast=True)
        S.bb("signdot", sx - 0.03, sx + 0.03, f - 0.07, f - 0.05, 0.56, 0.6, "dark", 0.0, nocast=True)


def damaged_arm(arms):
    for d in "EWNS":
        if d in arms:
            return d
    return None


def steps(dmg_arm, d):
    """Block columns (u0, u1, top) for one arm: only the damaged arm has the stepped gap."""
    if d != dmg_arm:
        return [(0.0, HX, WZ)]
    return [(0.0, 0.5, WZ), (0.5, 0.85, 1.45), (0.85, 1.3, 1.05), (1.3, 1.5, 1.7), (1.5, HX, WZ)]


def finished(S, arms, dmg=False):
    pillar = is_pillar(arms)
    dd = damaged_arm(arms) if dmg else None
    ys = -WT - 0.02
    # ---- plinth, blocks, cap rail per arm
    pl = PW + 0.2
    if pillar:
        S.bb("plinth", -pl, pl - 0.02, -pl, pl - 0.02, 0.0, 0.42, "concrete2", 0.0)
    else:
        S.bb("plinth", -WT - 0.2, WT + 0.15, -WT - 0.2, WT + 0.15, 0.0, 0.42, "concrete2", 0.0)
    for d in arms:
        ab(S, f"plinth{d}", d, 0.0, HX, -WT - 0.2, WT + 0.15, 0.0, 0.42, "concrete2")
        for k, (u0, u1, top) in enumerate(steps(dd, d)):
            ab(S, f"block{d}{k}", d, u0, u1, -WT, WT, 0.0, top, "concrete")
        capu = [(0.0, 0.5), (1.5, HX)] if d == dd else [(0.0, HX)]
        for k, (u0, u1) in enumerate(capu):
            ab(S, f"cap{d}{k}", d, u0, u1, -WT - 0.14, WT + 0.14, WZ, WZ + 0.16, "steel")
            ab(S, f"rail{d}{k}", d, u0, u1, -0.14, 0.14, WZ + 0.16, WZ + 0.3, "steel2")
            ab(S, f"capl{d}{k}", d, u0, u1, -WT - 0.15, -WT - 0.13, WZ + 0.02, WZ + 0.14, "dark", nocast=True)
        if d in "NS" and d != dd:      # small house mark on the ridge of runs whose south face cannot be seen
            ab(S, f"mark{d}", d, 0.65, 1.05, -0.1, 0.1, WZ + 0.3, WZ + 0.34, "house2", nocast=True)
    # hub top for pieces without a pillar is covered by the arms; the pillar itself:
    if pillar:
        if dmg and not arms:     # isolated post, broken off
            S.bb("pillar", -PW, PW, -PW, PW, 0.0, 1.55, "concrete", 0.0)
            S.bb("pbite", -PW, 0.1, -PW - 0.01, -PW + 0.04, 1.1, 1.55, "dark", 0.0, nocast=True)
            S.box("pcapfall", (0.9, 0.8, 0.16), (0.55, -0.95, 0.12), "steel", 0.0, rot=(0, 0, 25), stretch=False, nocast=False)
            beam(S, "prb0", (0.0, 0.0, 1.55), (-0.2, -0.05, 2.35), 0.05, "rebar")
            beam(S, "prb1", (0.3, 0.1, 1.55), (0.5, 0.15, 2.2), 0.05, "rebar")
        else:
            S.bb("pillar", -PW, PW, -PW, PW, 0.0, WZ + PZ, "concrete", 0.0)
            S.bb("pcap", -PW - 0.06, PW + 0.06, -PW - 0.06, PW + 0.06, WZ + PZ, WZ + PZ + 0.14, "steel", 0.0)
            S.bb("pcap2", -0.2, 0.2, -0.2, 0.2, WZ + PZ + 0.14, WZ + PZ + 0.26, "steel2", 0.0)
            S.bb("plamp", -0.15, 0.15, -0.215, -0.2, WZ + PZ + 0.16, WZ + PZ + 0.24, "lamp", 0.0, nocast=True)
    # ---- south-facing decoration: house band, trims, panel joints
    inner = PW if pillar else 0.0
    spans = []
    if "E" in arms:
        spans.append((inner, HX))
    if "W" in arms:
        spans.append((-HX, -inner))
    if pillar and "S" not in arms:
        spans.append((-PW, PW))
    broken = None            # u-range of the break on a damaged east/west arm, in x
    if dd == "E":
        broken = (0.5, 1.5)
    elif dd == "W":
        broken = (-1.5, -0.5)
    for (a, b) in spans:
        face = -PW - 0.02 if (pillar and (a, b) == (-PW, PW)) else ys
        S.bb("trimA", a, b, face - 0.03, face, 0.88, 0.97, "dark", 0.0, nocast=True)
        S.bb("trimB", a, b, face - 0.03, face, 1.55, 1.64, "dark", 0.0, nocast=True)
        parts = [(a, b)]
        if broken:       # the band stops at the break
            parts = [(p0, p1) for (p0, p1) in ((a, min(b, broken[0])), (max(a, broken[1]), b)) if p1 - p0 > 0.01]
        for (p0, p1) in parts:
            S.bb("band", p0, p1, face - 0.05, face, 0.97, 1.55, "house", 0.0, nocast=True)
        detail(S, a, b, face, broken, face != ys)
    # hazard chevrons at the foot of exposed plinth fronts
    fy = -(pl if pillar else WT + 0.2) - 0.03
    for k in range(8):
        x0, x1 = -HX + 0.425 * k, -HX + 0.425 * (k + 1)
        mid = (x0 + x1) / 2
        front = ("E" in arms and mid > 0) or ("W" in arms and mid < 0)
        if "S" in arms and abs(mid) < 0.85:
            front = False
        if front:
            yy = -WT - 0.2 - 0.03
            S.bb(f"haz{k}", x0, x1, yy, yy + 0.03, 0.28, 0.42, "yellow" if k % 2 == 0 else "dark", 0.0, nocast=True)
    if pillar and "S" not in arms:
        for k in range(4):
            x0 = -0.8 + 0.4 * k
            S.bb(f"hazp{k}", x0, x0 + 0.4, fy, fy + 0.03, 0.28, 0.42, "yellow" if k % 2 == 0 else "dark", 0.0, nocast=True)
    if not dmg:
        teeth(S, arms)
    else:
        teeth(S, arms, snapped=(2,))
        if dd is not None:
            damage(S, dd)


def damage(S, d):
    """Chipped block on arm d: dark bite, cracks (south face only), bent rebar, rubble beside the break."""
    if d in "EW":
        ab(S, "bite", d, 0.5, 0.85, -WT - 0.01, -WT + 0.04, 1.2, 1.45, "dark", nocast=True)
        ys = -WT - 0.05
        for k, (a, b) in enumerate((((0.3, 0.5), (0.55, 1.25)), ((0.55, 1.25), (0.3, 1.75)), ((1.55, 0.4), (1.4, 1.4)), ((0.1, 0.45), (0.45, 0.9)))):
            p0 = T(d, a[0], 0)
            p1 = T(d, b[0], 0)
            beam(S, f"crack{k}", (p0[0], ys, a[1]), (p1[0], ys, b[1]), 0.05, "dark", nocast=True)
        sc = (0.55, 1.45)
        S.bb("scorch", min(T(d, sc[0], 0)[0], T(d, sc[1], 0)[0]), max(T(d, sc[0], 0)[0], T(d, sc[1], 0)[0]), -WT - 0.06, -WT - 0.03, 0.3, 0.9, "char", 0.0, nocast=True)
    for k, ((u0, v0, z0), (u1, v1, z1)) in enumerate((((0.55, 0.0, 1.45), (0.3, -0.1, 2.35)), ((0.8, 0.05, 1.45), (1.1, 0.2, 2.2)),
                                                       ((1.15, 0.0, 1.05), (1.45, -0.05, 1.9)), ((1.3, 0.1, 1.05), (1.55, 0.2, 1.8)))):
        a, b = T(d, u0, v0), T(d, u1, v1)
        beam(S, f"rbar{k}", (a[0], a[1], z0), (b[0], b[1], z1), 0.05, "rebar")
    fx, fy = T(d, 1.1, -0.5)
    along_x = d in "EW"
    S.box("capfall", (1.0, 0.9, 0.14) if along_x else (0.9, 1.0, 0.14), (fx, fy, 1.3), "steel", 0.0, rot=(0, 28, 10) if along_x else (28, 0, 10),
          stretch=False, nocast=False)
    S.box("railfall", (1.0, 0.28, 0.14) if along_x else (0.28, 1.0, 0.14), (fx - 0.08, fy, 1.45), "steel2", 0.0,
          rot=(0, 28, 10) if along_x else (28, 0, 10), stretch=False, nocast=False)
    for k, (u, v, w, h) in enumerate(((0.5, -0.85, 0.34, 0.26), (0.95, -0.8, 0.4, 0.34), (1.3, -0.9, 0.3, 0.22), (0.1, -0.85, 0.26, 0.18),
                                      (1.4, -1.35, 0.3, 0.2))):
        x, y = T(d, u, v)
        S.box(f"rub{k}", (w, w * 0.8, h), (x, y, h / 2), "concrete_s", 0.0, rot=(0, 0, 33 * k))


def formwork(S, z1, both=True):
    """Timber shuttering boards on the south (and north) faces with walers and diagonal props."""
    for sy in ((-1, 1) if both else (-1,)):
        y = sy * (WT + 0.1)
        S.bb("fb", -HX, HX, y - 0.04, y + 0.04, 0.1, z1, "timber", 0.0)
        for zz in (0.5, 0.95, 1.4):
            if zz < z1:
                S.bb("waler", -HX, HX, (y - 0.12 if sy < 0 else y + 0.04), (y - 0.04 if sy < 0 else y + 0.12), zz, zz + 0.08, "girder", 0.0)
    for k in range(4):
        x = -1.275 + 0.85 * k
        beam(S, "prop", (x, -1.5, 0.12), (x, -WT - 0.12, min(z1, 1.5)), 0.07, "timber")


def build_site(S, i):
    if i == 0:     # trench dug, spoil heap, stakes and string lines
        S.bb("pit", -HX, HX, -0.75, 0.75, 0.0, 0.05, "soil", 0.0)
        S.bb("pit2", -HX, HX, -0.75, -0.45, 0.0, 0.1, "soil2", 0.0)
        S.bb("pit2", -HX, HX, 0.45, 0.75, 0.0, 0.1, "soil2", 0.0)
        S.bb("mound", -HX, 0.4, 0.85, 1.4, 0.0, 0.5, "soil", 0.18)
        for sx in (-1.6, 1.6):
            for sy in (-1.1, 1.1):
                post(S, "stake", sx, sy, 0.0, 0.8, 0.1, "timber")
                post(S, "staket", sx, sy, 0.62, 0.8, 0.12, "string")
        S.bb("line", -1.6, 1.6, -1.12, -1.08, 0.7, 0.72, "string", 0.0, nocast=True)
        S.bb("line", -1.6, 1.6, 1.08, 1.12, 0.7, 0.72, "string", 0.0, nocast=True)
        S.bb("sign", 0.2, 1.0, -1.45, -1.4, 0.5, 1.1, "yellow", 0.0)
        post(S, "signp", 0.35, -1.42, 0.0, 0.5, 0.07, "timber")
        post(S, "signp", 0.85, -1.42, 0.0, 0.5, 0.07, "timber")
    elif i == 1:   # footing poured, rebar cage standing, first shuttering
        S.bb("footing", -HX, HX, -0.85, 0.85, 0.0, 0.3, "raw", 0.0)
        for k in range(8):
            x = -1.55 + k * 0.44
            for sy in (-0.28, 0.28):
                post(S, "rb", x, sy, 0.3, 1.65, 0.05, "rebar")
        for zz in (0.6, 1.0, 1.4):
            for sy in (-0.28, 0.28):
                S.bb("rbh", -1.58, 1.58, sy - 0.025, sy + 0.025, zz, zz + 0.05, "rebar", 0.0, nocast=True)
        formwork(S, 0.6, both=False)
        S.bb("planks", -1.2, 0.2, 1.0, 1.35, 0.0, 0.3, "timber", 0.0)
        S.bb("crate", 0.9, 1.5, 0.95, 1.5, 0.0, 0.55, "crate", 0.03)
    elif i == 2:   # wall poured in raw concrete, shuttering still on the south face, caps and teeth waiting on the ground
        S.bb("plinth", -HX, HX, -WT - 0.2, WT + 0.15, 0.0, 0.42, "raw", 0.0)
        S.bb("block", -HX, HX, -WT, WT, 0.0, WZ - 0.1, "raw", 0.0)
        formwork(S, 1.1, both=False)
        for k in range(4):
            x = -1.275 + 0.85 * k
            post(S, "rb", x, 0.0, WZ - 0.1, WZ + 0.45, 0.05, "rebar")
        S.bb("capwait", -1.3, 1.0, 0.8, 1.2, 0.0, 0.16, "steel", 0.0)
        S.bb("capwait", -1.3, 1.0, 0.8, 1.2, 0.16, 0.3, "steel2", 0.0)
        for k in range(2):
            S.cone(f"toothw{k}", 0.46, 0.06, 0.78, (-1.0 + k * 0.9, -1.35, 0.39), "raw", verts=4, rot=(0, 0, 45), nocast=False)
        S.bb("crate", 1.1, 1.65, -1.55, -1.0, 0.0, 0.5, "crate", 0.03)


def build(S, state, i, n):
    building = state == "build"
    if building and i < 3:
        build_site(S, i)
        return
    dmg = state.endswith("damaged")           # also plain "damaged"
    base = state[:-len("_damaged")] if state.endswith("_damaged") else state
    arms = "EW" if (building or base in ("idle", "damaged")) else VARIANTS[base]
    if dmg:
        S.skin.update(SOOT_SKIN)
    finished(S, arms, dmg)
