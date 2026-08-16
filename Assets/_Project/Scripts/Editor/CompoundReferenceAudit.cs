using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Gen = LastBeacon.Editor.VerticalIslandBlockoutGenerator;

namespace LastBeacon.Editor
{
    /// <summary>
    /// READ ONLY. Documents the current upper compound — transforms, oriented
    /// footprints, entrances, courtyard, markers — and renders the review shots.
    /// Never saves the scene, never moves anything.
    /// </summary>
    public static class CompoundReferenceAudit
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const int W = 1600, H = 900;
        const float Fov = 72f, AerialFov = 50f, Eye = 1.7f;

        static readonly CultureInfo C = CultureInfo.InvariantCulture;
        static StringBuilder _json;

        [MenuItem("Tools/Last Beacon/Compound Reference Audit")]
        public static void Run()
        {
            string root = GetArg("-auditOutput") ?? Path.Combine(Path.GetTempPath(), "compound-audit");
            string shots = Path.Combine(root, "screenshots");
            Directory.CreateDirectory(shots);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            _json = new StringBuilder();
            _json.Append("{\n");

            Buildings();
            Courtyard();
            Entrances();
            Markers();
            Constraints();

            var labels = CaptureAll(shots);
            _json.Append("  \"aerial04_label_screen_coords\": ").Append(labels).Append("\n}\n");

            File.WriteAllText(Path.Combine(root, "Compound_Current_Spec.json"), _json.ToString());
            Debug.Log($"[CA] wrote spec + screenshots to {root}");

            if (player != null) player.gameObject.SetActive(true);
            Debug.Log("[CA] scene NOT saved, nothing moved.");
        }

        // ------------------------------------------------------------------ buildings

        static readonly (string Label, string Body, string Roof, Vector2 Centre, float Yaw, Vector2 Foot,
            float Wall, float Ridge, int DoorIndex)[] Blds =
        {
            ("Keeper's House",  "House_Body", "House_Roof", new Vector2(-18f, 20.5f), -3f,
                new Vector2(12f, 9f), 5.5f, 2.6f, 3),
            ("Generator Shed",  "Shed_Body", "Shed_Roof", new Vector2(17f, 13f), -5f,
                new Vector2(10f, 8f), 4.2f, 1f, 0),
            ("Workshop",        "Workshop_Body", "Workshop_Roof", new Vector2(19f, 26.2f), -17f,
                new Vector2(11f, 8f), 4.5f, 1.8f, 1),
            ("Stores / Radio",  "Stores_Body", "Stores_Roof", new Vector2(-17.5f, 8.5f), 18f,
                new Vector2(9f, 7f), 3.8f, 1.2f, 2)
        };

        static void Buildings()
        {
            _json.Append("  \"buildings\": [\n");
            var yard = new Vector3(0f, Gen.TierCompound, 17f);
            var gate = Gen.WpCompoundEntrance;
            var lh = new Vector3(Gen.LighthouseXZ.x, Gen.TierLighthouse, Gen.LighthouseXZ.y);

            for (int i = 0; i < Blds.Length; i++)
            {
                var b = Blds[i];
                var go = Find(b.Body);
                var t = go.transform;
                var aabb = go.GetComponent<Renderer>().bounds;
                var door = Gen.Doorways[b.DoorIndex];
                var thr = door.Threshold(door.Sill);
                var outward = door.Outward;

                // Oriented footprint: the four ground corners in the building's own frame.
                var corners = new[]
                {
                    new Vector2(-b.Foot.x / 2f, -b.Foot.y / 2f), new Vector2(b.Foot.x / 2f, -b.Foot.y / 2f),
                    new Vector2(b.Foot.x / 2f, b.Foot.y / 2f), new Vector2(-b.Foot.x / 2f, b.Foot.y / 2f)
                }.Select(c => Gen.At(b.Centre, b.Yaw, c, Gen.TierCompound)).ToArray();

                Debug.Log($"[CA] {b.Label}: pos {t.position} rot {t.rotation.eulerAngles} " +
                          $"foot {b.Foot.x}x{b.Foot.y} yaw {b.Yaw} door {thr} facing {Bearing(outward)}");

                _json.Append("    {");
                J("label", b.Label); J("object", b.Body);
                JV("position", t.position); JV("rotation_euler", t.rotation.eulerAngles); JV("local_scale", t.localScale);
                JV("aabb_min", aabb.min); JV("aabb_max", aabb.max);
                _json.Append("\"oriented_footprint\": [")
                     .Append(string.Join(", ", corners.Select(c => $"[{c.x.ToString("0.00", C)}, {c.z.ToString("0.00", C)}]")))
                     .Append("], ");
                JF("width", b.Foot.x); JF("depth", b.Foot.y); JF("yaw_degrees", b.Yaw);
                JF("wall_height", b.Wall); JF("ridge_height", b.Wall + b.Ridge);
                JF("floor_y", Gen.TierCompound);
                JV("door_threshold", thr); J("door_facing", Bearing(outward));
                JV("door_outward", outward);
                JF("door_width", door.Width); JF("door_height", door.Height); JF("door_sill", door.Sill);
                JF("dist_to_courtyard_centre", Flat(t.position, yard));
                JF("dist_to_inner_gate", Flat(t.position, gate));
                JF("dist_to_lighthouse", Flat(t.position, lh));
                _json.Length -= 2;
                _json.Append("},\n");
            }
            _json.Length -= 2;
            _json.Append("\n  ],\n");
        }

        // ------------------------------------------------------------------ courtyard

        static readonly Vector2[] YardPoly =
        {
            new Vector2(-7.5f, 6.5f), new Vector2(2.5f, 6.5f), new Vector2(9.5f, 11.5f),
            new Vector2(10.5f, 19.5f), new Vector2(6.5f, 25f), new Vector2(-3f, 25.5f),
            new Vector2(-10f, 21f), new Vector2(-10.5f, 12.5f)
        };

        static void Courtyard()
        {
            var yard = Find("MainYard").GetComponent<Renderer>().bounds;
            var centre = new Vector3(0f, Gen.TierCompound + 1f, 17f);

            float min = float.MaxValue, max = 0f;
            string minDir = "", maxDir = "";
            var spokes = new List<string>();
            for (int a = 0; a < 360; a += 15)
            {
                var dir = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad));
                float d = Physics.Raycast(centre, dir, out var hit, 40f) ? hit.distance : 40f;
                string what = hit.collider != null ? hit.collider.name : "open";
                spokes.Add($"{{\"bearing\": {a}, \"distance\": {d.ToString("0.00", C)}, \"hits\": \"{what}\"}}");
                if (d < min) { min = d; minDir = $"{a} deg ({what})"; }
                if (d > max) { max = d; maxDir = $"{a} deg"; }
            }

            Debug.Log($"[CA] courtyard bounds x {yard.min.x:0.0}..{yard.max.x:0.0} " +
                      $"z {yard.min.z:0.0}..{yard.max.z:0.0} top {yard.max.y:0.00}; " +
                      $"nearest obstruction {min:0.00} m at {minDir}, longest open {max:0.00} m at {maxDir}");

            _json.Append("  \"courtyard\": {");
            JV("centre", new Vector3(0f, Gen.TierCompound + 0.04f, 17f));
            _json.Append("\"polygon\": [")
                 .Append(string.Join(", ", YardPoly.Select(p => $"[{p.x.ToString("0.0", C)}, {p.y.ToString("0.0", C)}]")))
                 .Append("], ");
            JV("aabb_min", yard.min); JV("aabb_max", yard.max);
            JF("width_x", yard.size.x); JF("depth_z", yard.size.z); JF("surface_y", yard.max.y);
            JF("nearest_obstruction_m", min); J("nearest_obstruction_at", minDir);
            JF("longest_open_m", max);
            _json.Append("\"radial_clearance\": [").Append(string.Join(", ", spokes)).Append("]");
            _json.Append("},\n");
        }

        // ------------------------------------------------------------------ entrances

        static void Entrances()
        {
            var yard = new Vector3(0f, Gen.TierCompound + 1.2f, 17f);
            var lantern = Gen.LanternCentre;
            var gate = Gen.WpCompoundEntrance + Vector3.up * 1.2f;

            _json.Append("  \"entrances\": [\n");
            foreach (var b in Blds)
            {
                var door = Gen.Doorways[b.DoorIndex];
                var eye = door.Threshold(door.Sill + Eye) + door.Outward * 0.8f;

                string first = "open sky";
                if (Physics.Raycast(eye, door.Outward, out var ahead, 60f)) first = ahead.collider.name;

                _json.Append("    {");
                J("building", b.Label);
                J("wall", door.LocalX > 0 ? "local +X" : "local -X");
                J("faces", Bearing(door.Outward));
                JV("eye", eye);
                J("first_thing_seen", first);
                JB("courtyard_centre_visible", Clear(eye, yard));
                JB("lighthouse_visible", ClearTo(eye, lantern, "Lighthouse_"));
                JB("inner_gate_visible", Clear(eye, gate));
                _json.Length -= 2; _json.Append("},\n");

                Debug.Log($"[CA] {b.Label} door faces {Bearing(door.Outward)}; first sees {first}; " +
                          $"yard {Clear(eye, yard)}, lighthouse {ClearTo(eye, lantern, "Lighthouse_")}, " +
                          $"gate {Clear(eye, gate)}");
            }
            _json.Length -= 2; _json.Append("\n  ],\n");
        }

        // -------------------------------------------------------------------- markers

        static void Markers()
        {
            _json.Append("  \"markers\": [\n");
            foreach (var m in Object.FindObjectsByType<LastBeacon.Blockout.BlockoutMarker>(FindObjectsSortMode.None)
                         .OrderBy(m => m.name))
            {
                var p = m.transform.position;
                if (p.y < Gen.TierCompound - 1f) continue;      // compound and above only
                _json.Append("    {");
                J("name", m.name);
                J("parent", m.transform.parent != null ? m.transform.parent.name : "(root)");
                JV("position", p);
                JB("moves_with_building", false);
                _json.Length -= 2; _json.Append("},\n");
            }
            _json.Length -= 2; _json.Append("\n  ],\n");
        }

        static void Constraints()
        {
            _json.Append("  \"nearby_structures\": [\n");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                         .Where(r => r.bounds.center.y > Gen.TierCompound - 2f &&
                                     r.bounds.center.y < Gen.TierCompound + 8f &&
                                     (r.name.StartsWith("Rock_") || r.name.StartsWith("Yard_") ||
                                      r.name.StartsWith("Cliff_") || r.name.Contains("Retain")))
                         .OrderBy(r => r.name))
            {
                var b = r.bounds;
                _json.Append("    {");
                J("name", r.name);
                JV("aabb_min", b.min); JV("aabb_max", b.max);
                _json.Length -= 2; _json.Append("},\n");
            }
            _json.Length -= 2; _json.Append("\n  ],\n");
        }

        // ----------------------------------------------------------------- screenshots

        static string CaptureAll(string dir)
        {
            var yard = new Vector3(0f, Gen.TierCompound + Eye, 17f);
            var fp = new List<(string Name, Vector3 Eye, Vector3 Look)>
            {
                ("FP_InnerGate",        new Vector3(-5f, Gen.TierCompound + Eye, 4f),   new Vector3(2f, Gen.TierCompound + 4f, 26f)),
                ("FP_Courtyard_North",  yard, new Vector3(0f, Gen.TierLighthouse + 6f, 38f)),
                ("FP_Courtyard_West",   yard, new Vector3(-18f, Gen.TierCompound + 2f, 17f)),
                ("FP_Courtyard_East",   yard, new Vector3(18f, Gen.TierCompound + 2f, 17f)),
                ("FP_Courtyard_South",  yard, new Vector3(-6f, Gen.TierCompound + 1f, 2f)),
                ("FP_LighthouseStairs", new Vector3(0f, Gen.TierCompound + Eye, 21.6f), new Vector3(0f, Gen.TierCompound + 1f, 2f))
            };
            foreach (var (b, shot) in new[]
                     {
                         (Blds[0], "FP_KeeperDoor"), (Blds[1], "FP_GeneratorDoor"),
                         (Blds[2], "FP_WorkshopDoor"), (Blds[3], "FP_StoresDoor")
                     })
            {
                var d = Gen.Doorways[b.DoorIndex];
                var e = d.Threshold(d.Sill + Eye) + d.Outward * 0.6f;
                fp.Add((shot, e, e + d.Outward * 30f));
            }

            var aerials = new (string Name, Vector3 Eye, Vector3 Look, float Fov)[]
            {
                ("Aerial_South",   new Vector3(0f, 52f, -34f), new Vector3(0f, 20f, 24f), AerialFov),
                ("Aerial_North",   new Vector3(0f, 54f, 78f),  new Vector3(0f, 18f, 14f), AerialFov),
                ("Aerial_TopDown", new Vector3(0f, 130f, 19f), new Vector3(0f, 17f, 19.1f), 45f),
                ("Aerial_ReferenceMatch_Clean", new Vector3(-2f, 44f, -30f), new Vector3(0f, 21f, 24f), 52f)
            };

            var camGo = new GameObject("__AuditCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;

            bool fogWas = RenderSettings.fog;
            var skyWas = RenderSettings.ambientSkyColor;
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.52f, 0.55f);
            var fill = new GameObject("__AuditFill");
            fill.transform.rotation = Quaternion.Euler(46f, 205f, 0f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 1.3f; fl.shadows = LightShadows.Soft;

            string labels = "{}";
            try
            {
                foreach (var s in fp) Shoot(cam, dir, s.Name, s.Eye, s.Look, Fov);
                foreach (var a in aerials) Shoot(cam, dir, a.Name, a.Eye, a.Look, a.Fov);

                // Screen coordinates for the labelled version, from the same camera.
                var last = aerials.Last();
                cam.transform.position = last.Eye;
                cam.transform.rotation = Quaternion.LookRotation((last.Look - last.Eye).normalized, Vector3.up);
                cam.fieldOfView = last.Fov;

                var pts = new List<string>();
                void Pt(string name, Vector3 world)
                {
                    var sp = cam.WorldToScreenPoint(world);
                    if (sp.z <= 0f) return;
                    pts.Add($"\"{name}\": [{sp.x.ToString("0", C)}, {(H - sp.y).ToString("0", C)}]");
                }
                Pt("1 Workshop", new Vector3(-18.8f, 22f, 27.2f));
                Pt("2 Generator", new Vector3(-18f, 22f, 13.5f));
                Pt("3 Keepers House", new Vector3(18f, 23f, 20f));
                Pt("4 Stores Radio", new Vector3(18f, 21f, 7.8f));
                Pt("L Lighthouse", new Vector3(0f, 34f, 38f));
                Pt("Inner Gate", new Vector3(-6f, 18f, 2f));
                Pt("Courtyard", new Vector3(0f, 17.5f, 17f));
                foreach (var b in Blds)
                {
                    var d = Gen.Doorways[b.DoorIndex];
                    Pt("door_from_" + b.Label, d.Threshold(d.Sill + 1f));
                    Pt("door_to_" + b.Label, d.Threshold(d.Sill + 1f) + d.Outward * 4f);
                }
                labels = "{" + string.Join(", ", pts) + "}";
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(fill);
                RenderSettings.fog = fogWas;
                RenderSettings.ambientSkyColor = skyWas;
            }
            return labels;
        }

        static void Shoot(Camera cam, string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release(); Object.DestroyImmediate(rt);
        }

        // -------------------------------------------------------------------- helpers

        static bool Clear(Vector3 from, Vector3 to)
        {
            var d = to - from;
            return !Physics.Raycast(from, d.normalized, d.magnitude - 0.3f);
        }

        static bool ClearTo(Vector3 from, Vector3 to, string okPrefix)
        {
            var d = to - from;
            if (!Physics.Raycast(from, d.normalized, out var h, d.magnitude)) return true;
            return h.collider.name.StartsWith(okPrefix);
        }

        static string Bearing(Vector3 dir)
        {
            float a = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (a < 0) a += 360f;
            string[] names = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            return $"{names[Mathf.RoundToInt(a / 22.5f) % 16]} ({a:0} deg)";
        }

        static float Flat(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static GameObject Find(string n) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == n)?.gameObject;

        static void J(string k, string v) => _json.Append($"\"{k}\": \"{v}\", ");
        static void JF(string k, float v) => _json.Append($"\"{k}\": {v.ToString("0.000", C)}, ");
        static void JB(string k, bool v) => _json.Append($"\"{k}\": {(v ? "true" : "false")}, ");
        static void JV(string k, Vector3 v) => _json.Append(
            $"\"{k}\": [{v.x.ToString("0.000", C)}, {v.y.ToString("0.000", C)}, {v.z.ToString("0.000", C)}], ");

        static string GetArg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
