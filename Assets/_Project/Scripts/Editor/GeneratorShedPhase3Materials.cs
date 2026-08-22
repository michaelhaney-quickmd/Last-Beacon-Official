using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Phase 3 (correction pass) — interior material readability for the Generator
    /// Shed. Geometry, layout, collision and interaction positions are untouched.
    ///
    /// Two things were wrong before this pass and both are fixed here:
    ///
    /// 1. GAMMA. The generated albedo PNGs were written with LINEAR values but are
    ///    imported as sRGB, so every authored value arrived roughly half as bright
    ///    (an authored 0.335 became ~0.105 linear). The generator now sRGB-encodes
    ///    RGB and leaves alpha linear, because alpha carries smoothness.
    ///
    /// 2. TINTS. T_PaintedSteel_BaseColor has a linear mean of 0.723 — it is a light
    ///    map, so the material tint carries the actual value. Tints here are computed
    ///    as target / mapMean rather than picked by eye.
    ///
    /// The room's blue cast was never in the albedo: it comes from the scene's blue
    /// ambient (sky 0.055/0.070/0.100) and blue exponential fog. Both are suppressed
    /// for the review captures and restored afterwards.
    /// </summary>
    public static class GeneratorShedPhase3Materials
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string MatDir = "Assets/_Project/Art/Materials/ArtPass/GenShed";
        const string TexDir = "Assets/_Project/Art/Textures/GenShed";
        const string ExtFbx = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed.fbx";
        const string IntFbx = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed_Interior.fbx";
        const string P2Fbx  = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed_P2.fbx";

        static readonly Vector3 ShedOrigin = new Vector3(17f, 17f, 13f);
        const float ShedYaw = -5f;

        // UVs are in metres, so tiling is 1 / metres-per-repeat.
        const float WallTileU = 1f / 5.0f;   // 5.0 m across — macro features read at 2–5 m
        const float WallTileV = 1f / 4.2f;   // exactly floor..eave, so the band sits at 1.000 m
        const float FloorTile = 1f / 6.0f;   // 6.0 m — only ~1.5 repeats across the room
        const float SteelTile = 1f / 2.0f;
        const float RoofTile  = 1f / 2.2f;
        const float AwnTile   = 1f / 1.8f;

        /// <summary>Linear mean of the shared painted-steel / roof-metal maps, measured.</summary>
        const float MetalMapMean = 0.723f;

        // Approved relative value hierarchy, expressed as LINEAR albedo.
        const float VFloor = 0.110f;   // authored dark; a floor is lit harder than a wall
        const float VGen   = 0.165f;   // 49%
        const float VRoof  = 0.132f;   // 39%
        const float VSteel = 0.122f;   // 36%

        [MenuItem("Tools/Last Beacon/Generator Shed Phase 3 Materials")]
        public static void Run()
        {
            ConfigureTextures();
            BuildMaterials();
            Reimport();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplySlots(ExtFbx); ApplySlots(IntFbx); ApplySlots(P2Fbx);
            Report();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Shots();
        }

        // ---------------------------------------------------------------- textures
        static void ConfigureTextures()
        {
            Tex("T_ShedInt_Concrete_BaseColor", true, false, clampV: true);
            Tex("T_ShedInt_Floor_BaseColor",    true, false, clampV: false);
            Tex("T_ShedInt_Concrete_Normal",    false, true, clampV: true);
            Tex("T_ShedInt_Floor_Normal",       false, true, clampV: false);
            AssetDatabase.Refresh();
        }

        static void Tex(string name, bool srgb, bool normal, bool clampV)
        {
            string p = $"{TexDir}/{name}.png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            if (ti == null) { Debug.LogError($"[P3] missing texture {p}"); return; }
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb;
            // Alpha is smoothness DATA, never transparency.
            ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = true;
            ti.wrapModeU = TextureWrapMode.Repeat;
            // The wall map must not repeat vertically: the damp band is painted into
            // the bottom of it and a repeat would stripe it up the gable.
            ti.wrapModeV = clampV ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.maxTextureSize = 1024;
            ti.SaveAndReimport();
        }

        static Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{n}.png");

        // --------------------------------------------------------------- materials
        static void BuildMaterials()
        {
            Directory.CreateDirectory(MatDir);

            // --- architecture: value comes from the map, tint stays white ----------
            Setup(Mat("MAT_Concrete_Interior"), T("T_ShedInt_Concrete_BaseColor"),
                  T("T_ShedInt_Concrete_Normal"), Color.white, 0f, 0.90f,
                  new Vector2(WallTileU, WallTileV));
            Setup(Mat("MAT_ShedInt_Floor"), T("T_ShedInt_Floor_BaseColor"),
                  T("T_ShedInt_Floor_Normal"), Color.white, 0f, 1.00f,
                  new Vector2(FloorTile, FloorTile));

            // --- metals: the shared map is light, so the tint carries the value ----
            Tint("MAT_ShedInt_RoofUnderside", VRoof, 0.25f, 0.20f, 0.50f, RoofTile);
            Tint("MAT_ShedExt_AwningUnderside", 0.115f, 0.20f, 0.18f, 0.30f, AwnTile);
            // Interior beams get their OWN material so they can sit darker than the
            // generator shell; sharing MAT_Metal_Painted made the two the same value
            // and the machine lost its silhouette against the structure.
            Tint("MAT_ShedInt_Steel", VSteel, 0.30f, 0.24f, 0.55f, SteelTile);
            // Generator shell + door reveal + service panel.
            Tint("MAT_Metal_Painted", VGen, 0.15f, 0.30f, 0.45f, SteelTile);

            // --- generator separation: no two of these respond the same way --------
            Tint("MAT_Metal",       0.130f, 0.62f, 0.52f, 0.35f, SteelTile);  // raw, reflective
            Tint("MAT_PanelDark",   0.075f, 0.20f, 0.34f, 0.30f, SteelTile);  // control panel
            Tint("MAT_Rubber",      0.030f, 0.00f, 0.05f, 0.20f, SteelTile, withMap: false);  // near-black, very rough
            Tint("MAT_Rust",        0.115f, 0.08f, 0.14f, 0.60f, SteelTile);  // restrained oxide
            Tint("MAT_Painted_Red", 0.135f, 0.10f, 0.36f, 0.35f, SteelTile);  // primer / e-stop
            Tint("MAT_GaugeFace",   0.720f, 0.00f, 0.58f, 0.10f, SteelTile, withMap: false);  // light, stays readable
            AssetDatabase.SaveAssets();
        }

        static Material Mat(string name)
        {
            string p = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, p);
                Debug.Log($"[P3] created {p}");
            }
            return m;
        }

        /// <summary>
        /// Set a material to a TARGET linear albedo. If it carries a base map the tint
        /// is divided by that map's mean, so the number in the table is the value that
        /// actually reaches the shader instead of a tint picked by eye.
        /// </summary>
        static void Tint(string name, float targetLinear, float metallic, float smoothness,
                         float bump, float tile, bool withMap = true)
        {
            var m = Mat(name);
            if (withMap && m.GetTexture("_BaseMap") == null)
            {
                m.SetTexture("_BaseMap", T("T_PaintedSteel_BaseColor"));
                m.SetTexture("_BumpMap", T("T_PaintedSteel_Normal"));
                m.EnableKeyword("_NORMALMAP");
            }
            float mean = m.GetTexture("_BaseMap") != null ? MetalMapMean : 1f;
            float t = Mathf.Clamp01(targetLinear / mean);
            m.SetColor("_BaseColor", new Color(t, t * 1.005f, t * 1.015f)); // a hair cool, not blue
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_SmoothnessTextureChannel", 0f);   // slider, not the map alpha
            m.SetFloat("_Smoothness", smoothness);
            if (m.GetTexture("_BumpMap") != null) { m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            EditorUtility.SetDirty(m);
            Debug.Log($"[P3] {name,-30} target linear {targetLinear:0.000} -> tint {t:0.000} " +
                      $"metallic {metallic:0.00} smooth {smoothness:0.00} bump {bump:0.00} " +
                      $"tile {(tile > 0 ? 1f / tile : 0):0.0} m");
        }

        static void Setup(Material m, Texture2D baseMap, Texture2D normal, Color tint,
                          float metallic, float bump, Vector2 tile)
        {
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_SmoothnessTextureChannel", 1f);   // smoothness from albedo alpha
            m.SetFloat("_Smoothness", 1f);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            m.SetTextureScale("_BaseMap", tile);
            m.SetTextureOffset("_BaseMap", Vector2.zero);
            EditorUtility.SetDirty(m);
        }

        // ---------------------------------------------------------------- reimport
        static void Reimport()
        {
            RemapModel(ExtFbx, new[] { "MAT_Concrete", "MAT_Concrete_Plinth", "MAT_Concrete_Interior",
                "MAT_ShedInt_RoofUnderside", "MAT_ShedExt_AwningUnderside", "MAT_Metal",
                "MAT_Metal_Painted", "MAT_Metal_Roof", "MAT_Metal_Canopy", "MAT_Emissive_Warm" }
                .ToDictionary(n => n, n => n));
            // The interior beams are remapped to their own darker material even though
            // the FBX still names the slot MAT_Metal_Painted.
            RemapModel(IntFbx, new Dictionary<string, string>
            {
                { "MAT_ShedInt_Floor", "MAT_ShedInt_Floor" },
                { "MAT_Metal_Painted", "MAT_ShedInt_Steel" },
            });
            RemapModel(P2Fbx, new[] { "MAT_Metal", "MAT_Metal_Painted", "MAT_Rust", "MAT_Emissive_Warm",
                "MAT_Painted_Red", "MAT_PanelDark", "MAT_Rubber", "MAT_GaugeFace",
                "MAT_Emissive_Red", "MAT_Emissive_Green" }.ToDictionary(n => n, n => n));
        }

        static void RemapModel(string path, Dictionary<string, string> map)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(path);
            if (imp == null) { Debug.LogError($"[P3] no importer for {path}"); return; }
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (var kv in map)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{kv.Value}.mat");
                if (mat == null) { Debug.LogWarning($"[P3] no material {kv.Value}"); continue; }
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), mat);
            }
            imp.SaveAndReimport();
        }

        /// <summary>
        /// Push the model's material slot ORDER onto the scene copies. A reimport alone
        /// does not touch a prefab's stored materials array, so new regions would keep
        /// rendering with the old material.
        /// </summary>
        static void ApplySlots(string fbx)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (asset == null) { Debug.LogError($"[P3] cannot load {fbx}"); return; }
            var wanted = asset.GetComponentsInChildren<MeshRenderer>(true)
                              .GroupBy(r => r.name).ToDictionary(g => g.Key, g => g.First().sharedMaterials);
            int n = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!wanted.TryGetValue(r.name, out var mats)) continue;
                var cur = r.sharedMaterials;
                bool same = cur.Length == mats.Length;
                if (same) for (int i = 0; i < cur.Length; i++) if (cur[i] != mats[i]) { same = false; break; }
                if (same) continue;
                r.sharedMaterials = mats; EditorUtility.SetDirty(r); n++;
                Debug.Log($"[P3] slots re-applied on {r.name}: [{string.Join(", ", mats.Select(m => m ? m.name : "NULL"))}]");
            }
            Debug.Log($"[P3] {Path.GetFileName(fbx)}: {n} renderer(s) updated");
        }

        static void Report()
        {
            foreach (var n in new[] { "SM_GeneratorShed_Body", "SM_GeneratorShed_DoorFrame",
                                      "SM_ShedInt_Floor", "SM_ShedInt_Steel", "SM_Generator_Main" })
            {
                var r = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == n);
                if (r == null) { Debug.LogWarning($"[P3] {n} not in scene"); continue; }
                Debug.Log($"[P3] {n,-28} -> {string.Join(", ", r.sharedMaterials.Select(m => m ? m.name : "NULL"))}");
                foreach (var m in r.sharedMaterials.Where(x => x != null).Distinct())
                {
                    var bm = m.GetTexture("_BaseMap");
                    var st = m.GetTextureScale("_BaseMap");
                    Debug.Log($"[P3]      {m.name,-28} map={(bm ? bm.name : "NONE")} tint={m.GetColor("_BaseColor")} " +
                              $"tiling=({st.x:0.000},{st.y:0.000}) metallic={m.GetFloat("_Metallic"):0.00} " +
                              $"smoothSrc={(m.GetFloat("_SmoothnessTextureChannel") == 1f ? "alpha" : "slider " + m.GetFloat("_Smoothness").ToString("0.00"))}");
                }
                // What UV range does this mesh actually present to that tiling?
                var mf2 = r.GetComponent<MeshFilter>();
                if (mf2 != null && mf2.sharedMesh != null && mf2.sharedMesh.uv.Length > 0)
                {
                    var uvs = mf2.sharedMesh.uv;
                    Debug.Log($"[P3]      UV U[{uvs.Min(q => q.x):0.00},{uvs.Max(q => q.x):0.00}] " +
                              $"V[{uvs.Min(q => q.y):0.00},{uvs.Max(q => q.y):0.00}]");
                }
            }
        }

        // ------------------------------------------------------------------- shots
        static Vector3 W(float lx, float lz, float h)
        {
            float t = ShedYaw * Mathf.Deg2Rad, c = Mathf.Cos(t), s = Mathf.Sin(t);
            return new Vector3(ShedOrigin.x + lx * c + lz * s, ShedOrigin.y + h, ShedOrigin.z - lx * s + lz * c);
        }

        static void Shots()
        {
            const string dir = "GenShed_Phase3_Review";
            Directory.CreateDirectory(dir);

            // ---- remember everything the review rig disturbs ----------------------
            var sceneLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var lightWas = sceneLights.ToDictionary(l => l, l => l.enabled);
            var ambMode = RenderSettings.ambientMode;
            var ambLight = RenderSettings.ambientLight;
            var fogOn = RenderSettings.fog;

            // Doors open for the interior views, restored afterwards. They hinge on
            // local Z and are authored closed at identity.
            var leaves = new[] { "SM_GeneratorShed_Door_L", "SM_GeneratorShed_Door_R" };
            var doorWas = new Dictionary<Transform, Quaternion>();
            for (int i = 0; i < leaves.Length; i++)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == leaves[i]);
                if (t == null) continue;
                doorWas[t] = t.localRotation;
                t.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 95f : -95f);
            }

            // ================= SET A — temporary neutral material review ===========
            // Bright, white, no fog, no coloured ambient. This is NOT game lighting;
            // it exists so albedo, roughness and normal response can be judged.
            foreach (var l in sceneLights) l.enabled = false;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f);

            var rig = new List<GameObject>();
            void L(string n, LightType ty, Vector3 pos, Vector3 rot, float inten, float range)
            {
                var g = new GameObject(n); var lt = g.AddComponent<Light>();
                lt.type = ty; lt.color = Color.white; lt.intensity = inten; lt.range = range;
                lt.shadows = ty == LightType.Directional ? LightShadows.Soft : LightShadows.None;
                g.transform.position = pos; g.transform.rotation = Quaternion.Euler(rot);
                rig.Add(g);
            }
            L("__RevKey", LightType.Directional, W(0f, 0f, 6f), new Vector3(40f, 120f, 0f), 0.35f, 1f);
            L("__RevA", LightType.Point, W(-2.6f, -1.6f, 3.2f), Vector3.zero, 2.4f, 13f);
            L("__RevB", LightType.Point, W(-2.6f,  1.6f, 3.2f), Vector3.zero, 2.4f, 13f);
            L("__RevC", LightType.Point, W( 3.0f, -1.6f, 3.2f), Vector3.zero, 2.2f, 13f);
            L("__RevD", LightType.Point, W( 3.0f,  1.6f, 3.2f), Vector3.zero, 2.2f, 13f);
            DynamicGI.UpdateEnvironment();

            Capture(dir, "A1_doorway",            W(-7.6f, 0f, 1.70f),    W(0.6f, 0f, 1.15f), 62f);
            Capture(dir, "A2_generator_3q",       W(-3.3f, -2.5f, 1.70f), W(0.9f, 0.5f, 1.05f), 62f);
            Capture(dir, "A3_wall_closeup",       W(-2.4f, 2.30f, 1.55f), W(-2.4f, 3.66f, 1.95f), 48f);
            Capture(dir, "A4_wall_plinth",        W(-2.5f, 2.30f, 1.30f), W(-2.5f, 3.66f, 0.55f), 58f);
            Capture(dir, "A5_floor",              W(-2.8f, 1.4f, 1.60f),  W(-0.9f, -0.2f, 0.02f), 62f);
            Capture(dir, "A6_generator_material", W(-1.5f, -1.7f, 1.45f), W(0.35f, -0.95f, 1.20f), 42f);
            Capture(dir, "A7_roof_steel",         W(-1.4f, 1.7f, 1.60f),  W(1.5f, -0.4f, 4.25f), 66f);
            Capture(dir, "A8_electrical_wall",    W(2.0f, -1.9f, 1.65f),  W(4.5f, -2.1f, 1.55f), 58f);

            // measure the rendered value hierarchy off A1 rather than assert it
            MeasureHierarchy(dir);

            // ================= SET B — restore the game's night lighting ===========
            foreach (var g in rig) Object.DestroyImmediate(g);
            foreach (var kv in lightWas) if (kv.Key != null) kv.Key.enabled = kv.Value;
            RenderSettings.ambientMode = ambMode;
            RenderSettings.ambientLight = ambLight;
            RenderSettings.fog = fogOn;
            DynamicGI.UpdateEnvironment();

            Capture(dir, "B1_doorway_night",    W(-7.6f, 0f, 1.70f),    W(0.6f, 0f, 1.15f), 62f);
            Capture(dir, "B2_generator_night",  W(-3.3f, -2.5f, 1.70f), W(0.9f, 0.5f, 1.05f), 62f);
            Capture(dir, "B3_electrical_night", W(2.0f, -1.9f, 1.65f),  W(4.5f, -2.1f, 1.55f), 58f);

            foreach (var kv in doorWas) if (kv.Key != null) kv.Key.localRotation = kv.Value;
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[P3] shots -> {Path.GetFullPath(dir)}; scene lighting and doors restored");
        }

        /// <summary>Sample the review render and report the value hierarchy actually achieved.</summary>
        static void MeasureHierarchy(string dir)
        {
            var path = Path.Combine(dir, "A1_doorway.png");
            if (!File.Exists(path)) return;
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(path));
            float Sample(float u, float v, int r = 10)
            {
                int cx = (int)(u * tex.width), cy = (int)(v * tex.height);
                float s = 0; int n = 0;
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        int px = Mathf.Clamp(cx + x, 0, tex.width - 1), py = Mathf.Clamp(cy + y, 0, tex.height - 1);
                        s += tex.GetPixel(px, py).grayscale; n++;
                    }
                return s / n;
            }
            float wall = Sample(0.385f, 0.58f);
            Debug.Log("[P3] rendered value probe on A1 (grayscale, upper wall = 100%):");
            foreach (var probe in new[] { ("upper wall", 0.385f, 0.58f), ("floor", 0.50f, 0.28f),
                                          ("lower band", 0.385f, 0.46f), ("generator", 0.50f, 0.47f),
                                          ("roof/steel", 0.42f, 0.93f) })
            {
                float v = Sample(probe.Item2, probe.Item3);
                Debug.Log($"[P3]    {probe.Item1,-12} {v:0.000}   {(wall > 0 ? v / wall * 100f : 0):0} %");
            }
            Object.DestroyImmediate(tex);
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var go = new GameObject("__P3Cam"); var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.07f);
            cam.nearClipPlane = 0.03f; cam.farClipPlane = 600f; cam.fieldOfView = fov;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
            Debug.Log($"[P3] shot {name}");
        }
    }
}
