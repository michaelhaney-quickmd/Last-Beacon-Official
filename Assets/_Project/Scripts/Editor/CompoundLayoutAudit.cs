using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Read-only survey of the upper compound: building footprints, how far apart
    /// they are, walk times from the courtyard centre, and whether any building
    /// blocks the lighthouse from the yard. Used to plan the functional layout.
    /// </summary>
    public static class CompoundLayoutAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const float WalkSpeed = 4.5f;
        const float EyeHeight = 1.7f;

        static readonly string[] Bodies =
        {
            "House_Body", "Shed_Body", "Workshop_Body", "Stores_Body"
        };

        [MenuItem("Tools/Last Beacon/Audit Compound Layout")]
        public static void Audit()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            var playerGo = player != null ? player.gameObject : null;
            if (playerGo != null)
                playerGo.SetActive(false);
            Physics.SyncTransforms();

            Debug.Log($"[Compound] {Bodies.Count(Exists)} buildings present (excluding the lighthouse)");

            foreach (var name in Bodies.Where(Exists))
            {
                var b = BoundsOf(name);
                Debug.Log($"[Compound] {name}: {b.size.x:0.0} x {b.size.z:0.0} m, " +
                          $"centre ({b.center.x:0.0}, {b.center.z:0.0}), " +
                          $"x {b.min.x:0.0}..{b.max.x:0.0}, z {b.min.z:0.0}..{b.max.z:0.0}, " +
                          $"height {b.size.y:0.0}");
            }

            // --- separations, including overlaps ----------------------------------
            var present = Bodies.Where(Exists).ToArray();
            for (int i = 0; i < present.Length; i++)
            for (int j = i + 1; j < present.Length; j++)
            {
                var a = BoundsOf(present[i]);
                var b = BoundsOf(present[j]);

                bool xOver = a.min.x < b.max.x && b.min.x < a.max.x;
                bool zOver = a.min.z < b.max.z && b.min.z < a.max.z;

                if (xOver && zOver)
                {
                    float area = (Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x)) *
                                 (Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));
                    Debug.LogWarning($"[Compound] {present[i]} and {present[j]} INTERSECT over {area:0.0} m2");
                }
                else if (xOver)
                {
                    Debug.Log($"[Compound] {present[i]} / {present[j]} gap " +
                              $"{Mathf.Max(a.min.z, b.min.z) - Mathf.Min(a.max.z, b.max.z):0.0} m (north/south)");
                }
                else if (zOver)
                {
                    Debug.Log($"[Compound] {present[i]} / {present[j]} gap " +
                              $"{Mathf.Max(a.min.x, b.min.x) - Mathf.Min(a.max.x, b.max.x):0.0} m (east/west)");
                }
            }

            // --- walk times from the courtyard centre ------------------------------
            var yard = BoundsOf("MainYard");
            var centre = new Vector3(yard.center.x, yard.max.y, yard.center.z);
            Debug.Log($"[Compound] Courtyard {yard.size.x:0.0} x {yard.size.z:0.0} m, centre ({centre.x:0.0}, {centre.z:0.0})");

            var destinations = new List<(string Name, Vector3 At)>();
            foreach (var door in new[]
                     { "Shed_Door", "Workshop_Door", "Stores_Door", "House_Door" })
            {
                if (Exists(door))
                    destinations.Add((door, BoundsOf(door).center));
            }

            destinations.Add(("Lighthouse entrance", new Vector3(0f, Gen.TierLighthouse, 32.5f)));
            destinations.Add(("Inner Gate", Gen.WpCompoundEntrance));

            foreach (var (name, at) in destinations)
            {
                float d = Vector3.Distance(new Vector3(centre.x, 0f, centre.z), new Vector3(at.x, 0f, at.z));
                Debug.Log($"[Compound] centre -> {name}: {d:0.0} m, {d / WalkSpeed:0.0} s");
            }

            if (Exists("Workshop_Door") && Exists("Shed_Door"))
            {
                var w = BoundsOf("Workshop_Door").center;
                var g = BoundsOf("Shed_Door").center;
                float d = Vector3.Distance(new Vector3(w.x, 0f, w.z), new Vector3(g.x, 0f, g.z));
                Debug.Log($"[Compound] Workshop -> Generator: {d:0.0} m, {d / WalkSpeed:0.0} s");
            }

            // --- lighthouse visibility from around the courtyard --------------------
            var lantern = Gen.LanternCentre;
            var blocked = new List<string>();
            int samples = 0, visible = 0;

            for (float x = yard.min.x; x <= yard.max.x; x += 2f)
            for (float z = yard.min.z; z <= yard.max.z; z += 2f)
            {
                var eye = new Vector3(x, yard.max.y + EyeHeight, z);
                samples++;

                var dir = lantern - eye;
                if (!Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude) ||
                    hit.collider.name.StartsWith("Lighthouse_"))
                {
                    visible++;
                }
                else
                {
                    blocked.Add($"({x:0},{z:0}) blocked by {hit.collider.name}");
                }
            }

            Debug.Log($"[Compound] Lighthouse visible from {visible}/{samples} courtyard points " +
                      $"({visible * 100 / samples}%)");
            if (blocked.Count > 0)
                Debug.Log("[Compound] Blocked: " + string.Join(", ", blocked.Distinct().Take(8)));

            // --- service passage and gate throat -----------------------------------
            var shed = BoundsOf("Shed_Body");
            var work = BoundsOf("Workshop_Body");
            var mid = new Vector3((shed.center.x + work.center.x) * 0.5f, Gen.TierCompound + 1f,
                                  (shed.center.z + work.center.z) * 0.5f);
            var across = new Vector3(work.center.x - shed.center.x, 0f, work.center.z - shed.center.z).normalized;
            var along = new Vector3(across.z, 0f, -across.x);

            float narrowest = float.MaxValue;
            for (float o = -3f; o <= 3f; o += 0.25f)
            {
                var at = mid + along * o;
                if (!Physics.Raycast(at, across, out var w2, 20f)) continue;
                if (!Physics.Raycast(at, -across, out var s2, 20f)) continue;
                narrowest = Mathf.Min(narrowest, w2.distance + s2.distance);
            }
            Debug.Log($"[Compound] Service passage narrowest clear width {narrowest:0.00} m");

            var spur = BoundsOf("Rock_GateSpur");
            var retain = BoundsOf("Yard_RetainSouthWest");
            Debug.Log($"[Compound] Inner Gate throat {spur.min.x - retain.max.x:0.0} m wide " +
                      $"(retain to {retain.max.x:0.0}, spur from {spur.min.x:0.0})");

            foreach (var name in Bodies.Where(Exists))
            {
                float yaw = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .First(t => t.name == name).rotation.eulerAngles.y;
                if (yaw > 180f) yaw -= 360f;
                Debug.Log($"[Compound] {name} rotation {yaw:0.0} deg");
            }

            if (playerGo != null)
                playerGo.SetActive(true);
        }

        static bool Exists(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Any(t => t.name == name);

        static Bounds BoundsOf(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .First(t => t.name == name).GetComponent<Renderer>().bounds;
    }
}
