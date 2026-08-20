"""
Cliff_Wall_Chunk_A — rebuilt as ROCK CHUNKS FIRST, wall second.

Construction is completely different from the loft/grid attempts: every visible
form is an irregular convex polyhedron, and the wall is the boolean UNION of
about 35 of them. There is no profile, no station, no row and no column, so
there is no grid topology to leak through into the silhouette.

Design space:  x = depth (east, into rock), y = height, z = along the wall.
Blender frame: (x, -z, y)  ->  FBX defaults  ->  Unity (x, y, z).
"""
import bpy, bmesh, math, os, random
from mathutils import Vector

BASE = "/Users/michaelhaney/Projects/Last Beacon Official/Blender"
OUT = os.path.join(BASE, "renders_chunk")
NAME = "Cliff_Wall_Chunk_A"
SEED = 8160216
rnd = random.Random(SEED)

CLEAR_Y, MAX_WEST, BACK = 4.5, -0.15, 5.6
Q = 0.17                      # vertex quantisation -> flat facets, not blobs

def q(v, s=Q): return round(v / s) * s

# --- five masses: z range, crest, base depth of the FRONT face ---------------
MASSES = [
    (-5.50, -3.85, 6.90, 0.55),
    (-3.85, -2.40, 8.30, 0.10),   # dominant
    (-2.40,  0.45, 7.80, 0.35),   # widest
    ( 0.45,  1.90, 6.20, 0.95),   # recessed -> notch
    ( 1.90,  5.50, 7.40, 0.45),
]

def hull_from_points(pts, name):
    bm = bmesh.new()
    for p in pts:
        bm.verts.new(p)
    bm.verts.ensure_lookup_table()
    bmesh.ops.convex_hull(bm, input=bm.verts)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(ob)
    return ob

def chunk(cx, cy, cz, rx, ry, rz, n, name):
    """One irregular polyhedral rock chunk. Few points = large flat facets."""
    pts = []
    for _ in range(n):
        u = rnd.uniform(-1, 1); th = rnd.uniform(0, 2 * math.pi)
        s = math.sqrt(max(0.0, 1 - u * u)) * rnd.uniform(0.80, 1.0)
        dx, dy, dz = s * math.cos(th) * rx, u * ry, s * math.sin(th) * rz
        # design space -> blender frame, quantised so faces come out planar
        pts.append((q(cx + dx), q(-(cz + dz)), q(cy + dy)))
    return hull_from_points(pts, name)

def build_chunks():
    """Core well BEHIND, chunks mostly IN FRONT of it.

    Two earlier failures: free chunks that never touched (debris), then chunks
    buried in the core so only tips emerged (flat wall with slivers). A chunk's
    FRONT is now placed directly, and its radius guarantees the back end bites
    into the core, so most of each chunk is visible rock.
    """
    CORE_FRONT, CORE_BASE = 2.10, -0.45
    obs = []

    for mi, (z0, z1, crest, front_bias) in enumerate(MASSES):
        pts = []
        for sx in (CORE_FRONT, BACK):
            for sy in (CORE_BASE, crest):
                for sz in (z0, z1):
                    jy = rnd.uniform(-0.10, 0.10) if sy == crest else 0.0
                    pts.append((q(sx), q(-(sz)), q(sy + jy)))
        obs.append(hull_from_points(pts, f"core{mi}"))

        span = z1 - z0

        # PRIMARY chunks: few and large. These carry the vertical mass read.
        n_prim = 2 if span < 1.8 else 3
        for pI in range(n_prim):
            rx = rnd.uniform(1.9, 2.5)
            fr = 0.05 + front_bias * 0.25 + rnd.uniform(0.0, 0.28)
            ry = rnd.uniform(2.0, 2.9)
            rz = rnd.uniform(0.55 + span * 0.30, 0.85 + span * 0.42)
            cy = -0.2 + (crest + 0.2) * (pI + 0.5) / n_prim
            cy = min(cy, crest + 0.15 - ry * 0.45)
            cz = z0 + span * ((pI + rnd.uniform(0.3, 0.7)) / n_prim)
            obs.append(chunk(fr + rx, cy, cz, rx, ry, rz, rnd.choice([9, 10, 11]),
                             f"m{mi}_P{pI}"))

        # SECONDARY chunks: smaller, on the front face, for facet breakup only.
        n_sec = max(4, int(round(span * 2.4)))
        for c in range(n_sec):
            rx = rnd.uniform(1.15, 1.65)
            fr = rnd.choice([0.0, 0.10, 0.22, 0.34, 0.48, 0.62]) + front_bias * 0.20
            ry = rnd.uniform(0.85, 1.35)
            rz = rnd.uniform(0.80, 1.35)
            cy = rnd.uniform(-0.15, 0.6) + (crest + 0.2) * (c + rnd.uniform(0.15, 0.85)) / n_sec
            cy = min(cy, crest + 0.18 - ry * 0.5)
            cz = z0 + span * rnd.uniform(0.06, 0.94)
            obs.append(chunk(fr + rx, cy, cz, rx, ry, rz, rnd.choice([8, 9, 10]),
                             f"m{mi}_s{c}"))

        if mi in (0, 2, 4):
            rx = rnd.uniform(1.10, 1.55)
            obs.append(chunk(0.30 + rx, -0.25, z0 + span * rnd.uniform(0.25, 0.75),
                             rx, 1.15, 1.05 + 0.35 * min(1.5, span), 9, f"m{mi}_low"))
    return obs

def union_all(obs):
    target = obs[0]
    target.name = NAME
    bpy.context.view_layer.objects.active = target
    for o in obs[1:]:
        m = target.modifiers.new(name="b", type='BOOLEAN')
        m.operation = 'UNION'; m.object = o; m.solver = 'EXACT'
        bpy.ops.object.modifier_apply(modifier=m.name)
        bpy.data.objects.remove(o, do_unlink=True)
    return target

def cleanup(ob):
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.02)
    # merge near-coplanar faces so the union's slivers become broad planes
    bpy.ops.mesh.dissolve_limited(angle_limit=math.radians(12.0))
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode='OBJECT')

    # clamp to the approved envelope, then the clearance plane
    for v in ob.data.vertices:
        v.co.y = max(-5.5, min(5.5, v.co.y))          # wall span 11 m
        v.co.z = max(-0.55, min(8.45, v.co.z))        # base and crest
        limit = 0.0 if v.co.z < CLEAR_Y else MAX_WEST
        if v.co.x < limit:
            v.co.x = limit
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.012)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.shade_flat()

    mat = bpy.data.materials.get("MAT_Cliff_Rock") or bpy.data.materials.new("MAT_Cliff_Rock")
    mat.use_nodes = True
    n = mat.node_tree.nodes.get("Principled BSDF")
    if n:
        n.inputs["Base Color"].default_value = (0.255, 0.235, 0.205, 1.0)   # neutral grey-brown
        n.inputs["Roughness"].default_value = 0.93
    mat.diffuse_color = (0.255, 0.235, 0.205, 1.0)
    ob.data.materials.clear(); ob.data.materials.append(mat)
    ob.location = (0, 0, 0); ob.rotation_euler = (0, 0, 0); ob.scale = (1, 1, 1)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def report(ob):
    me = ob.data
    xs = [v.co.x for v in me.vertices]; ws = [v.co.y for v in me.vertices]; hs = [v.co.z for v in me.vertices]
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    worst = min((v.co.x for v in me.vertices if v.co.z < CLEAR_Y), default=0.0)
    areas = sorted(p.area for p in me.polygons)
    bm = bmesh.new(); bm.from_mesh(me)
    nm = len([e for e in bm.edges if not e.is_manifold]); bm.free()
    print(f"[BL] faces {len(me.polygons)}  triangles {tris}  verts {len(me.vertices)}")
    print(f"[BL] size depth {max(xs)-min(xs):.2f}  height {max(hs)-min(hs):.2f}  width {max(ws)-min(ws):.2f}")
    print(f"[BL] height range {min(hs):.2f}..{max(hs):.2f}   min X {min(xs):+.3f}")
    print(f"[BL] clearance below {CLEAR_Y}: min X {worst:+.3f} {'OK' if worst >= -1e-6 else 'VIOLATION'}")
    print(f"[BL] facet area m2  p10 {areas[len(areas)//10]:.2f}  median {areas[len(areas)//2]:.2f}  p90 {areas[-max(1,len(areas)//10)]:.2f}")
    print(f"[BL] facet edge m   p10 {areas[len(areas)//10]**0.5:.2f}  median {areas[len(areas)//2]**0.5:.2f}  p90 {areas[-max(1,len(areas)//10)]**0.5:.2f}")
    print(f"[BL] non-manifold edges {nm}")

def render(ob):
    sc = bpy.context.scene
    try: sc.render.engine = 'BLENDER_EEVEE_NEXT'
    except Exception: sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 1400, 950
    sc.world = bpy.data.worlds.new("W")
    sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.02, 0.022, 0.026, 1)
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.25

    key = bpy.data.objects.new("Key", bpy.data.lights.new("Key", 'SUN'))
    sc.collection.objects.link(key)
    key.data.energy = 4.2; key.data.angle = math.radians(1.2)
    key.data.color = (1.0, 0.96, 0.90)
    key.rotation_euler = (math.radians(58), 0, math.radians(-42))   # upper-left front

    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'SUN'))
    sc.collection.objects.link(fill)
    fill.data.energy = 0.9; fill.data.color = (0.55, 0.66, 0.88)
    fill.rotation_euler = (math.radians(28), 0, math.radians(135))

    cd = bpy.data.cameras.new("Cam"); cd.type = 'ORTHO'
    cam = bpy.data.objects.new("Cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    centre = Vector((2.6, 0.0, 4.1))
    for nm2, eye, o in (("west", Vector((-30, 0, 4.1)), 13.0),
                        ("threequarter", Vector((-20, 18, 12.0)), 16.5),
                        ("south", Vector((2.6, 28, 4.1)), 12.0)):
        cam.location = eye; cd.ortho_scale = o
        cam.rotation_euler = (centre - eye).normalized().to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, f"chunk_{nm2}.png")
        bpy.ops.render.render(write_still=True)
        print(f"[BL] rendered chunk_{nm2}")

    # topology view: real wireframe geometry over a flat shell
    wire = ob.copy(); wire.data = ob.data.copy()
    sc.collection.objects.link(wire)
    wm = wire.modifiers.new("wire", type='WIREFRAME')
    wm.thickness = 0.035; wm.use_replace = True
    wmat = bpy.data.materials.new("WIRE"); wmat.use_nodes = True
    wn = wmat.node_tree.nodes.get("Principled BSDF")
    if wn:
        wn.inputs["Base Color"].default_value = (1.0, 0.72, 0.16, 1)
        wn.inputs["Emission Color"].default_value = (1.0, 0.72, 0.16, 1)
        wn.inputs["Emission Strength"].default_value = 2.0
    wire.data.materials.clear(); wire.data.materials.append(wmat)
    cam.location = Vector((-30, 0, 4.1)); cd.ortho_scale = 13.0
    cam.rotation_euler = (centre - cam.location).normalized().to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, "chunk_topology.png")
    bpy.ops.render.render(write_still=True)
    print("[BL] rendered chunk_topology")
    bpy.data.objects.remove(wire, do_unlink=True)
    for o in (cam, key, fill): bpy.data.objects.remove(o, do_unlink=True)

os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
chunks = build_chunks()
print(f"[BL] chunks built: {len(chunks)}")
obj = union_all(chunks)
cleanup(obj)
report(obj)
render(obj)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BASE, f"{NAME}.blend"))
print("[BL] saved")
