"""Shared Warzone 2100 track helper (M10-06): swaps a vehicle module's own box/cylinder treads for the WZ track meshes.

Meshes (GPL-2.0-or-later, see README.md; none is a CC BY-SA file): components/prop/prltrk1.pie (light), prmtrk2.pie (medium), prhtrk3.pie
(heavy), each one model holding BOTH tracks (a left and a right run, split at x = 0), textured with page-16-droid-drives.png re-graded to
the rubber/steel "track" ramp (regrade.py).  Use from a vehicle module's build():

    sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "wz"))
    import wz_tracks as WT
    import pie_import as P
    WT.tracks(S, P, length=2.9, width=0.66, height=0.8, loc=(1.12, 0.0, 0.0), weight="medium")

Footprint, in vehicle units (x east, y front, z up):  `loc` = (x of each track's centre line, y of the track centre, z of the ground
contact); `width` is ONE track's width, `length` its length (idler to sprocket), `height` its height above loc z.  Each run is scaled
non-uniformly from the model's own bounds, so the proportions of the Warzone tread (sprocket, road wheels, idler) are kept.
Returns [left, right] objects.  Both are plain 'track' meshes without a house mask (the house paint stays on the module's skirts).
"""
import os

COMP = "/home/claude/ref/wz2100/data/base/components/"
WEIGHTS = {"light": "prop/prltrk1.pie", "medium": "prop/prmtrk2.pie", "heavy": "prop/prhtrk3.pie"}


def tracks(S, P, length, width, height, loc, weight="medium", name="wztrk"):
    """Add a left and right WZ track to the scene `S` (build_vehicle's scene namespace); `P` is the pie_import module."""
    import regrade as R
    comp = os.environ.get("WZ_COMPONENTS", COMP)
    pie = P.parse_pie(os.path.join(comp, WEIGHTS[weight]))
    lv = pie["levels"][0]
    xs = [abs(p[0]) for p in lv["points"]]
    ys = [-p[2] for p in lv["points"]]
    zs = [p[1] for p in lv["points"]]
    x0, x1, y0, y1, z0, z1 = min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)
    m, _ = P.image_material(f"{name}_mat", R.graded(pie["texture"], "track"), None, rough=0.9)
    cx, cy, cz = loc
    out = []
    for s, side in ((-1, "L"), (1, "R")):
        keep = {i for i, t in enumerate(lv["polys"]) if all((lv["points"][j][0] * s) > 0 for j in t[:3])}
        ob = P.build_object(name + side, pie, 1.0, only=keep)
        for v in ob.data.vertices:
            u = (abs(v.co.x) - x0) / (x1 - x0)
            ty = (v.co.y - y0) / (y1 - y0)
            tz = (v.co.z - z0) / (z1 - z0)
            v.co.x = s * (cx + (u - 0.5) * width)
            v.co.y = cy + (ty - 0.5) * length
            v.co.z = cz + tz * height
        ob.data.update()
        out.append(S.mesh(name + side, ob, m))
    return out
