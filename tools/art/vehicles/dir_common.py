"""Shared Directorate vehicle materials (PAL_DIR) for tools/art/build_vehicle.py modules.  Import with
sys.path.insert(0, <this dir>); `import dir_common as D`; then MATERIALS = D.MATERIALS, PALETTE = D.PALETTE.
Brutalist warm concrete, bone-white stencil panels, oxide red body paint; the glow is a sickly pale amber (the Directorate
palette has no green, so it quantises to brass / bone)."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import common as C

PALETTE = C.PAL_DIR
MATERIALS = {
    "hull": ("#8A7C68", dict(rough=0.8)), "hull2": ("#6E6150", dict(rough=0.8)), "concrete": ("#A39580", dict(rough=0.9)),
    "track": ("#2A2623", dict(rough=0.9)), "steel": ("#555B61", dict(rough=0.5, metal=0.5)), "dark": ("#1B1816", dict(rough=0.9)),
    "rust": ("#5E3420", dict(rough=0.95)), "bone": ("#E8DFC8", dict(rough=0.8)), "white": ("#E8DFC8", dict(rough=0.8)),
    "oxide": ("#A33426", dict(rough=0.8)), "oxide2": ("#5E2018", dict(rough=0.85)),
    "glow": ("#9C7A1C", dict(rough=0.4, emit="#9C7A1C")), "lens": ("#FFE79A", dict(rough=0.3, emit="#FFE08A")),
    "glass": ("#3A3F44", dict(rough=0.25, metal=0.3)), "brass": ("#C9A640", dict(rough=0.5, metal=0.4)),
    "umber": ("#6B523B", dict(rough=0.9)),
}
