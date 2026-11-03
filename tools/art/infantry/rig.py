"""Blender-side helpers for infantry figures: primitive builders that take a placement matrix, a limb helper
(box between two points) and a two-bone IK solver, so poses are given as foot / hand targets instead of angles.
Only imported inside the Blender render process (needs bpy)."""
import math
import bpy
import bmesh
from mathutils import Matrix, Vector


SCALE = 1.05   # model units -> world units: a soldier is about 23 px tall at 1x


def lin(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c] + [1.0]


def make_mat(name, hexc, rough=0.8, metal=0.0, emit=None, emit_strength=2.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(hexc)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = lin(emit)
        b.inputs["Emission Strength"].default_value = emit_strength
    return m


class Scene:
    """Collects the objects of one figure. All parts hang off `rig` (an empty rotated for the facing)."""

    def __init__(self, mats):
        self.mats = mats
        self.rig = bpy.data.objects.new("rig", None)
        self.rig.scale = (SCALE, SCALE, SCALE)      # whole figure scale, keeps IK lengths in model units
        bpy.context.scene.collection.objects.link(self.rig)
        self.objs = []          # (object, material name)

    # ---- primitives: `M` is a parent matrix (default identity), `loc` / `rot` (deg XYZ) are local to it
    def _place(self, mesh, name, mat, M, loc, rot, smooth=False, bevel=0.0):
        o = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(o)
        o.parent = self.rig
        R = Matrix.Rotation(math.radians(rot[2]), 4, "Z") @ Matrix.Rotation(math.radians(rot[1]), 4, "Y") @ Matrix.Rotation(math.radians(rot[0]), 4, "X")
        o.matrix_basis = (M or Matrix.Identity(4)) @ Matrix.Translation(loc) @ R
        mesh.materials.append(self.mats[mat])
        if smooth:
            for p in mesh.polygons:
                p.use_smooth = True
        if bevel:
            bm = o.modifiers.new("bev", "BEVEL")
            bm.width = bevel
            bm.segments = 1
        self.objs.append((o, mat))
        return o

    def box(self, size, loc, mat, M=None, rot=(0, 0, 0), bevel=0.012):
        me = bpy.data.meshes.new("b")
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        bm.to_mesh(me)
        bm.free()
        return self._place(me, "box", mat, M, loc, rot, bevel=bevel)

    def cyl(self, r, h, loc, mat, axis="Z", M=None, verts=12, r2=None):
        me = bpy.data.meshes.new("c")
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=verts, radius1=r, radius2=r if r2 is None else r2, depth=h)
        bm.to_mesh(me)
        bm.free()
        rot = {"Z": (0, 0, 0), "X": (0, 90, 0), "Y": (90, 0, 0)}[axis]
        return self._place(me, "cyl", mat, M, loc, rot, smooth=True)

    def sphere(self, radii, loc, mat, M=None, seg=12):
        me = bpy.data.meshes.new("s")
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2 + 1, radius=1.0)
        bmesh.ops.scale(bm, vec=Vector(radii), verts=bm.verts)
        bm.to_mesh(me)
        bm.free()
        return self._place(me, "sph", mat, M, loc, (0, 0, 0), smooth=True)

    def limb(self, a, b, w, mat, d=None, bevel=0.012):
        """Box from point a to point b (figure coords), cross-section w x d."""
        a, b = Vector(a), Vector(b)
        v = b - a
        up = "Z" if abs(v.normalized().z) < 0.95 else "X"
        q = v.to_track_quat("Y", up)
        M = Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4()
        return self.box((w, v.length, d or w), (0, 0, 0), mat, M, bevel=bevel)

    def clear(self):
        for o, _m in self.objs:
            me = o.data
            bpy.data.objects.remove(o)
            bpy.data.meshes.remove(me)
        self.objs = []

    def destroy(self):
        self.clear()
        bpy.data.objects.remove(self.rig)


def ik2(root, target, l1, l2, pole):
    """Two-bone IK: returns the middle joint (knee / elbow). The end point is `target` clamped to reach."""
    root, target, pole = Vector(root), Vector(target), Vector(pole)
    d = target - root
    dist = min(max(d.length, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    dirn = d.normalized()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    perp = pole - dirn * pole.dot(dirn)
    if perp.length < 1e-6:
        perp = Vector((0, 1, 0))
    return root + dirn * a + perp.normalized() * h, root + dirn * dist


def rx(deg):
    return Matrix.Rotation(math.radians(deg), 4, "X")


def ry(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Y")


def rz(deg):
    return Matrix.Rotation(math.radians(deg), 4, "Z")


def tr(x, y, z):
    return Matrix.Translation((x, y, z))
