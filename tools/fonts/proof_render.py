#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""Draws sample UI text from the generated atlases using only the .font.toml metrics (no font engine).

Proves that the metrics place glyphs correctly: each glyph is cut from the atlas at (x, y, w, h) and pasted at the pen
position plus (xoffset, yoffset), and the pen advances by advance plus kerning, exactly as BitmapFont.DrawAtlas does.
Writes scratch/font_proof.png in the repository root (git-ignored).
"""
from __future__ import annotations

import sys
import tomllib
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
FONTS = ROOT / "data" / "fonts"
OUT = ROOT / "scratch" / "font_proof.png"
SAMPLE = "RTS_2027 — Authority Refinery 1,250 ¢ äöü"
LINES = (
    ("noto-sans-regular", 24, (232, 216, 138)),
    ("noto-sans-bold", 16, (232, 216, 138)),
    ("noto-sans-regular", 12, (240, 240, 240)),
)


def load(face: str, size: int):
    """Returns (metrics dict, atlas image) for one face and size."""
    with open(FONTS / f"{face}_{size}.font.toml", "rb") as fh:
        meta = tomllib.load(fh)
    atlas = Image.open(FONTS / meta["texture"]).convert("RGBA")
    glyphs = {g["codepoint"]: g for g in meta["glyph"]}
    kerning = {(k["left"], k["right"]): k["amount"] for k in meta.get("kerning", [])}
    return meta, atlas, glyphs, kerning


def draw_line(canvas: Image.Image, face: str, size: int, colour, x: int, y: int) -> int:
    """Draws SAMPLE at (x, y) and returns the pen width it used."""
    meta, atlas, glyphs, kerning = load(face, size)
    pen = x
    previous = None
    for ch in SAMPLE:
        cp = ord(ch) if ord(ch) in glyphs else ord("?")
        if previous is not None:
            pen += kerning.get((previous, cp), 0)
        g = glyphs[cp]
        if g["w"] and g["h"]:
            cut = atlas.crop((g["x"], g["y"], g["x"] + g["w"], g["y"] + g["h"]))
            tint = Image.new("RGBA", cut.size, colour + (0,))
            tint.putalpha(cut.getchannel("A"))
            canvas.alpha_composite(tint, (pen + g["xoffset"], y + g["yoffset"]))
        pen += g["advance"]
        previous = cp
    return pen - x


def main() -> int:
    """Renders the proof image and prints its path."""
    width, height = 1100, 150
    canvas = Image.new("RGBA", (width, height), (24, 26, 32, 255))
    y = 12
    for face, size, colour in LINES:
        meta = load(face, size)[0]
        draw_line(canvas, face, size, colour, 16, y)
        y += meta["line_height"] + 12
    OUT.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(OUT, format="PNG")
    print(OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
