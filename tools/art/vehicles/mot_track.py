"""Motion-pass helpers for ground vehicle modules (tools/art/vehicles/<id>.py): the 'move' state of tracked and wheeled vehicles.

Same rules as the pilot (auth_tank.py tracks, auth_jeep.py wheels):
  tracked: links() draws a ring of link plates round a track loop (two idlers joined by straight runs), shifted a quarter pitch per
           move frame, so one 4-frame cycle is one link pitch; bolts() puts three rolling bolts on a road-wheel face (one cycle =
           120 deg, 30 deg per frame).
  wheeled: hub_bar() puts a two-fold bar on a wheel face (45 deg per frame, one cycle = 180 deg), as auth_jeep.py.
STEP_PX (sheet px of ground travel per move frame) = world travel per frame * 80 px per cell / 3.4 world units per cell
(build_vehicle.py CELL, common.UNIT_PX).  Modules set STEP_PX = {"move": <Loop>.step_px()} or step_wheel_px(R).
Idle reuses move frame 0 (k = 0), so wheels and links do not pop when a vehicle stops.
"""
import math

CELL = 3.4        # build_vehicle.py: world units per map cell
UNIT_PX = 80      # common.UNIT_PX: sheet px per map cell
LINK_PITCH = 0.6  # world units, target link pitch (auth_tank: 0.61)
LINK = ("#3A3E33", dict(rough=0.8))   # the link material of auth_tank.py; modules add it to MATERIALS as "link"


def px_per_world():
    return UNIT_PX / CELL


class Loop:
    """A track loop: idler centres at y = yc +- h, idler axle height zc, link path radius r (the idler radius plus a hair)."""

    def __init__(self, yc, h, r, zc, n=None):
        self.yc, self.h, self.r, self.zc = yc, h, r, zc
        self.length = 4 * h + 2 * math.pi * r
        self.n = n or max(4, round(self.length / LINK_PITCH))
        self.pitch = self.length / self.n

    def pose(self, s):
        """Point (y, z) and outward tangent (deg) at arc length s from the top of the front idler, round the loop.
        Front idler top -> nose -> bottom run (front to rear) -> rear idler -> top run (rear to front)."""
        r, h, zc = self.r, self.h, self.zc
        arc = math.pi * r
        s %= self.length
        if s < arc:
            a = s / r
            return self.yc + h + r * math.sin(a), zc + r * math.cos(a), math.degrees(a)
        s -= arc
        if s < 2 * h:
            return self.yc + h - s, zc - r, 180.0
        s -= 2 * h
        if s < arc:
            a = s / r
            return self.yc - h - r * math.sin(a), zc - r * math.cos(a), 180.0 + math.degrees(a)
        s -= arc
        return self.yc - h + s, zc + r, 0.0

    def step_px(self):
        return round(self.pitch / 4 * px_per_world(), 2)


def lb_loop(y0, y1, h=0.9, n=None):
    """The track loop of lb_common.tracks(S, x, y0, y1, ...) with its default-geometry idlers (idler radius h*0.55, axle z h/2 + 0.1)."""
    return Loop(yc=(y0 + y1) / 2, h=(y1 - y0) / 2 - 0.4, r=h * 0.55 + 0.01, zc=h / 2 + 0.1, n=n)


def links(S, prefix, x, loop, width, k, plate=0.55, thick=0.05, mat="link"):
    """Link plates round the loop at column x, shifted k/4 of a pitch (move frame k)."""
    for i in range(loop.n):
        y, z, tilt = loop.pose(i * loop.pitch + k * loop.pitch / 4)
        S.box(f"{prefix}{i}", (width, loop.pitch * plate, thick), (x, y, z), mat, 0.0, rot=(-tilt, 0, 0))


def bolts(S, prefix, face_x, y, z, R, k, mat="dark"):
    """Three rolling bolts on a road-wheel face at x = face_x (wheel centre y, z; radius R), 30 deg per frame."""
    for j in range(3):
        a = math.radians(120 * j + 30 * k)
        S.cyl(f"{prefix}{j}", 0.2 * R, 0.05, (face_x, y + 0.67 * R * math.sin(a), z + 0.67 * R * math.cos(a)), mat, "X", 6)


def hub_bar(S, name, face_x, y, z, R, k, mat="dark"):
    """Two-fold hub bar on a wheel face, turning -45 deg per frame (rolls forward, +y), as auth_jeep.py."""
    S.box(name, (0.03, 0.97 * R, 0.05), (face_x, y, z), mat, 0.0, rot=(-45 * k, 0, 0))


def step_wheel_px(R):
    """Move-frame step for a wheel that turns 45 deg per frame (hub_bar): ground travel R * pi / 4."""
    return round(R * math.pi / 4 * px_per_world(), 2)


BOB = (0.0, 0.05, 0.10, 0.05)   # sprung-body bob per move frame (world units), auth_jeep.py


def lb_track_move(S, tag, x, y0, y1, n, k, w=0.85, h=0.9):
    """Move detail for one lb_common.tracks() unit: link ring plus bolts on its road wheels (same positions as lb_common)."""
    loop = lb_loop(y0, y1, h=h)          # link count from the length (n is the road-wheel count here)
    links(S, f"link{tag}", x, loop, w * 0.95, k)
    sx = math.copysign(1, x)
    for i in range(n):
        y = y0 + 0.55 + i * (y1 - y0 - 1.1) / max(n - 1, 1)
        R = h * 0.38 if i != n // 2 else h * 0.3
        bolts(S, f"bolt{tag}{i}", x + sx * (w / 2 + 0.075), y, h / 2 + 0.05, R, k)
