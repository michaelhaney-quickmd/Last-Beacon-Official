using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// READ ONLY. Scores candidate cliff masses for the first art replacement test:
    /// how much of each is actually on screen from the Dock and the Main Gate, how
    /// close it comes to the walked route, and whether anything stands on it.
    /// Never saves the scene.
    /// </summary>
    public static class CliffTestCandidateAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";

        static readonly string[] Candidates =
        {
            "Rock_Outcrop_SeaW", "Rock_Outcrop_SeaE", "Rock_ShoreEast", "Rock_ShoreWest",
            "Cliff_TerraceEastFace_Battered", "Rock_TerraceEast", "Cliff_EastFlank_Battered",
            "Cliff_SouthFace_Battered", "Rock_Outcrop_W", "Rock_Outcrop_E", "Cliff_WestFlank_Battered"
        };

        static readonly (string Name, Vector3 Eye, Vector3 Look, float Fov)[] Views =
        {
            // Aimed at what the player actually looks AT on each approach, rather
            // than along it: the first pass pointed the Main Gate camera west across
            // the terrace, so the east wall behind it scored zero.
            ("DockNorth",    new Vector3(0f, 2.1f, -47f),   new Vector3(0f, 7f, -26f), 68f),
            ("TraverseUp",   new Vector3(-4f, 7.7f, -24f),  new Vector3(12f, 11f, -16f), 70f),
            ("GateApproach", new Vector3(6.5f, 10.7f, -18f), new Vector3(20f, 12f, -15f), 70f)
        };

        [MenuItem("Tools/Last Beacon/Audit Cliff Test Candidates")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var camGo = new GameObject("__ScoreCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;

            foreach (var name in Candidates)
            {
                var go = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == name)?.gameObject;
                if (go == null) { Debug.Log($"[CC] {name}: MISSING"); continue; }

                var b = go.GetComponent<Renderer>().bounds;
                var col = go.GetComponent<Collider>();

                var coverage = new List<string>();
                foreach (var v in Views)
                {
                    cam.transform.position = v.Eye;
                    cam.transform.rotation = Quaternion.LookRotation((v.Look - v.Eye).normalized, Vector3.up);
                    cam.fieldOfView = v.Fov;
                    coverage.Add($"{v.Name} {ScreenCoverage(cam, col, b):0.00}%");
                }

                // Distance from the walked route, and whether the route stands on it.
                float nearest = float.MaxValue;
                bool standsOn = false;
                var route = Gen.WalkPath;
                for (int i = 1; i < route.Length; i++)
                {
                    int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(route[i - 1], route[i]) / 0.5f));
                    for (int s = 0; s <= steps; s++)
                    {
                        var at = Vector3.Lerp(route[i - 1], route[i], s / (float)steps);
                        nearest = Mathf.Min(nearest, Mathf.Sqrt(b.SqrDistance(at)));
                        var top = Physics.RaycastAll(at + Vector3.up * 2f, Vector3.down, 6f)
                            .OrderByDescending(h => h.point.y).FirstOrDefault();
                        if (top.collider == col) standsOn = true;
                    }
                }

                Debug.Log($"[CC] {name,-32} {b.size.x,5:0.0} x {b.size.z,5:0.0} x {b.size.y,5:0.0}  " +
                          $"centre ({b.center.x,6:0.0},{b.center.z,6:0.0})  top {b.max.y,5:0.0}  " +
                          $"screen [{string.Join(", ", coverage)}]  " +
                          $"route {nearest,5:0.0} m  standsOn={standsOn}");
            }

            cam.targetTexture = null; rt.Release();
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
            if (player != null) player.gameObject.SetActive(true);
            Debug.Log("[CC] scene NOT saved.");
        }

        /// <summary>Renders the four review views of the chosen first target.</summary>
        [MenuItem("Tools/Last Beacon/Render Cliff Test Target")]
        public static void RenderTarget()
        {
            string dir = GetArg("-cliffOutput") ?? System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "lb-clifftest");
            System.IO.Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var target = new Vector3(19.5f, 11.5f, -15.5f);
            var shots = new (string Name, Vector3 Eye, Vector3 Look, float Fov)[]
            {
                ("01_FromDock",        new Vector3(0f, 2.1f, -47f),    target, 60f),
                ("02_MainGateApproach",new Vector3(6.5f, 10.7f, -18f), target, 70f),
                ("03_SideProfile",     new Vector3(19.5f, 13f, -36f),  new Vector3(19.5f, 10f, -15.5f), 45f),
                ("04_CloseThreeQuarter", new Vector3(9f, 14.5f, -27f), target, 55f)
            };

            var camGo = new GameObject("__CliffCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;

            bool fogWas = RenderSettings.fog;
            RenderSettings.fog = false;
            var fill = new GameObject("__CliffFill");
            fill.transform.rotation = Quaternion.Euler(38f, 215f, 0f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 1.25f; fl.shadows = LightShadows.Soft;
            var ambWas = RenderSettings.ambientSkyColor;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.5f);

            try
            {
                foreach (var s2 in shots)
                {
                    cam.transform.position = s2.Eye;
                    cam.transform.rotation = Quaternion.LookRotation((s2.Look - s2.Eye).normalized, Vector3.up);
                    cam.fieldOfView = s2.Fov;
                    var rt2 = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
                    cam.targetTexture = rt2;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt2;
                    var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, s2.Name + ".png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex); rt2.Release(); Object.DestroyImmediate(rt2);
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo); Object.DestroyImmediate(fill);
                RenderSettings.fog = fogWas; RenderSettings.ambientSkyColor = ambWas;
            }

            foreach (var n in new[] { "Rock_TerraceEast", "Cliff_TerraceEastFace_Battered" })
            {
                var go = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == n);
                var b = go.GetComponent<Renderer>().bounds;
                Debug.Log($"[CT] {n}: pos {go.position} rot {go.rotation.eulerAngles} " +
                          $"AABB x {b.min.x:0.00}..{b.max.x:0.00} y {b.min.y:0.00}..{b.max.y:0.00} " +
                          $"z {b.min.z:0.00}..{b.max.z:0.00}");
            }
            Debug.Log($"[CT] rendered to {dir}; scene NOT saved.");
            if (player != null) player.gameObject.SetActive(true);
        }

        static string GetArg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        /// <summary>Percentage of the frame this collider actually occupies.</summary>
        static float ScreenCoverage(Camera cam, Collider col, Bounds b)
        {
            int hits = 0, total = 0;
            for (int px = 20; px < 1600; px += 20)
            for (int py = 20; py < 900; py += 20)
            {
                total++;
                var ray = cam.ScreenPointToRay(new Vector3(px, py, 0f));
                if (Physics.Raycast(ray, out var h, 400f) && h.collider == col) hits++;
            }
            return total == 0 ? 0f : hits * 100f / total;
        }
    }
}
