#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Grouped rows for the "AI-assisted artwork" table in docs/CREDITS.md (D-032, D-033; audit REPO-04).

One row per art directory (or per named file). The PNG counts are read from the tree, so they follow the files.

    python3 tools/credits/ai_art_rows.py          # print the rows to paste under the table
    python3 tools/credits/ai_art_rows.py --check  # exit 1 if docs/CREDITS.md lacks a current row

Stdlib only. Run from anywhere; paths are resolved from the repository root.
"""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CREDITS = os.path.join(ROOT, "docs", "CREDITS.md")

# (label shown in the Asset cell, paths counted under ROOT, tool, human work, notes)
GROUPS = [
    ("data/sprites/vehicles/", ["data/sprites/vehicles"],
     "Blender (bpy, Cycles) scripts written in code with Claude: tools/art/build_vehicle.py and tools/art/vehicles/*.py; move-state pass tools/art/mot_pass.py",
     "Owner approved the session-43 art pilots and fan-out (D-315)",
     "Original, built from primitives in code; no downloads or external models. Session 43 (D-315) re-rendered the 24 tracked and wheeled vehicles with a 4-frame move state"),
    ("data/sprites/buildings/", ["data/sprites/buildings"],
     "Blender (bpy, Cycles) scripts written in code with Claude: tools/art/build_structure.py and tools/art/structures/*.py; detail and idle pass tools/art/bld_pass.py",
     "Owner approved the auth_power pilot look and the fan-out (D-315)",
     "Original, built from primitives in code; no downloads or external models. Session 43 (D-315) re-rendered every building with the detail pass and its own idle motion"),
    ("data/sprites/infantry/", ["data/sprites/infantry"],
     "Blender (bpy, Cycles) rig and scripts written in code with Claude: tools/art/build_infantry.py and tools/art/infantry/rig.py; pass tools/art/inf_pass.py",
     "Owner approved infantry variant C and the pilot boost (D-315)",
     "Original, built from primitives in code; no downloads or external models. Session 43 (D-315) re-rendered the infantry; the Expedition and Left Behind infantry fan-out was not finished (docs/HANDOFF.md), so those sheets are the earlier ones"),
    ("data/portraits/", ["data/portraits"],
     "Blender (bpy, Cycles) scene scripts written in code with Claude: tools/booklet/render_portraits.py, run by tools/art/render_s24_portraits.ps1 on the owner's laptop (D-243)",
     "Owner approved the pilot portraits and the fan-out (D-245)",
     "Procedural, built in code; no external models named in the scripts. 96 x 96 RGBA, transparent background"),
    ("data/ui/skins/", ["data/ui/skins"],
     "Blender (bpy) skin scripts written in code with Claude: tools/art/ui/auth_skin.py, dir_skin.py, exp_skin.py and lb_skin.py, rendered on the owner's laptop (D-243)",
     "Owner approved the Authority pilot and the three other faction skins (D-245, D-252)",
     "Procedural nine-slice UI skins, one folder per faction with a skin.toml; no external textures named in the scripts"),
    ("data/sprites/fx/smoke_plume.png, data/sprites/fx/sparks.png", ["data/sprites/fx/smoke_plume.png", "data/sprites/fx/sparks.png"],
     "Procedural pixel-art effects, Pillow and numpy only, no Blender: tools/art/fx/build_fx.py (D-069); smoke re-rendered as volumetric smoke in session 43 (D-316)",
     "Owner approved the effects (D-069, D-316)",
     "Original, generated in code; no external images. The session-43 smoke render (D-316) is the one recorded in the decision log"),
    ("art/booklet/ (repository only, not in the game build)", ["art/booklet"],
     "Blender (bpy, Cycles) hero renders: tools/booklet/render_heroes.py, run by tools/booklet/render_heroes.ps1; contact sheets from the same tool",
     "Owner approved the Field Manual hero renders (D-210)",
     "Original, procedural scenes in code; the Field Manual images are not copied into the game build"),
    ("art/pilot/ (repository only, not in the game build)", ["art/pilot"],
     "Blender (bpy, Cycles) pilot scripts written in code with Claude: tools/art/build_tank.py, render_a.py, pixelize.py and hand_pixel.py (art pilot, M0-12)",
     "Owner approved the sprite style from the art pilot (D-039)",
     "Original, built from primitives in code (art/pilot/README.md); the pilot arms A, B and C and their contact sheets are not in the game build"),
]


def count_pngs(paths):
    """Number of PNG files under the given paths (a path may be a folder or one file)."""
    total = 0
    for rel in paths:
        full = os.path.join(ROOT, rel)
        if os.path.isfile(full):
            total += 1
            continue
        for dirpath, _dirs, files in os.walk(full):
            total += sum(1 for f in files if f.lower().endswith(".png"))
    return total


def rows():
    """The Markdown rows, in the four-column layout of the AI-assisted artwork table."""
    out = []
    for label, paths, tool, human, notes in GROUPS:
        missing = [p for p in paths if not os.path.exists(os.path.join(ROOT, p))]
        if missing:
            raise SystemExit("missing path(s): " + ", ".join(missing))
        n = count_pngs(paths)
        out.append(f"| {label} ({n} PNG files) | {tool} | {human} | {notes} |")
    return out


def main(argv):
    current = rows()
    if "--check" not in argv:
        print("\n".join(current))
        return 0
    with open(CREDITS, encoding="utf-8") as fh:
        text = fh.read()
    stale = [r for r in current if r not in text]
    for r in stale:
        print("not in docs/CREDITS.md (count or text changed):", r[:100])
    return 1 if stale else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
