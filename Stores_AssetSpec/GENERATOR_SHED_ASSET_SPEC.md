# Generator Shed — Asset Authoring Spec

**Last Beacon** · replacement art shell for the `GeneratorShed` blockout group.
Metres throughout. Derived from `VerticalIslandBlockoutGenerator.cs`.

The industrial frontage: broad opening turned **west onto the yard**, the most readable
workspace in the compound.

---

## ⚠ Read this first — unresolved conflict

There are **two competing specs** for this building:

| | Footprint | Wall height | Door |
|---|---|---|---|
| **Blockout** (this document) | 10.0 × 8.0 | 4.2 | 3.5 × 3.2 |
| **Reference board** (the one already built) | 3.6 × 4.2 | 2.4 (eave) | 1.6 × 2.1 |

The asset I already built from the reference board covers **39% of the blockout's floor
area** and was reverted for that reason. **Decide which is authoritative before
authoring.** This document specifies the **blockout** version. If the reference board
wins instead, the blockout needs shrinking and the existing asset can be re-imported as-is
(Tools ▸ Last Beacon ▸ Import Generator Shed).

---

## 1. Siting

| | Value |
|---|---|
| World position (origin) | **(17.0, 17.0, 13.0)** |
| Rotation | **Y = −5°** |
| Scale | 1, 1, 1 |
| Compound floor | Y = **17.0** — local Y 0 sits here |

Origin at **footprint centre, floor level**.

---

## 2. Shed_Body (walls)

| | Value |
|---|---|
| Outer footprint | **10.0 (local X) × 8.0 (local Z)** |
| Wall height | **4.2** |
| Wall thickness | **0.3** |
| Interior clear floor | **9.4 × 7.4** |
| Local extents | X −5.0…+5.0 · Z −4.0…+4.0 · Y 0…4.2 |

- The **10.0 m walls** face ±Z → **gable ends**.
- The **8.0 m walls** face ±X → **eave walls**.

Blockout material is **concrete** for this one — it is the utilitarian building.

---

## 3. Shed_Roof

| | Value |
|---|---|
| Footprint | **10.6 × 8.6** → **0.3 m overhang all round** |
| Roof height (eave → ridge) | **1.0** |
| Eave height (world Y) | **21.2** |
| Ridge height (world Y) | **22.2** |
| Pitch | **10.7°** (1.0 rise over 5.3 run) — the shallowest roof in the compound |
| Ridge axis | along **local Z**, at local X = 0, length **8.6** |
| Sloped planes | face **±X** · Gables face **±Z** |

Total height floor to ridge: **5.2 m**. Nearly flat — read it as an industrial shed roof,
not a pitched cottage.

---

## 4. Doorway — the wide one

| | Value |
|---|---|
| Which wall | the **−X eave wall** (8.0 m wide) |
| Clear width | **3.5** (along local Z) |
| Clear height | **3.2** |
| Sill | **0.0** — flush, vehicles/equipment roll in |
| Opening spans (local) | Z **−1.75 … +1.75**, Y **0 … 3.2** |
| Opening centre (local) | X −4.85, Z 0.0 |
| Wall band pierced | local X −5.0 … −4.7 |
| Threshold centre (world) | **(12.02, 17.00, 12.56)** |
| Outward facing (world) | **(−0.996, 0, −0.087)** — due west onto the yard |

3.5 m wide and 3.2 m tall — this is a **roller/double-leaf equipment opening**, centred
on the wall, and it is the reason the building reads as the workspace. It takes 44% of
the 8 m wall. Keep the width; it also satisfies the "wide enough for 1–2 players"
gameplay note with plenty of margin.

---

## 5. Interior fixtures that must still fit

| Fixture | Local XZ | Centre Y | Size (X×Y×Z) | Occupies Y |
|---|---|---|---|---|
| `Generator_Body` | (+0.5, 0.0) | 0.9 | 3.2 × 1.8 × 2.0 | 0.0 – 1.8 |
| `Generator_FuelCap` | (+1.7, 0.0) | 1.95 | 0.7 × 0.3 × 0.7 | 1.8 – 2.1 |
| `Generator_Breaker` | (+3.5, −2.5) | 1.5 | 0.9 × 1.4 × 0.35 | 0.8 – 2.2 |
| `Generator_FusePanel` | (+3.5, −1.1) | 1.5 | 0.9 × 1.2 × 0.35 | 0.9 – 2.1 |

The generator is a **3.2 × 2.0 × 1.8 m** mass just off centre, directly in line with the
doorway — it must stay the focal point seen from the yard. Breaker and fuse panel are
flat against the inside of the **+X wall**; leave that face clear from 0.8 to 2.2 across
local Z −2.8 … −0.7.

## 6. Lean-to on the north face — exterior, please model

Sits outside the +Z wall (local Z beyond 4.0), opening onto the service space.

| Piece | Local XZ | Centre Y | Size (X×Y×Z) | Occupies Y |
|---|---|---|---|---|
| `Shed_LeanToRoof` | (0.0, +4.7) | 2.9 | 6.0 × 0.2 × 1.4 | 2.8 – 3.0 |
| `Shed_LeanToPost_W` | (−2.8, +5.3) | 1.4 | 0.2 × 2.8 × 0.2 | 0.0 – 2.8 |
| `Shed_LeanToPost_E` | (+2.8, +5.3) | 1.4 | 0.2 × 2.8 × 0.2 | 0.0 – 2.8 |
| `Shed_FuelDrum_A` | (−1.6, +4.7) | 0.45 | 0.8 × 0.9 × 0.8 | 0.0 – 0.9 |
| `Shed_FuelDrum_B` | (−0.6, +4.7) | 0.45 | 0.8 × 0.9 × 0.8 | 0.0 – 0.9 |

A 6 m × 1.4 m open canopy on two posts, 2.8 m clear, sheltering two fuel drums. It
reaches local Z 5.4.

## 7. Gameplay markers — must stay reachable

Four task stations, the densest cluster in the compound.

| Marker | World position | Purpose |
|---|---|---|
| `Generator_StartPoint` | (15.93, 18.40, 13.81) | prime and start |
| `Generator_FuelPoint` | (18.69, 19.10, 13.15) | pour fuel can |
| `Generator_RepairPoint` | (17.63, 18.00, 11.55) | damage repair panel |
| `Fuse_Storage` | (20.58, 18.50, 12.21) | generator fuse panel |
| `Generator_Breaker` | (20.70, 18.50, 10.81) | breaker |

All five must be approachable on foot with a 0.7 m wide capsule. Keep interior
circulation around the generator ≥ 1.0 m on the door side and the +X wall side.

Exterior practical `Lamp_GeneratorShed`: **(11.20, 20.60, 12.60)** — outside the big
opening.

## 8. Format, export, materials

Identical to the Stores spec — see `STORES_ASSET_SPEC.md` §8. In brief:

- Blender Z-up, metres, origin at footprint centre / floor level, **door in the −X wall**
- Local **X = the 10.0 m axis**, local **Y = the 8.0 m axis**, **Z = up**
- Apply all transforms; flat shade or auto-smooth 30°; 1 UV unit per metre
- FBX: `apply_unit_scale=True, global_scale=1.0, apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE'`
- Name pieces `SM_GenShed_<Piece>`; door leaves their own objects, **origin on the hinge edge**
- Materials, auto-remapped by name: `MAT_Wood_Wet` · `MAT_Wood_Painted` · `MAT_Metal` ·
  `MAT_Metal_Painted` · `MAT_Concrete` · `MAT_Rust` · `MAT_Glass` · `MAT_Emissive_Warm`
- Blockout reads this as **concrete walls + metal roof** (the reference board said wood
  plank walls — another point the two specs disagree on)
- **Triangle budget: 1800 – 2800** for the shell. No colliders, no lights, no cameras.

## 9. What must not change

- Position (17.0, 17.0, 13.0), rotation Y = −5°
- Footprint 10.0 × 8.0, wall height 4.2
- Door: −X wall, **3.5 × 3.2**, sill 0, centred on the wall
- Roof ridge along local Z, eave 21.2, ridge 22.2
- The generator's 3.2 × 2.0 × 1.8 volume stays clear and stays visible from the yard
- The five marker positions in §7
