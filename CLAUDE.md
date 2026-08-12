# CLAUDE.md — Last Beacon

Working rules for Claude Code in this repository. The design documents are the source of truth; this file is the operating manual.

## Read before gameplay work

- **[LAST BEACON.md](LAST%20BEACON.md)** — the GDD. Read it before modifying or adding any gameplay system. It defines scope, pillars, and the explicit non-goals.
- **[LAST BEACON - CLAUDE CODE WORKFLOW.md](LAST%20BEACON%20-%20CLAUDE%20CODE%20WORKFLOW.md)** — the phase order. Don't jump ahead of the current phase.

GDD Section 41 lists ten rules that govern this project. The ones that get broken most often:

1. Build the smallest playable version first, then refine.
2. Do not introduce systems not described in the GDD without explicit sign-off. Flag the proposal, wait for approval.
3. Do not expand map size to solve a gameplay problem (GDD Section 6 — compactness is the design, not a limitation).
4. Add automated tests for core state logic (shift state, task completion, inspection outcomes, power/circuit state).
5. Report assumptions before making major structural changes.

## Project shape

- **Unity 6000.5.4f1**, URP. Editor lives at `/Volumes/Unity/UnityEditors/6000.5.4f1`.
- The Unity project is at the repository root — `Assets/`, `Packages/`, `ProjectSettings/` sit alongside the design docs.

```
Assets/
  _Project/
    Scripts/Runtime/    LastBeacon.Runtime asmdef — all gameplay code
    Scripts/Editor/     LastBeacon.Editor asmdef — editor tooling, gizmos, inspectors
    Prefabs/
    ScriptableObjects/  data-driven config (task definitions, inspection clue sets, enemy stats)
    Scenes/             Sandbox.unity is the current test scene
    Art/                Materials, Models, Textures
    Audio/
    Input/              InputSystem_Actions.inputactions
    Settings/           URP render pipeline + volume assets
  Tests/
    EditMode/           LastBeacon.Tests.EditMode
    PlayMode/           LastBeacon.Tests.PlayMode
```

## Architecture decisions

These were decided at Phase 0 and constrain everything downstream:

- **Netcode: Netcode for GameObjects (NGO) 2.13**, with Unity Multiplayer Services (Relay + Lobby) for host-based sessions. The game is 1–4 player co-op (GDD Section 1) — **write interaction, carry, repair, and shift state as server-authoritative from the start.** Do not build singleplayer-only versions of these systems intending to retrofit networking later.
- **Voice: Vivox 16.10** with positional/proximity audio. The Mimic's voice imitation (GDD Section 18) and the co-op chatter that Section 43 describes both depend on it.
- **Audio: built-in Unity audio** (AudioSource + AudioMixer groups). No FMOD/Wwise at this scope.
- **Camera:** first person. Cinemachine 3.1 is available for the intro cinematic (GDD Section 5) and camera shake; plain transforms are fine for normal FPS look.
- **Enemy navigation:** Unity AI Navigation (NavMesh) — `com.unity.ai.navigation` is installed.

## Code conventions

- Namespace root is `LastBeacon`. Sub-namespaces by system: `LastBeacon.Player`, `LastBeacon.Tasks`, `LastBeacon.Inspection`, `LastBeacon.Beacon`, `LastBeacon.Enemies`, `LastBeacon.Traps`, `LastBeacon.Shift`.
- Modular components over monolithic managers (GDD Section 40). The GDD names the components it expects: `PlayerController`, `Interactable`, `CarryableItem`, `RepairableSystem`, `FuelConsumer`, `PowerConsumer`, `TaskManager`, `InspectionManager`, `ShiftManager`, `EventDirector`, `EnemyController`, `TrapBase`, `BeaconController`, `ResourceInventory`. Use those names.
- Data-driven systems use ScriptableObjects (GDD Section 40) — task definitions, clue sets, event pools, enemy stats.
- Keep blockout geometry separate from final art (GDD Rule 8). ProBuilder blockout stays identifiable and replaceable.
- Use clear editor labels and gizmos for anything with spatial config — trap sockets, task locations, enemy spawn points, beam volumes (GDD Rule 9).
- The `EventDirector` is rule-based only. No ML, no procedural dialogue (GDD Sections 31–32, 39).

## Git

- Commit after every phase or sub-step that leaves the game launchable.
- Art and audio are tracked via Git LFS (`.gitattributes`). Run `git lfs install` once per machine.
- Scene/prefab merges use UnityYAMLMerge — see [README.md](README.md) for the one-time config command.
- Never commit `Library/`, `Temp/`, or `Logs/`.

## Session habits

Per the workflow doc: end each session by summarizing what changed and flagging every assumption made (GDD Rule 10).
