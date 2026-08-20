"""
Cliff_TerraceEast_B — second variant. Same envelope and clearance as A.

Why A reads as a slab: every mass was a straight extrusion along the wall, so
every west-facing plane shared one normal and took identical light. B varies the
profile between each mass's two ends, so the west face alternates between planes
tilted north-west, due west and south-west, and the value pattern comes out of
geometry alone.

Axis convention as established for A:
    Blender (x, -station, height)  --FBX defaults-->  Unity (x, height, station)
"""
import bpy, bmesh, math, os
from mathutils import Vector

BASE = "/Users/michaelhaney/Projects/Last Beacon Official/Blender"
OUT = os.path.join(BASE, "renders_compare")
BACK, CLEAR_Y = 5.5, 4.5

# --- Variant A, rebuilt here so both render under identical conditions -------
A_SOUTH = [(0.70, 0.0), (1.00, 3.2), (3.00, 6.0), (5.10, 7.8), (BACK, 7.2), (BACK, 0.0)]
A_FULL  = [(0.00, 0.0), (0.35, 3.6), (2.60, 6.8), (5.00, 8.9), (BACK, 8.1), (BACK, 0.0)]
A_B     = [(0.00, 0.0), (0.35, 3.2), (-0.15, 5.8), (4.80, 8.5), (BACK, 7.8), (BACK, 0.0)]
A_C     = [(0.00, 0.0), (0.55, 2.6), (2.90, 5.6), (4.50, 7.7), (BACK, 7.0), (BACK, 0.0)]
A_D     = [(0.00, 0.0), (0.25, 3.4), (2.20, 6.4), (4.70, 8.4), (BACK, 7.7), (BACK, 0.0)]
A_E     = [(0.00, 0.0), (0.45, 3.2), (2.70, 6.2), (4.90, 8.2), (BACK, 7.5), (BACK, 0.0)]
A_STATIONS = [(-5.50, A_SOUTH), (-3.70, A_FULL), (-3.50, A_B), (-1.20, A_B),
              (-1.00, A_C), (1.30, A_C), (1.50, A_D), (2.80, A_D), (3.00, A_E), (5.50, A_E)]

# --- Variant B ---------------------------------------------------------------
# Five masses of deliberately unequal width, lean, crest and base.
# M1 south 1.55 | M2 narrow 1.05 pushed forward | M3 widest 2.85 tallest
# M4 short 1.25 recessed -> the deep notch | M5 3.70 lower shoulder, simple north
P1s = [(0.55,  0.0), (0.75, 2.2), (2.30, 4.0), (2.10, 5.6), (4.30, 6.9), (BACK, 6.3), (BACK,  0.0)]
P1n = [(0.00,  0.0), (0.30, 2.6), (1.95, 4.4), (2.20, 6.0), (4.15, 7.6), (BACK, 7.0), (BACK,  0.0)]
# M2 carried LOWER (-0.4) so the bench line is not a straight cut.
P2s = [(0.00, -0.4), (0.15, 2.8), (1.35, 4.6), (1.80, 6.2), (3.55, 7.9), (BACK, 7.2), (BACK, -0.4)]
# P2n and P3s share every point below y 4.0: that fracture only exists up high.
P2n = [(0.00, -0.4), (0.35, 2.4), (1.70, 4.0), (2.05, 6.0), (3.30, 7.9), (BACK, 7.2), (BACK, -0.4)]
P3s = [(0.00, -0.4), (0.35, 2.4), (1.70, 4.0), (2.60, 5.8), (4.60, 8.5), (BACK, 7.8), (BACK, -0.4)]
P3n = [(0.15,  0.0), (0.55, 2.7), (2.15, 4.6), (2.95, 6.2), (5.00, 8.5), (BACK, 7.8), (BACK,  0.0)]
P4s = [(0.60,  0.0), (0.95, 2.5), (2.50, 4.2), (3.10, 5.4), (5.20, 6.5), (BACK, 6.0), (BACK,  0.0)]
P4n = [(0.60, -0.4), (0.80, 2.3), (2.25, 4.0), (2.90, 5.2), (5.05, 6.5), (BACK, 6.0), (BACK, -0.4)]
# P5s shares its top three points with P4n: that mass merges into its neighbour.
P5s = [(0.25,  0.0), (0.50, 2.6), (1.85, 4.4), (2.90, 5.2), (5.05, 6.5), (BACK, 6.0), (BACK,  0.0)]
P5n = [(0.30,  0.0), (0.60, 2.9), (2.05, 4.8), (3.10, 6.0), (4.55, 7.4), (BACK, 6.8), (BACK,  0.0)]

B_STATIONS = [(-5.50, P1s), (-3.95, P1n), (-3.80, P2s), (-2.75, P2n),
              (-2.60, P3s), ( 0.25, P3n), ( 0.40, P4s), ( 1.65, P4n),
              ( 1.80, P5s), ( 5.50, P5n)]

def wipe():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    for blk in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.objects):
        for b in list(blk):
            if getattr(b, "users", 0) == 0:
                try: blk.remove(b)
                except Exception: pass

def build(name, stations):
    n = len(stations[0][1])
    verts, faces = [], []
    for z, prof in stations:
        for (x, y) in prof:
            verts.append((x, -z, y))
    for s in range(len(stations) - 1):
        a, b = s * n, (s + 1) * n
        for j in range(n):
            k = (j + 1) % n
            faces.append((a + j, a + k, b + k, b + j))
    faces.append(tuple(range(n - 1, -1, -1)))
    last = (len(stations) - 1) * n
    faces.append(tuple(range(last, last + n)))

    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.validate()
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)

    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.shade_flat()

    mat = bpy.data.materials.get("MAT_Cliff_Rock") or bpy.data.materials.new("MAT_Cliff_Rock")
    mat.use_nodes = True
    n2 = mat.node_tree.nodes.get("Principled BSDF")
    if n2:
        n2.inputs["Base Color"].default_value = (0.150, 0.170, 0.200, 1.0)
        n2.inputs["Roughness"].default_value = 0.96
    mat.diffuse_color = (0.150, 0.170, 0.200, 1.0)
    ob.data.materials.clear(); ob.data.materials.append(mat)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.select_set(False)
    return ob

def report(ob):
    me = ob.data
    xs = [v.co.x for v in me.vertices]; hs = [v.co.z for v in me.vertices]; ws = [v.co.y for v in me.vertices]
    worst = min((v.co.x for v in me.vertices if v.co.z < CLEAR_Y), default=0.0)
    for e in me.edges:
        a, b = me.vertices[e.vertices[0]].co, me.vertices[e.vertices[1]].co
        lo, hi = (a, b) if a.z <= b.z else (b, a)
        if lo.z < CLEAR_Y < hi.z:
            t = (CLEAR_Y - lo.z) / (hi.z - lo.z); worst = min(worst, lo.x + t * (hi.x - lo.x))
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(f"[BL] {ob.name}: faces {len(me.polygons)} (tris {tris})  verts {len(me.vertices)}  "
          f"depth {max(xs)-min(xs):.2f}  height {max(hs)-min(hs):.2f}  width {max(ws)-min(ws):.2f}  "
          f"height range {min(hs):.2f}..{max(hs):.2f}")
    print(f"[BL] {ob.name}: clearance below {CLEAR_Y} -> min X {worst:+.3f} "
          f"{'OK' if worst >= -1e-6 else 'VIOLATION'};  min X overall {min(xs):+.3f}")
    bm = bmesh.new(); bm.from_mesh(me)
    print(f"[BL] {ob.name}: non-manifold edges {len([e for e in bm.edges if not e.is_manifold])}")
    bm.free()

def scene_setup():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'SINGLE'
    sc.display.shading.single_color = (0.30, 0.32, 0.36)
    sc.display.shading.show_object_outline = False
    sc.render.resolution_x, sc.render.resolution_y = 1200, 900
    sc.world = bpy.data.worlds.new("W"); sc.world.color = (0.045, 0.05, 0.06)
    cd = bpy.data.cameras.new("Cam"); cd.type = 'ORTHO'
    cam = bpy.data.objects.new("Cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    return cam, cd

def shoot(cam, cd, ob, others, name, eye, ortho):
    for o in others: o.hide_render = (o is not ob)
    ob.hide_render = False
    centre = Vector((2.5, 0.0, 4.2))
    cam.location = eye; cd.ortho_scale = ortho
    cam.rotation_euler = (centre - eye).normalized().to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[BL] rendered {name}")

os.makedirs(OUT, exist_ok=True)
wipe()
a = build("Cliff_TerraceEast_A", A_STATIONS)
b = build("Cliff_TerraceEast_B", B_STATIONS)
report(a); report(b)

cam, cd = scene_setup()
WEST = Vector((-30, 0.0, 4.2)); TQ = Vector((-20, 20, 12.0))
shoot(cam, cd, a, [a, b], "A_west", WEST, 14.5)
shoot(cam, cd, b, [a, b], "B_west", WEST, 14.5)
shoot(cam, cd, a, [a, b], "A_threequarter", TQ, 17.0)
shoot(cam, cd, b, [a, b], "B_threequarter", TQ, 17.0)

for o in (a, b): o.hide_render = False
bpy.ops.object.select_all(action='DESELECT'); b.select_set(True)
bpy.context.view_layer.objects.active = b
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BASE, "Cliff_TerraceEast_B.blend"))
print("[BL] saved B blend")
