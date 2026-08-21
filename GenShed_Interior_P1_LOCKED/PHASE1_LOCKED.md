# GENERATOR SHED INTERIOR — PHASE 1 LOCKED

**Revision 2 — two approved corrections applied and revalidated.**

Locked 2026-08-20. Architectural shell and gameplay placeholder layout only.
No final props, textures, lighting, or generator detail.

Source of truth: `Blender/SM_GeneratorShed_Interior.blend`
(snapshot in this package as `SM_GeneratorShed_Interior_P1_LOCKED.blend`)

Every number below was read back off the built mesh, not copied from the brief.

## Frame

Positions are quoted in **blockout-local** coordinates — the frame the spec is
written in. The Blender source is rotated 180° about Z from it, measured rather
than assumed: the art doorway sits at Blender +X while the blockout doorway spec
is local −X, and the art awning sits at Blender +Y while the blockout calls it
the −Z gable. So `blender_x = −local_x` and `blender_y = −local_z`.

The doorway is on the **−X** wall. The electrical wall is **+X**, opposite it.

## Shell

| | measured | spec |
|---|---|---|
| Exterior footprint | 10.000 × 8.000 m | 10.0 × 8.0 |
| **Clear interior dimensions** | **9.400 × 7.400 m** | 9.4 × 7.4 |
| Wall thickness | 0.300 m | 0.30 |
| Wall height (eave) | 4.200 m | 4.2 |
| Interior ridge (ceiling underside) | 5.088 m | — |
| Ceiling pitch | 10.70° | 10.7° |

The 5.2 m in the brief is the **exterior** floor-to-ridge height. The interior
ridge is 5.088 m: a true 10.70° plane springing from the eave at the interior
wall line. It nests 6–7 mm under the approved exterior roof underside across the
whole span, so the ceiling is a 15 mm deck skin — the roof leaves only 22 mm of
room there.

## Doorway

| | measured |
|---|---|
| **Width** | **3.500 m** (jamb faces at local z −1.750 / +1.750) |
| **Height** | **3.200 m** |
| Sill | 0.000 |
| Header | 1.000 m |

## Gameplay placeholders

| | position (local x, z) | footprint | height range |
|---|---|---|---|
| **Generator body** | **(+0.500, 0.000)** | **2.000 × 3.200 m** | 0.000 – 1.800 |
| **Fuel cap** | **(+1.100, 0.000)** | 0.700 × 0.700 m | 1.800 – 2.100 |
| **Breaker** | **(+4.530, −2.500)** | 0.350 deep × 0.900 wide | 0.800 – 2.200 |
| **Fuse panel** | **(+4.530, −1.100)** | 0.350 deep × 0.900 wide | 0.900 – 2.100 |

**Generator rotation: yawed 90° from the original blockout footprint — CANONICAL.**
The 3.200 m face fronts the doorway; the 2.000 m dimension runs along the door
axis. Centre unchanged at (+0.500, 0.000). The generator inherits the shed's own
−5° yaw on top of this; the 90° is relative to the shed.

**Fuel cap** moved from the original (+1.700, 0.000). At that position it would
hang off the yawed deck with 0.15 m of its 0.70 m still supported. It keeps its
end of the generator and its 0.05 m inset from that end, preserving the physical
relationship. Approved.

**Generator_RepairPoint — CANONICAL at local (+0.500, −2.200).**
Corrected from (+0.500, −1.500), which the approved 90° yaw had swallowed: the
yawed body spans local z −1.6…+1.6, so the old point stood 0.1 m *inside* the
machine. Verified after the move:

| | |
|---|---|
| Distance from the −Z service face | **0.600 m** (target band 0.5–0.7) |
| Player capsule (0.35 m radius) to generator | **0.250 m** clear |
| Plan gap to the generator | +0.600 m, so the capsule stands clear |
| Doorway circulation | unaffected — the doorway spans local z ±1.75 at x = −5.0; this marker is 5.5 m away along x |
| Interaction line of sight | clear; nothing between the marker and the service face |

The other two generator markers were re-measured at the same time:
`Generator_StartPoint` (−1.000, +0.900) stands 0.500 m clear, and
`Generator_FuelPoint` (+1.100, 0.000) is top access at 2.1 m, over the deck
rather than clear of it in plan.

**Breaker and fuse panel** are wall-mounted: the 0.350 face is the depth and the
0.900 face is the width, with the back face embedded 5 mm into the +X wall
(interior face at 4.700) so it cannot z-fight. The original x of +3.500 stood
them 1.025 m clear of the wall they are specified to mount on. Cross-wall
positions (−2.500, −1.100) and both heights are unchanged.

## Circulation — player capsule 0.70 m wide, 1.80 m tall

| route | clear | minimum | |
|---|---|---|---|
| **Doorway-side clearance** | **4.200 m** | 1.000 | PASS |
| **Electrical-wall clearance** | **2.855 m** | 1.000 | PASS |
| Generator ↔ gable walls | 2.100 / 2.100 m | — | |
| **Narrowest circulation clearance** | **2.100 m** | 1.000 | PASS |
| Lowest overhead steel | 4.020 m | 1.800 | PASS |

The narrowest route is the side circulation, not the electrical approach — the
better trade, since the electrical wall is where the interactions are.

## Generator visibility

**Result: FULLY VISIBLE — 8 of 8 corners inside the door aperture.**

Measured by projecting the generator's silhouette through the doorway plane from
an eye at 1.70 m standing 3.30 m outside the opening. Nothing occludes it.

Partial occlusion of the fuse panel from outside the doorway is accepted as part
of this lock.

## Structure

- 1 ridge member, 0.14 × 0.24 m, along the ridge at local x = 0, span 7.40 m
- 3 rafters, 0.10 × 0.18 m, at local z = −2.6, 0.0, +2.6
- 0 pilasters — rafters bear directly on the 0.30 m walls. The pair originally
  drawn on the electrical wall fouled the approved breaker volume; removing them
  was the fix that did not move an approved gameplay position.
- No other overhead members. Shell total: **274 triangles**.

## Views

| file | view |
|---|---|
| `01_doorway_view_inward.png` | from outside the opening, looking in at 1.70 m eye height |
| `02_plan_topdown.png` | top-down plan, cut at 1.60 m |
| `03_rearcorner_three_quarter.png` | rear corner three-quarter, looking back toward the doors |
| `04_generator_to_electrical_wall.png` | beside the generator, facing the electrical wall |
| `05_side_section_elevation.png` | side section showing wall and roof heights |

Neutral review materials and flat review lighting throughout. These are **not**
the target look — the concept image is a Phase 2+ art target.

## Carried into Unity

The blockout generator prop was turned to match and the scene regenerated. Scene
mesh now reads 2.000 × 1.800 × 3.200 (x, height, z), and the fuel cap moved with
it. The blockout shed shell renderers are off again after the regenerate, so the
z-fighting fix against the art shell still holds.

## Roof envelope — corrected

The exterior art `SM_GeneratorShed_Body` carried its own roof deck whose inner
cap ran 4.200 at the wall line to 4.895 at the ridge: an **8.41°** plane, not the
approved 10.70, sitting **0.208 m below** this interior ceiling at the ridge.
That was the clip. The interior was not shrunk to fit it.

The cap was moved onto the approved envelope by a **pure vertex move** — two
ridge vertices and twelve eave vertices, no faces added or removed:

| | before | after |
|---|---|---|
| Inner cap at the wall line | 4.200 | **4.245** |
| Inner cap at the ridge | 4.895 | **5.133** |
| Inner cap pitch | 8.41° | **10.70°** |

**Verified after the correction:**

| check | result |
|---|---|
| Body inner cap → interior ceiling deck top | **+0.030 m, constant** across the span |
| Roof underside → interior ceiling deck top | **+0.006 m** (tightest clearance in the assembly) |
| Exterior vertices intruding below the locked interior ceiling | **0** |
| Body vertices punching through the roof top surface | **0** (worst is 12 mm inside) |
| Non-manifold edges after the move | **0** |
| Body triangle count | 292 → **292** |
| Body bounds | X ±5.100, Y ±4.100, Z 0…**5.200** — unchanged |

Footprint, wall locations, doorway, door system, lean-to and roof silhouette are
all untouched, and no face was added or deleted, so every material assignment
survives. UVs stretch slightly on the two cap quads and the two inner gable
faces — all interior-facing surfaces that Phase 2 will re-treat anyway.

The corrected FBX was re-exported and the Unity material match, retire-shell,
collision and lean-to passes re-run. In game the interior ceiling is now the roof
underside at the approved pitch rather than the old 8.41° cap.

## Open flags for Phase 2

1. **Interior and exterior wall planes are coincident.** The Phase 1 interior
   shell carries its own walls on exactly the same planes as the exterior Body
   (inner x ±4.700 / y ±3.700, outer ±5.000 / ±4.000). Merging both as-is will
   z-fight across every wall. This is a merge decision, not a layout one: either
   the interior contributes only floor, ceiling, plinth and steel and leans on
   the Body's walls, or the Body's interior-facing wall skin comes out. Out of
   scope for the roof correction, which was scoped to roof/ceiling surfaces.
2. The electrical wall is the far wall opposite the doors, per the approved
   coordinates. The concept image puts the panels on a side wall. Locked as-is
   by instruction — the concept is visual inspiration only.

## Test suite

72 EditMode tests: **70 passed, 2 failed. No regressions** — see the report
accompanying this revision for the evidence that both failures pre-date these
corrections.

A new regression guard was added,
`GeneratorInteractionMarkers_StandOutsideTheMachineAndWithinReach`, which asserts
the generator's service markers stand outside the machine and within reach, and
that the fuel point sits above it. It passes. Nothing previously asserted marker
*positions* — only that the names existed — which is exactly why the yaw was able
to swallow the repair point unnoticed.
