# Workshop — Asset Authoring Spec

**Last Beacon** · replacement art shell for the `Workshop` blockout group.
Metres throughout. Derived from `VerticalIslandBlockoutGenerator.cs`.

Set back to the north-east, deliberately angled. Its entrance is a **side door into a
sheltered nook** — intentionally *not* a second broad frontage like the generator shed.

---

## 1. Siting

| | Value |
|---|---|
| World position (origin) | **(19.0, 17.0, 26.2)** |
| Rotation | **Y = −17°** — the strongest rotation of any compound building |
| Scale | 1, 1, 1 |
| Compound floor | Y = **17.0** — local Y 0 sits here |

Origin at **footprint centre, floor level**. The −17° yaw is a composition decision;
please build to local axes and let the transform do the rotating.

---

## 2. Workshop_Body (walls)

| | Value |
|---|---|
| Outer footprint | **11.0 (local X) × 8.0 (local Z)** |
| Wall height | **4.5** |
| Wall thickness | **0.3** |
| Interior clear floor | **10.4 × 7.4** |
| Local extents | X −5.5…+5.5 · Z −4.0…+4.0 · Y 0…4.5 |

- The **11.0 m walls** face ±Z → **gable ends**.
- The **8.0 m walls** face ±X → **eave walls**.

---

## 3. Workshop_Roof

| | Value |
|---|---|
| Footprint | **11.6 × 8.6** → **0.3 m overhang all round** |
| Roof height (eave → ridge) | **1.8** |
| Eave height (world Y) | **21.5** |
| Ridge height (world Y) | **23.3** |
| Pitch | **17.2°** (1.8 rise over 5.8 run) |
| Ridge axis | along **local Z**, at local X = 0, length **8.6** |
| Sloped planes | face **±X** · Gables face **±Z** |

Total height floor to ridge: **6.3 m**.

---

## 4. Doorway

| | Value |
|---|---|
| Which wall | the **−X eave wall** (8.0 m wide) |
| Clear width | **1.6** (along local Z) |
| Clear height | **2.2** |
| Sill | **0.0** — flush, no step |
| Opening spans (local) | Z **−3.3 … −1.7**, Y **0 … 2.2** |
| Opening centre (local) | X −5.35, **Z −2.5** |
| Wall band pierced | local X −5.5 … −5.2 |
| Threshold centre (world) | **(14.47, 17.00, 22.20)** |
| Outward facing (world) | **(−0.956, 0, −0.292)** — west-south-west |

The door is **strongly off-centre**: 2.5 m toward −Z, pushed to the corner of the wall.
That is what makes it read as a side entrance into a nook rather than a frontage. Keep
it there.

---

## 5. The entrance alcove — the defining exterior feature

Everything here sits **outside** the wall line (local X beyond −5.5) and forms a
sheltered outdoor work nook at the door. This is the building's character; please model it.

| Piece | Local XZ | Centre Y | Size (X×Y×Z) | Occupies Y |
|---|---|---|---|---|
| `Workshop_AlcoveCanopy` | (−6.9, −2.5) | 2.6 | 3.0 × 0.2 × 3.0 | 2.5 – 2.7 |
| `Workshop_AlcovePost` | (−8.2, −3.8) | 1.3 | 0.2 × 2.6 × 0.2 | 0.0 – 2.6 |
| `Workshop_BenchProp` | (−6.6, −1.2) | 0.5 | 2.4 × 1.0 × 1.0 | 0.0 – 1.0 |

Note the **workbench is outside**, under the canopy — not indoors. The canopy reaches
to local X −8.4 and the post stands at the outer corner. Clear headroom under the
canopy is 2.5 m.

## 6. Interior fixtures that must still fit

| Fixture | Local XZ | Centre Y | Size (X×Y×Z) | Occupies Y |
|---|---|---|---|---|
| `Workshop_ToolRack` | (+1.0, +3.6) | 1.5 | 4.0 × 1.6 × 0.3 | 0.7 – 2.3 |
| `Workshop_ScrapBin` | (+4.0, +3.0) | 0.5 | 1.8 × 1.0 × 1.8 | 0.0 – 1.0 |

The 4 m tool rack sits flat against the inside of the **+Z gable wall** — leave that
wall face clear from 0.7 to 2.3 across local X −1 … +3.

## 7. Gameplay markers — must stay reachable

| Marker | World position | Note |
|---|---|---|
| `Workshop_Bench` | **(13.04, 18.10, 23.12)** | trap repair + ammo crafting (GDD 24) |

Only one marker, but it is a primary task station — the player must be able to stand at
the bench under the canopy and work. Do not enclose the alcove.

Exterior practical `Lamp_Workshop`: **(13.60, 20.60, 21.90)**.

## 8. Service space to the east (context, do not model)

Between the workshop and the generator shed there is a service yard with loose props at
world (21.4 … 23.6, ~17.5, 20.6 … 23.4) — scrap pile, pipe run, spare parts crate. The
workshop's **+Z / +X** faces look onto it, so those elevations should read as working,
serviceable backs rather than finished frontage.

## 9. Format, export, materials

Identical to the Stores spec — see `STORES_ASSET_SPEC.md` §8. In brief:

- Blender Z-up, metres, origin at footprint centre / floor level, **door in the −X wall**
- Local **X = the 11.0 m axis**, local **Y = the 8.0 m axis**, **Z = up**
- Apply all transforms; flat shade or auto-smooth 30°; 1 UV unit per metre
- FBX: `apply_unit_scale=True, global_scale=1.0, apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE'`
- Name pieces `SM_Workshop_<Piece>`; door leaf its own object, **origin on the hinge edge**
- Materials, auto-remapped by name: `MAT_Wood_Wet` · `MAT_Wood_Painted` · `MAT_Metal` ·
  `MAT_Metal_Painted` · `MAT_Concrete` · `MAT_Rust` · `MAT_Glass` · `MAT_Emissive_Warm`
- Blockout reads this as **wood walls + metal roof**
- **Triangle budget: 2000 – 3000** for the shell. No colliders, no lights, no cameras.

## 10. What must not change

- Position (19.0, 17.0, 26.2), rotation Y = −17°
- Footprint 11.0 × 8.0, wall height 4.5
- Door: −X wall, 1.6 × 2.2, sill 0, centred local Z −2.5
- Roof ridge along local Z, eave 21.5, ridge 23.3
- The alcove stays open, and `Workshop_Bench` at (13.04, 18.10, 23.12) stays usable
