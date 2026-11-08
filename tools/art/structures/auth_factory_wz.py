"""Candidate Authority Vehicle Factory built from a Warzone 2100 model, for tools/art/build_structure.py.  Contract: see auth_refinery.py.

NOT a replacement of auth_factory (that sprite stays untouched); this is the art-wz pilot.  Model: structs/blfact2.pie (factory with both
modules) with texture page-34-buildings.png (re-graded to grey-green concrete) and its tcmask page-34_tcmask.png (masked texels become the
magenta house ramp).  GPL-2.0-or-later, see docs/CREDITS.md.  Only the idle state is built (one frame); the old factory's damaged/build/active
states are not part of the pilot.
"""
import os
import sys

_here = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(_here, "..", "wz"))
import importlib.util

_spec = importlib.util.spec_from_file_location("structures_auth_refinery_ref", os.path.join(_here, "auth_refinery.py"))
_ref = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_ref)

_spec2 = importlib.util.spec_from_file_location("structures_auth_factory_ref", os.path.join(_here, "auth_factory.py"))
_fact = importlib.util.module_from_spec(_spec2)
_spec2.loader.exec_module(_fact)

ID = "auth_factory_wz"
TITLE = "Authority Vehicle Factory (Warzone model, candidate)"
FOOTPRINT = (3, 3)
CANVAS = (192, 224)
ORIGIN = (0, 32)
DOCK = (1, 3)
CAP_MATERIAL = "concrete2"
STATES = [("idle", 1, 5)]
FX = []
MATERIALS = dict(_fact.MATERIALS)

STRUCTS = os.environ.get("WZ_STRUCTS", "/home/claude/ref/wz2100/data/base/structs/")
SC = float(os.environ.get("WZ_FACT_SC", "0.026"))   # world units per Warzone unit (342 wide -> 8.9 of the 10.2 footprint)
ZS = 0.82                                           # height squash so the 150-high model fits the 224 px frame (2 cells of headroom budget)
ROT = float(os.environ.get("WZ_FACT_ROT", "180"))


def build(S, state, i, n):
    import pie_import as P
    import regrade as R
    _fact.apron(S, False, 0)          # same pad, test-track lane and kerb as the current factory, so the comparison isolates the building
    pie = P.parse_pie(os.path.join(STRUCTS, "blfact2.pie"))
    page = pie["texture"]
    m, mm = P.image_material("wz_fact", R.graded(page, "concrete"), R.mask_path(page), rough=0.85, mask_threshold=0.08)
    ob = P.build_object("wzfact", pie, SC, offset=(0, 0, 0.14), rot_z_deg=ROT, ystretch=S.YS, zstretch=ZS)
    S.mesh("wzfact", ob, m, mask=mm)
