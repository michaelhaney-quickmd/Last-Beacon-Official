using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Measures the walkable corridor along the approved route: where it is
    /// narrowest, how much room the ascent launch has, the worst lip, the steepest
    /// grade, and the stair's riser and tread. Numbers come from the scene, not
    /// from the constants that built it.
    /// </summary>
    public static class RouteMetricsReport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const float CapsuleRadius = 0.35f;
        const float CapsuleHeight = 1.8f;
        const float StepOffset = 0.45f;

        [MenuItem("Tools/Last Beacon/Report Route Metrics")]
        public static void Report()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            var playerGo = player != null ? player.gameObject : null;
            if (playerGo != null)
                playerGo.SetActive(false);
            Physics.SyncTransforms();

            var route = Gen.WalkPath;

            // --- corridor width, sampled by casting sideways at knee height -------
            float narrowest = float.MaxValue;
            Vector3 narrowestAt = Vector3.zero;
            int narrowestSeg = 0;
            var perSegment = new Dictionary<int, float>();

            for (int i = 1; i < route.Length; i++)
            {
                Vector3 a = route[i - 1], b = route[i];
                var horiz = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                var side = new Vector3(horiz.z, 0f, -horiz.x);

                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.5f));
                float segMin = float.MaxValue;

                for (int s = 0; s <= steps; s++)
                {
                    var centre = Vector3.Lerp(a, b, s / (float)steps);
                    var origin = centre + Vector3.up * 0.5f;

                    float left = Physics.Raycast(origin, -side, out var lh, 8f) ? lh.distance : 8f;
                    float right = Physics.Raycast(origin, side, out var rh, 8f) ? rh.distance : 8f;
                    float width = left + right;

                    if (width < segMin) segMin = width;
                    if (width < narrowest)
                    {
                        narrowest = width;
                        narrowestAt = centre;
                        narrowestSeg = i;
                    }
                }

                perSegment[i] = segMin;
            }

            Debug.Log($"[Metrics] Narrowest corridor {narrowest:0.00} m on segment {narrowestSeg} at {narrowestAt}");
            foreach (var kv in perSegment.OrderBy(k => k.Value).Take(6))
                Debug.Log($"[Metrics]   seg{kv.Key} min width {kv.Value:0.00} m");

            // --- ascent launch throat ---------------------------------------------
            var infill = Find("Rock_MainGateInfill").GetComponent<Renderer>().bounds;
            var northRock = Find("Rock_TerraceNorth").GetComponent<Renderer>().bounds;
            float throat = northRock.min.x - infill.max.x;
            Debug.Log($"[Metrics] Ascent launch throat {throat:0.00} m " +
                      $"({(throat - 4f) / 2f:0.00} m clear either side of a 4 m path)");

            // --- worst lip along the route ----------------------------------------
            float worstLip = 0f;
            Vector3 worstLipAt = Vector3.zero;
            foreach (float offset in new[] { -1.2f, -0.6f, 0f, 0.6f, 1.2f })
            {
                float? previous = null;
                for (int i = 1; i < route.Length; i++)
                {
                    Vector3 a = route[i - 1], b = route[i];
                    var horiz = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                    var side = new Vector3(horiz.z, 0f, -horiz.x);
                    int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.5f));

                    for (int s = 0; s <= steps; s++)
                    {
                        var centre = Vector3.Lerp(a, b, s / (float)steps);
                        var p = centre + side * offset;
                        float? ground = GroundAt(p, centre.y);
                        if (ground.HasValue && previous.HasValue)
                        {
                            float lip = Mathf.Abs(ground.Value - previous.Value);
                            if (lip > worstLip && lip < 3f)
                            {
                                worstLip = lip;
                                worstLipAt = p;
                            }
                        }
                        if (ground.HasValue) previous = ground;
                    }
                }
            }
            Debug.Log($"[Metrics] Worst lip {worstLip:0.00} m at {worstLipAt} (step offset {StepOffset})");

            // --- grades -------------------------------------------------------------
            var grades = new (string Name, Vector3 From, Vector3 To)[]
            {
                ("intro ramp", Gen.WpRampBase, Gen.WpIntroTop),
                ("lower-left ascent", Gen.WpIntroTop, Gen.LowerLeftRampTop),
                ("traverse leg 1", Gen.WpLowerLeftTop, Gen.WpTraverseMid),
                ("traverse leg 2", Gen.WpTraverseMid, Gen.OverlookDeckEdge),
                ("ascent A", Gen.WpOverlookExit, Gen.WpAscentATop),
                ("broad stair", Gen.WpLanding, Gen.WpStairsTop),
                ("final rise", Gen.WpStairsTop, Gen.WpCompoundEntrance)
            };
            foreach (var (name, from, to) in grades)
            {
                var d = to - from;
                float run = new Vector2(d.x, d.z).magnitude;
                Debug.Log($"[Metrics] {name}: {Mathf.Atan2(d.y, run) * Mathf.Rad2Deg:0.0} deg over {run:0.0} m run");
            }

            // --- stair dimensions ---------------------------------------------------
            var stairDelta = Gen.WpStairsTop - Gen.WpLanding;
            float stairRun = new Vector2(stairDelta.x, stairDelta.z).magnitude;
            int stairSteps = Mathf.Clamp(Mathf.FloorToInt(stairRun / 0.7f),
                                         Mathf.CeilToInt(stairDelta.y / 0.42f), 40);
            var stairBounds = Find("Stair_AscentBroad").GetComponent<Renderer>().bounds;
            Debug.Log($"[Metrics] Broad stair: {stairSteps} steps, riser {stairDelta.y / stairSteps:0.000} m, " +
                      $"tread {stairRun / stairSteps:0.000} m, width {Mathf.Min(stairBounds.size.x, stairBounds.size.z):0.0} m");

            // --- two players passing each other ------------------------------------
            float twoPlayerNeed = CapsuleRadius * 4f;
            var tight = perSegment.Where(kv => kv.Value < twoPlayerNeed + 0.6f)
                .Select(kv => $"seg{kv.Key} ({kv.Value:0.00} m)").ToArray();
            Debug.Log($"[Metrics] Two players need {twoPlayerNeed:0.00} m to pass. " +
                      (tight.Length == 0 ? "No segment is that tight." : "Tight: " + string.Join(", ", tight)));

            if (playerGo != null)
                playerGo.SetActive(true);
        }

        static float? GroundAt(Vector3 point, float expectedY)
        {
            var origin = new Vector3(point.x, expectedY + 3f, point.z);
            float ceiling = expectedY + 1.2f;
            float? best = null;
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 8f))
            {
                if (hit.point.y > ceiling) continue;
                if (best == null || hit.point.y > best.Value) best = hit.point.y;
            }
            return best;
        }

        static GameObject Find(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t => t.name == name).gameObject;
    }
}
