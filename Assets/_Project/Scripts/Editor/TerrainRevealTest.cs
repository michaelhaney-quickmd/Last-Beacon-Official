using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Controlled reveal test. Temporarily disables the Renderer and Collider of the
    /// Category-C broad support masses, one at a time and then in combination, and
    /// measures what the Terrain does in their absence.
    ///
    /// NOTHING IS DELETED AND THE SCENE IS NEVER SAVED. Every object is restored
    /// before the method returns.
    /// </summary>
    public static class TerrainRevealTest
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const int Width = 1600, Height = 900;
        const float StepOffset = 0.45f;
        const float SeaLevel = -1.2f;
        /// <summary>Rise a CharacterController can take over 1 m at its 50 degree limit.</summary>
        const float WalkableRise = 1.19f;

        static readonly string[] CategoryC =
        {
            // The two removable masses are gone; these are the ones kept as support.
            "Rock_ShoreBed", "Cliff_OverlookBench"
        };

        /// <summary>Decks whose underside must not end up hanging in the air.</summary>
        static readonly (string Name, string Guards)[] Decks =
        {
            ("Dock_Apron", "Rock_ShoreBed"),
            ("Shelf_LowerLeftPivot", "(was Cliff_LowerWestBench)"),
            ("Terrace_Deck", "Cliff_OverlookBench"),
            ("Terrace_Throat", "Cliff_OverlookBench"),
            ("Ascent_Landing", "Cliff_OverlookBench"),
            ("Path_AscentD_FinalRise", "(was Cliff_FinalRiseShoulder)"),
            ("Stair_AscentBroad", "(was Cliff_FinalRiseShoulder)")
        };

        [MenuItem("Tools/Last Beacon/Terrain/Controlled Reveal Test")]
        public static void Run()
        {
            string output = GetArg("-revealOutput") ?? Path.Combine(Path.GetTempPath(), "lb-reveal");
            Directory.CreateDirectory(output);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var playerGo = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None)
                .FirstOrDefault()?.gameObject;
            if (playerGo != null) playerGo.SetActive(false);
            Physics.SyncTransforms();

            var terrain = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).First();

            var states = new List<(string Label, string[] Off)>
            {
                ("00_baseline", new string[0]),
                ("01_no_ShoreBed", new[] { "Rock_ShoreBed" }),

                ("03_no_OverlookBench", new[] { "Cliff_OverlookBench" }),
                ("04_kept_only", new string[0])
            };

            var passed = new List<string>();

            foreach (var (label, off) in states)
            {
                SetActive(off, false);
                Physics.SyncTransforms();
                Measure(label, terrain);
                Capture(output, label, off.Length == 0);
                SetActive(off, true);
                Physics.SyncTransforms();
            }

            // ---- combination of whatever cleared the deck-support bar individually ----
            // Decided from the per-object deck results logged above; recomputed here so
            // the combination is not hand-picked.
            foreach (var name in CategoryC)
            {
                SetActive(new[] { name }, false);
                Physics.SyncTransforms();
                bool floats = Decks.Where(d => d.Guards == name).Any(d => DeckIsUnsupported(d.Name));
                SetActive(new[] { name }, true);
                if (!floats) passed.Add(name);
            }
            Physics.SyncTransforms();

            Debug.Log($"[RV] combination set (passed deck support individually): " +
                      (passed.Count == 0 ? "NONE" : string.Join(", ", passed)));

            if (passed.Count > 0)
            {
                SetActive(passed.ToArray(), false);
                Physics.SyncTransforms();
                Measure("05_combined", terrain);
                Capture(output, "05_combined", true);
                SetActive(passed.ToArray(), true);
                Physics.SyncTransforms();
            }

            // ---- prove everything is back on -------------------------------------
            foreach (var n in CategoryC)
            {
                var go = Find(n);
                Debug.Log($"[RV] restored {n}: renderer {go.GetComponent<Renderer>().enabled}, " +
                          $"collider {go.GetComponent<Collider>().enabled}");
            }
            if (playerGo != null) playerGo.SetActive(true);
            Debug.Log("[RV] Scene NOT saved. No object deleted.");
        }

        // ------------------------------------------------------------------ measuring

        static HashSet<(int, int)> _baseline;

        static HashSet<(int, int)> Measure(string label, Terrain terrain)
        {
            float baseY = terrain.transform.position.y;
            Debug.Log($"[RV] ===================== {label} =====================");

            // --- terrain visibility, by what is on top -----------------------------
            int onTerrain = 0, onPb = 0, submerged = 0, total = 0;
            var heights = new Dictionary<(int, int), float>();

            for (int z = -56; z <= 54; z++)
            for (int x = -48; x <= 44; x++)
            {
                var hits = Physics.RaycastAll(new Vector3(x, 70f, z), Vector3.down, 90f)
                    .Where(h => h.collider.name != "Sea")
                    .OrderByDescending(h => h.point.y)
                    .ToArray();
                if (hits.Length == 0) continue;

                float top = hits[0].point.y;
                heights[(x, z)] = top;
                if (top < SeaLevel) { submerged++; continue; }
                total++;
                if (hits[0].collider is TerrainCollider) onTerrain++; else onPb++;
            }

            Debug.Log($"[RV] {label} surface above sea: {total} cells — " +
                      $"terrain on top {onTerrain} ({(total == 0 ? 0 : onTerrain * 100 / total)}%), " +
                      $"ProBuilder on top {onPb} ({(total == 0 ? 0 : onPb * 100 / total)}%), " +
                      $"submerged {submerged}");

            // --- how much of each Category-C mass is actually visible ---------------
            foreach (var n2 in CategoryC)
            {
                var go2 = Find(n2);
                var col2 = go2.GetComponent<Collider>();
                if (!col2.enabled) continue;
                var b2 = go2.GetComponent<Renderer>().bounds;
                int visible = 0, footprint = 0;
                for (float x = b2.min.x; x <= b2.max.x; x += 0.5f)
                for (float z = b2.min.z; z <= b2.max.z; z += 0.5f)
                {
                    var hits2 = Physics.RaycastAll(new Vector3(x, 70f, z), Vector3.down, 90f)
                        .Where(h => h.collider.name != "Sea")
                        .OrderByDescending(h => h.point.y).ToArray();
                    if (hits2.Length == 0) continue;
                    footprint++;
                    if (hits2[0].collider == col2) visible++;
                }
                Debug.Log($"[RV] {label} mass {n2,-24} is the top surface at " +
                          $"{visible}/{footprint} of its own footprint " +
                          $"({(footprint == 0 ? 0 : visible * 100 / footprint)}% visible from above)");
            }

            // --- penetrations through named gameplay surfaces -----------------------
            int pen = 0;
            var penList = new List<string>();
            foreach (var name in new[]
                     {
                         "Dock_Apron", "Dock_SupplyApron", "Dock_Deck", "Path_IntroRamp",
                         "Path_LowerLeftAscent", "Shelf_LowerLeftPivot", "Path_TraverseLeg1",
                         "Path_TraverseLeg2", "Terrace_Deck", "Terrace_Throat",
                         "Path_AscentA_ShortRise", "Ascent_Landing", "Stair_AscentBroad",
                         "Path_AscentD_FinalRise", "MainYard", "Cliff_BandD_Knoll", "Lighthouse_Plinth"
                     })
            {
                var go = Find(name);
                if (go == null) continue;
                var col = go.GetComponent<Collider>();
                var b = go.GetComponent<Renderer>().bounds;
                float worst = float.MaxValue;
                Vector3 worstAt = Vector3.zero;

                for (float x = b.min.x; x <= b.max.x; x += 0.5f)
                for (float z = b.min.z; z <= b.max.z; z += 0.5f)
                {
                    float top = float.MinValue;
                    foreach (var h in Physics.RaycastAll(new Vector3(x, b.max.y + 3f, z), Vector3.down, b.size.y + 6f))
                        if (h.collider == col && h.point.y > top) top = h.point.y;
                    if (top == float.MinValue) continue;

                    float ty = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
                    if (top - ty < worst) { worst = top - ty; worstAt = new Vector3(x, top, z); }
                }

                if (worst < 0f)
                {
                    pen++;
                    penList.Add($"{name} {worst:0.00} m at ({worstAt.x:0.0},{worstAt.z:0.0})");
                }
            }
            Debug.Log($"[RV] {label} terrain penetrations: {pen}" +
                      (pen == 0 ? "" : " — " + string.Join("; ", penList)));

            // --- route: what the player stands on, and clearance --------------------
            int terrainUnderfoot = 0;
            float worstRoute = float.MaxValue;
            var route = Gen.WalkPath;
            for (int i = 1; i < route.Length; i++)
            {
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / 0.5f));
                for (int s = 0; s <= steps; s++)
                {
                    var at = Vector3.Lerp(route[i - 1], route[i], s / (float)steps);
                    var top = Physics.RaycastAll(at + Vector3.up * 2f, Vector3.down, 6f)
                        .OrderByDescending(h => h.point.y).FirstOrDefault();
                    if (top.collider is TerrainCollider) terrainUnderfoot++;
                    float ty = terrain.SampleHeight(at) + terrain.transform.position.y;
                    worstRoute = Mathf.Min(worstRoute, at.y - ty);
                }
            }
            Debug.Log($"[RV] {label} route: terrain underfoot at {terrainUnderfoot} samples, " +
                      $"min terrain clearance {worstRoute:0.00} m");

            // --- decks hanging in the air --------------------------------------------
            foreach (var (name, guards) in Decks)
            {
                var r = DeckSupport(name);
                if (label == "00_baseline") BaselineFloat[name] = r.FloatingPercent;
                Debug.Log($"[RV] {label} deck {name,-24} (guarded by {guards,-24}) " +
                          $"max gap under {r.MaxGap:0.00} m, {r.FloatingPercent:0}% of it over 2 m");
            }

            // --- lighthouse sightlines -------------------------------------------------
            var blocked = new List<string>();
            foreach (var (name, eye, _) in Gen.ReviewCameras)
                if (!Sees(eye, out var b2)) blocked.Add($"{name}<-{b2}");
            foreach (var (n2, at) in new[]
                     {
                         ("dock", Gen.WpJettyEnd), ("intro", Gen.WpIntroTop),
                         ("ascent", Gen.LowerLeftRampTop), ("pivot", Gen.WpLowerLeftTop),
                         ("traverse", Gen.WpTraverseMid), ("terrace", Gen.WpFenceLookout)
                     })
                if (!Sees(at + Vector3.up * Gen.EyeHeight, out var b3)) blocked.Add($"{n2}<-{b3}");
            Debug.Log($"[RV] {label} lighthouse visibility: " +
                      (blocked.Count == 0 ? "ALL VISIBLE" : "BLOCKED " + string.Join(", ", blocked)));

            // --- walkable connectivity, to catch new shortcuts --------------------------
            var reach = Flood(heights);
            if (_baseline == null)
            {
                _baseline = reach;
                Debug.Log($"[RV] {label} walkable cells reachable from the dock: {reach.Count} (baseline)");
            }
            else
            {
                var added = reach.Where(c => !_baseline.Contains(c)).ToArray();
                var high = added.Where(c => heights[c] > 12f).ToArray();
                Debug.Log($"[RV] {label} walkable cells {reach.Count}, " +
                          $"{added.Length} newly reachable, {high.Length} of them above y12" +
                          (high.Length == 0 ? "" : " — e.g. " +
                           string.Join(", ", high.Take(5).Select(c => $"({c.Item1},{c.Item2})@{heights[c]:0.0}"))));
            }

            return reach;
        }

        /// <summary>Cells reachable on foot from the dock apron, over a 1 m grid.</summary>
        static HashSet<(int, int)> Flood(Dictionary<(int, int), float> h)
        {
            var seen = new HashSet<(int, int)>();
            var start = (0, -38);
            if (!h.ContainsKey(start)) return seen;

            var queue = new Queue<(int, int)>();
            queue.Enqueue(start);
            seen.Add(start);

            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = (c.Item1 + d.Item1, c.Item2 + d.Item2);
                    if (seen.Contains(n) || !h.ContainsKey(n)) continue;
                    if (h[n] < SeaLevel) continue;
                    if (Mathf.Abs(h[n] - h[c]) > WalkableRise) continue;
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return seen;
        }

        /// <summary>
        /// Is there solid material immediately beneath this deck? Probed with an
        /// overlap sphere just under the underside — a downward ray started inside
        /// the supporting mass registers no hit and reports a supported deck as
        /// floating, which is what the first version of this did.
        /// </summary>
        static (float MaxGap, float FloatingPercent) DeckSupport(string name)
        {
            var go = Find(name);
            if (go == null) return (0f, 0f);
            var col = go.GetComponent<Collider>();
            var b = go.GetComponent<Renderer>().bounds;
            int n = 0, floating = 0;
            float maxGap = 0f;

            for (float x = b.min.x; x <= b.max.x; x += 0.5f)
            for (float z = b.min.z; z <= b.max.z; z += 0.5f)
            {
                bool over = Physics.RaycastAll(new Vector3(x, b.max.y + 3f, z), Vector3.down, b.size.y + 6f)
                    .Any(hh => hh.collider == col);
                if (!over) continue;
                n++;

                // Walk down in 0.25 m steps until something solid is found.
                float gap = 0f;
                bool found = false;
                for (float d = 0.15f; d <= 6f; d += 0.25f)
                {
                    var p = new Vector3(x, b.min.y - d, z);
                    if (Physics.OverlapSphere(p, 0.2f).Any(c => c != col && c.name != "Sea"))
                    {
                        gap = d; found = true; break;
                    }
                }
                if (!found) gap = 6f;
                if (gap > maxGap) maxGap = gap;
                if (gap > 2f) floating++;
            }
            return (maxGap, n == 0 ? 0f : floating * 100f / n);
        }

        /// <summary>Baseline floating percentage per deck, so removal is judged on
        /// what it CAUSES rather than on conditions that already existed.</summary>
        static readonly Dictionary<string, float> BaselineFloat = new Dictionary<string, float>();

        static bool DeckIsUnsupported(string deck)
        {
            float now = DeckSupport(deck).FloatingPercent;
            float was = BaselineFloat.TryGetValue(deck, out var b) ? b : 0f;
            return now - was > 10f;
        }

        static bool Sees(Vector3 eye, out string blocker)
        {
            blocker = "";
            var dir = Gen.LanternCentre - eye;
            if (!Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude)) return true;
            if (hit.collider.name.StartsWith("Lighthouse_")) return true;
            blocker = hit.collider is TerrainCollider ? "TERRAIN" : hit.collider.name;
            return false;
        }

        // ------------------------------------------------------------------ plumbing

        static void SetActive(string[] names, bool on)
        {
            foreach (var n in names)
            {
                var go = Find(n);
                if (go == null) { Debug.LogWarning($"[RV] {n} not found"); continue; }
                go.GetComponent<Renderer>().enabled = on;
                go.GetComponent<Collider>().enabled = on;
            }
        }

        static GameObject Find(string name) =>
            Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == name)?.gameObject;

        static readonly (string Name, Vector3 Eye, Vector3 Look, float Fov)[] Views =
        {
            ("aerial",     new Vector3(-60f, 78f, -120f), new Vector3(0f, 8f, 4f), 60f),
            ("silhouette", new Vector3(0f, 7f, -115f),    new Vector3(0f, 14f, 10f), 45f),
            ("dock",       new Vector3(0f, 2.1f, -47f),   new Vector3(0f, 24f, 38f), 65f),
            ("lowerleft",  new Vector3(-14f, 5.7f, -28f), new Vector3(0f, 20f, 20f), 70f),
            ("traverse",   new Vector3(0f, 8.2f, -22f),   new Vector3(6f, 14f, 0f), 70f),
            ("maingate",   new Vector3(15.5f, 10.7f, -17.5f), new Vector3(2f, 12f, -14f), 70f),
            ("finalascent",new Vector3(4f, 13.2f, -9f),   new Vector3(-4f, 18f, 4f), 70f),
            ("innergate",  new Vector3(-6f, 18.7f, 2f),   new Vector3(0f, 26f, 30f), 70f),
            ("courtyard",  new Vector3(0f, 18.7f, 17f),   new Vector3(0f, 30f, 38f), 70f),
            ("underdeck",  new Vector3(20f, 4f, -26f),    new Vector3(8f, 10f, -14f), 60f)
        };

        static void Capture(string root, string label, bool allViews)
        {
            var dir = Path.Combine(root, label);
            Directory.CreateDirectory(dir);

            var camGo = new GameObject("__RevealCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;

            bool fogWas = RenderSettings.fog;
            var skyWas = RenderSettings.ambientSkyColor;
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.47f, 0.5f);
            var fill = new GameObject("__RevealFill");
            fill.transform.rotation = Quaternion.Euler(48f, 200f, 0f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 1.2f; fl.shadows = LightShadows.Soft;

            try
            {
                foreach (var v in Views)
                {
                    if (!allViews && v.Name != "aerial" && v.Name != "silhouette" &&
                        v.Name != "dock" && v.Name != "maingate" && v.Name != "underdeck") continue;

                    cam.transform.position = v.Eye;
                    cam.transform.rotation = Quaternion.LookRotation((v.Look - v.Eye).normalized, Vector3.up);
                    cam.fieldOfView = v.Fov;

                    var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    cam.targetTexture = rt;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    File.WriteAllBytes(Path.Combine(dir, v.Name + ".png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(fill);
                RenderSettings.fog = fogWas;
                RenderSettings.ambientSkyColor = skyWas;
            }
        }

        static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
