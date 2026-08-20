# Keeper's House — Asset Authoring Spec

**Last Beacon** · replacement art shell for the `KeepersHouse` blockout group.
Metres throughout. Derived from `VerticalIslandBlockoutGenerator.cs`.

This is the **largest** compound building and the domestic frontage onto the yard.

---

## 1. Siting

| | Value |
|---|---|
| World position (origin) | **X −18.0, Y 20.5→ see note, Z 20.5** |
| Actual origin | **(−18.0, 17.0, 20.5)** |
| Rotation | **Y = −3°** |
| Scale | 1, 1, 1 |
| Compound floor | Y = **17.0** — the building's local Y 0 sits here |

Origin at **footprint centre, floor level**.

---

## 2. House_Body (walls)

| | Value |
|---|---|
| Outer footprint | **12.0 (local X) × 9.0 (local Z)** |
| Wall height | **5.5** |
| Wall thickness | **0.3** |
| Interior clear floor | **11.4 × 8.4** |
| Local extents | X −6.0…+6.0 · Z −4.5…+4.5 · Y 0…5.5 |

- The **12.0 m walls** face ±Z → **gable ends**.
- The **9.0 m walls** face ±X → **eave walls**.

Tallest walls in the compound at 5.5 m — this building reads two-storey-ish in
silhouette even though the interior is one volume.

---

## 3. House_Roof

| | Value |
|---|---|
| Footprint | **12.6 × 9.6** → **0.3 m overhang all round** |
| Roof height (eave → ridge) | **2.6** |
| Eave height (world Y) | **22.5** |
| Ridge height (world Y) | **25.1** |
| Pitch | **22.4°** (2.6 rise over 6.3 run) — the steepest roof in the compound |
| Ridge axis | along **local Z**, at local X = 0, length **9.6** |
| Sloped planes | face **±X** · Gables face **±Z** |

Total height floor to ridge: **8.1 m**.

---

## 4. Doorway — note the raised sill

| | Value |
|---|---|
| Which wall | the **+X eave wall** (9.0 m wide) |
| Clear width | **1.4** (along local Z) |
| Clear height | **2.2** |
| **Sill** | **0.3** — the opening starts 0.3 above the floor |
| Opening spans (local) | Z **−0.7 … +0.7**, Y **0.3 … 2.5** |
| Opening centre (local) | X +5.85, Z 0.0 |
| Wall band pierced | local X 5.7 … 6.0 |
| Threshold centre (world) | **(−12.01, 17.00, 20.81)** |
| Outward facing (world) | **(0.999, 0, 0.052)** — due east onto the yard |

**The 0.3 sill is not decorative and must be preserved.** The porch deck outside is
0.3 thick, so its top surface is at local Y 0.3. The opening is carried up with the
sill, giving **2.2 m of clear headroom measured from the porch deck you actually stand
on**. This was a bug earlier in development — the opening was originally cut from
floor level, so the porch ate 0.3 m of it and the door measured 1.9 m clear. If you
cut the opening 0.3–2.5 you reproduce the fix; if you cut it 0.0–2.2 you reintroduce
the bug.

---

## 5. Porch (exterior, part of the composition)

| Piece | Local XZ | Centre Y | Size (X×Y×Z) | Occupies Y |
|---|---|---|---|---|
| `House_Porch` (deck) | (7.4, 0.0) | 0.15 | 2.8 × 0.3 × 4.2 | 0.0 – 0.3 |
| `House_PorchCanopy` | (7.4, 0.0) | 2.8 | 2.8 × 0.2 × 4.2 | 2.7 – 2.9 |
| `House_PorchPost_N` | (8.6, +1.8) | 1.5 | 0.2 × 2.6 × 0.2 | 0.2 – 2.8 |
| `House_PorchPost_S` | (8.6, −1.8) | 1.5 | 0.2 × 2.6 × 0.2 | 0.2 – 2.8 |

The porch projects to local X 8.8 — 2.8 m beyond the wall. Please model it: it is the
domestic signal that distinguishes this building from the industrial pair.

## 6. Windows (in the blockout)

| Piece | Local XZ | Centre Y | Size | Wall |
|---|---|---|---|---|
| `House_Window_N` | (−5.9, +3.0) | 2.4 | 0.3 × 1.4 × 1.4 | −X eave wall |
| `House_Window_S` | (−5.9, −3.0) | 2.4 | 0.3 × 1.4 × 1.4 | −X eave wall |

Two 1.4 × 1.4 windows, sill at 1.7, head at 3.1, on the **rear (−X)** wall. Add more
windows if the composition wants them — these two are the only ones the blockout
commits to.

## 7. Interior fixtures that must still fit

| Fixture | Local XZ | Centre Y | Size (X×Y×Z) | Occupies Y |
|---|---|---|---|---|
| `House_Bunks` | (−3.6, +2.0) | 0.6 | 3.6 × 1.2 × 2.0 | 0.0 – 1.2 |
| `Cabinet_Medical` | (+5.4, −2.0) | 0.8 | 0.8 × 1.6 × 1.6 | 0.0 – 1.6 |
| `House_IncidentBoard` | (+5.4, +3.4) | 1.6 | 0.2 × 1.4 × 2.0 | 0.9 – 2.3 |
| `House_StationClock` | (+5.4, +2.0) | 2.6 | 0.25 × 0.9 × 0.9 | 2.15 – 3.05 |

Don't run a wall or partition through these. The clock sits high on the +X wall beside
the door — leave that wall face clear above 2.1.

## 8. Gameplay markers — must stay reachable

| Marker | World position |
|---|---|
| `Bunks_Point` | (−21.70, 18.40, 22.31) |
| `Medical_Storage` | (−12.50, 18.70, 18.79) |
| `StationClock_Point` | (−12.71, 19.60, 22.78) |
| `IncidentBoard_Point` | (−12.79, 18.60, 24.18) |

Exterior practical `Lamp_KeepersHouse`: **(−11.20, 21.40, 20.90)** — outside the porch,
4.4 m above the floor.

---

## 9. Format, export, materials

Identical to the Stores spec — see `STORES_ASSET_SPEC.md` §8. In brief:

- Blender Z-up, metres, origin at footprint centre / floor level, **door in the +X wall**
- Local **X = the 12.0 m axis**, local **Y = the 9.0 m axis**, **Z = up**
- Apply all transforms before export; flat shade or auto-smooth 30°; 1 UV unit per metre
- FBX: `apply_unit_scale=True, global_scale=1.0, apply_scale_options='FBX_SCALE_NONE', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE'`
- Name pieces `SM_House_<Piece>`; door leaf as its own object with its **origin on the hinge edge**
- Materials, auto-remapped by name: `MAT_Wood_Wet` · `MAT_Wood_Painted` · `MAT_Metal` ·
  `MAT_Metal_Painted` · `MAT_Concrete` · `MAT_Rust` · `MAT_Glass` · `MAT_Emissive_Warm`
- Blockout reads this building as **wood walls + plank roof** (not metal — it is the
  domestic one)
- **Triangle budget: 2200 – 3200** for the shell. No colliders, no lights, no cameras.

## 10. What must not change

- Position (−18.0, 17.0, 20.5), rotation Y = −3°
- Footprint 12.0 × 9.0, wall height 5.5
- Door: +X wall, 1.4 × 2.2, **sill 0.3**, centred local Z 0
- Roof ridge along local Z, eave 22.5, ridge 25.1
- The four marker positions in §8
