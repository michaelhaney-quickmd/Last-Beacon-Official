# Last Beacon

1–4 player cooperative first-person horror set at a remote lighthouse station during an overnight storm. Keep the station running until dawn.

- **Engine:** Unity 6 (6000.5.4f1), URP
- **Target:** PC / Steam
- **Status:** Phase 1 complete — compact compound blockout in place, no gameplay code yet. Next: Phase 2, core player systems.
- **Co-op:** Netcode for GameObjects + Relay/Lobby, server-authoritative. Voice via Vivox.

Open the project at the repository root with Unity 6000.5.4f1. The current test scene is `Assets/_Project/Scenes/Sandbox.unity`.

## Documents

| File | Purpose |
| --- | --- |
| [LAST BEACON.md](LAST%20BEACON.md) | Game Design Document — source of truth for scope and rules |
| [LAST BEACON - CLAUDE CODE WORKFLOW.md](LAST%20BEACON%20-%20CLAUDE%20CODE%20WORKFLOW.md) | Phase-by-phase build order |

Read the GDD before making gameplay changes (GDD Section 41, Rule 1).

## Repository setup

Art and audio are tracked with Git LFS. Once per machine, before committing binary assets:

```bash
git lfs install
```

Unity scene/prefab merges use UnityYAMLMerge (`.gitattributes`). Configure it once per machine:

```bash
git config merge.unityyamlmerge.cmd '"/Volumes/Unity/UnityEditors/6000.5.4f1/Unity.app/Contents/Helpers/UnityYAMLMerge" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

## Blockout

`Tools > Last Beacon > Generate Compact Compound` rebuilds the blockout scene from scratch. It is a starting point for hand editing — once you start moving faces, stop re-running it. Layout constants live at the top of [CompactCompoundBlockoutGenerator.cs](Assets/_Project/Scripts/Editor/CompactCompoundBlockoutGenerator.cs); the EditMode tests enforce the footprint, walkway, gate, marker and line-of-sight budgets.

## Next step

Phase 2 in the workflow doc: core player systems — `PlayerController`, `Interactable`, `CarryableItem`, held-item inventory, health/damage/revive, flashlight. Server-authoritative from the start (see [CLAUDE.md](CLAUDE.md)).
