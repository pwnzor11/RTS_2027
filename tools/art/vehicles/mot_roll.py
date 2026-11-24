"""Motion helpers shared by the S43 fan-out vehicle modules (tools/art/vehicles/<id>.py), same look as the pilot.

Wheeled: a 2-fold hub bar on each wheel turns 45 deg per frame (auth_jeep.py) and the sprung body bobs by BOB (sprung()).
Tracked: road-wheel bolts roll (auth_tank.py: 120 deg apart, 30 deg per frame) and a ring of link plates runs round the track loop,
the ring shifted k/4 of a pitch per frame (track_links()).  No bob on tracks, as the tank.
Every helper takes the frame index k (0..3).  Idle is built with k = 0, so idle carries move frame 0's detail (no pop on stop).
Units: world units, x east, y north (front), z up; 3.4 units = 1 map cell = PX_CELL sheet px.
"""
import math

CELL = 3.4
PX_CELL = 80
BOB = (0.0, 0.05, 0.10, 0.05)      # sprung body height above the wheels (world units) for move frames 0..3
LINK_PITCH = 0.6                   # target link pitch (auth_tank.py: 0.61)
LINK_MAT = ("#3A3E33", dict(rough=0.8))


def roll_step(r):
    """STEP_PX (move) for a 2-fold hub bar turning 45 deg per frame on wheels of radius r: ground travel of one frame in sheet px."""
    return {"move": round(math.pi * r / 4 / CELL * PX_CELL, 2)}


def hub_bar(S, tag, s, x, y, r, tw, k):
    """Hub bar on the outer face of a wheel centred (x, y, r), axle along X, tyre width tw.  s = -1 left side, +1 right side.
    Turns -45 deg per frame, so the wheel rolls forward (+y); the bar is 2-fold, so 4 frames close the loop."""
    S.box(f"bar{tag}", (0.03, 0.95 * r, 0.16 * r), (x + s * (tw / 2 + 0.05), y, r), "dark", 0.0, rot=(-45.0 * k, 0, 0))


def sprung(S, k):
    """Call after the wheels and axles: every part added from here on (chassis, cab, turret) rides up by BOB[k].  k = 0 clears."""
    S.set_pose(dz=-BOB[k])


def road_bolts(S, tag, s, x, y, zc, r, tw, k):
    """Three rolling bolts on a road wheel's outer face, wheel centred (x, y, zc), radius r, width tw (auth_tank.py)."""
    xo = x + s * (tw / 2 + 0.005)
    for j in range(3):
        a = math.radians(120 * j + 30 * k)
        S.cyl(f"bolt{tag}{j}", 0.2 * r, 0.05, (xo, y + 0.67 * r * math.sin(a), zc + 0.67 * r * math.cos(a)), "dark", "X", 6)


def loop_pose(s, h, zc, r):
    """(y, z, tilt deg) at arc length s along a track loop: idler centres at y = +/-h, z = zc, radius r (auth_tank.link_pose).
    s = 0 at the top of the front idler; over the nose, along the bottom run, round the rear idler, along the top run."""
    run = 2 * h
    arc = math.pi * r
    s %= 2 * run + 2 * arc
    if s < arc:
        a = s / r
        return h + r * math.sin(a), zc + r * math.cos(a), math.degrees(a)
    s -= arc
    if s < run:
        return h - s, zc - r, 180.0
    s -= run
    if s < arc:
        a = s / r
        return -h - r * math.sin(a), zc - r * math.cos(a), 180.0 + math.degrees(a)
    s -= arc
    return -h + s, zc + r, 0.0


def track_geom(h, r):
    """(links, pitch) of a loop with idler half-spacing h and radius r: the link count is set so the pitch is near LINK_PITCH."""
    loop = 4 * h + 2 * math.pi * r
    n = max(8, round(loop / LINK_PITCH))
    return n, loop / n


def track_links(S, tag, x, yc, h, zc, r, k, width=0.0, side=False):
    """Ring of link plates round the track loop (centre y = yc).  side=False: plates lie on the top and ends of the track, width = x-width.
    side=True: plates lie on the outer side face at x (the face is the visible one), 0.12 deep across the belt.  Ring shifted k/4 pitch."""
    n, pitch = track_geom(h, r)
    for i in range(n):
        y, z, tilt = loop_pose(i * pitch + k * pitch / 4, h, zc, r)
        size = (0.05, pitch * 0.55, 0.12) if side else (width, pitch * 0.55, 0.05)
        S.box(f"link{tag}{i}", size, (x, yc + y, z), "link", 0.0, rot=(-tilt, 0, 0))


def track_step(h, r, scale=1.0):
    """STEP_PX (move) for the link ring: one frame moves the ring a quarter pitch.  scale = the model's own scale (1 unless wrapped)."""
    _n, pitch = track_geom(h, r)
    return {"move": round(pitch * scale / 4 / CELL * PX_CELL, 2)}
