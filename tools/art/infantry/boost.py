"""S43 readability boost for the infantry modules (art pass 'inf').  Pure python, safe to import with or without bpy.

RTS_INF_BOOST is "" (default: every module renders exactly as before), "pilot", or "head=1.25,gun=1.3,stance=1.3,house=1.45,limb=1.12".
  head    scale of the head / helmet / hood parts about the head centre (bigger read at 1x)
  gun     length scale of the weapon, gunw its thickness (positions inside the gun frame scale too; the hands follow the grips)
  stance  foot x offsets and hip spread (wider, planted stance)
  limb    arm / leg thickness
  house   size of the house-colour panels (shoulders, chest, back, helmet band, armbands)
  flash   muzzle flash geometry scale
"""
import os

NEUTRAL = dict(head=1.0, gun=1.0, gunw=1.0, stance=1.0, limb=1.0, house=1.0, flash=1.0)
PILOT = dict(head=1.25, gun=1.12, gunw=1.45, stance=1.30, limb=1.12, house=1.45, flash=1.35)


def params():
    v = os.environ.get("RTS_INF_BOOST", "").strip()
    d = dict(NEUTRAL)
    if not v:
        return d
    if v == "pilot":
        return dict(PILOT)
    for kv in v.split(","):
        k, x = kv.split("=")
        d[k.strip()] = float(x)
    return d


def canvas_extra():
    """The boosted figure's rifle and muzzle flash reach further: the frame grows by 4 px (60 -> 64 at 1.25 scale), origin follows."""
    return 4 if os.environ.get("RTS_INF_BOOST", "").strip() else 0
