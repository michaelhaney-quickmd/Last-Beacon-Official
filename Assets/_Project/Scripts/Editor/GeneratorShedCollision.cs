using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Builds primitive collision for the generator shed art shell and retires the
    /// blockout shell colliders that used to seal the doorway.
    ///
    /// Only Shed_Body and Shed_Roof are switched off — the generator, fuel drums,
    /// lean-to posts and panels keep their collision, because those are gameplay
    /// props rather than the building shell.
    /// </summary>
    public static class GeneratorShedCollision
    {
        const string ScenePath  = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string PrefabPath = "Assets/_Project/Prefabs/PF_GeneratorShed.prefab";
        const string InstName   = "PF_GeneratorShed";

        // From the approved spec, in the prefab root's local space (root = footprint
        // centre at floor level).
        const float W = 10.0f, D = 8.0f, WALL_H = 4.2f, WT = 0.3f;
        const float DOOR_W = 3.5f, DOOR_H = 3.2f;

        // Player capsule from CLAUDE.md: radius 0.35, height 1.8.
        const float PlayerRadius = 0.35f, PlayerHeight = 1.8f;

        [MenuItem("Tools/Last Beacon/Build Generator Shed Collision")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Collision";
            Directory.CreateDirectory(shots);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            if (pf == null) { Debug.LogError("[COL] PF_GeneratorShed not in the scene"); return; }
            var collision = pf.Find("Collision");
            if (collision == null)
            { collision = new GameObject("Collision").transform; collision.SetParent(pf, false); }

            // which local side is the doorway on?
            var frame = pf.GetComponentsInChildren<MeshRenderer>()
                          .FirstOrDefault(r => r.name.Contains("DoorFrame"));
            Vector3 frameLocal = pf.InverseTransformPoint(frame.bounds.center);
            bool doorOnMinusX = frameLocal.x < 0f;
            Debug.Log($"[COL] door frame in prefab-local space {frameLocal} -> doorway is on " +
                      $"local {(doorOnMinusX ? "-X" : "+X")}");
            float dsx = doorOnMinusX ? -1f : 1f;

            foreach (Transform c in collision.Cast<Transform>().ToList())
                Object.DestroyImmediate(c.gameObject);

            void Box(string name, Vector3 centre, Vector3 size)
            {
                var go = new GameObject(name);
                go.transform.SetParent(collision, false);
                go.transform.localPosition = centre;
                go.transform.localRotation = Quaternion.identity;
                var bc = go.AddComponent<BoxCollider>();
                bc.size = size;
            }

            float half = WT / 2f;
            float sideLen = (D - DOOR_W) / 2f;                 // wall either side of the opening
            float sideCz  = DOOR_W / 2f + sideLen / 2f;
            // doorway wall, split around the opening, plus the header above it
            Box("Col_Wall_Door_A", new Vector3(dsx * (W / 2 - half), WALL_H / 2,  sideCz),
                                   new Vector3(WT, WALL_H, sideLen));
            Box("Col_Wall_Door_B", new Vector3(dsx * (W / 2 - half), WALL_H / 2, -sideCz),
                                   new Vector3(WT, WALL_H, sideLen));
            Box("Col_Wall_Door_Header", new Vector3(dsx * (W / 2 - half),
                                   DOOR_H + (WALL_H - DOOR_H) / 2f, 0f),
                                   new Vector3(WT, WALL_H - DOOR_H, DOOR_W));
            // the other three walls
            Box("Col_Wall_Back", new Vector3(-dsx * (W / 2 - half), WALL_H / 2, 0f),
                                 new Vector3(WT, WALL_H, D));
            Box("Col_Wall_SideA", new Vector3(0f, WALL_H / 2,  D / 2 - half), new Vector3(W, WALL_H, WT));
            Box("Col_Wall_SideB", new Vector3(0f, WALL_H / 2, -(D / 2 - half)), new Vector3(W, WALL_H, WT));
            Physics.SyncTransforms();
            Debug.Log($"[COL] built {collision.childCount} primitive box colliders under Collision");

            // ---------- retire ONLY the blockout shell colliders -------------------
            foreach (var n in new[] { "Shed_Body", "Shed_Roof", "Shed_DoorLeaf_A", "Shed_DoorLeaf_B" })
            {
                var g = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.name == n).Select(t => t.gameObject)
                    .FirstOrDefault(o => o.GetComponent<Collider>() != null);
                if (g == null) { Debug.LogWarning($"[COL] {n}: no collider found"); continue; }
                foreach (var c in g.GetComponents<Collider>()) c.enabled = false;
                Debug.Log($"[COL] blockout {n}: collider DISABLED (renderer left {(g.GetComponent<MeshRenderer>()?.enabled)})");
            }
            var group = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            if (group != null)
            {
                var kept = group.GetComponentsInChildren<Collider>().Where(c => c.enabled)
                                .Select(c => c.name).ToArray();
                Debug.Log($"[COL] blockout props still colliding ({kept.Length}): {string.Join(", ", kept)}");
            }
            Physics.SyncTransforms();

            Verify(pf, shots);

            PrefabUtility.ApplyPrefabInstance(pf.gameObject, InteractionMode.AutomatedAction);
            Debug.Log($"[COL] prefab updated: {PrefabPath}");
            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[COL] scene saved. Blockout still VISIBLE.");
        }

        static bool CapsuleFree(Vector3 footPos)
        {
            Vector3 p0 = footPos + Vector3.up * PlayerRadius;
            Vector3 p1 = footPos + Vector3.up * (PlayerHeight - PlayerRadius);
            return !Physics.CheckCapsule(p0, p1, PlayerRadius, ~0, QueryTriggerInteraction.Ignore);
        }

        static void Verify(Transform pf, string shots)
        {
            Debug.Log("[COL] ---------------- VERIFICATION ----------------");
            var dd = pf.GetComponent<ScriptedDoubleDoor>();

            // walk the courtyard -> doorway -> generator line at player height
            var frame = pf.GetComponentsInChildren<MeshRenderer>()
                          .FirstOrDefault(r => r.name.Contains("DoorFrame"));
            Vector3 doorWorld = frame.bounds.center; doorWorld.y = 17f;
            Vector3 inward = (new Vector3(17f, 17f, 13f) - doorWorld); inward.y = 0f; inward.Normalize();

            foreach (float angle in new[] { 0f, 45f, 95f })
            {
                if (dd != null) { dd.PoseImmediate(angle); Physics.SyncTransforms(); }
                var results = "";
                int blocked = 0, total = 0;
                for (float t = -3f; t <= 5.5f; t += 0.5f)
                {
                    Vector3 p = doorWorld + inward * t;
                    bool free = CapsuleFree(p);
                    total++; if (!free) blocked++;
                    results += free ? "." : "X";
                }
                Debug.Log($"[COL] doors {angle,3:0} deg | courtyard->generator walk line: {results} " +
                          $"({blocked}/{total} blocked)  '.' = passable, 'X' = blocked");
                Capture(shots, $"col_{angle:00}", doorWorld + inward * -7f + Vector3.up * 1.7f,
                        doorWorld + Vector3.up * 1.6f, 60f);
            }
            if (dd != null) { dd.PoseImmediate(0f); Physics.SyncTransforms(); }

            // closed leaf must block; wall must block; open doorway must pass
            Vector3 mid = doorWorld + inward * 0.1f;
            Debug.Log($"[COL] closed doors, standing in the doorway: {(CapsuleFree(mid) ? "PASSABLE — leaf not blocking" : "BLOCKED (correct)")}");
            if (dd != null) { dd.PoseImmediate(95f); Physics.SyncTransforms(); }
            Debug.Log($"[COL] doors at 95 deg, same spot:            {(CapsuleFree(mid) ? "PASSABLE (correct)" : "BLOCKED — doorway still sealed")}");
            if (dd != null) { dd.PoseImmediate(0f); Physics.SyncTransforms(); }

            // solid wall away from the doorway must stay blocked
            Debug.Log($"[COL] footprint centre (the generator prop stands here): " +
                      $"{(CapsuleFree(pf.TransformPoint(Vector3.zero)) ? "free" : "blocked — the generator occupies it, expected")}");
            // Probe the wall PLANE itself (x=+/-4.85, z=+/-3.85), not the interior.
            // A wall reads "blocked"; a clear interior spot reads "free".
            var probes = new (string label, Vector3 local, bool wantBlocked)[]
            {
                ("doorway wall plane, off to the side", new Vector3(-4.85f, 0f, 3.0f), true),
                ("back wall plane",                     new Vector3( 4.85f, 0f, 0f),   true),
                ("side wall plane +Z",                  new Vector3( 0f,    0f, 3.85f), true),
                ("side wall plane -Z",                  new Vector3( 0f,    0f,-3.85f), true),
                ("interior, clear of the generator",    new Vector3(-3.0f,  0f, 2.4f),  false),
                ("interior, in front of the breaker",   new Vector3( 2.3f,  0f,-2.0f),  false),
                ("breaker prop itself (gameplay solid)",new Vector3( 3.4f,  0f,-2.5f),  true),
                ("outside, clear of the courtyard kerb", new Vector3(-9.5f,  0f, 0f),   false),
            };
            foreach (var (label, local, wantBlocked) in probes)
            {
                Vector3 wp = pf.TransformPoint(local);
                bool free = CapsuleFree(wp);
                bool ok = wantBlocked ? !free : free;
                string who = "";
                if (!free)
                {
                    var hits = Physics.OverlapCapsule(wp + Vector3.up * PlayerRadius,
                        wp + Vector3.up * (PlayerHeight - PlayerRadius), PlayerRadius,
                        ~0, QueryTriggerInteraction.Ignore);
                    who = " <- " + string.Join(", ", hits.Select(h =>
                    {
                        var t = h.transform; string path = t.name;
                        while (t.parent != null && t.parent.parent != null) { t = t.parent; path = t.name + "/" + path; }
                        return path;
                    }).Distinct().Take(4));
                }
                Debug.Log($"[COL] {label,-38} {(free ? "free" : "blocked")}  " +
                          $"expected {(wantBlocked ? "blocked" : "free")}  {(ok ? "OK" : "** FAIL **")}{who}");
            }

            // door leaves must not trap the player against the frame when open
            if (dd != null) { dd.PoseImmediate(95f); Physics.SyncTransforms(); }
            string across = ""; int trapped = 0;
            for (float off = -1.6f; off <= 1.601f; off += 0.4f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, inward).normalized * off;
                Vector3 p = doorWorld + inward * 0.2f + side;
                bool free = CapsuleFree(p);
                across += free ? "." : "X";
                if (!free) trapped++;
            }
            Debug.Log($"[COL] doors open 95 deg, across the 3.5 m opening (-1.6..+1.6 m): {across} " +
                      $"({trapped}/9 blocked) — the clear centre is what the player walks through");
            // how wide is the actual clear gap when open?
            float clear = 0f;
            for (float off = -1.7f; off <= 1.701f; off += 0.05f)
            {
                Vector3 side = Vector3.Cross(Vector3.up, inward).normalized * off;
                if (CapsuleFree(doorWorld + inward * 0.2f + side)) clear += 0.05f;
            }
            Debug.Log($"[COL] usable clear width with the doors open: {clear:0.00} m " +
                      $"(a {PlayerRadius * 2f:0.00} m wide player needs {PlayerRadius * 2f:0.00} m)");
            if (dd != null) { dd.PoseImmediate(0f); Physics.SyncTransforms(); }
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__ColCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;
            DynamicGI.UpdateEnvironment();
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }

        static string GetArg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
