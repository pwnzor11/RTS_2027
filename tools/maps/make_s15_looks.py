#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
"""S15 terrain/resource look variants (M11-07, M11-08). Writes data/theaters/variants/{a,b,c}/<theater>.toml, prints the WCAG
contrast table, and (D-181) writes the shipped data/theaters/<theater>.toml as a copy of variant C with a "shipped" header.
Layout: data/theaters/<theater>.toml = shipped (C); data/theaters/variants/classic/ = the pre-S15 hand-written look (the
source of the ashfall/pitchblende numbers, never rewritten); variants/a, b, c = generated. RTS_TERRAIN_LOOK=classic|a|b|c picks one.

  A "Re-tune"        palette only: water blue and lighter, lighter roads, ash a different hue, spread of luminance.
  B "Edges"          A + road edge lines, shoreline band, cliff lip/drop shadow, large-scale tone variation (16 texels per cell).
  C "Edges + fields" B + resource deposits (slag heaps / crystals with a rim and bed) + minimap resource colours.

Run from the repo root: python3 tools/maps/make_s15_looks.py [--table-only]
"""
import os, sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
THEATERS = os.path.join(REPO, "data", "theaters")
FILES = {"rust_belt": "rustbelt", "glass_coast": "glass_coast", "mountains": "mountains", "ash_plains": "ash_plains"}


def lum(c):
    def lin(v):
        x = v / 255.0
        return x / 12.92 if x <= 0.03928 else ((x + 0.055) / 1.055) ** 2.4
    return 0.2126 * lin(c[0]) + 0.7152 * lin(c[1]) + 0.0722 * lin(c[2])


def contrast(a, b):
    la, lb = lum(a), lum(b)
    return (max(la, lb) + 0.05) / (min(la, lb) + 0.05)


def hexc(c):
    return "#%02X%02X%02X" % tuple(c)


def parse_hex(s):
    return tuple(int(s[i:i + 2], 16) for i in (1, 3, 5))


def solve(direction, target):
    """Scale an RGB direction so its luminance is ~target (bisection on the scale, clamped to 0..255)."""
    lo, hi = 0.0, 4.0
    for _ in range(40):
        mid = (lo + hi) / 2
        c = tuple(min(255, round(v * mid)) for v in direction)
        if lum(c) < target:
            lo = mid
        else:
            hi = mid
    return tuple(min(255, round(v * hi)) for v in direction)


R = 1.54
CHAIN = [(0.062 * R ** i) - 0.05 for i in range(6)]   # luminance targets, each step >= 1.54 in WCAG contrast

# order of the six required types (darkest to lightest), hue directions, and extra types.
BIOMES = {
    "rust_belt": dict(
        order=["cliff", "ash", "water", "rough", "shallow", "road"],
        hue=dict(cliff=(30, 28, 26), ash=(84, 72, 98), water=(52, 90, 140), rough=(150, 98, 72), shallow=(90, 185, 200), road=(196, 188, 172),
                 clear=(128, 118, 102), rock=(96, 98, 106)),
        extra=dict(clear=0.23, rock=0.068),
        road_line=(52, 46, 40), foam=(205, 232, 242), cliff_top=(190, 180, 160),
        ash_f=((176, 92, 44), (236, 150, 72), (104, 50, 28), (30, 18, 14)),
        pit_f=((110, 210, 120), (176, 250, 170), (40, 120, 80), (14, 40, 30)),
        glint=(240, 255, 200), mini_ash=(255, 150, 40), mini_pit=(110, 240, 120)),
    "glass_coast": dict(
        order=["cliff", "water", "ash", "road", "shallow", "rough"],
        hue=dict(cliff=(36, 52, 50), water=(30, 76, 130), ash=(128, 96, 88), road=(130, 150, 150), rough=(190, 160, 110), shallow=(70, 170, 168),
                 clear=(214, 204, 176), rock=(78, 112, 104)),
        extra=dict(clear=0.70, rock=0.068),
        road_line=(224, 238, 232), foam=(240, 248, 246), cliff_top=(170, 215, 205),
        ash_f=((128, 60, 38), (190, 98, 60), (70, 32, 24), (40, 20, 16)),
        pit_f=((40, 150, 110), (110, 220, 170), (16, 80, 66), (10, 40, 34)),
        glint=(230, 255, 230), mini_ash=(255, 140, 50), mini_pit=(60, 235, 110)),
    "mountains": dict(
        order=["cliff", "rough", "water", "shallow", "road", "ash"],
        hue=dict(cliff=(40, 35, 32), rough=(96, 84, 74), water=(50, 90, 135), shallow=(96, 170, 196), road=(150, 142, 128), ash=(190, 196, 210),
                 clear=(112, 102, 88), rock=(92, 88, 82)),
        extra=dict(clear=0.24, rock=0.068),
        road_line=(236, 224, 190), foam=(210, 232, 244), cliff_top=(196, 190, 180),
        ash_f=((176, 92, 44), (236, 150, 72), (104, 50, 28), (30, 18, 14)),
        pit_f=((110, 210, 120), (176, 250, 170), (40, 120, 80), (14, 40, 30)),
        glint=(240, 255, 200), mini_ash=(255, 150, 40), mini_pit=(110, 240, 120)),
    "ash_plains": dict(
        order=["cliff", "rough", "water", "shallow", "ash", "road"],
        hue=dict(cliff=(26, 26, 30), rough=(88, 62, 48), water=(54, 92, 138), shallow=(80, 170, 170), ash=(150, 137, 140), road=(200, 196, 186),
                 clear=(120, 116, 76), rock=(84, 84, 92)),
        extra=dict(clear=0.2, rock=0.068),
        road_line=(52, 46, 48), foam=(205, 232, 242), cliff_top=(170, 160, 180),
        ash_f=((140, 56, 34), (232, 120, 58), (66, 28, 22), (28, 14, 14)),
        pit_f=((90, 214, 150), (170, 250, 200), (30, 120, 90), (10, 44, 34)),
        glint=(236, 255, 214), mini_ash=(255, 130, 30), mini_pit=(80, 240, 150)),
}


# M12-01/02 decor tables (look C only). Each decal: (name, shape, [land types], permille per cell, colour, alpha, size_min, size_max) with sizes in
# sixteenths of a cell. The rules that match one land type are tried in order and their permilles add up (keep each land type under ~100).
# Sprites: rock/rough/bank kinds and the share (permille) of a cluster's cells that carry one.
DECOR = {
    "rust_belt": dict(
        decals=[("pothole", "blob", ["road"], 22, (30, 28, 26), 190, 5, 9),
                ("road crack", "crack", ["road", "clear"], 24, (36, 32, 28), 210, 14, 24),
                ("oil spill", "soft", ["road"], 12, (22, 24, 44), 170, 12, 20),
                ("manhole", "ring", ["road"], 4, (70, 66, 60), 255, 6, 7),
                ("soot stain", "soft", ["clear", "rough", "road"], 18, (16, 14, 14), 120, 20, 32),
                ("snow patch", "soft", ["clear", "ash", "rough"], 14, (236, 240, 246), 150, 18, 30),
                ("small crater", "ring", ["clear", "ash"], 8, (52, 44, 38), 200, 18, 28),
                ("frost crack", "crack", ["water"], 30, (214, 234, 248), 170, 16, 28)],
        sprites=dict(rock=("boulder", 300), rough=("rubble", 220), bank=("iceslab", 120))),
    "glass_coast": dict(
        decals=[("sand ripple", "ripple", ["clear", "rough"], 30, (166, 144, 100), 200, 20, 32),
                ("glint patch", "soft", ["clear"], 10, (255, 250, 220), 100, 16, 26),
                ("shells and bone", "dots", ["clear", "rough", "coast"], 16, (140, 112, 84), 230, 20, 30),
                ("dune shadow", "soft", ["clear"], 14, (150, 128, 96), 120, 24, 40),
                ("salt crust", "soft", ["clear", "rough"], 12, (250, 250, 244), 120, 24, 36),
                ("glass bowl", "ring", ["clear"], 5, (60, 150, 140), 170, 18, 26),
                ("drowned stripe", "stripe", ["shallow"], 26, (232, 240, 232), 160, 12, 18)],
        sprites=dict(rock=("glasscrest", 300), rough=("rubble", 150))),
    "mountains": dict(
        decals=[("scree", "dots", ["rough", "clear"], 28, (82, 76, 68), 230, 22, 32),
                ("snow patch", "soft", ["clear", "rough"], 12, (240, 244, 250), 160, 20, 32),
                ("pebbles", "dots", ["clear"], 16, (174, 168, 156), 240, 16, 24),
                ("frost crack", "crack", ["clear"], 8, (190, 212, 232), 190, 14, 22),
                ("frozen pool", "blob", ["clear"], 6, (150, 190, 218), 190, 10, 16)],
        sprites=dict(rock=("boulder", 360), rough=("rubble", 150), bank=("iceslab", 100))),
    "ash_plains": dict(
        decals=[("ash drift", "soft", ["clear", "ash"], 34, (196, 192, 196), 120, 24, 40),
                ("cracked earth", "crack", ["clear", "ash"], 32, (44, 34, 28), 210, 14, 26),
                ("crater", "ring", ["clear", "ash", "rough"], 12, (46, 40, 38), 210, 18, 30),
                ("burnt grass", "soft", ["clear", "ash"], 22, (38, 32, 22), 150, 18, 30),
                ("bones", "dots", ["clear", "ash"], 10, (234, 230, 216), 245, 16, 24)],
        sprites=dict(rock=("boulder", 280), rough=("rubble", 200))),
}


def shades(base):
    dark = tuple(max(0, round(v * 0.88)) for v in base)
    light = tuple(min(255, round(v * 1.14)) for v in base)
    return dark, base, light


def build(biome):
    b = BIOMES[biome]
    base = {}
    for name, tl in zip(b["order"], CHAIN):
        base[name] = solve(b["hue"][name], tl)
    for name, tl in b["extra"].items():
        base[name] = solve(b["hue"][name], tl)
    return base


def read_shipped(path):
    """Overlay + header comments are copied from the shipped file so variants keep its ashfall/pitchblende numbers."""
    return open(path).read()


COMMENTS = dict(clear="Open ground.", road="Roads: lighter than the ground around them.", rough="Rubble and scrub.", ash="Ash ground: its own hue.",
                water="Deep water: clearly blue.", rock="Rock outcrops.", cliff="Cliffs: dark face (B and C add a light lip).", shallow="Fords and lagoon: lighter blue-green than deep water.")


def toml_for(biome, look):
    b = BIOMES[biome]
    base = build(biome)
    shipped = read_shipped(os.path.join(THEATERS, "variants", "classic", FILES[biome] + ".toml"))
    out = []
    names = {"rust_belt": "Rust Belt", "glass_coast": "Glass Coast", "mountains": "Mountains", "ash_plains": "Ash Plains"}
    desc = {"a": "A 'Re-tune': palette only", "b": "B 'Edges': A plus road edge lines, shoreline, cliff lip and drop shadow, large-scale tone",
            "c": "C 'Edges + fields': B plus resource deposits (heaps/crystals with a rim) and minimap resource colours"}[look]
    out.append(f"# {names[biome]} terrain palette, S15 look variant {desc}.")
    out.append("# Generated by tools/maps/make_s15_looks.py. Pick it with RTS_TERRAIN_LOOK=" + look + " (see TerrainPalette.ActiveLook)"
               + ("; this is also what data/theaters/" + FILES[biome] + ".toml ships (D-181)." if look == "c" else "."))
    out.append("# ASSUMED (S15): every colour is a proposal for the owner to pick from.")
    out.append("format_version = 1")
    out.append("texel_cells = 8" if look == "a" else "texel_cells = 16")
    out.append("")
    for t in ["clear", "road", "rough", "ash", "water", "rock", "cliff", "shallow"]:
        d, m, l = shades(base[t])
        out += [f"[land.{t}]", f"# {COMMENTS[t]}", f'dark = "{hexc(d)}"', f'base = "{hexc(m)}"', f'light = "{hexc(l)}"', ""]
    # overlay: copy numbers from the shipped file
    import re
    ash_cov = re.search(r"\[overlay\.ashfall\].*?max_coverage_permille = (\d+)", shipped, re.S).group(1)
    ash_speck = re.search(r"\[overlay\.ashfall\].*?speck = \"(#[0-9A-Fa-f]{6})\"", shipped, re.S).group(1)
    out += ["[overlay.ashfall]", f'speck = "{ash_speck}"', f"max_coverage_permille = {ash_cov}", ""]
    chunk = re.search(r"\[overlay\.pitchblende\].*?chunk = \"(#[0-9A-Fa-f]{6})\"", shipped, re.S).group(1)
    glint = hexc(b["glint"]) if look == "c" else re.search(r"glint = \"(#[0-9A-Fa-f]{6})\"", shipped).group(1)
    pit_cov = re.search(r"\[overlay\.pitchblende\].*?max_coverage_permille = (\d+)", shipped, re.S).group(1)
    out += ["[overlay.pitchblende]", f'chunk = "{chunk}"', f'glint = "{glint}"', f"max_coverage_permille = {pit_cov}", "glint_one_in = 7", ""]
    if look in "bc":
        out += ["[style]", "# Distances are in texels (16 per cell here).",
                *(["# The sim's real density scale is 0..12 (EconomyState.MaxDensity); the old rules read it as 0..255, which is why fields looked faint.",
                   "density_full = 12"] if look == "c" else []),
                "blotch_shift = 4", "tone_amp = 7", "tone_cells = 7",
                f'road_edge = "{hexc(b["road_line"])}"', "road_edge_texels = 1",
                f'shore = "{hexc(b["foam"])}"', "shore_texels = 3",
                f'cliff_top = "{hexc(b["cliff_top"])}"', "cliff_top_texels = 1", "cliff_shadow_permille = 500", "cliff_shadow_texels = 4", ""]
    if look == "c":
        out += ["[field]", "# Resource cells as deposits: a darker bed, a rim where the field meets other ground, 1..4 heaps per cell (more and bigger when richer).",
                "rim_texels = 1", "bed_permille = 200", "max_heaps = 4", ""]
        for key, cols in (("ashfall", b["ash_f"]), ("pitchblende", b["pit_f"])):
            body, light, shade, rim = cols
            out += [f"[field.{key}]", f'body = "{hexc(body)}"', f'light = "{hexc(light)}"', f'shade = "{hexc(shade)}"', f'rim = "{hexc(rim)}"', ""]
        out += ["[minimap]", "# Distinct resource colours on the minimap (brighter when richer).",
                f'ashfall = "{hexc(b["mini_ash"])}"', f'pitchblende = "{hexc(b["mini_pit"])}"', ""]
        d = DECOR[biome]
        out += ["[decor]", "# M12 decor (shipped look only; the classic, a and b files have none). Decals are derived from an integer hash of (map hash, cell) and baked to a",
                "# second texture; sizes are sixteenths of a cell. Sprites: the kind per land class and the permille of a cluster's cells that carry one.",
                "# ASSUMED (M12): all decor colours and chances are proposals for the owner to tune."]
        for key, (kind, per) in d["sprites"].items():
            out += [f'{key} = "{kind}"', f"{key}_permille = {per}"]
        out.append("")
        for name, shape, lands, per, col, alpha, lo, hi in d["decals"]:
            out += ["[[decor.decal]]", f'name = "{name}"', f'shape = "{shape}"', "on = [" + ", ".join(f'"{l}"' for l in lands) + "]",
                    f"permille = {per}", f'colour = "{hexc(col)}"', f"alpha = {alpha}", f"size_min = {lo}", f"size_max = {hi}", ""]
    return "\n".join(out)


def shipped_base(biome):
    text = open(os.path.join(THEATERS, "variants", "classic", FILES[biome] + ".toml")).read()
    import re
    res = {}
    for t in ["clear", "road", "rough", "ash", "water", "rock", "cliff", "shallow"]:
        m = re.search(r"\[land\.%s\].*?base = \"(#[0-9A-Fa-f]{6})\"" % t, text, re.S)
        res[t] = parse_hex(m.group(1))
    return res


PAIRS_TYPES = ["road", "ash", "water", "cliff", "rough", "shallow"]


def table():
    lines = ["| Biome | Look | min of 15 pairs | road/ash | road/water | ash/water | ash/cliff | water/cliff | rough/shallow |", "|---|---|---|---|---|---|---|---|---|"]
    for biome in BIOMES:
        for look, base in (("classic", shipped_base(biome)), ("A/B/C", build(biome))):
            ps = [(a, b2) for i, a in enumerate(PAIRS_TYPES) for b2 in PAIRS_TYPES[i + 1:]]
            m = min(contrast(base[a], base[b2]) for a, b2 in ps)
            sel = [contrast(base[a], base[b2]) for a, b2 in (("road", "ash"), ("road", "water"), ("ash", "water"), ("ash", "cliff"), ("water", "cliff"), ("rough", "shallow"))]
            lines.append(f"| {biome} | {look} | {m:.2f} | " + " | ".join(f"{v:.2f}" for v in sel) + " |")
    return "\n".join(lines)


if __name__ == "__main__":
    if "--table-only" not in sys.argv:
        for look in "abc":
            d = os.path.join(THEATERS, "variants", look)
            os.makedirs(d, exist_ok=True)
            for biome, fn in FILES.items():
                text = toml_for(biome, look) + "\n"
                with open(os.path.join(d, fn + ".toml"), "w", newline="\n") as f:
                    f.write(text)
                if look == "c":   # D-181: the shipped file is C; only the two header lines differ
                    lines = text.split("\n")
                    lines[0] = lines[0].replace("S15 look variant C", "SHIPPED look (D-181) = S15 variant C")
                    lines[1] = ("# Generated by tools/maps/make_s15_looks.py (do not hand-edit; variants/c is the same data). Look files: variants/classic = pre-S15 look, "
                                "variants/a|b|c; RTS_TERRAIN_LOOK=classic|a|b|c selects one, unset = this file.")
                    with open(os.path.join(THEATERS, fn + ".toml"), "w", newline="\n") as f:
                        f.write("\n".join(lines))
    print(table())
