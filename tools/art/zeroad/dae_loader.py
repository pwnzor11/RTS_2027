"""Minimal Collada (.dae) reader for the 0 A.D. static prop meshes (Blender 5 has no Collada importer).

Pure python (ElementTree + numpy): returns world-space triangles with UVs, Z-up, in metres.
Art read through this loader stays CC-BY-SA 3.0 (Wildfire Games / 0 A.D. contributors); see docs/CREDITS.md.
"""
import xml.etree.ElementTree as ET
import numpy as np

NS = "{http://www.collada.org/2005/11/COLLADASchema}"


def _t(tag):
    return NS + tag


def _floats(el):
    return np.array(el.text.split(), dtype=np.float64)


def _node_matrix(node):
    m = np.eye(4)
    for ch in node:
        tag = ch.tag.replace(NS, "")
        if tag == "matrix":
            m = m @ np.array(ch.text.split(), dtype=np.float64).reshape(4, 4)
        elif tag == "translate":
            v = _floats(ch)
            t = np.eye(4); t[:3, 3] = v; m = m @ t
        elif tag == "scale":
            v = _floats(ch)
            m = m @ np.diag([v[0], v[1], v[2], 1.0])
        elif tag == "rotate":
            v = _floats(ch); ax = v[:3] / (np.linalg.norm(v[:3]) or 1); a = np.radians(v[3])
            c, s = np.cos(a), np.sin(a); x, y, z = ax
            r = np.eye(4)
            r[:3, :3] = [[c + x * x * (1 - c), x * y * (1 - c) - z * s, x * z * (1 - c) + y * s],
                         [y * x * (1 - c) + z * s, c + y * y * (1 - c), y * z * (1 - c) - x * s],
                         [z * x * (1 - c) - y * s, z * y * (1 - c) + x * s, c + z * z * (1 - c)]]
            m = m @ r
    return m


def load(path):
    """-> (verts Nx3, uvs Nx2 per triangle corner (Mx3x2), tris Mx3 int, up-axis fixed to Z, metres)."""
    root = ET.parse(path).getroot()
    unit = root.find(f"{_t('asset')}/{_t('unit')}")
    scale = float(unit.get("meter")) if unit is not None else 1.0
    up = root.findtext(f"{_t('asset')}/{_t('up_axis')}", "Z_UP")
    geoms = {}
    for g in root.iter(_t("geometry")):
        mesh = g.find(_t("mesh"))
        srcs = {s.get("id"): s for s in mesh.findall(_t("source"))}
        vmap = {}
        vtx = mesh.find(_t("vertices"))
        for inp in vtx.findall(_t("input")):
            vmap[vtx.get("id")] = srcs[inp.get("source")[1:]] if inp.get("semantic") == "POSITION" else None
        pos_src = [srcs[i.get("source")[1:]] for i in vtx.findall(_t("input")) if i.get("semantic") == "POSITION"][0]
        P = _floats(pos_src.find(_t("float_array"))).reshape(-1, 3)
        out_p, out_uv, out_t = [], [], []
        for prim in list(mesh.findall(_t("triangles"))) + list(mesh.findall(_t("polylist"))) + list(mesh.findall(_t("polygons"))):
            ins = prim.findall(_t("input"))
            stride = max(int(i.get("offset")) for i in ins) + 1
            voff = [int(i.get("offset")) for i in ins if i.get("semantic") == "VERTEX"][0]
            tcin = [i for i in ins if i.get("semantic") == "TEXCOORD"]
            if prim.tag == _t("polygons"):
                rows, vc = [], []
                for p in prim.findall(_t("p")):
                    a = np.array(p.text.split(), dtype=np.int64).reshape(-1, stride)
                    rows.append(a); vc.append(len(a))
                idx = np.vstack(rows)
            else:
                idx = np.array(prim.find(_t("p")).text.split(), dtype=np.int64).reshape(-1, stride)
                if prim.tag == _t("polylist"):
                    vc = [int(x) for x in prim.find(_t("vcount")).text.split()]
                else:
                    vc = [3] * (idx.shape[0] // 3)
            if tcin:
                uvs = _floats(srcs[tcin[0].get("source")[1:]].find(_t("float_array")))
                nc = 2
                acc = srcs[tcin[0].get("source")[1:]].find(f"{_t('technique_common')}/{_t('accessor')}")
                nc = int(acc.get("stride"))
                uvs = uvs.reshape(-1, nc)[:, :2]
                uoff = int(tcin[0].get("offset"))
            pos = 0
            for n in vc:
                poly = idx[pos:pos + n]; pos += n
                for k in range(1, n - 1):
                    for j in (0, k, k + 1):
                        out_p.append(P[poly[j, voff]])
                        out_uv.append(uvs[poly[j, uoff]] if tcin else (0.0, 0.0))
        geoms[g.get("id")] = (np.array(out_p).reshape(-1, 3), np.array(out_uv).reshape(-1, 2))
    allp, alluv = [], []

    def walk(node, parent):
        m = parent @ _node_matrix(node)
        for ig in node.findall(_t("instance_geometry")):
            gid = ig.get("url")[1:]
            if gid in geoms:
                p, uv = geoms[gid]
                ph = np.hstack([p, np.ones((len(p), 1))]) @ m.T
                allp.append(ph[:, :3]); alluv.append(uv)
        for ch in node.findall(_t("node")):
            walk(ch, m)

    vs = root.find(f"{_t('library_visual_scenes')}/{_t('visual_scene')}")
    for n in vs.findall(_t("node")):
        walk(n, np.eye(4))
    if not allp:                       # no scene graph instance: take every geometry
        for p, uv in geoms.values():
            allp.append(p); alluv.append(uv)
    V = np.vstack(allp) * scale
    UV = np.vstack(alluv)
    if up == "Y_UP":
        V = np.stack([V[:, 0], -V[:, 2], V[:, 1]], 1)
    elif up == "X_UP":
        V = np.stack([-V[:, 1], V[:, 0], V[:, 2]], 1)
    return V.reshape(-1, 3, 3), UV.reshape(-1, 3, 2)


if __name__ == "__main__":
    import sys
    for f in sys.argv[1:]:
        V, UV = load(f)
        lo, hi = V.reshape(-1, 3).min(0), V.reshape(-1, 3).max(0)
        print(f.split("/")[-1], len(V), "tris size", np.round(hi - lo, 2), "uv", np.round(UV.reshape(-1, 2).min(0), 2), np.round(UV.reshape(-1, 2).max(0), 2))
