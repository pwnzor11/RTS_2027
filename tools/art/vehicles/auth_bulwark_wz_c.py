"""M10-06b kitbash pilot, cannon-only variant of auth_bulwark_wz.py (no cupola machine gun).  Contract: see auth_jeep.py."""
import importlib.util
import os

_spec = importlib.util.spec_from_file_location("vehicles_auth_bulwark_wz_full",
                                               os.path.join(os.path.dirname(os.path.abspath(__file__)), "auth_bulwark_wz.py"))
_W = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_W)
_W.MG = False

ID = "auth_bulwark_wz_c"
TITLE = "Authority Bulwark heavy tank (Warzone cannon only, candidate)"
CANVAS, TARGET, STATES, LAYERS, PIVOT, MOUNT = _W.CANVAS, _W.TARGET, _W.STATES, _W.LAYERS, _W.PIVOT, _W.MOUNT
build = _W.build
