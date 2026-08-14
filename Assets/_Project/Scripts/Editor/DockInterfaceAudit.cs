using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Audits the dock-to-lower-ascent interface for the things that make a blockout
    /// read as intersecting plates rather than one intentional surface: coplanar
    /// walkable slabs stacked on each other, cliff masses protruding into the player
    /// corridor, and lips at the seams where surfaces meet.
    /// </summary>
    public static class DockInterfaceAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";

        /// <summary>The dock interface region: jetty through to the top of the intro ramp.</summary>
        static readonly Bounds Region = new Bounds(
            new Vector3(-2f, 1.5f, -38f), new Vector3(50f, 9f, 26f));

        [MenuItem("Tools/Last Beacon/Audit Dock Interface")]
        public static void Audit()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            var playerGo = player != null ? player.gameObject : null;
            if (playerGo != null)
                playerGo.SetActive(false);
            Physics.SyncTransforms();

            var inRegion = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.bounds.Intersects(Region))
                .OrderBy(c => c.name)
                .ToArray();

            Debug.Log($"[DockAudit] {inRegion.Length} colliders in the dock region");

            // --- coplanar walkable surfaces ---------------------------------------
            // Two slabs whose top faces are within 12 cm and whose footprints overlap
            // are the same floor drawn twice: z-fighting, and two colliders to catch on.
            var duplicates = new List<string>();
            for (int i = 0; i < inRegion.Length; i++)
            for (int j = i + 1; j < inRegion.Length; j++)
            {
                var a = inRegion[i].bounds;
                var b = inRegion[j].bounds;

                bool xzOverlap = a.min.x < b.max.x && b.min.x < a.max.x &&
                                 a.min.z < b.max.z && b.min.z < a.max.z;
                if (!xzOverlap)
                    continue;

                float topGap = Mathf.Abs(a.max.y - b.max.y);
                if (topGap > 0.12f)
                    continue;

                // A ramp bedding its end into a landing is intentional: its top sits
                // just below the landing's, inside the landing's own thickness. Only
                // exactly-level tops are true duplicates that z-fight.
                if (topGap > 0.02f)
                {
                    bool aBedded = a.max.y < b.max.y && a.max.y > b.min.y;
                    bool bBedded = b.max.y < a.max.y && b.max.y > a.min.y;
                    if (aBedded || bBedded)
                        continue;
                }

                float overlapArea = (Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x)) *
                                    (Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));
                if (overlapArea < 0.5f)
                    continue;

                duplicates.Add($"{inRegion[i].name} / {inRegion[j].name} — tops {a.max.y:0.00} vs {b.max.y:0.00}, " +
                               $"{overlapArea:0.0} m2 of shared footprint");
            }

            Debug.Log(duplicates.Count == 0
                ? "[DockAudit] No coplanar walkable surfaces."
                : $"[DockAudit] {duplicates.Count} coplanar walkable overlap(s):\n  " +
                  string.Join("\n  ", duplicates));

            // --- cliff intruding into the corridor --------------------------------
            // Cast sideways from the route at knee height; report the nearest rock.
            var routeLegs = new (string Name, Vector3 A, Vector3 B)[]
            {
                ("jetty", Gen.WpJettyEnd, Gen.WpShoreApron),
                ("apron", Gen.WpShoreApron, Gen.WpRampBase),
                ("intro ramp", Gen.WpRampBase, Gen.WpIntroTop),
                ("lower-left ascent", Gen.WpIntroTop, Gen.LowerLeftRampTop)
            };

            float worstClearance = float.MaxValue;
            string worstAt = "";

            foreach (var (name, a, b) in routeLegs)
            {
                var horiz = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                var side = new Vector3(horiz.z, 0f, -horiz.x);
                float legMin = float.MaxValue;
                string legAt = "";

                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    var centre = Vector3.Lerp(a, b, s / (float)steps);
                    var origin = centre + Vector3.up * 0.6f;

                    foreach (var dir in new[] { side, -side })
                    {
                        if (!Physics.Raycast(origin, dir, out var hit, 12f))
                            continue;
                        if (!hit.collider.name.StartsWith("Rock_") && !hit.collider.name.StartsWith("Cliff_"))
                            continue;
                        if (hit.distance < legMin)
                        {
                            legMin = hit.distance;
                            legAt = $"{centre} -> {hit.collider.name}";
                        }
                    }
                }

                if (legMin < float.MaxValue)
                {
                    Debug.Log($"[DockAudit] {name}: nearest rock {legMin:0.00} m from centreline ({legAt})");
                    if (legMin < worstClearance)
                    {
                        worstClearance = legMin;
                        worstAt = $"{name}, {legAt}";
                    }
                }
            }

            Debug.Log($"[DockAudit] Minimum cliff-to-route clearance {worstClearance:0.00} m at {worstAt}");

            // --- seam lips ----------------------------------------------------------
            // Probes straddle each seam by 0.3 m, so what is measured is the step
            // across the joint rather than the slope either side of it.
            ReportLip("dock -> apron", new Vector3(0f, 0.4f, -41.8f), new Vector3(0f, 0.4f, -41.2f));
            ReportLip("apron -> intro ramp", new Vector3(-3.8f, 0.4f, -40.3f), new Vector3(-4.2f, 0.45f, -39.7f));
            ReportLip("intro ramp -> lower ascent",
                Gen.WpIntroTop + new Vector3(0.2f, 0f, -0.3f), Gen.WpIntroTop + new Vector3(-0.2f, 0f, 0.3f));
            ReportLip("apron -> cargo apron", new Vector3(3.2f, 0.4f, -38.5f), new Vector3(3.8f, 0.4f, -38.5f));

            // --- stacked walkable colliders under the route -------------------------
            var stacked = new List<string>();
            foreach (var (name, a, b) in routeLegs)
            {
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.75f));
                for (int s = 0; s <= steps; s++)
                {
                    var centre = Vector3.Lerp(a, b, s / (float)steps);
                    var hits = Physics.RaycastAll(centre + Vector3.up * 3f, Vector3.down, 6f)
                        .Where(h => h.point.y > centre.y - 1.5f && h.point.y < centre.y + 1.2f)
                        .Select(h => h.collider.name)
                        .Distinct()
                        .ToArray();

                    if (hits.Length <= 1)
                        continue;

                    // Sort by height and report only pairs that are either the same
                    // floor twice, or too far apart for the controller to step.
                    var heights = Physics.RaycastAll(centre + Vector3.up * 3f, Vector3.down, 6f)
                        .Where(h => h.point.y > centre.y - 1.5f && h.point.y < centre.y + 1.2f)
                        .OrderByDescending(h => h.point.y)
                        .ToArray();

                    for (int k = 1; k < heights.Length; k++)
                    {
                        float gap = heights[k - 1].point.y - heights[k].point.y;
                        if (gap < 0.12f || gap > 0.45f)
                            stacked.Add($"{name} at {centre}: {heights[k - 1].collider.name} / " +
                                        $"{heights[k].collider.name}, {gap:0.00} m apart");
                    }
                }
            }

            Debug.Log(stacked.Count == 0
                ? "[DockAudit] No stacked walkable colliders under the route."
                : $"[DockAudit] {stacked.Count} stacked-collider point(s):\n  " +
                  string.Join("\n  ", stacked.Take(12)));

            if (playerGo != null)
                playerGo.SetActive(true);
        }

        static void ReportLip(string label, Vector3 before, Vector3 after)
        {
            float? a = GroundAt(before);
            float? b = GroundAt(after);
            if (a == null || b == null)
            {
                Debug.Log($"[DockAudit] {label}: no ground on one side ({a}, {b})");
                return;
            }

            Debug.Log($"[DockAudit] {label} lip {Mathf.Abs(b.Value - a.Value):0.000} m " +
                      $"({a.Value:0.00} -> {b.Value:0.00})");
        }

        static float? GroundAt(Vector3 p)
        {
            float ceiling = p.y + 1.2f;
            float? best = null;
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f))
            {
                if (hit.point.y > ceiling) continue;
                if (best == null || hit.point.y > best.Value) best = hit.point.y;
            }
            return best;
        }
    }
}
