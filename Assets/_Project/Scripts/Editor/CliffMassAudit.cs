using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Audits the NON-WALKABLE cliff and rock massing: masses buried almost
    /// entirely inside another, thin plate-like slabs, cliff surfaces intruding on
    /// the validated walking corridor, and ledges beside the route that would read
    /// as accidental floor.
    ///
    /// Internal cliff-to-cliff overlap is fine and expected; this looks for the
    /// cases that cost geometry without adding silhouette, or that mislead.
    /// </summary>
    public static class CliffMassAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const float StepOffset = 0.45f;
        const float PathHalfWidth = 2.25f;

        [MenuItem("Tools/Last Beacon/Audit Cliff Massing")]
        public static void Audit()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            var playerGo = player != null ? player.gameObject : null;
            if (playerGo != null)
                playerGo.SetActive(false);
            Physics.SyncTransforms();

            var masses = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.name.StartsWith("Rock_") || r.name.StartsWith("Cliff_"))
                .OrderBy(r => r.name)
                .ToArray();

            Debug.Log($"[Cliff] {masses.Length} cliff/rock meshes");

            // --- buried masses -----------------------------------------------------
            var buried = new List<string>();
            foreach (var a in masses)
            {
                float volume = a.bounds.size.x * a.bounds.size.y * a.bounds.size.z;
                if (volume < 1f)
                    continue;

                foreach (var b in masses)
                {
                    if (a == b)
                        continue;

                    float overlap = OverlapVolume(a.bounds, b.bounds);
                    float fraction = overlap / volume;
                    if (fraction > 0.8f)
                        buried.Add($"{a.name} is {fraction:P0} inside {b.name} " +
                                   $"({volume:0} m3, contributing {volume - overlap:0} m3)");
                }
            }

            Debug.Log(buried.Count == 0
                ? "[Cliff] No mass is buried inside another."
                : $"[Cliff] {buried.Count} buried mass(es):\n  " + string.Join("\n  ", buried));

            // --- thin plates -------------------------------------------------------
            var plates = masses
                .Where(r =>
                {
                    var s = r.bounds.size;
                    float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                    float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                    return min < 1.5f && max > 8f;
                })
                .Select(r => $"{r.name} {r.bounds.size.x:0.0} x {r.bounds.size.y:0.0} x {r.bounds.size.z:0.0}")
                .ToArray();

            Debug.Log(plates.Length == 0
                ? "[Cliff] No thin plate-like masses."
                : $"[Cliff] {plates.Length} thin plate(s):\n  " + string.Join("\n  ", plates));

            // --- cliff intruding on the corridor -----------------------------------
            var intrusions = new List<string>();
            var route = Gen.WalkPath;

            for (int i = 1; i < route.Length; i++)
            {
                var a = route[i - 1];
                var b = route[i];
                var horiz = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                var side = new Vector3(horiz.z, 0f, -horiz.x);

                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.75f));
                for (int s = 0; s <= steps; s++)
                {
                    var centre = Vector3.Lerp(a, b, s / (float)steps);
                    var origin = centre + Vector3.up * 0.9f;

                    foreach (var dir in new[] { side, -side })
                    {
                        if (!Physics.Raycast(origin, dir, out var hit, PathHalfWidth))
                            continue;
                        if (!hit.collider.name.StartsWith("Rock_") && !hit.collider.name.StartsWith("Cliff_"))
                            continue;
                        intrusions.Add($"seg{i} {centre}: {hit.collider.name} at {hit.distance:0.00} m");
                    }
                }
            }

            Debug.Log(intrusions.Count == 0
                ? "[Cliff] No cliff surface intrudes on the walking corridor."
                : $"[Cliff] {intrusions.Distinct().Count()} corridor intrusion(s):\n  " +
                  string.Join("\n  ", intrusions.Distinct().Take(10)));

            // --- ledges beside the route that read as floor ------------------------
            // A cliff top within a step of the path, just off its edge, is an
            // accidental walkable surface.
            var ledges = new List<string>();
            for (int i = 1; i < route.Length; i++)
            {
                var a = route[i - 1];
                var b = route[i];
                var horiz = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                var side = new Vector3(horiz.z, 0f, -horiz.x);

                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 1.5f));
                for (int s = 0; s <= steps; s++)
                {
                    var centre = Vector3.Lerp(a, b, s / (float)steps);
                    foreach (float lateral in new[] { -3.2f, -2.7f, 2.7f, 3.2f })
                    {
                        var at = centre + side * lateral;
                        if (!Physics.Raycast(at + Vector3.up * 2.5f, Vector3.down, out var hit, 6f))
                            continue;
                        if (!hit.collider.name.StartsWith("Rock_") && !hit.collider.name.StartsWith("Cliff_"))
                            continue;
                        if (Mathf.Abs(hit.point.y - centre.y) <= StepOffset)
                            ledges.Add($"seg{i} {lateral:+0.0;-0.0} m out: {hit.collider.name} " +
                                       $"at {hit.point.y - centre.y:+0.00;-0.00} m");
                    }
                }
            }

            Debug.Log(ledges.Count == 0
                ? "[Cliff] No cliff ledge sits within a step of the route edge."
                : $"[Cliff] {ledges.Distinct().Count()} accidental ledge(s):\n  " +
                  string.Join("\n  ", ledges.Distinct().Take(10)));

            if (playerGo != null)
                playerGo.SetActive(true);
        }

        static float OverlapVolume(Bounds a, Bounds b)
        {
            float x = Mathf.Max(0f, Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x));
            float y = Mathf.Max(0f, Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y));
            float z = Mathf.Max(0f, Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z));
            return x * y * z;
        }
    }
}
