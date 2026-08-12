# LAST BEACON
## Game Design Document — Compact Co-op Lighthouse Horror

**Status:** New primary design direction  
**Engine:** Unity 6  
**Render Pipeline:** URP  
**Camera:** First person  
**Players:** 1–4 cooperative  
**Primary Target:** PC / Steam  
**Development Approach:** Unity + Claude Code + ProBuilder + Blender + Claude Blender MCP + Tripo where appropriate

---

# 1. High Concept

**Last Beacon** is a 1–4 player cooperative first-person horror game set at a remote lighthouse station during a violent overnight storm.

Players are stranded on the island after a shipwreck and must work together to keep the lighthouse station operational until dawn.

The game combines:

- cooperative maintenance tasks
- suspicious visitor/event inspection
- resource restocking
- lighthouse operation
- trap preparation
- limited ammunition
- short intense horror encounters
- infrastructure repair
- team decision-making

The core fantasy is:

> **Four stranded people trying to perform an ordinary lighthouse shift while increasingly unnatural things arrive from the sea.**

The lighthouse compound should feel like a workplace first and a battlefield second.

Players should usually remain close enough to:

- see one another
- hear one another
- quickly help one another
- perform separate nearby tasks

The game should create situations such as:

> “You fuel the generator. I’ll reset the breaker. Someone check the dock. Wait—don’t open that gate yet.”

---

# 2. Core Design Philosophy

The game is **not** primarily a large horde shooter.

It is **not** primarily a sprawling tower-defense game.

It is **not** primarily a complex survival crafting game.

The primary loop is:

> **Work → Inspect → Decide → Prepare → Survive the Consequence → Repair → Advance the Night**

Ordinary tasks should create the baseline.

Horror interrupts those tasks.

Player decisions sometimes create or prevent dangerous events.

Combat is the payoff rather than the constant activity.

---

# 3. Design Pillars

## 3.1 Cooperative Work

Players complete simple physical jobs around the lighthouse station.

Examples:

- fuel generator
- replace fuse
- reset breaker
- repair fence
- clean beacon lens
- restock ammunition
- carry supplies
- patch windows
- prime pump
- repair radio
- reload trap
- move fuel cans
- clear storm debris

Tasks should be easy to understand.

Avoid complex minigames.

Typical interaction:

1. Find problem.
2. Bring correct item/tool.
3. Perform short physical interaction.
4. Receive clear visual/audio confirmation.

---

## 3.2 Suspicion and Inspection

Periodically something arrives or happens that players must evaluate.

Examples:

- distress call
- approaching boat
- survivor at gate
- supply crate
- radio transmission
- strange object washed ashore
- person claiming to be rescue personnel
- malfunction that may or may not be natural

Players gather clues and choose how to respond.

Possible decisions:

- admit
- reject
- inspect further
- ignore
- retrieve
- destroy
- attack

Incorrect decisions can create later consequences.

---

## 3.3 Lighthouse Operation

The lighthouse must remain important throughout the entire night.

It is not merely a final holdout location.

Players operate:

- beacon rotation
- beam direction
- beam focus
- lighthouse power
- mechanical systems
- emergency controls

The beam can:

- reveal enemies
- expose disguised threats
- slow certain creatures
- weaken certain creatures
- illuminate distant locations
- reveal movement near dock or cliffs
- interact with reflector systems later

---

## 3.4 Preparation and Defense

Players prepare the compound for danger.

Initial defense types:

1. Barricade
2. Tripwire alarm
3. Shock trap
4. Electric fence

Potential later defenses:

- flare trap
- snare
- floodlight
- harpoon emplacement

Keep the initial system small.

Major defenses should use predefined build sockets.

---

## 3.5 Limited Combat

Weapons matter because ammunition is scarce.

Initial weapons:

- pistol
- shotgun
- flare gun
- wrench

Possible later weapons:

- rifle
- harpoon gun
- revolver
- axe

Combat should feel dangerous.

Players should prefer:

- traps
- beacon support
- teamwork
- positioning
- retreat

over mindlessly shooting every enemy.

---

# 4. Art Direction

## Primary Visual Goal

> **Chunky, stylized nautical horror with realistic lighting.**

The geometry should be closer in production complexity to simple indie co-op horror games than to photorealistic AAA environments.

Use:

- simplified lighthouse architecture
- chunky machinery
- exaggerated levers
- large readable gauges
- broad faceted rocks
- simple wooden structures
- simple characters
- readable enemy silhouettes
- medium-detail textures
- wet surfaces
- fog
- rain
- strong lighthouse beam
- warm lamps
- cold blue-gray nighttime ambient lighting

Avoid:

- photoreal microdetail
- dense high-poly environments
- individually modeled tiny surface details
- excessive clutter
- complicated realistic machinery
- movie-quality reflections everywhere
- PS1-level extreme simplification
- overly comedic/cartoon art

Lighting and atmosphere should create most of the visual richness.

---

# 5. Intro

The game begins with a short fragmented cinematic.

Suggested sequence:

1. Storm at sea.
2. Alarm inside vessel.
3. Sudden impact.
4. Ship begins flooding.
5. Player falls into ocean.
6. Lightning reveals wreckage.
7. Brief underwater silhouette.
8. Lighthouse beam passes across water.
9. Darkness.
10. Player wakes coughing on shoreline.

Control immediately transfers to the player.

The first objective:

> Reach the lighthouse station.

This sequence establishes that the players are survivors rather than established employees.

---

# 6. Map Philosophy

The previous large multi-tier island design is no longer the target.

Scrap or significantly reduce:

- large separate outer platform
- long bridge defense route
- large lower industrial shelf
- many switchback paths
- multiple distant holdout zones
- large storage-yard battlefield
- many lighthouse floors
- four independent enemy lanes
- long traversal between important systems

The new map should be:

> **Compact, dense, readable and socially connected.**

Players should generally remain within approximately **20–50 meters** of one another during routine play.

---

# 7. New Map Structure

## Central Compound

Approximate playable footprint:

**50–70 meters across**

Contains:

- lighthouse
- keeper's house
- generator shed
- workshop
- electrical/control station
- storage area
- central courtyard
- main gate

Players should be able to cross the main compound in approximately:

**8–15 seconds**

---

## Dock

Approximately:

**15–25 seconds from the compound**

Functions:

- supply delivery
- boat arrivals
- survivor events
- salvage
- fuel
- suspicious activity
- dangerous excursion

The lighthouse should remain visible from the dock.

---

## Sea Cave / Shoreline

Approximately:

**20–30 seconds from compound**

Functions:

- optional investigation
- salvage
- enemy route
- story event
- occasional repair target

Do not make the cave large.

It should feel dangerous because it is away from the group.

---

## Cliff Relay / Foghorn Station

Optional small nearby location.

Approximately:

**15–20 seconds from compound**

Functions:

- warning system
- reflector
- electrical relay
- occasional maintenance objective

---

# 8. Lighthouse Layout

Reduce lighthouse vertical complexity.

Use approximately three functional layers.

## Ground / Operations Floor

Contains:

- radio
- switchboard
- lighthouse status
- remote beacon control
- emergency supplies
- logs
- main compound access

Used constantly.

---

## Mechanical Level

Contains:

- rotation motor
- gear system
- manual crank
- cooling
- mechanical repair points

Visited during failures.

---

## Lantern Room

Contains:

- lens
- direct beacon controls
- manual aiming
- overcharge
- cleaning/repair point
- balcony

Visited during important moments rather than constantly.

---

## Generator

Keep generator in a separate shed or shallow side annex.

Do not require repeated deep basement traversal.

---

# 9. Core Night Structure

A full night consists of several **shifts**.

Each shift follows roughly:

1. Work
2. Inspection Event
3. Preparation
4. Consequence
5. Repair
6. Secure Station
7. Advance Time

Not every shift must contain all elements in exactly the same order.

---

# 10. Work Phase

Players receive approximately:

**3–6 station tasks**

Examples:

- Fuel generator
- Restock shotgun shells
- Replace lighthouse fuse
- Repair dock lamp
- Clean beacon lens
- Refill medical cabinet
- Repair fence
- Reset pump
- Bring supplies to workshop
- Check radio
- Reload shock trap

Tasks should be distributed around the compact compound.

Four players should naturally divide them.

Solo mode receives fewer simultaneous tasks.

---

# 11. Restocking System

Keep this straightforward.

Resource storage locations include:

- fuel shelf
- ammunition cabinet
- fuse cabinet
- medical cabinet
- trap storage
- workshop component shelf

Players physically carry objects.

Examples:

Dock shipment contains:

- 2 fuel cans
- ammunition box
- replacement fuse
- medical supply crate

Players must deliver them to correct storage locations.

Avoid a complex inventory management simulation initially.

---

# 12. Inspection Event System

Each shift can include one major inspection event.

Possible event categories:

## Survivor

Someone arrives at gate or dock.

Players inspect:

- appearance
- identification/logbook information
- behavior
- responses
- reaction to beacon/light

---

## Boat

A vessel requests docking.

Players can:

- permit docking
- deny docking
- illuminate vessel
- inspect from distance

---

## Supply Crate

A crate arrives or washes ashore.

Players inspect for:

- correct markings
- unusual sounds
- damage
- contamination

---

## Radio Transmission

Players receive:

- distress call
- coordinates
- weather report
- rescue message
- suspicious repeated transmission

---

# 13. Inspection Complexity

Do not create a complicated detective simulator.

The inspection should initially use approximately:

**2–4 clues**

Example legitimate sailor:

- correct vessel name
- matching manifest
- believable dialogue
- normal beam reaction

Example Mimic:

- incorrect vessel name
- repeats phrases
- unusual light reaction
- identification mismatch

Players make a simple decision.

---

# 14. Consequence System

The important mechanic is:

> Player decisions affect later danger.

Wrong decisions should not always cause instant punishment.

Delayed consequences are more frightening.

Examples:

## Admit a Mimic

It enters the Keeper's House.

Later:

- lights fail
- player footsteps are mimicked
- special enemy attacks inside compound

---

## Accept contaminated crate

Players place crate in storage.

Later:

- creature emerges
- supplies become unusable
- electrical failure begins

---

## Follow fake distress call

Players leave for dock.

Meanwhile:

- compound attack begins
- generator sabotaged
- gate breached

---

## Reject legitimate survivor

Possible consequences:

- lose supplies
- lose information
- reputation/story consequence
- survivor later found dead

Avoid making every decision binary obvious good/bad.

---

# 15. Shift Completion

Each shift should have a clear ending ritual.

Recommended:

> **Secure the station and ring the shift bell.**

Requirements may include:

- major threat eliminated
- beacon functioning
- critical doors closed
- players inside compound

Once ready:

Players activate the bell or clock.

Time advances.

Example:

**10:00 PM → Midnight**

This provides a satisfying reset similar to completing a work segment.

---

# 16. Night Progression

Example night:

## Shift 1 — 8 PM

Mostly maintenance.

Very low threat.

Teach mechanics.

---

## Shift 2 — 10 PM

First suspicious arrival.

Small enemy encounter possible.

---

## Shift 3 — Midnight

More maintenance failures.

Inspection becomes harder.

Special enemy introduced.

---

## Shift 4 — 2 AM

Compound defense becomes important.

Resources become tighter.

---

## Shift 5 — 4 AM

Multiple problems.

Major consequence event.

---

## Dawn

Final short survival sequence.

Keep beacon functional until sunrise.

---

# 17. Enemy Philosophy

Enemies should be individually readable and threatening.

Avoid constant giant swarms.

Typical enemy counts:

Early:

**1–3**

Middle:

**3–8**

Late:

**5–15**

Large swarm:

Rare special event.

---

# 18. Initial Enemy Types

## Drowned

Basic physical attacker.

Behavior:

- approaches gate/path
- attacks barricades
- attacks players
- attracted to noise/light under some conditions

---

## Mimic

Inspection-related special enemy.

Can imitate:

- survivor
- voice
- radio transmission
- teammate callout

Initially implement simply.

Do not build complex voice-generation AI.

Use predefined audio lines.

---

## Climber

Later enemy.

Can bypass normal entrance routes.

---

## Lantern Eater

Later enemy.

Targets lights/electrical systems.

---

## Brute

Late-game team threat.

Rare.

Requires concentrated fire and traps.

---

# 19. Lighthouse Beam

The beacon should be one of the game's signature systems.

Modes:

## Search Mode

Wide beam.

Reveals enemies.

Low power usage.

---

## Focus Mode

Narrower beam.

Slows or weakens enemies.

Medium power usage.

---

## Overcharge

High-intensity beam.

Temporarily stuns or damages vulnerable enemies.

Causes:

- heat
- power drain
- possible system damage

---

## Manual Emergency Mode

Used if rotation motor fails.

Player must physically crank lighthouse mechanism.

---

# 20. Beacon Inspection Mechanic

The beacon can also be used during inspections.

Certain suspicious entities react differently.

Examples:

- Mimic avoids direct beam.
- Creature briefly reveals distorted silhouette.
- legitimate humans shield eyes normally.
- contaminated object reacts visibly.

This connects the inspection mechanic directly to the lighthouse theme.

---

# 21. Trap System

Initial traps:

## Barricade

Cheap.

Blocks enemies.

Requires repair.

---

## Tripwire Alarm

Does little/no damage.

Provides early warning.

---

## Shock Trap

Ground-based electrical burst.

- damages or stuns enemies
- consumes power
- cooldown
- useful at gate

---

## Electric Fence

Persistent defensive barrier.

- slows enemies
- consumes power
- can overload
- requires repair

---

# 22. Power System

Generator feeds:

- lighthouse
- compound lights
- traps
- workshop
- pumps

Players may eventually prioritize power.

For MVP:

Use simple circuits.

Example:

- Beacon
- Compound
- Defenses

Avoid complex electrical simulation initially.

---

# 23. Generator

Generator interactions:

1. Add fuel.
2. Prime.
3. Start.
4. Reset breaker if needed.
5. Repair damage.

Readability is more important than realism.

Use:

- oversized fuel cap
- obvious starter
- large gauge
- large warning light

---

# 24. Workshop

Workshop allows:

- trap repair
- simple ammo crafting
- tool repair
- component preparation

Do not create huge crafting trees.

Initial recipes:

- shotgun shells
- barricade kit
- shock trap component

---

# 25. Resources

Initial resource categories:

- Fuel
- Scrap
- Electrical Parts
- Wood
- Ammunition
- Medical Supplies

Avoid dozens of resource types.

---

# 26. Combat

Players should usually carry one firearm plus utility items.

Ammo scarcity should encourage:

- checking targets
- using traps
- using beacon
- retreating
- cooperating

Enemies should react strongly to hits.

Readable feedback is important.

---

# 27. Cooperative Design

The game should encourage proximity without forcing players to stand shoulder-to-shoulder.

Ideal arrangement:

One player:

- fuels generator

Another:

- repairs switchboard

Another:

- boards Keeper's House window

Another:

- watches dock

Everyone is still within quick response distance.

During major threat:

Players regroup.

After threat:

Players split again.

---

# 28. Cooperative Tasks

Examples of optional cooperative interactions:

- two-player heavy crate carry
- faster two-player barricade installation
- one player holds flashlight while another repairs
- one rotates beacon while another spots targets
- two-player revive
- player hands resource directly to teammate

These should add teamwork without being mandatory in solo.

---

# 29. Solo Scaling

Solo mode should automatically reduce:

- simultaneous maintenance tasks
- inspection frequency
- enemy count
- system degradation
- carry requirements

Solo bonuses may include:

- faster repairs
- lighter heavy objects
- slower trap damage
- longer event warning

Never require two simultaneous switches for progression.

---

# 30. Difficulty Scaling

Difficulty should increase through:

- more simultaneous tasks
- reduced resource abundance
- tougher inspection clues
- more system failures
- stronger enemy combinations
- shorter recovery periods

Avoid relying primarily on inflated enemy health.

---

# 31. Event Director

Use a **rule-based event director**.

This is standard gameplay logic, not machine learning.

Director monitors:

- shift number
- current player count
- unfinished tasks
- player locations
- infrastructure health
- recent events
- enemy count
- resource state
- previous inspection decisions

Director chooses appropriate events from predefined pools.

---

# 32. Director Rules

Examples:

If:

- players have had no threat recently
- dock has not been used
- inspection event available

Then:

- schedule dock event

If:

- Mimic was admitted
- minimum delay elapsed
- players are occupied

Then:

- trigger Mimic sabotage event

If:

- generator damaged
- major attack active

Then:

- do not trigger another unrelated catastrophic failure

The director should create tension without overwhelming players unfairly.

---

# 33. Horror Philosophy

Horror should rely on:

- uncertainty
- sound
- darkness
- interrupted routine
- proximity
- suspicious behavior
- something being somewhere it should not be

Not only:

- jumpscares
- giant enemy counts
- constant screaming

Players should sometimes have several minutes where nothing attacks them.

That makes the next disturbance matter.

---

# 34. Audio Direction

Critical sounds:

- generator hum
- lighthouse rotation
- ocean
- rain
- foghorn
- distant bells
- radio static
- wood creaking
- footsteps outside
- electrical buzzing
- banging at gate
- strange voices
- trap activation

Audio should frequently provide the first warning.

---

# 35. UI

Keep UI minimal.

Display:

- health
- held item
- teammate status
- active tasks
- current shift/time
- critical warnings

Most system state should appear physically.

Examples:

Generator health:

Use gauge.

Power:

Use switchboard lights.

Beacon heat:

Use analog meter.

Gate damage:

Visible structural damage plus small interaction indicator.

---

# 36. Map Readability

The lighthouse should remain visible from most exterior locations.

Players should quickly recognize:

- lighthouse
- Keeper's House
- generator shed
- workshop
- gate
- dock path

Avoid maze-like architecture.

The compact map should become familiar after a few matches.

---

# 37. Initial Vertical Slice

Build only:

## Map

- compact compound
- lighthouse exterior
- operations room
- generator shed
- workshop
- Keeper's House exterior
- main gate
- short dock path

No sea cave required initially.

---

## Player Systems

- FPS movement
- interaction
- item pickup
- simple inventory
- health
- revive
- flashlight

---

## Maintenance

Implement:

1. Fuel generator
2. Replace fuse
3. Repair fence
4. Restock ammunition

---

## Lighthouse

Implement:

- power on/off
- rotating beacon
- player-controlled direction
- basic reveal/slow effect

---

## Inspection

Implement one visitor event.

Example:

Sailor arrives.

Player checks:

- name
- ship
- beam reaction

Player chooses:

- admit
- reject

One variation is legitimate.

One variation is Mimic.

---

## Defenses

Implement:

- barricade
- shock trap

---

## Combat

Implement:

- pistol or shotgun
- Drowned enemy
- Mimic enemy

---

## Shift

Implement:

1. Maintenance
2. Visitor inspection
3. Defense preparation
4. Consequence
5. Repair
6. Ring bell
7. End prototype

Target duration:

**10–15 minutes**

---

# 38. MVP Success Criteria

The prototype succeeds if players:

- naturally talk about tasks
- divide nearby responsibilities
- become suspicious during inspection
- argue or discuss decisions
- enjoy physically operating equipment
- feel tension leaving the compound
- regroup naturally during danger
- understand beacon function
- care about scarce ammunition
- enjoy repairing after attacks
- want to play another shift

---

# 39. Explicit Non-Goals for MVP

Do NOT implement yet:

- huge island
- many distant defense lanes
- procedural terrain
- large crafting trees
- deep skill progression
- dozens of traps
- open-world exploration
- many lighthouse floors
- complex NPC dialogue system
- machine-learning AI director
- voice recognition
- procedural dialogue
- giant enemy hordes
- advanced campaign persistence
- fully dynamic tides
- elaborate weather simulation
- many weapon types
- vehicles

Keep the first version extremely focused.

---

# 40. Technical Architecture

Favor modular systems.

Suggested components:

- `PlayerController`
- `Interactable`
- `CarryableItem`
- `RepairableSystem`
- `FuelConsumer`
- `PowerConsumer`
- `TaskManager`
- `InspectionManager`
- `ShiftManager`
- `EventDirector`
- `EnemyController`
- `TrapBase`
- `BeaconController`
- `ResourceInventory`

Data-driven systems should use ScriptableObjects where appropriate.

Avoid giant monolithic managers.

---

# 41. Claude Code Rules

When implementing this project:

1. Read this GDD before modifying gameplay systems.
2. Prefer the smallest playable implementation.
3. Do not introduce systems not described here without approval.
4. Do not expand map size to solve gameplay problems.
5. Preserve compact cooperative gameplay.
6. Keep systems modular.
7. Add automated tests for core state logic.
8. Keep visual blockout separate from final art.
9. Use clear editor labels and gizmos.
10. Report assumptions before making major structural changes.

---

# 42. Asset Production Strategy

## ProBuilder

Use for:

- compound layout
- lighthouse blockout
- rooms
- stairs
- paths
- gate
- dock
- workshop shell
- Keeper's House shell

---

## Blender / Blender MCP

Use for:

- simplified final architecture
- beacon machinery
- generator
- trap mechanisms
- doors
- modular building kit
- low-poly cliff modules

---

## Tripo

Use selectively for:

- small props
- weapons
- rough enemy concepts
- tools
- barrels
- crates
- furniture

Do not generate whole levels as one model.

---

# 43. Final Core Experience

A successful session should sound like this:

> “Generator needs fuel.”

> “I'm getting it.”

> “Someone's knocking at the gate.”

> “What's their name?”

> “It matches the log.”

> “Put the light on them.”

> “They're acting weird.”

> “Don't open it.”

> “Fence just went down!”

> “Everyone to the gate!”

That interaction is the heart of **Last Beacon**.

The game is about completing simple cooperative work while deciding what can be trusted and surviving the consequences when the lighthouse station stops feeling safe.