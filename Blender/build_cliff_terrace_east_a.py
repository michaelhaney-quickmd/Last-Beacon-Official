"""
Cliff_TerraceEast_A — first low-poly visual shell for the Last Beacon terrace
east wall. Built from explicit profiles lofted along Z, so every face is a large
planar facet and nothing is sculpted, subdivided or noised.

Authored in Blender's native Z-up so a standard FBX export lands in Unity with
the requested orientation. Axis mapping:

    Blender +X  ->  Unity +X   into the rock / east
    Blender +Z  ->  Unity +Y   up
    Blender +Y  ->  Unity +Z   along the wall, south at -Y, north at +Y

Modelling literally Y-up inside Blender would be double-converted on export.
Export with the FBX DEFAULTS (Forward -Z, Up +Y). Those are the settings that
actually convert Z-up to Y-up; Forward +Y / Up +Z performs no conversion and
lands the model on its side. The default also mirrors the wall end for end,
which is why the station coordinate is negated when the verts are built.

Pivot: bottom-centre of the WEST face, at local (0, 0, 0).
"""
import bpy, bmesh, math, os
from mathutils import Vector

OUT = os.path.join(os.path.dirname(bpy.data.filepath) if bpy.data.filepath else
                   "/Users/michaelhaney/Projects/Last Beacon Official/Blender", "renders")
BLEND = "/Users/michaelhaney/Projects/Last Beacon Official/Blender/Cliff_TerraceEast_A.blend"
NAME = "Cliff_TerraceEast_A"
BACK = 5.5          # depth: back face plane
CLEAR_Y = 4.5       # below this, no geometry west of x = 0
MAX_OVERHANG = 0.5  # above CLEAR_Y, up to this far west

# --- profiles ---------------------------------------------------------------
# (x, y) west face bottom -> crest -> back top -> back bottom. 7 points each.
# (x, y) west face bottom -> crest -> back top -> back bottom. 6 points each.
# Three west segments with STRONG angle contrast, so the face reads as a few
# large planes. Four progressive segments read as a curve, which is not the brief.
A_SOUTH = [(0.70, 0.0), (1.00, 3.2), (3.00, 6.0), (5.10, 7.8), (BACK, 7.2), (BACK, 0.0)]
A_FULL  = [(0.00, 0.0), (0.35, 3.6), (2.60, 6.8), (5.00, 8.9), (BACK, 8.1), (BACK, 0.0)]
B       = [(0.00, 0.0), (0.35, 3.2), (-0.15, 5.8), (4.80, 8.5), (BACK, 7.8), (BACK, 0.0)]
C       = [(0.00, 0.0), (0.55, 2.6), (2.90, 5.6), (4.50, 7.7), (BACK, 7.0), (BACK, 0.0)]
D       = [(0.00, 0.0), (0.25, 3.4), (2.20, 6.4), (4.70, 8.4), (BACK, 7.7), (BACK, 0.0)]
E       = [(0.00, 0.0), (0.45, 3.2), (2.70, 6.2), (4.90, 8.2), (BACK, 7.5), (BACK, 0.0)]

# Station z, profile. Paired stations 0.2 m apart form the fracture bands.
STATIONS = [
    (-5.50, A_SOUTH), (-3.70, A_FULL),      # south mass, tapered tip
    (-3.50, B),       (-1.20, B),           # fracture -> mass B
    (-1.00, C),       ( 1.30, C),           # fracture -> mass C (big step down)
    ( 1.50, D),       ( 2.80, D),           # fracture -> mass D (step up)
    ( 3.00, E),       ( 5.50, E),           # fracture -> mass E, simple north end
]

def wipe():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for b in list(block):
            if b.users == 0:
                block.remove(b)

def build():
    n = len(STATIONS[0][1])
    verts, faces = [], []
    for z, prof in STATIONS:
        for (x, y) in prof:
            # Station is negated: Blender's default FBX export maps
            # (x, y, z)_blender -> (x, z, -y)_unity, so authoring the wall along
            # -Y makes it arrive along Unity +Z with south at -Z.
            verts.append((x, -z, y))

    for s in range(len(STATIONS) - 1):
        a, b = s * n, (s + 1) * n
        for j in range(n):
            k = (j + 1) % n
            faces.append((a + j, a + k, b + k, b + j))
    faces.append(tuple(range(n - 1, -1, -1)))                       # south cap
    last = (len(STATIONS) - 1) * n
    faces.append(tuple(range(last, last + n)))                      # north cap

    mesh = bpy.data.meshes.new(NAME)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    obj = bpy.data.objects.new(NAME, mesh)
    bpy.context.collection.objects.link(obj)
    return obj

def finish(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')

    bpy.ops.object.shade_flat()
    obj.data.materials.clear()
    mat = bpy.data.materials.new("MAT_Cliff_Rock")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (0.150, 0.170, 0.200, 1.0)
        bsdf.inputs["Roughness"].default_value = 0.96
    mat.diffuse_color = (0.150, 0.170, 0.200, 1.0)
    obj.data.materials.append(mat)

    obj.location = (0, 0, 0); obj.rotation_euler = (0, 0, 0); obj.scale = (1, 1, 1)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj

def report(obj):
    me = obj.data
    xs = [v.co.x for v in me.vertices]; ys = [v.co.z for v in me.vertices]; zs = [v.co.y for v in me.vertices]
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    print(f"[BL] name           : {obj.name}")
    print(f"[BL] dimensions     : depth X {max(xs)-min(xs):.2f}  height {max(ys)-min(ys):.2f}  width {max(zs)-min(zs):.2f}")
    print(f"[BL] bounds (Unity) : x {min(xs):.2f}..{max(xs):.2f}  y {min(ys):.2f}..{max(ys):.2f}  z {min(zs):.2f}..{max(zs):.2f}")
    print(f"[BL] faces          : {len(me.polygons)}   (triangulates to {tris})")
    print(f"[BL] vertices       : {len(me.vertices)}   edges {len(me.edges)}")
    print(f"[BL] transforms     : loc {tuple(round(v,3) for v in obj.location)} "
          f"rot {tuple(round(math.degrees(v),3) for v in obj.rotation_euler)} "
          f"scale {tuple(round(v,3) for v in obj.scale)}")
    print(f"[BL] materials      : {[m.name for m in me.materials]}")

    worst = min((v.co.x for v in me.vertices if v.co.z < CLEAR_Y), default=0.0)
    for e in me.edges:
        a, b = me.vertices[e.vertices[0]].co, me.vertices[e.vertices[1]].co
        lo, hi = (a, b) if a.z <= b.z else (b, a)
        if lo.z < CLEAR_Y < hi.z:                       # edge crosses the plane
            t = (CLEAR_Y - lo.z) / (hi.z - lo.z)
            worst = min(worst, lo.x + t * (hi.x - lo.x))
    print(f"[BL] clearance <{CLEAR_Y}m : min local X = {worst:+.3f}  "
          f"{'OK' if worst >= -1e-6 else 'VIOLATION'}  (vertices AND edge crossings)")
    over = min((v.co.x for v in me.vertices if v.co.z >= CLEAR_Y), default=0.0)
    print(f"[BL] overhang >{CLEAR_Y}m  : min local X = {over:+.3f}  "
          f"{'OK' if over >= -MAX_OVERHANG else 'EXCEEDS'} (allowance -{MAX_OVERHANG})")

    bm = bmesh.new(); bm.from_mesh(me)
    nonman = [e for e in bm.edges if not e.is_manifold]
    loose = [v for v in bm.verts if not v.link_faces]
    print(f"[BL] non-manifold   : {len(nonman)} edges, {len(loose)} loose verts")
    bm.free()
    return len(me.polygons), len(me.vertices), tris

def render_views(obj):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'SINGLE'
    scene.display.shading.single_color = (0.28, 0.30, 0.34)
    scene.display.shading.show_object_outline = False
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("W")
    scene.world.color = (0.045, 0.05, 0.06)

    cam_data = bpy.data.cameras.new("Cam"); cam_data.type = 'ORTHO'
    cam = bpy.data.objects.new("Cam", cam_data)
    scene.collection.objects.link(cam); scene.camera = cam

    centre = Vector((2.5, 0.0, 4.4))          # Blender Z-up
    shots = [
        ("01_west_face",     Vector((-30, 0.0, 4.4)),    18.5),
        ("02_south_profile", Vector((2.5, 30, 4.4)),     17.5),
        ("03_three_quarter", Vector((-20, 20, 13.0)),    21.0),
        ("04_top_crest",     Vector((2.5, 0.01, 34)),    15.0),
    ]
    for name, eye, ortho in shots:
        cam.location = eye
        cam_data.ortho_scale = ortho
        d = (centre - eye).normalized()
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = os.path.join(OUT, name + ".png")
        bpy.ops.render.render(write_still=True)
        print(f"[BL] rendered {name}")

    bpy.data.objects.remove(cam, do_unlink=True)

wipe()
o = finish(build())
f, v, t = report(o)
render_views(o)
os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
print(f"[BL] saved {BLEND}")
print(f"[BL] scene objects: {[ob.name for ob in bpy.data.objects]}")
