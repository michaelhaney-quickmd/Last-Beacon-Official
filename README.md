# Last Beacon

1–4 player cooperative first-person horror set at a remote lighthouse station during an overnight storm. Keep the station running until dawn.

- **Engine:** Unity 6 (6000.5.4f1), URP
- **Target:** PC / Steam
- **Status:** Phase 0 complete — project set up, no gameplay code yet. Next: Phase 1, compound blockout.
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

## Next step

Phase 1 in the workflow doc: ProBuilder blockout of the vertical-slice compound — lighthouse exterior with three floor volumes, generator shed, workshop, Keeper's House exterior, main gate, short dock path. No sea cave.
