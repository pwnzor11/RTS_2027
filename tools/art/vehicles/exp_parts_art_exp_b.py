"""Extra Expedition vehicle parts for the art-exp-b package (boats, tracks, small fittings).  Import after exp_common (`import exp_parts_art_exp_b as P`).
Boats sit IN the water: hull bottom below the waterline (z=0), a white foam collar at the waterline, a bow wave and a short stern wake; no skirt.
Original design."""
import exp_common as E


def waterline(S, w, ln, y=0.0, bow=0.0, stern=0.5):
    """Thin foam collar hugging a hull of width w, length ln at the waterline, a small bow curl and a short stern wake (flat white slabs at z~0)."""
    S.box("foam", (w + 0.16, ln + 0.14, 0.04), (0, y, 0.02), "foamw", 0.02, nocast=True)
    S.box("bowcurl", (w * 0.55, 0.22, 0.05), (0, y + ln / 2 + 0.1 + bow, 0.03), "foamw", 0.03, nocast=True)
    for s in (-1, 1):
        S.box(f"bowwave{s}", (0.14, 0.7 + bow, 0.04), (s * (w / 2 + 0.12), y + ln / 2 - 0.45, 0.02), "foamw", 0.02, nocast=True)
    S.box("wakec", (0.5, stern + 0.3, 0.04), (0, y - ln / 2 - stern / 2 + 0.05, 0.02), "foamw", 0.02, nocast=True)


def rail(S, tag, x0, x1, y0, y1, z, h=0.28, mat="chrome"):
    """Open deck rail: posts at the ends and a top bar along the line (x0,y0)-(x1,y1) (axis-aligned runs only)."""
    if abs(x1 - x0) > abs(y1 - y0):
        S.box(f"rail{tag}", (abs(x1 - x0), 0.04, 0.04), ((x0 + x1) / 2, y0, z + h), mat, 0.0)
    else:
        S.box(f"rail{tag}", (0.04, abs(y1 - y0), 0.04), (x0, (y0 + y1) / 2, z + h), mat, 0.0)
    for k, (x, y) in enumerate(((x0, y0), (x1, y1))):
        S.box(f"railp{tag}{k}", (0.04, 0.04, h), (x, y, z + h / 2), mat, 0.0)


def tracks(S, w, ln, y=0.0, h=0.5, sides=(-1, 1), tw=0.55):
    """Chunky tracked running gear: two track pods with wheels, a dark rubber belt and chrome sprockets."""
    for s in sides:
        S.box(f"trk{s}", (tw, ln, h), (s * (w / 2 - tw / 2), y, h / 2), "rubber", 0.12)
        S.box(f"trkcap{s}", (tw + 0.08, ln - 0.3, 0.08), (s * (w / 2 - tw / 2), y, h + 0.02), "chrome2", 0.03)
        for k in range(5):
            yy = y - ln / 2 + 0.35 + k * (ln - 0.7) / 4
            S.cyl(f"rw{s}_{k}", h * 0.42, tw * 0.9, (s * (w / 2 - tw / 2), yy, h * 0.5), "chrome2", "X", 14)
        S.cyl(f"spr{s}", h * 0.5, tw * 1.05, (s * (w / 2 - tw / 2), y + ln / 2 - h * 0.45, h * 0.52), "chrome", "X", 14)
        S.box(f"fender{s}", (tw + 0.12, ln - 0.1, 0.06), (s * (w / 2 - tw / 2), y, h + 0.08), "navy2", 0.02)


BOAT_MATERIALS = dict(E.MATERIALS)
BOAT_MATERIALS["foamw"] = ("#F4FAF6", dict(rough=1.0))
BOAT_MATERIALS["waterstain"] = ("#4F575F", dict(rough=1.0))


def hull_bottom_band(S, w, ln, y=0.0, h=0.22):
    """Grey ash tidemark band along the waterline of a boat hull."""
    S.box("tideband", (w + 0.03, ln + 0.03, h), (0, y, h / 2), "ash", 0.05)
