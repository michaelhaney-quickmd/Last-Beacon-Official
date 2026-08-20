# Stores / Radio — Asset Authoring Spec

**Last Beacon** · target: a replacement art shell for the `StoresRadio` blockout group.
All units are **metres**. All figures derived from `VerticalIslandBlockoutGenerator.cs`
(the script that generates the scene).

---

## 1. Siting

| | Value |
|---|---|
| World position (origin) | **X −17.5, Y 17.0, Z 8.5** |
| Rotation | **Y = +18°** (X and Z rotations are zero) |
| Scale | 1, 1, 1 |
| Compound floor level | Y = **17.0** — the building's Y 0 sits exactly here |

`Y = 17.0` is the compound plateau. Do not change it.

The origin is at the **footprint centre, at floor level** — not at a corner, not at the
centroid of the mass. Author to that and the asset drops in with no correction.

---

## 2. Stores_Body (walls)

| | Value |
|---|---|
| Outer footprint | **9.0 (local X) × 7.0 (local Z)** |
| Wall height | **3.8** (floor to top of wall / eave line) |
| Wall thickness | **0.3** |
| Interior clear floor | **8.4 × 6.4** |
| Interior clear height | **3.8** to the underside of the roof |
| Local extents | X −4.5…+4.5 · Z −3.5…+3.5 · Y 0…3.8 |

Walls sit **inside** the footprint: outer face of every wall is exactly on the
9.0 × 7.0 boundary.

- The two **9.0 m walls** face ±Z. These are the **gable ends**.
- The two **7.0 m walls** face ±X. These are the **eave walls**.

---

## 3. Stores_Roof

| | Value |
|---|---|
| Footprint | **9.6 × 7.6** → **0.3 m overhang on all four sides** |
| Roof height (eave → ridge) | **1.2** |
| Eave height (world Y) | **20.8** |
| Ridge height (world Y) | **22.0** |
| Roof pitch | **14.0°** (1.2 rise over 4.8 run) |
| Ridge axis | runs along **local Z**, at local X = 0, length **7.6** |
| Sloped planes | face **±X** |
| Triangular gables | face **±Z** |

Shallow roof — 14°, not a steep gable. Overall building height is **5.0 m** floor to ridge.

---

## 4. Doorway — this is the critical one

| | Value |
|---|---|
| Which wall | the **+X eave wall** (7.0 m wide), *not* a gable end |
| Clear width | **1.6** (measured along local Z) |
| Clear height | **2.2** |
| Sill | **0.0** — flush with the floor, no threshold step |
| Opening centre (local) | X +4.35, **Z +1.5** |
| Opening spans (local) | Z **+0.7 … +2.3**, Y **0 … 2.2** |
| Wall band it pierces | local X 4.2 … 4.5 |
| Threshold centre (world) | **(−12.76, 17.00, 8.54)** |
| Outward facing (world) | **(0.951, 0, −0.309)** — east-south-east |

The opening is **off-centre**: 1.5 m toward +Z from the wall's midpoint. That
asymmetry is deliberate — the door is angled to acknowledge both the yard and the
gate arrival — so please keep it where it is.

There is a small **canopy** over the door: centred local (5.4, 1.5), size
1.4 (X) × 0.2 (Y) × 2.4 (Z), centre height 2.4 above floor — it projects outside
the wall to local X 6.1.

---

## 5. Interior fixtures that must still fit

Local XZ, height is the **centre** above floor, size is X × Y × Z.

| Fixture | Local XZ | Centre Y | Size | Occupies Y |
|---|---|---|---|---|
| `Stores_RadioSet` | (−3.0, −2.0) | 1.1 | 1.6 × 1.2 × 0.8 | 0.5 – 1.7 |
| `Stores_ManifestDesk` | (−3.0, 0.0) | 0.5 | 1.6 × 1.0 × 1.2 | 0.0 – 1.0 |
| `Cabinet_Ammunition` | (+3.0, −2.2) | 0.8 | 1.8 × 1.6 × 0.8 | 0.0 – 1.6 |
| `Stores_DeliveryShelf` | (0.0, +2.6) | 0.8 | 3.0 × 1.6 × 0.8 | 0.0 – 1.6 |

You do **not** need to model these — they are separate blockout props. But don't put
a wall, post, or interior partition through those volumes.

## 6. Gameplay markers — must stay reachable

World positions. A player must be able to stand next to each one and look at it.

| Marker | World position |
|---|---|
| `Radio_Point` | (−20.97, 18.70, 7.53) |
| `Manifest_Point` | (−20.35, 18.10, 9.43) |
| `Ammo_Storage` | (−15.33, 18.70, 5.48) |
| `Delivery_Records` | (−16.70, 18.70, 10.97) |

Exterior practical light `Lamp_Stores` is at **(−11.90, 20.60, 8.30)** — just outside
and above the door. If you model a wall lamp, put it there.

## 7. Player metrics — size clearances against these

| | Value |
|---|---|
| Capsule radius | 0.35 (0.70 m wide) |
| Capsule height | 1.80 |
| Step offset | 0.45 |
| Slope limit | 50° |
| Walk speed | 4.5 m/s |

The 1.6 m door passes two players abreast. Keep interior circulation ≥ 1.0 m.

---

## 8. Deliverable format

### Build in Blender with

- **Z up, Y forward** (Blender default), metres, scene unit scale 1.0
- Origin at **footprint centre, floor level** (see §1)
- The **door in the +X wall** in local space, matching §4
- Local **X = the 9.0 m axis**, local **Y = the 7.0 m axis**, **Z = up**
- Apply all object transforms before export (`Object ▸ Apply ▸ All Transforms`)
- Flat shading, or auto-smooth at 30°
- UVs: cube-projected is fine; keep texel density consistent — use **1 UV unit per metre**

### Export FBX with exactly these settings

```
use_selection        = True
apply_unit_scale     = True
global_scale         = 1.0
apply_scale_options  = 'FBX_SCALE_NONE'
axis_forward         = '-Z'
axis_up              = 'Y'
object_types         = {'MESH'}
use_mesh_modifiers   = True
mesh_smooth_type     = 'FACE'
add_leaf_bones       = False
bake_anim            = False
```

### Two traps I hit on the generator shed — worth avoiding

1. **Multi-object FBX scale.** On the Unity side a multi-object FBX must import with
   `useFileScale = true`. With `false`, only the root is neutralised and **every child
   node arrives at 100× scale** — the shed came in 684 m wide. A single-mesh FBX
   behaves the opposite way. I handle this on import; just be aware that if you send
   one merged mesh vs. many pieces, the correct setting differs.
2. **Child transforms are useless for orientation.** If you apply all transforms
   (which you should), every FBX child ends up sharing the root's position, so
   nothing can be located by `transform.position` — only by renderer bounds. Doesn't
   affect you; it just means **the door must be where §4 says**, since I can't infer
   facing from the file.

### Modularity

Separate objects are welcome and preferred — wall panels, roof panels, door, window,
trim, posts. Name them `SM_Stores_<Piece>`. Keep the door leaf as its own object so it
can be hinged; put its **origin on the hinge edge**, not the panel centre.

### Materials

Use these names and they will auto-remap to existing URP materials on import — no
setup needed:

`MAT_Wood_Wet` · `MAT_Wood_Painted` · `MAT_Metal` · `MAT_Metal_Painted` ·
`MAT_Concrete` · `MAT_Rust` · `MAT_Glass` · `MAT_Emissive_Warm`

Palette is cool blue-grey exterior, warm interior (2400–2700 K practicals). The
blockout currently reads Stores as **wood walls + metal roof**.

### Triangle budget

**1500 – 2500 tris** for the shell. For reference the generator shed is 920 tris at
3.6 × 4.2 m; Stores has roughly four times the floor area, so this is proportionate.
No colliders — the blockout keeps collision. Don't include lights or cameras.

---

## 9. What must not change

- World position (−17.5, 17.0, 8.5) and rotation Y = +18°
- Outer footprint 9.0 × 7.0 and wall height 3.8
- Door: +X wall, 1.6 × 2.2, centred local Z +1.5, sill 0
- Roof ridge along local Z, eave 20.8, ridge 22.0
- The four gameplay marker positions in §6

Everything else — plank language, trim, window placement, vents, pipework, surface
detail, silhouette breakup — is yours.
