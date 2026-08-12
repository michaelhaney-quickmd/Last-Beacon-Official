# LAST BEACON — Claude Code Build Workflow

## How to use this document

This is the step-by-step plan for building **Last Beacon** with Claude Code. Work through the phases in order — each one produces something playable before moving on. Don't skip ahead to combat or enemies before the compound and player systems actually work.

At the start of every new Claude Code session, tell it to read `LAST BEACON.md` (the GDD) first. That file is the source of truth for scope and rules — this document is the order of operations.

General habits that keep this project healthy:

- Commit to git after every phase (or sub-step) that leaves the game in a working, launchable state.
- Ask Claude Code to build the smallest version of a feature first, test it, then refine — not the full-featured version in one pass.
- If Claude proposes something not in the GDD (new mechanic, bigger map, extra system), have it flag that explicitly and get your sign-off before it builds it. This is already Rule 3/10 in the GDD's Claude Code Rules section — hold it to that.
- Playtest after every phase, even solo. Compact co-op games reveal problems fast when you actually walk through the space.

---

## Phase 0 — Project Setup

**Goal:** empty Unity 6 project that opens cleanly and is under version control.

1. Create a new Unity 6 project using the URP template.
2. Initialize git, add a Unity-appropriate `.gitignore`, make the first commit.
3. Create a `CLAUDE.md` in the Unity project root (separate from the GDD) that tells Claude Code:
   - where the GDD lives and that it must be read before gameplay work
   - the folder/naming conventions you want (see below)
   - to prefer ScriptableObjects for data-driven systems, and modular components over monolithic managers (GDD Section 40)
4. Set up a basic folder structure, e.g.:
   ```
   Assets/
     _Project/
       Scripts/
       Prefabs/
       ScriptableObjects/
       Scenes/
       Art/
       Audio/
   ```
5. Install packages you know you'll need: Input System, ProBuilder, Cinemachine (optional, for first-person camera work), TextMeshPro.
6. Create an empty test scene and confirm you can press Play with no errors.

**Done when:** project boots, git history exists, empty scene runs.

---

## Phase 1 — Compound Blockout

**Goal:** a walkable, readable version of the Central Compound (GDD Section 7), built in ProBuilder.

1. Blockout only what the Initial Vertical Slice needs (GDD Section 37): lighthouse exterior, operations room, generator shed, workshop, Keeper's House exterior, main gate, short dock path. No sea cave, no cliff relay yet.
2. Keep the footprint to the 50–70m compound target, with the compound crossable in 8–15 seconds.
3. Block in the lighthouse's three floors (ground/operations, mechanical, lantern room) as simple stacked volumes — detail comes later.
4. Add temporary lighting (URP) so the space reads at night: cold ambient, warm lamp fill, and a placeholder rotating beacon light.
5. Walk it yourself in first person (even with a default character controller) before writing any real player code — confirm sightlines match GDD Section 36 (lighthouse visible from most exterior points).

**Done when:** you can walk the full vertical-slice footprint and it feels compact and legible, not maze-like.

---

## Phase 2 — Core Player Systems

**Goal:** a player that can move, look, interact, carry, and take damage.

Build in this order, testing each before the next:

1. `PlayerController` — first-person movement and look.
2. `Interactable` — generic interaction system (raycast/prompt-based) that other systems will hook into.
3. `CarryableItem` + pickup/drop/place logic.
4. Simple inventory (slot for held item, not a full inventory grid).
5. Health + damage + revive (single-player revive can be a stub for now; full co-op revive comes back in Phase 6's cooperative pass).
6. Flashlight.

**Done when:** one player can walk the compound, pick up a placeholder object, carry it somewhere, and take/recover from damage.

---

## Phase 3 — Maintenance Tasks

**Goal:** the "ordinary workplace" loop from GDD Sections 3.1 and 10.

Implement the four vertical-slice tasks only:

1. Fuel generator (`FuelConsumer`, `RepairableSystem`)
2. Replace fuse
3. Repair fence
4. Restock ammunition

Each should follow the GDD's four-step interaction pattern: find problem → bring correct item → short interaction → clear visual/audio confirmation. Build the resource-carry loop (GDD Section 11) alongside this: a dock/storage delivery of fuel cans, ammo box, and a fuse that the player must physically move to the right cabinet.

**Done when:** a player can complete all four tasks solo, with clear feedback each time, using the physical carry loop rather than a menu.

---

## Phase 4 — Lighthouse & Beacon

**Goal:** `BeaconController` — the game's signature system (GDD Section 19).

1. Power on/off, tied into a simple power system (GDD Section 22: three circuits — Beacon, Compound, Defenses; no full electrical sim).
2. Rotating beacon with player-controlled direction.
3. Search mode only for now (reveal effect). Focus mode and Overcharge can come in Phase 7 once there are enemies worth using them on.
4. Manual crank fallback (emergency mode) can be stubbed until the mechanical-failure content exists.

**Done when:** a player can power the lighthouse, rotate the beam, and see it visibly sweep the compound and dock.

---

## Phase 5 — Inspection System

**Goal:** one working visitor event (GDD Sections 12–14, 37).

1. `InspectionManager` handling a single event type: sailor at the gate.
2. Give the player 2–4 clues to check: name, ship, beam reaction (GDD Section 13).
3. Build both branches — one legitimate variant, one Mimic variant — using the example clue sets in GDD Section 13.
4. Wire the beacon into inspection (GDD Section 20): Mimic reacts differently to direct beam.
5. Player choice: admit or reject.
6. Stub in one delayed consequence for a wrong decision (GDD Section 14) — doesn't need to be elaborate yet, just prove the "decision now, consequence later" mechanic works.

**Done when:** you can trigger the sailor event, gather clues, make a call, and see it was either correct or not — with the wrong call producing a later effect, not an instant punishment.

---

## Phase 6 — Defenses

**Goal:** the two starting traps (GDD Section 21).

1. `TrapBase` as the shared trap component.
2. Barricade — cheap, blockable, repairable.
3. Shock trap — power-consuming, cooldown, stuns/damages.
4. Revisit revive and any other two-player cooperative interactions now if you're testing with more than one player (GDD Section 28).

**Done when:** a barricade can be built and broken, and a shock trap can be armed and triggered.

---

## Phase 7 — Combat & Enemies

**Goal:** something to use the defenses and beacon against.

1. One weapon: pistol or shotgun, with scarce ammo (ties back to Phase 3's ammo restocking).
2. `EnemyController` base.
3. Drowned — basic attacker, approaches gate/path, attacks barricades and players.
4. Mimic — reuse/extend the Phase 5 inspection logic; if admitted, it should eventually become a threat inside the compound (GDD Section 14, "Admit a Mimic"). Keep its behavior to predefined audio lines — no generative voice work (GDD Section 18).
5. Now implement Focus mode / Overcharge on the beacon so it has combat relevance (weaken/stun).

**Done when:** a Drowned can be fought off with the pistol, traps, and beacon working together — not just gunned down in the open.

---

## Phase 8 — Shift Loop

**Goal:** tie every system above into the actual game loop (GDD Sections 9, 15, 37).

1. `ShiftManager` driving: Work → Inspection → Preparation → Consequence → Repair → Secure Station → Advance Time.
2. Shift-completion ritual: ring the bell once requirements are met (threat cleared, beacon working, doors closed, players inside).
3. `EventDirector` — rule-based only (GDD Sections 31–32). Start with just enough rules to schedule the one inspection event and avoid stacking two catastrophic failures at once.
4. Minimal UI (GDD Section 35): health, held item, active tasks, current shift/time, critical warnings. Prefer physical/diegetic indicators (gauges, switchboard lights) over HUD elements where the GDD calls for it.

**Done when:** you can play one full shift start to finish: do the maintenance tasks, handle the inspection, defend if needed, repair, ring the bell, and see time advance.

---

## Phase 9 — Playtest the Vertical Slice

**Goal:** validate against the GDD's own success criteria (Section 38) before adding anything else.

Run the full loop (target 10–15 minutes, GDD Section 37) solo and, if possible, with others, and check whether players:

- naturally talk about tasks and divide responsibilities
- get suspicious during the inspection and discuss the decision
- enjoy physically operating the generator/beacon
- feel tension leaving the compound (e.g., toward the dock)
- regroup naturally when the Drowned attacks
- understand what the beacon does
- care about scarce ammo
- want to play another shift

If any of these fall flat, fix that system before building anything new — don't add scope on top of a loop that isn't landing yet.

**Done when:** you have a clear, honest answer for each bullet above.

---

## Phase 10 — Expand Beyond the Vertical Slice

Only start this once Phase 9 checks out. Pull from the GDD's own "possible later" lists rather than inventing new scope:

- More map: Dock as a full location, then Sea Cave, then Cliff Relay/Foghorn (GDD Section 7)
- More enemies: Climber, Lantern Eater, Brute (GDD Section 18)
- More defenses: flare trap, snare, floodlight, harpoon emplacement (GDD Section 21)
- More weapons: rifle, harpoon gun, revolver, axe (GDD Section 26)
- More inspection event types: boat, supply crate, radio transmission (GDD Section 12)
- Workshop crafting (GDD Section 24), solo scaling (Section 29), difficulty scaling (Section 30)
- Full night progression across all 5 shifts to dawn (GDD Section 16)

Keep checking new work against GDD Section 39 (Explicit Non-Goals) — that list exists specifically to stop scope creep at this stage.

---

## Suggested first prompts per phase

Copy-paste starting points for Claude Code sessions:

- **Phase 0:** "Read LAST BEACON.md. Set up a new Unity 6 URP project with the folder structure and CLAUDE.md described in the workflow doc. Don't add any gameplay code yet."
- **Phase 1:** "Read LAST BEACON.md sections 6-8 and 37. Blockout the vertical-slice compound in ProBuilder — compact compound, lighthouse exterior with 3 floor volumes, generator shed, workshop, Keeper's House exterior, gate, short dock path. No sea cave."
- **Phase 3:** "Implement the Fuel Generator maintenance task per GDD section 10-11: FuelConsumer + RepairableSystem components, dock-to-shed carry loop, clear visual/audio confirmation on completion."
- **Phase 5:** "Implement InspectionManager for the sailor-at-the-gate event from GDD section 13 and 37. Build both the legitimate and Mimic variants with the listed clues. Wire in beacon reaction per section 20."
- **Phase 8:** "Implement ShiftManager tying together Work, Inspection, Preparation, Consequence, Repair, Secure Station, Advance Time per GDD section 9 and 15. Use a rule-based EventDirector per section 31-32 — no ML."

Always end a session by asking Claude Code to summarize what changed and flag any assumptions it made, per GDD Rule 10.
