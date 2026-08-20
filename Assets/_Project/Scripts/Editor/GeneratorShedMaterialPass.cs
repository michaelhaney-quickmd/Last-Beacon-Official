using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Material + practical-lamp pass for the generator shed.
    ///
    /// Materials are authored NEUTRAL — the cold night look must come from the
    /// existing scene ambient, moon key, fog and beam, not from blue baked into
    /// base colours. Global art-pass lighting is untouched; only this building's
    /// own practical is retuned.
    /// </summary>
    public static class GeneratorShedMaterialPass
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string Fbx       = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed.fbx";
        const string MatDir    = "Assets/_Project/Art/Materials/ArtPass/GenShed";
        const string TexDir    = "Assets/_Project/Art/Textures/GenShed";
        const string InstName  = "PF_GeneratorShed";

        // 4 m per tile against 1-UV-unit-per-metre UVs. 8 m read as flat on screen;
        // 4 m puts the mottling at a size the eye actually resolves in play.
        const float Tile = 0.25f;

        // base colour, metallic, smoothness, textured?
        static readonly (string name, Color c, float metal, float smooth, bool tex)[] Spec =
        {
            ("MAT_Concrete",        new Color(0.340f, 0.360f, 0.370f), 0.00f, 0.12f, true),
            ("MAT_Concrete_Plinth", new Color(0.175f, 0.180f, 0.185f), 0.00f, 0.13f, true),
            ("MAT_Metal_Painted",   new Color(0.100f, 0.110f, 0.120f), 0.40f, 0.25f, false),
            ("MAT_Metal",           new Color(0.070f, 0.080f, 0.090f), 0.50f, 0.27f, false),
            ("MAT_Rust",            new Color(0.115f, 0.070f, 0.050f), 0.25f, 0.18f, false),
        };

        // practical: keep the colour temperature, drop the output
        const float LampIntensity = 2.6f;   // was 6
        const float LampRange     = 13f;    // was 16

        [MenuItem("Tools/Last Beacon/Generator Shed Material Pass")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Materials";
            Directory.CreateDirectory(shots);

            // ---------- texture importers ---------------------------------------
            void Cfg(string file, bool srgb, bool normal)
            {
                string p = $"{TexDir}/{file}";
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                if (ti == null) { Debug.LogError($"[MAT] missing texture {p}"); return; }
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (!normal) ti.sRGBTexture = srgb;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Bilinear;
                ti.anisoLevel = 4;
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            Cfg("T_Concrete_BaseColor.png", true, false);
            Cfg("T_Concrete_AO.png", false, false);
            Cfg("T_Concrete_Normal.png", false, true);

            var texBase = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Concrete_BaseColor.png");
            var texNrm  = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Concrete_Normal.png");
            var texAO   = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_Concrete_AO.png");

            // ---------- materials -------------------------------------------------
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mats = new Dictionary<string, Material>();
            foreach (var s in Spec)
            {
                string p = $"{MatDir}/{s.name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
                m.shader = shader;
                m.SetColor("_BaseColor", s.c);
                m.SetFloat("_Metallic", s.metal);
                m.SetFloat("_Smoothness", s.smooth);
                m.SetFloat("_WorkflowMode", 1f);            // metallic workflow
                m.SetFloat("_Surface", 0f);                 // opaque: base alpha is smoothness, not transparency
                m.SetFloat("_AlphaClip", 0f);
                if (s.tex)
                {
                    m.SetTexture("_BaseMap", texBase);
                    m.SetTexture("_BumpMap", texNrm);
                    m.SetTexture("_OcclusionMap", texAO);
                    m.EnableKeyword("_NORMALMAP");
                    m.EnableKeyword("_OCCLUSIONMAP");
                    m.DisableKeyword("_METALLICSPECGLOSSMAP");
                    m.SetFloat("_BumpScale", 0.35f);
                    m.SetFloat("_OcclusionStrength", 0.65f);
                    // smoothness lives in the base map's ALPHA channel
                    m.SetFloat("_SmoothnessTextureChannel", 1f);
                    m.SetTextureScale("_BaseMap", new Vector2(Tile, Tile));
                    m.SetTextureOffset("_BaseMap", Vector2.zero);
                }
                EditorUtility.SetDirty(m);
                mats[s.name] = m;
                Debug.Log($"[MAT] {s.name,-22} base ({s.c.r:0.000}, {s.c.g:0.000}, {s.c.b:0.000})  " +
                          $"metallic {s.metal:0.00}  smoothness {s.smooth:0.00}  " +
                          $"{(s.tex ? $"textured, tiling {Tile} (= {1f / Tile:0} m per tile)" : "untextured")}");
            }
            // emissive bulb: keep warm but stop it blooming
            var em = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/MAT_Emissive_Warm.mat");
            if (em != null)
            {
                em.SetColor("_EmissionColor", new Color(1.00f, 0.66f, 0.34f) * 2.6f);
                EditorUtility.SetDirty(em);
                Debug.Log("[MAT] MAT_Emissive_Warm    emission (1.00, 0.66, 0.34) x 2.6 (was x6)");
            }
            AssetDatabase.SaveAssets();

            // ---------- reimport the FBX (it now carries a plinth slot) -----------
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.useFileScale = true; imp.globalScale = 1f; imp.addCollider = false;
            imp.importNormals = ModelImporterNormals.Import;
            imp.weldVertices = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.SaveAndReimport();

            // slot order straight from the FBX, so remapping cannot drift
            var srcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var slotOrder = new Dictionary<string, string[]>();
            foreach (var r in srcPrefab.GetComponentsInChildren<MeshRenderer>())
                slotOrder[r.name] = r.sharedMaterials
                    .Select(x => x == null ? "" : x.name.Replace(" (Instance)", "").Trim()).ToArray();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);

            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            if (pf == null) { Debug.LogError("[MAT] PF_GeneratorShed not in the scene"); return; }

            int applied = 0, unmatched = 0;
            foreach (var r in pf.GetComponentsInChildren<MeshRenderer>())
            {
                if (!slotOrder.TryGetValue(r.name, out var order)) continue;
                var arr = new Material[order.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    if (mats.TryGetValue(order[i], out var m)) { arr[i] = m; applied++; }
                    else
                    {
                        var fallback = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{order[i]}.mat");
                        arr[i] = fallback;
                        if (fallback == null) { unmatched++; Debug.LogWarning($"[MAT] no material for '{order[i]}' on {r.name}"); }
                    }
                }
                r.sharedMaterials = arr;
            }
            Debug.Log($"[MAT] material slots applied: {applied}, unmatched: {unmatched}");
            var bodyR = pf.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("Body"));
            if (bodyR != null)
                Debug.Log($"[MAT] Body slots now: {string.Join(", ", bodyR.sharedMaterials.Select(m => m ? m.name : "null"))}");

            // ---------- practical lamp -------------------------------------------
            var lamp = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Lamp_GeneratorShed");
            if (lamp != null)
            {
                Debug.Log($"[MAT] lamp before: intensity {lamp.intensity}, range {lamp.range}, " +
                          $"{(lamp.useColorTemperature ? lamp.colorTemperature + "K" : "no temperature")}");
                lamp.intensity = LampIntensity; lamp.range = LampRange;
                lamp.useColorTemperature = true; lamp.colorTemperature = 2500f;
                lamp.color = Color.white;
                Debug.Log($"[MAT] lamp after : intensity {lamp.intensity}, range {lamp.range}, 2500K");
            }
            Physics.SyncTransforms();

            // ---------- renders ---------------------------------------------------
            var all = pf.GetComponentsInChildren<MeshRenderer>();
            var bb = all[0].bounds; foreach (var r in all) bb.Encapsulate(r.bounds);
            var front  = new Vector3(2.0f, 18.7f, 15.0f);
            var frontL = new Vector3(15.0f, 18.6f, 12.6f);
            var tq     = new Vector3(7.0f, 23.0f, 20.5f);
            var tqL    = new Vector3(17.0f, 18.6f, 13.0f);
            var close  = new Vector3(8.6f, 18.4f, 12.4f);
            var closeL = new Vector3(12.4f, 19.1f, 12.7f);

            Capture(shots, "1_front_gameplay", front, frontL, 60f);
            Capture(shots, "2_threequarter",   tq, tqL, 55f);
            Capture(shots, "3_closeup_lamp",   close, closeL, 45f);
            if (lamp != null) lamp.enabled = false;
            Capture(shots, "4_closeup_no_lamp", close, closeL, 45f);
            Capture(shots, "5_front_no_lamp",   front, frontL, 60f);
            if (lamp != null) lamp.enabled = true;

            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[MAT] scene saved. Geometry and global lighting untouched.");
        }


        /// <summary>Tight detail shots: wall base (plinth vs wall) and a clean wall panel.</summary>
        [MenuItem("Tools/Last Beacon/Generator Shed Material Detail")]
        public static void Detail()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Materials";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "PF_GeneratorShed");
            var lamp = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Lamp_GeneratorShed");

            // straight on at the wall left of the doors, filling frame with concrete
            var wallPt = pf.TransformPoint(new Vector3(-5.05f, 1.6f, 2.6f));
            var outward = (pf.TransformPoint(new Vector3(-9f, 1.6f, 2.6f)) - wallPt).normalized;
            Capture(shots, "6_wall_panel",  wallPt + outward * 3.2f, wallPt, 45f);
            // wall base: plinth band against the wall above it
            var basePt = pf.TransformPoint(new Vector3(-5.05f, 0.75f, 2.2f));
            Capture(shots, "7_wall_base",   basePt + outward * 2.6f + Vector3.up * 0.5f, basePt, 45f);
            // gameplay distance, ~8 m back, to judge whether the texture still reads
            Capture(shots, "8_wall_gameplay_dist", wallPt + outward * 8f + Vector3.up * 0.4f, wallPt, 50f);
            if (lamp != null) lamp.enabled = false;
            Capture(shots, "9_wall_base_no_lamp", basePt + outward * 2.6f + Vector3.up * 0.5f, basePt, 45f);
            if (lamp != null) lamp.enabled = true;
            if (player != null) player.gameObject.SetActive(true);
            Debug.Log("[MAT] detail shots written; scene not saved");
        }


        /// <summary>Is the blockout shell z-fighting with the art shell?</summary>
        [MenuItem("Tools/Last Beacon/Diagnose Shed Surface Overlap")]
        public static void Overlap()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Materials";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);

            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "PF_GeneratorShed");
            var artBody = pf.GetComponentsInChildren<MeshRenderer>()
                            .FirstOrDefault(r => r.name.Contains("Body"));
            var grp = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            var blockRends = grp.GetComponentsInChildren<MeshRenderer>();

            Debug.Log("[OVL] ---- blockout pieces still RENDERING on this plot ----");
            foreach (var r in blockRends.Where(r => r.enabled))
            {
                float gap = Mathf.Min(
                    Mathf.Abs(r.bounds.min.x - artBody.bounds.min.x),
                    Mathf.Abs(r.bounds.max.x - artBody.bounds.max.x));
                Debug.Log($"[OVL]   {r.name,-24} bounds {r.bounds.size} | nearest wall-face gap to art: {gap * 100f:0.0} cm");
            }
            var b = blockRends.FirstOrDefault(r => r.name == "Shed_Body");
            if (b != null)
            {
                Debug.Log($"[OVL] blockout Shed_Body X {b.bounds.min.x:0.000}..{b.bounds.max.x:0.000}  " +
                          $"Z {b.bounds.min.z:0.000}..{b.bounds.max.z:0.000}");
                Debug.Log($"[OVL] art  Body        X {artBody.bounds.min.x:0.000}..{artBody.bounds.max.x:0.000}  " +
                          $"Z {artBody.bounds.min.z:0.000}..{artBody.bounds.max.z:0.000}");
                Debug.Log($"[OVL] surface separation: -X {Mathf.Abs(b.bounds.min.x - artBody.bounds.min.x) * 1000f:0} mm, " +
                          $"+X {Mathf.Abs(b.bounds.max.x - artBody.bounds.max.x) * 1000f:0} mm, " +
                          $"-Z {Mathf.Abs(b.bounds.min.z - artBody.bounds.min.z) * 1000f:0} mm, " +
                          $"+Z {Mathf.Abs(b.bounds.max.z - artBody.bounds.max.z) * 1000f:0} mm");
                Debug.LogWarning("[OVL] surfaces closer than ~10 mm will z-fight and break up the material");
            }
            var eye = new Vector3(2.0f, 18.7f, 15.0f); var look = new Vector3(15.0f, 18.6f, 12.6f);
            Capture(shots, "A_blockout_visible", eye, look, 60f);
            var was = blockRends.Select(r => r.enabled).ToArray();
            foreach (var r in blockRends) r.enabled = false;
            Capture(shots, "B_blockout_hidden", eye, look, 60f);
            Capture(shots, "C_blockout_hidden_wall", new Vector3(8.6f, 18.4f, 12.4f),
                    new Vector3(12.4f, 19.1f, 12.7f), 45f);
            for (int i = 0; i < blockRends.Length; i++) blockRends[i].enabled = was[i];
            if (player != null) player.gameObject.SetActive(true);
            Debug.Log("[OVL] blockout renderers restored; scene not saved");
        }


        // Blockout pieces the ART SHELL now replaces. Renderers only — colliders were
        // already handled in the collision pass, and nothing is deleted, so this is
        // fully reversible.
        static readonly string[] SupersededByArt =
        {
            "Shed_Body", "Shed_Roof", "Shed_DoorLeaf_A", "Shed_DoorLeaf_B",
            "Shed_LeanToRoof", "Shed_LeanToPost_W", "Shed_LeanToPost_E",
        };
        // Props the art does NOT provide — these stay visible.
        static readonly string[] KeptProps =
        {
            "Generator_Body", "Generator_FuelCap", "Generator_Breaker",
            "Generator_FusePanel", "Shed_FuelDrum_A", "Shed_FuelDrum_B",
        };

        [MenuItem("Tools/Last Beacon/Retire Blockout Shed Shell")]
        public static void RetireBlockoutShell()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Materials";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);

            var grp = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            if (grp == null) { Debug.LogError("[RETIRE] GeneratorShed group not found"); return; }

            foreach (var n in SupersededByArt)
            {
                var r = grp.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(x => x.name == n);
                if (r == null) { Debug.LogWarning($"[RETIRE] {n}: not found"); continue; }
                r.enabled = false;
                var c = r.GetComponent<Collider>();
                Debug.Log($"[RETIRE] {n,-22} renderer OFF (collider {(c == null ? "none" : c.enabled ? "ON" : "off")}) — replaced by the art shell");
            }
            foreach (var n in KeptProps)
            {
                var r = grp.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(x => x.name == n);
                if (r != null) Debug.Log($"[RETIRE] {n,-22} renderer {(r.enabled ? "ON (kept — the art does not provide this)" : "off")}");
            }
            var still = grp.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled).Select(r => r.name);
            Debug.Log($"[RETIRE] blockout pieces still drawing: {string.Join(", ", still)}");
            Physics.SyncTransforms();

            var eye = new Vector3(2.0f, 18.7f, 15.0f); var look = new Vector3(15.0f, 18.6f, 12.6f);
            Capture(shots, "R1_front_gameplay", eye, look, 60f);
            Capture(shots, "R2_threequarter", new Vector3(7.0f, 23.0f, 20.5f), new Vector3(17.0f, 18.6f, 13.0f), 55f);
            Capture(shots, "R3_closeup_lamp", new Vector3(8.6f, 18.4f, 12.4f), new Vector3(12.4f, 19.1f, 12.7f), 45f);
            var lamp = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Lamp_GeneratorShed");
            if (lamp != null) lamp.enabled = false;
            Capture(shots, "R4_closeup_no_lamp", new Vector3(8.6f, 18.4f, 12.4f), new Vector3(12.4f, 19.1f, 12.7f), 45f);
            Capture(shots, "R5_front_no_lamp", eye, look, 60f);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            var wallPt = pf.TransformPoint(new Vector3(-5.05f, 1.6f, 2.6f));
            var outward = (pf.TransformPoint(new Vector3(-9f, 1.6f, 2.6f)) - wallPt).normalized;
            Capture(shots, "R6_wall_panel_no_lamp", wallPt + outward * 3.2f, wallPt, 45f);
            if (lamp != null) lamp.enabled = true;
            Capture(shots, "R7_wall_panel", wallPt + outward * 3.2f, wallPt, 45f);
            Capture(shots, "R8_wall_gameplay_dist", wallPt + outward * 8f + Vector3.up * 0.4f, wallPt, 50f);

            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[RETIRE] scene saved. Nothing deleted — renderers can be switched back on.");
        }


        /// <summary>Variant pass: lighter wall + more structured staining, rendered
        /// against the current values from identical cameras.</summary>
        [MenuItem("Tools/Last Beacon/Generator Shed Wall Variant")]
        public static void WallVariant()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Variant";
            Directory.CreateDirectory(shots);

            void Cfg(string f, bool srgb, bool nrm)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath($"{TexDir}/{f}");
                if (ti == null) { Debug.LogError($"[VAR] missing {f}"); return; }
                ti.textureType = nrm ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (!nrm) ti.sRGBTexture = srgb;
                ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 4; ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            Cfg("T_ConcreteB_BaseColor.png", true, false);
            Cfg("T_ConcreteB_AO.png", false, false);
            Cfg("T_ConcreteB_Normal.png", false, true);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);

            var eye = new Vector3(2.0f, 18.7f, 15.0f); var look = new Vector3(15.0f, 18.6f, 12.6f);
            var wallPt = pf.TransformPoint(new Vector3(-5.05f, 1.6f, 2.6f));
            var outward = (pf.TransformPoint(new Vector3(-9f, 1.6f, 2.6f)) - wallPt).normalized;
            var tq = new Vector3(7.0f, 23.0f, 20.5f); var tqL = new Vector3(17.0f, 18.6f, 13.0f);
            var lamp = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Lamp_GeneratorShed");

            void Sheet(string tag)
            {
                Capture(shots, tag + "_1_front", eye, look, 60f);
                Capture(shots, tag + "_2_threequarter", tq, tqL, 55f);
                Capture(shots, tag + "_3_wall", wallPt + outward * 3.2f, wallPt, 45f);
                Capture(shots, tag + "_4_wall_8m", wallPt + outward * 8f + Vector3.up * 0.4f, wallPt, 50f);
                if (lamp != null) lamp.enabled = false;
                Capture(shots, tag + "_5_wall_no_lamp", wallPt + outward * 3.2f, wallPt, 45f);
                if (lamp != null) lamp.enabled = true;
            }

            var conc = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/MAT_Concrete.mat");
            var plin = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/MAT_Concrete_Plinth.mat");
            Sheet("A_current");
            Debug.Log($"[VAR] A_current: wall base {conc.GetColor("_BaseColor")}, plinth {plin.GetColor("_BaseColor")}");

            // ---- apply the variant ------------------------------------------------
            var bB = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_ConcreteB_BaseColor.png");
            var nB = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_ConcreteB_Normal.png");
            var aB = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_ConcreteB_AO.png");
            var wallC = new Color(0.420f, 0.437f, 0.448f);
            var plinC = new Color(0.215f, 0.222f, 0.228f);   // keeps the same ~51% ratio
            foreach (var (m, c) in new[] { (conc, wallC), (plin, plinC) })
            {
                m.SetColor("_BaseColor", c);
                m.SetTexture("_BaseMap", bB);
                m.SetTexture("_BumpMap", nB);
                m.SetTexture("_OcclusionMap", aB);
                m.SetFloat("_BumpScale", 0.5f);
                m.SetFloat("_SmoothnessTextureChannel", 1f);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            Sheet("B_variant");
            Debug.Log($"[VAR] B_variant: wall base {wallC}, plinth {plinC}, structured texture, bump 0.5");

            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[VAR] variant LEFT APPLIED. Revert = set MAT_Concrete to 0.340/0.360/0.370 " +
                      "and swap the maps back to T_Concrete_*.");
        }


        /// <summary>Re-import after the awning/box change and check the fuel drums.</summary>
        [MenuItem("Tools/Last Beacon/Generator Shed Refresh Art")]
        public static void RefreshArt()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Variant";
            Directory.CreateDirectory(shots);
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.useFileScale = true; imp.globalScale = 1f; imp.addCollider = false;
            imp.importNormals = ModelImporterNormals.Import; imp.weldVertices = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.SaveAndReimport();

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var slotOrder = new Dictionary<string, string[]>();
            foreach (var r in src.GetComponentsInChildren<MeshRenderer>())
                slotOrder[r.name] = r.sharedMaterials
                    .Select(x => x == null ? "" : x.name.Replace(" (Instance)", "").Trim()).ToArray();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);

            int applied = 0;
            foreach (var r in pf.GetComponentsInChildren<MeshRenderer>())
            {
                if (!slotOrder.TryGetValue(r.name, out var order)) continue;
                var arr = new Material[order.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    arr[i] = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{order[i]}.mat");
                    if (arr[i] != null) applied++;
                    else Debug.LogWarning($"[REFRESH] no material '{order[i]}' for {r.name}");
                }
                r.sharedMaterials = arr;
            }
            Physics.SyncTransforms();
            Debug.Log($"[REFRESH] slots applied {applied}");

            var lean = pf.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("LeanTo"));
            var frame = pf.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("DoorFrame"));
            var centre = new Vector3(17f, 17f, 13f);
            var doorDir = frame.bounds.center - centre; doorDir.y = 0; doorDir.Normalize();
            var leanDir = lean.bounds.center - centre; leanDir.y = 0; leanDir.Normalize();
            // right-hand side when facing the doors from outside
            var rightOfDoor = Vector3.Cross(Vector3.up, -doorDir).normalized;
            Debug.Log($"[REFRESH] door faces {doorDir}, lean-to sits toward {leanDir}");
            Debug.Log($"[REFRESH] lean-to is on the {(Vector3.Dot(leanDir, rightOfDoor) > 0 ? "RIGHT" : "LEFT")} " +
                      $"of the door elevation (dot {Vector3.Dot(leanDir, rightOfDoor):0.00})");

            // The awning moved gable, so the blockout drums must follow it. Mirror
            // each drum across the shed's local Z axis; x offset is preserved.
            var shedRot = Quaternion.Euler(0f, -5f, 0f);
            foreach (var n in new[] { "Shed_FuelDrum_A", "Shed_FuelDrum_B" })
            {
                var g = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.name == n).Select(t => t.gameObject)
                    .FirstOrDefault(o => o.GetComponent<Renderer>() != null);
                if (g == null) { Debug.LogWarning($"[REFRESH] {n} not found"); continue; }
                var before = g.GetComponent<Renderer>().bounds.center;
                bool wasUnder = lean.bounds.Contains(new Vector3(before.x, lean.bounds.center.y, before.z));

                var local = Quaternion.Inverse(shedRot) * (g.transform.position - centre);
                local.z = -local.z;
                g.transform.position = centre + shedRot * local;
                Physics.SyncTransforms();

                var after = g.GetComponent<Renderer>().bounds.center;
                bool nowUnder = lean.bounds.Contains(new Vector3(after.x, lean.bounds.center.y, after.z));
                Debug.Log($"[REFRESH] {n}: {before} -> {after}");
                Debug.Log($"[REFRESH]   under the awning: was {wasUnder}, now {nowUnder}  " +
                          $"| shed-local z {-local.z:0.00} -> {local.z:0.00}");
            }
            var eye = new Vector3(2.0f, 18.7f, 15.0f); var look = new Vector3(15.0f, 18.6f, 12.6f);
            Capture(shots, "C_after_1_front", eye, look, 60f);
            Capture(shots, "C_after_2_threequarter", new Vector3(7.0f, 23.0f, 20.5f), new Vector3(17.0f, 18.6f, 13.0f), 55f);
            Capture(shots, "C_after_3_awning", new Vector3(6.5f, 20.0f, 22.0f), new Vector3(16.0f, 18.6f, 16.5f), 50f);
            Capture(shots, "C_after_4_doorwall", new Vector3(8.6f, 18.4f, 12.4f), new Vector3(12.4f, 19.1f, 12.7f), 45f);
            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[REFRESH] scene saved");
        }


        /// <summary>Retire the blockout lean-to colliders left stranded when the art
        /// awning moved gable. Renderers were already off; this clears the invisible
        /// collision. Reversible — nothing is deleted.</summary>
        [MenuItem("Tools/Last Beacon/Retire Blockout LeanTo Collision")]
        public static void RetireLeanToCollision()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Variant";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            const float PR = 0.35f, PH = 1.8f;
            bool Free(Vector3 foot) => !Physics.CheckCapsule(
                foot + Vector3.up * PR, foot + Vector3.up * (PH - PR), PR, ~0,
                QueryTriggerInteraction.Ignore);

            // sample the ground where the blockout lean-to used to stand
            var centre = new Vector3(17f, 17f, 13f);
            var rot = Quaternion.Euler(0f, -5f, 0f);
            var probes = new List<(Vector3 p, string label)>();
            for (float lx = -3.0f; lx <= 3.01f; lx += 1.0f)
                for (float lz = 4.2f; lz <= 5.61f; lz += 0.7f)
                    probes.Add((centre + rot * new Vector3(lx, 0f, lz), $"local ({lx:0.0},{lz:0.0})"));

            int blockedBefore = probes.Count(x => !Free(x.p));
            Debug.Log($"[LEANCOL] before: {blockedBefore}/{probes.Count} sample points blocked on the vacated gable");

            var grp = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            foreach (var n in new[] { "Shed_LeanToRoof", "Shed_LeanToPost_W", "Shed_LeanToPost_E" })
            {
                var t = grp.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
                if (t == null) { Debug.LogWarning($"[LEANCOL] {n} not found"); continue; }
                var cols = t.GetComponents<Collider>();
                foreach (var c in cols) c.enabled = false;
                var r = t.GetComponent<MeshRenderer>();
                Debug.Log($"[LEANCOL] {n,-20} {cols.Length} collider(s) DISABLED (renderer {(r != null && r.enabled ? "ON" : "off")})");
            }
            Physics.SyncTransforms();
            int blockedAfter = probes.Count(x => !Free(x.p));
            Debug.Log($"[LEANCOL] after : {blockedAfter}/{probes.Count} sample points blocked");
            foreach (var x in probes.Where(x => !Free(x.p)))
                Debug.Log($"[LEANCOL]   still blocked at {x.label} — something else occupies it");

            // the drums under the NEW awning should still collide
            foreach (var n in new[] { "Shed_FuelDrum_A", "Shed_FuelDrum_B" })
            {
                var g = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.name == n).Select(t => t.gameObject)
                    .FirstOrDefault(o => o.GetComponent<Collider>() != null);
                if (g != null)
                    Debug.Log($"[LEANCOL] {n} collider {(g.GetComponent<Collider>().enabled ? "still ON (kept)" : "off")}");
            }
            var still = grp.GetComponentsInChildren<Collider>(true).Where(c => c.enabled).Select(c => c.name);
            Debug.Log($"[LEANCOL] blockout shed colliders still active: {string.Join(", ", still)}");

            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[LEANCOL] scene saved.");
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__MatCam");
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
