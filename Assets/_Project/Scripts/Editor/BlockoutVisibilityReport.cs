using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Diagnostic for the lighthouse-visibility budget. Samples every tier surface
    /// and reports which points cannot see the tower, and what blocks them, so a
    /// failing coverage figure can be traced to specific geometry.
    /// </summary>
    public static class BlockoutVisibilityReport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const float Eye = VerticalIslandBlockoutGenerator.EyeHeight;

        static readonly string[] BuildingBodies =
        {
            "House_Body", "Shed_Body", "Workshop_Body", "Electrical_Body", "Storage_Body"
        };

        [MenuItem("Tools/Last Beacon/Report Lighthouse Visibility")]
        public static void Report()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var solids = BuildingBodies
                .Select(n => Find(n).GetComponent<Renderer>().bounds)
                .Append(Find("Lighthouse_Plinth").GetComponent<Renderer>().bounds)
                .ToArray();

            var tiers = new (string Name, float XMin, float XMax, float ZMin, float ZMax, float Y)[]
            {
                ("dock", -6f, 6f, -47f, -35f, 0.2f),
                ("lower-left", -18f, -10f, -32f, -24f, 4f),
                ("overlook", 8f, 18f, -19.5f, -12.5f, 9f),
                ("ascent landing", -2f, 5f, -10f, -7f, 11.5f),
                ("compound", -26f, 26f, 3f, 31f, 17f),
                ("knoll", -11f, 11f, 33f, 43f, 21f)
            };

            int totalSamples = 0, totalVisible = 0;
            var blockerTally = new Dictionary<string, int>();

            foreach (var tier in tiers)
            {
                int samples = 0, visible = 0;
                var blockedPoints = new List<string>();

                for (float x = tier.XMin; x <= tier.XMax; x += 3f)
                for (float z = tier.ZMin; z <= tier.ZMax; z += 3f)
                {
                    var p = new Vector3(x, tier.Y + Eye, z);
                    if (solids.Any(b => p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z))
                        continue;

                    samples++;
                    if (Visible(p, out string blocker))
                    {
                        visible++;
                    }
                    else
                    {
                        blockedPoints.Add($"({x:0},{z:0})<-{blocker}");
                        blockerTally[blocker] = blockerTally.GetValueOrDefault(blocker) + 1;
                    }
                }

                totalSamples += samples;
                totalVisible += visible;
                Debug.Log($"[Visibility] {tier.Name}: {visible}/{samples} " +
                          $"({(samples == 0 ? 0 : visible * 100 / samples)}%)\n" +
                          string.Join("  ", blockedPoints));
            }

            Debug.Log($"[Visibility] TOTAL {totalVisible}/{totalSamples} " +
                      $"({totalVisible * 100 / totalSamples}%)");
            Debug.Log("[Visibility] Blockers: " + string.Join(", ",
                blockerTally.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} x{kv.Value}")));
        }

        static bool Visible(Vector3 eye, out string blocker)
        {
            blocker = null;
            var targets = new[]
            {
                VerticalIslandBlockoutGenerator.LanternCentre,
                VerticalIslandBlockoutGenerator.LanternCentre + Vector3.up * 2.5f,
                VerticalIslandBlockoutGenerator.LanternCentre - Vector3.up * 6f
            };

            foreach (var target in targets)
            {
                Vector3 dir = target - eye;
                if (Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude))
                {
                    if (hit.collider.name.StartsWith("Lighthouse_"))
                        return true;
                    blocker ??= hit.collider.name;
                }
                else
                {
                    blocker ??= "clear miss";
                }
            }

            return false;
        }

        static GameObject Find(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t => t.name == name).gameObject;
    }
}
