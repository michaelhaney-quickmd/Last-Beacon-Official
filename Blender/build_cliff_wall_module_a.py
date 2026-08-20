"""
Cliff_Wall_Module_A — modular low-poly cliff wall for Last Beacon.

Reads first as 5 large vertical rock masses, second as medium planar breakup.

The grid is a scaffold only and is deliberately destroyed:
  * every column carries its OWN irregular set of row heights, so no row line
    runs straight across the face
  * columns are spaced irregularly, 0.32 - 1.15 m
  * adjacent columns are zipped by height, so faces come out as a mix of quads
    and triangles of varying size rather than a uniform lattice
  * depth is assigned in CLUSTERS - runs of 2-4 columns and 2-4 rows share a
    plane - so neighbouring faces are usually coplanar and only occasionally
    break. Per-face random depth would read as cobblestone.
  * fracture lines start partway up, so splits terminate instead of running the
    full height

Authoring frame: wall along -Y, height +Z, depth +X.
FBX defaults then land it in Unity as X=depth, Y=height, Z=wall.
"""
import bpy, bmesh, math, os, random
from mathutils import Vector

BASE = "/Users/michaelhaney/Projects/Last Beacon Official/Blender"
OUT = os.path.join(BASE, "renders_module")
NAME = "Cliff_Wall_Module_A"
SEED = 20260816

WIDTH, DEPTH_BACK = 11.0, 5.6
CLEAR_Y, MAX_WEST = 4.5, -0.15      # below CLEAR_Y no geometry west of x=0
QUANT_X, QUANT_CREST = 0.16, 0.25

rnd = random.Random(SEED)

# --- five masses: width, crest, depth bias. Deliberately unequal. ------------
#     (z_start, z_end, crest, depth_bias)   z runs south -5.5 -> north +5.5
MASSES = [
    (-5.50, -3.95, 7.00,  0.10),   # south end, medium, strong silhouette
    (-3.95, -2.55, 8.40, -0.55),   # narrow, pushed forward, DOMINANT crest
    (-2.55,  0.55, 7.95,  0.15),   # widest
    ( 0.55,  1.85, 6.30,  0.70),   # short and recessed -> the notch
    ( 1.85,  5.50, 7.50,  0.30),   # north, lower shoulder, simple butt end
]
FRACTURES = [-3.95, -2.55, 0.55, 1.85]      # split lines between masses

def mass_at(z):
    for m in MASSES:
        if m[0] - 1e-6 <= z <= m[1] + 1e-6:
            return m
    return MASSES[-1]

def quant(v, q):
    return round(v / q) * q

# --- columns: irregular spacing, doubled at fractures ------------------------
def make_columns():
    cols, z = [], -WIDTH / 2.0
    cols.append(z)
    while z < WIDTH / 2.0 - 0.2:
        step = rnd.choice([0.30, 0.36, 0.44, 0.52, 0.60, 0.72, 0.86, 0.98])
        z = min(z + step, WIDTH / 2.0)
        # snap near a fracture so the split lands on a real column pair
        for f in FRACTURES:
            if abs(z - f) < 0.26:
                z = f
        if z - cols[-1] > 0.18:
            cols.append(z)
    if abs(cols[-1] - WIDTH / 2.0) > 1e-6:
        cols.append(WIDTH / 2.0)
    out = []
    for c in cols:
        out.append(c)
        if any(abs(c - f) < 1e-6 for f in FRACTURES):
            out.append(c + 0.13)            # doubled column = the fracture face
    return out

COLS = make_columns()

# --- depth clusters: runs of columns sharing a plane -------------------------
cluster_of, cid, left = {}, 0, 0
for i in range(len(COLS)):
    if left == 0:
        left = rnd.choice([2, 3, 3, 4])
        cid += 1
        prev = cluster_of.get(i - 1, 0.0)
        cluster_of[cid] = max(prev - 0.34, min(prev + 0.34, rnd.uniform(-0.30, 0.30)))
    cluster_of[i] = cluster_of[cid]
    left -= 1

def batter(t):
    """West face lean: x grows with height. ~62 degrees overall."""
    return 0.30 + 4.55 * (t ** 1.30)

def column_points(i, z):
    m = mass_at(z if not any(abs(z - (f + 0.13)) < 1e-6 for f in FRACTURES) else z - 0.13)
    crest = quant(m[2] + rnd.uniform(-0.18, 0.18), QUANT_CREST)
    base = -0.40 if m[3] > 0.5 or (i % 7 == 3) else 0.0     # a few masses carried lower

    # each column gets its OWN row heights: no straight row lines
    anchors = [0.0, 0.17, 0.31, 0.45, 0.575, 0.70, 0.80, 0.885, 1.0]
    ts = [0.0]
    for a in anchors[1:-1]:
        if rnd.random() < 0.86:                              # drop only a few rows
            ts.append(min(0.96, max(0.06, a + rnd.uniform(-0.055, 0.055))))
    if rnd.random() < 0.35:                                  # add an extra high row
        ts.append(rnd.uniform(0.62, 0.93))
    ts.append(1.0)
    ts = sorted(set(round(t, 4) for t in ts))

    pts, run_left, run_x = [], 0, None
    for t in ts:
        y = base + (crest - base) * t
        if run_left <= 0:                                    # depth runs: 2-4 rows share a plane
            run_left = rnd.choice([2, 2, 3, 3, 4])
            jitter = rnd.uniform(-0.26, 0.26) * (0.35 + 0.65 * t)
            run_x = batter(t) + m[3] + cluster_of[i] + jitter
        else:
            run_x = run_x + rnd.uniform(-0.05, 0.05)         # near-coplanar drift
        run_left -= 1

        x = quant(run_x, QUANT_X)
        x = max(x, 0.0) if y < CLEAR_Y else max(x, MAX_WEST)
        pts.append((x, y))
    return pts

def zip_columns(A, B, ia, ib, verts):
    """Walk two height-ordered polylines, emitting quads where the rows nearly
    align and triangles where they do not. This is what stops the surface
    reading as a lattice."""
    faces, i, j = [], 0, 0
    while i < len(A) - 1 or j < len(B) - 1:
        ai, bj = min(i, len(A) - 1), min(j, len(B) - 1)
        an = min(i + 1, len(A) - 1); bn = min(j + 1, len(B) - 1)
        ya, yb = A[an][1], B[bn][1]
        if i < len(A) - 1 and j < len(B) - 1 and abs(ya - yb) < 0.38:
            faces.append((ia[ai], ia[an], ib[bn], ib[bj]))   # quad
            i += 1; j += 1
        elif (ya < yb and i < len(A) - 1) or j >= len(B) - 1:
            faces.append((ia[ai], ia[an], ib[bj]))           # triangle
            i += 1
        else:
            faces.append((ia[ai], ib[bn], ib[bj]))
            j += 1
    return faces

def build():
    verts, faces = [], []
    col_idx, back_idx = [], []

    for i, z in enumerate(COLS):
        pts = column_points(i, z)
        idx = []
        for (x, y) in pts:
            idx.append(len(verts)); verts.append((x, -z, y))
        col_idx.append(idx)
        bt = len(verts); verts.append((DEPTH_BACK, -z, pts[-1][1]))
        bb = len(verts); verts.append((DEPTH_BACK, -z, pts[0][1]))
        back_idx.append((bt, bb))

    cols_pts = [[(verts[k][0], verts[k][2]) for k in ci] for ci in col_idx]

    for i in range(len(COLS) - 1):
        faces += zip_columns(cols_pts[i], cols_pts[i + 1], col_idx[i], col_idx[i + 1], verts)
        ti, tj = col_idx[i][-1], col_idx[i + 1][-1]
        bi, bj = col_idx[i][0], col_idx[i + 1][0]
        (bti, bbi), (btj, bbj) = back_idx[i], back_idx[i + 1]
        faces.append((ti, btj, bti))            # top strip, split as two tris
        faces.append((ti, tj, btj))
        faces.append((bti, btj, bbj, bbi))      # back
        faces.append((bi, bbi, bbj, bj))        # bottom

    for end, flip in ((0, False), (len(COLS) - 1, True)):
        ci = col_idx[end]; bt, bb = back_idx[end]
        fan = [(ci[k], ci[k + 1]) for k in range(len(ci) - 1)]
        for a, b in fan:
            faces.append((bb, b, a) if flip else (bb, a, b))
        faces.append((bb, ci[-1], bt) if flip else (bb, bt, ci[-1]))

    me = bpy.data.meshes.new(NAME)
    me.from_pydata(verts, [], faces); me.validate(verbose=False)
    ob = bpy.data.objects.new(NAME, me); bpy.context.collection.objects.link(ob)
    return ob

def finish(ob):
    bpy.context.view_layer.objects.active = ob; ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.remove_doubles(threshold=0.0005)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.shade_flat()
    mat = bpy.data.materials.get("MAT_Cliff_Rock") or bpy.data.materials.new("MAT_Cliff_Rock")
    mat.use_nodes = True
    n = mat.node_tree.nodes.get("Principled BSDF")
    if n:
        n.inputs["Base Color"].default_value = (0.150, 0.170, 0.200, 1.0)
        n.inputs["Roughness"].default_value = 0.96
    mat.diffuse_color = (0.150, 0.170, 0.200, 1.0)
    ob.data.materials.clear(); ob.data.materials.append(mat)
    ob.location = (0, 0, 0); ob.rotation_euler = (0, 0, 0); ob.scale = (1, 1, 1)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob

def report(ob):
    me = ob.data
    xs = [v.co.x for v in me.vertices]; ws = [v.co.y for v in me.vertices]; hs = [v.co.z for v in me.vertices]
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    quads = sum(1 for p in me.polygons if len(p.vertices) == 4)
    tri_f = sum(1 for p in me.polygons if len(p.vertices) == 3)
    worst = min((v.co.x for v in me.vertices if v.co.z < CLEAR_Y), default=0.0)
    for e in me.edges:
        a, b = me.vertices[e.vertices[0]].co, me.vertices[e.vertices[1]].co
        lo, hi = (a, b) if a.z <= b.z else (b, a)
        if lo.z < CLEAR_Y < hi.z:
            t = (CLEAR_Y - lo.z) / (hi.z - lo.z); worst = min(worst, lo.x + t * (hi.x - lo.x))
    areas = sorted(p.area for p in me.polygons)
    bm = bmesh.new(); bm.from_mesh(me)
    nm = len([e for e in bm.edges if not e.is_manifold]); bm.free()
    print(f"[BL] {NAME}: faces {len(me.polygons)} ({quads} quads, {tri_f} tris) -> {tris} triangles")
    print(f"[BL] verts {len(me.vertices)}  columns {len(COLS)}")
    print(f"[BL] size: depth {max(xs)-min(xs):.2f}  height {max(hs)-min(hs):.2f}  width {max(ws)-min(ws):.2f}")
    print(f"[BL] height range {min(hs):.2f}..{max(hs):.2f}   min X {min(xs):+.3f}")
    print(f"[BL] clearance below {CLEAR_Y}: min X {worst:+.3f} {'OK' if worst >= -1e-6 else 'VIOLATION'}")
    print(f"[BL] facet area m2: median {areas[len(areas)//2]:.2f}  p10 {areas[len(areas)//10]:.2f}  p90 {areas[-len(areas)//10]:.2f}")
    print(f"[BL] non-manifold edges {nm}")

def render(ob):
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'
    sc.display.shading.color_type = 'SINGLE'
    sc.display.shading.single_color = (0.30, 0.32, 0.36)
    sc.display.shading.show_object_outline = False
    sc.render.resolution_x, sc.render.resolution_y = 1400, 950
    sc.world = bpy.data.worlds.new("W"); sc.world.color = (0.045, 0.05, 0.06)
    cd = bpy.data.cameras.new("Cam"); cd.type = 'ORTHO'
    cam = bpy.data.objects.new("Cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    centre = Vector((2.6, 0.0, 4.2))
    for name, eye, o in (("west", Vector((-30, 0, 4.2)), 13.5),
                         ("south", Vector((2.6, 30, 4.2)), 12.5),
                         ("threequarter", Vector((-20, 20, 12.5)), 17.0),
                         ("crest", Vector((2.6, 0.01, 34)), 13.5)):
        cam.location = eye; cd.ortho_scale = o
        cam.rotation_euler = (centre - eye).normalized().to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, f"module_{name}.png")
        bpy.ops.render.render(write_still=True)
        print(f"[BL] rendered module_{name}")
    bpy.data.objects.remove(cam, do_unlink=True)

os.makedirs(OUT, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
o = finish(build())
report(o)
render(o)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BASE, f"{NAME}.blend"))
print("[BL] saved")
