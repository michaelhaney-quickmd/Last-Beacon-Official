using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Material match pass against the neutral front reference. Materials only —
    /// geometry, UVs, pivots, door setup and clearances are untouched, and the
    /// global art-pass lighting is left alone apart from this building's practical.
    ///
    /// Base colours stay NEUTRAL; the cold night look comes from scene lighting.
    /// </summary>
    public static class GeneratorShedMaterialMatch
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string MatDir    = "Assets/_Project/Art/Materials/ArtPass/GenShed";
        const string TexDir    = "Assets/_Project/Art/Textures/GenShed";
        const string InstName  = "PF_GeneratorShed";

        // Concrete tiles 4 m across, 5 m up. The V scale matters: the base-map's
        // ground-grime gradient lives in the lowest 32% of V, so one V tile must
        // span the wall height rather than repeating up it.
        static readonly Vector2 ConcreteTile = new Vector2(0.25f, 0.20f);
        static readonly Vector2 SteelTile    = new Vector2(0.50f, 0.50f);   // 2 m
        static readonly Vector2 RoofTile     = new Vector2(0.35f, 0.35f);   // ~2.9 m

        const float LampK = 2500f;
        const float LampIntensity = 2.1f;   // 2.6 blew out, 1.7 read too flat
        const float LampRange = 12f;

        [MenuItem("Tools/Last Beacon/Generator Shed Material Match")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Match";
            Directory.CreateDirectory(shots);

            void Cfg(string f, bool srgb, bool nrm)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath($"{TexDir}/{f}");
                if (ti == null) { Debug.LogError($"[MM] missing {f}"); return; }
                ti.textureType = nrm ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (!nrm) ti.sRGBTexture = srgb;
                ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 8; ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            foreach (var f in new[] { "T_ConcreteC_BaseColor.png", "T_ConcreteC_AO.png",
                                      "T_PaintedSteel_BaseColor.png", "T_RoofMetal_BaseColor.png",
                                      "T_RoofCorrugation_BaseColor.png",
                                      "T_CanopyCorrugation_BaseColor.png" })
                Cfg(f, true, false);
            foreach (var f in new[] { "T_ConcreteC_Normal.png", "T_PaintedSteel_Normal.png",
                                      "T_RoofMetal_Normal.png", "T_RoofCorrugation_Normal.png",
                                      "T_CanopyCorrugation_Normal.png" })
                Cfg(f, false, true);

            Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{n}.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            //            name                base colour                     metal  smooth  base map               normal                 ao                  tiling
            var spec = new (string n, Color c, float m, float s, string bm, string nm, string ao, Vector2 t)[]
            {
                ("MAT_Concrete",        new Color(0.340f,0.348f,0.352f), 0.00f, 0.11f,
                    "T_ConcreteC_BaseColor", "T_ConcreteC_Normal", "T_ConcreteC_AO", ConcreteTile),
                ("MAT_Concrete_Plinth", new Color(0.165f,0.170f,0.172f), 0.00f, 0.10f,
                    "T_ConcreteC_BaseColor", "T_ConcreteC_Normal", "T_ConcreteC_AO", ConcreteTile),
                ("MAT_Metal_Painted",   new Color(0.135f,0.140f,0.145f), 0.32f, 0.22f,
                    "T_PaintedSteel_BaseColor", "T_PaintedSteel_Normal", null, SteelTile),
                ("MAT_Metal",           new Color(0.092f,0.096f,0.100f), 0.45f, 0.25f,
                    "T_RoofMetal_BaseColor", "T_RoofMetal_Normal", null, RoofTile),
                ("MAT_Rust",            new Color(0.115f,0.072f,0.052f), 0.20f, 0.16f,
                    null, null, null, SteelTile),
                // Same board values as MAT_Metal; the only difference is the
                // corrugation normal, which must not land on the conduit.
                ("MAT_Metal_Roof",      new Color(0.092f,0.096f,0.100f), 0.45f, 0.25f,
                    "T_RoofCorrugation_BaseColor", "T_RoofCorrugation_Normal", null, RoofTile),
                // Canopy slopes along Y where the roof slopes along X, so its ribs
                // need the transposed map. Same values, same pitch — reads as one material.
                ("MAT_Metal_Canopy",    new Color(0.092f,0.096f,0.100f), 0.45f, 0.25f,
                    "T_CanopyCorrugation_BaseColor", "T_CanopyCorrugation_Normal", null, RoofTile),
            };

            foreach (var s in spec)
            {
                string p = $"{MatDir}/{s.n}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
                m.shader = shader;
                m.SetColor("_BaseColor", s.c);
                m.SetFloat("_Metallic", s.m);
                m.SetFloat("_Smoothness", s.s);
                m.SetFloat("_WorkflowMode", 1f);
                m.SetFloat("_Surface", 0f); m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_SmoothnessTextureChannel", 1f);   // smoothness = base-map alpha
                if (s.bm != null)
                {
                    m.SetTexture("_BaseMap", T(s.bm));
                    m.SetTextureScale("_BaseMap", s.t);
                    m.SetTextureOffset("_BaseMap", Vector2.zero);
                }
                else m.SetTexture("_BaseMap", null);
                if (s.nm != null)
                {
                    m.SetTexture("_BumpMap", T(s.nm)); m.EnableKeyword("_NORMALMAP");
                    // the corrugation is the point of the roof material, so it gets
                    // full strength; the broad surface maps stay subtle at 0.30
                    bool corrugated = s.n == "MAT_Metal_Roof" || s.n == "MAT_Metal_Canopy";
                    m.SetFloat("_BumpScale", corrugated ? 1.0f : 0.30f);
                }
                else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
                if (s.ao != null) { m.SetTexture("_OcclusionMap", T(s.ao)); m.EnableKeyword("_OCCLUSIONMAP");
                                    m.SetFloat("_OcclusionStrength", 0.70f); }
                else { m.SetTexture("_OcclusionMap", null); m.DisableKeyword("_OCCLUSIONMAP"); }
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_MetallicGlossMap", null);
                EditorUtility.SetDirty(m);
                Debug.Log($"[MM] {s.n,-20} base ({s.c.r:0.000},{s.c.g:0.000},{s.c.b:0.000}) " +
                          $"metal {s.m:0.00} smooth {s.s:0.00} " +
                          $"map {(s.bm ?? "none"),-26} tiling {s.t.x}/{s.t.y} " +
                          $"(= {1f / s.t.x:0.0} x {1f / s.t.y:0.0} m)");
            }
            var em = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/MAT_Emissive_Warm.mat");
            if (em != null)
            {
                em.SetColor("_EmissionColor", new Color(1.00f, 0.68f, 0.36f) * 1.9f);
                EditorUtility.SetDirty(em);
                Debug.Log("[MM] MAT_Emissive_Warm  emission x1.9 (was x2.6)");
            }
            AssetDatabase.SaveAssets();

            // The FBX slot list changes when a material is split out, so reimport and
            // re-apply slots to the scene instance. (Deliberately NOT reusing
            // RefreshArt: that also mirrors the fuel drums, which is not idempotent.)
            const string FbxPath = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            mi.useFileScale = true; mi.globalScale = 1f; mi.addCollider = false;
            mi.importNormals = ModelImporterNormals.Import; mi.weldVertices = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.SaveAndReimport();
            var srcFbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var slotOrder = new Dictionary<string, string[]>();
            foreach (var r in srcFbx.GetComponentsInChildren<MeshRenderer>())
                slotOrder[r.name] = r.sharedMaterials
                    .Select(x => x == null ? "" : x.name.Replace(" (Instance)", "").Trim()).ToArray();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);

            var inst = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            int slotsApplied = 0, slotsMissing = 0;
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
            {
                if (!slotOrder.TryGetValue(r.name, out var order)) continue;
                var arr = new Material[order.Length];
                for (int i = 0; i < order.Length; i++)
                {
                    arr[i] = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{order[i]}.mat");
                    if (arr[i] != null) slotsApplied++;
                    else { slotsMissing++; Debug.LogWarning($"[MM] no material '{order[i]}' for {r.name}"); }
                }
                r.sharedMaterials = arr;
            }
            Debug.Log($"[MM] scene slots re-applied: {slotsApplied}, missing: {slotsMissing}");
            var roofR = inst.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(x => x.name.Contains("Roof"));
            Debug.Log($"[MM] Roof slots now: {string.Join(", ", roofR.sharedMaterials.Select(m => m ? m.name : "null"))}");

            var lamp = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Lamp_GeneratorShed");
            if (lamp != null)
            {
                Debug.Log($"[MM] lamp before: {lamp.intensity} intensity, {lamp.range} range, {lamp.colorTemperature}K");
                lamp.useColorTemperature = true; lamp.colorTemperature = LampK;
                lamp.color = Color.white; lamp.intensity = LampIntensity; lamp.range = LampRange;
                Debug.Log($"[MM] lamp after : {LampIntensity} intensity, {LampRange} range, {LampK}K");
            }
            Physics.SyncTransforms();

            var pf = inst;
            var front = new Vector3(2.0f, 18.7f, 15.0f);
            var frontL = new Vector3(15.0f, 18.6f, 12.6f);
            var wallPt = pf.TransformPoint(new Vector3(5.05f, 1.6f, -2.6f));
            var outward = (pf.TransformPoint(new Vector3(9f, 1.6f, -2.6f)) - wallPt).normalized;

            Capture(shots, "1_front_gameplay", front, frontL, 60f);
            Capture(shots, "3_closeup_lamp", new Vector3(8.6f, 18.4f, 12.4f), new Vector3(12.4f, 19.1f, 12.7f), 45f);
            Capture(shots, "4_threequarter", new Vector3(7.0f, 23.0f, 20.5f), new Vector3(17.0f, 18.6f, 13.0f), 55f);
            Capture(shots, "5_wall_5m", wallPt + outward * 5f + Vector3.up * 0.3f, wallPt, 50f);
            Capture(shots, "6_wall_10m", wallPt + outward * 10f + Vector3.up * 0.5f, wallPt, 50f);
            if (lamp != null) lamp.enabled = false;
            Capture(shots, "2_front_no_lamp", front, frontL, 60f);
            Capture(shots, "7_wall_5m_no_lamp", wallPt + outward * 5f + Vector3.up * 0.3f, wallPt, 50f);
            if (lamp != null) lamp.enabled = true;

            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[MM] scene saved. Geometry, UVs, pivots and global lighting untouched.");
        }


        /// <summary>Read the material assets back and compare against the palette
        /// reference board (v1.0). Reports actual values, not intended ones.</summary>
        [MenuItem("Tools/Last Beacon/Verify Generator Shed Palette")]
        public static void VerifyPalette()
        {
            // (name, base, metallic, smoothness) exactly as printed on the board
            var board = new (string n, Color c, float m, float s)[]
            {
                ("MAT_Concrete",        new Color(0.340f,0.348f,0.352f), 0.00f, 0.11f),
                ("MAT_Concrete_Plinth", new Color(0.165f,0.170f,0.172f), 0.00f, 0.10f),
                ("MAT_Metal_Painted",   new Color(0.135f,0.140f,0.145f), 0.32f, 0.22f),
                ("MAT_Metal",           new Color(0.092f,0.096f,0.100f), 0.45f, 0.25f),
                ("MAT_Rust",            new Color(0.115f,0.072f,0.052f), 0.20f, 0.16f),
            };
            Debug.Log("[PAL] material            board -> asset                              verdict");
            bool allOk = true;
            foreach (var b in board)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{b.n}.mat");
                if (m == null) { Debug.LogError($"[PAL] {b.n}: MISSING"); allOk = false; continue; }
                var c = m.GetColor("_BaseColor");
                float me = m.GetFloat("_Metallic"), sm = m.GetFloat("_Smoothness");
                bool ok = Mathf.Abs(c.r-b.c.r)<0.002f && Mathf.Abs(c.g-b.c.g)<0.002f &&
                          Mathf.Abs(c.b-b.c.b)<0.002f && Mathf.Abs(me-b.m)<0.005f &&
                          Mathf.Abs(sm-b.s)<0.005f;
                if (!ok) allOk = false;
                Debug.Log($"[PAL] {b.n,-20} ({c.r:0.000},{c.g:0.000},{c.b:0.000}) m {me:0.00} s {sm:0.00}   " +
                          $"{(ok ? "MATCH" : "** DIFFERS **")}");
            }
            // where is each material actually used?
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            var usage = new Dictionary<string, List<string>>();
            foreach (var r in pf.GetComponentsInChildren<MeshRenderer>())
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (!usage.TryGetValue(m.name, out var l)) usage[m.name] = l = new List<string>();
                    var short_ = r.name.Replace("SM_GeneratorShed_", "");
                    if (!l.Contains(short_)) l.Add(short_);
                }
            Debug.Log("[PAL] --- actual usage on the shed ---");
            foreach (var b in board)
                Debug.Log($"[PAL] {b.n,-20} {(usage.TryGetValue(b.n, out var l) ? string.Join(", ", l) : "** NOT USED ON THIS ASSET **")}");
            var em = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/MAT_Emissive_Warm.mat");
            Debug.Log($"[PAL] MAT_Emissive_Warm   emission {em.GetColor("_EmissionColor")}, " +
                      $"used by {(usage.TryGetValue("MAT_Emissive_Warm", out var el) ? string.Join(", ", el) : "nothing")}");
            var lamp = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Lamp_GeneratorShed");
            if (lamp != null)
                Debug.Log($"[PAL] practical: {lamp.colorTemperature}K, intensity {lamp.intensity}, range {lamp.range}");
            Debug.Log($"[PAL] palette verdict: {(allOk ? "ALL FIVE MATCH THE BOARD" : "one or more differ")}");
        }


        /// <summary>Close roof views to confirm the corrugation reads and runs
        /// down-slope rather than across it.</summary>
        [MenuItem("Tools/Last Beacon/Verify Roof Corrugation")]
        public static void VerifyRoof()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Match";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            var roof = pf.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("Roof"));
            Debug.Log($"[ROOF] slots: {string.Join(", ", roof.sharedMaterials.Select(m => m ? m.name : "null"))}");
            var mr = roof.sharedMaterials.FirstOrDefault(m => m != null && m.name == "MAT_Metal_Roof");
            if (mr != null)
                Debug.Log($"[ROOF] MAT_Metal_Roof bump {mr.GetFloat("_BumpScale")}, " +
                          $"normal {(mr.GetTexture("_BumpMap") ? mr.GetTexture("_BumpMap").name : "none")}, " +
                          $"tiling {mr.GetTextureScale("_BaseMap")}");
            var canopy = pf.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("LeanTo"));
            Debug.Log($"[ROOF] LeanTo slots: {string.Join(", ", canopy.sharedMaterials.Select(m => m ? m.name : "null"))}");
            var conduit = pf.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("Fixtures"));
            Debug.Log($"[ROOF] Fixtures slots: {string.Join(", ", conduit.sharedMaterials.Select(m => m ? m.name : "null"))} " +
                      "(must NOT include MAT_Metal_Roof)");

            var b = roof.bounds;
            // look along the ridge and straight down the slope
            Capture(shots, "8_roof_close_alongridge",
                    b.center + new Vector3(-3.2f, 2.4f, -3.2f), b.center + new Vector3(0.6f, -0.3f, 0.6f), 40f);
            Capture(shots, "9_roof_close_downslope",
                    b.center + new Vector3(-4.6f, 3.0f, 0.4f), b.center + new Vector3(0.4f, -0.4f, 0.2f), 38f);
            Capture(shots, "10_roof_grazing",
                    b.center + new Vector3(-6.5f, 0.9f, 1.2f), b.center + new Vector3(1.0f, -0.2f, 0.4f), 45f);
            var cb = canopy.bounds;
            Debug.Log($"[ROOF] canopy bounds centre {cb.center} size {cb.size} " +
                      $"(x {cb.min.x:0.0}..{cb.max.x:0.0}, z {cb.min.z:0.0}..{cb.max.z:0.0})");
            // Stand OUTSIDE the awning gable and look back at it. The awning faces
            // the lower-Z side, so the camera goes further -Z, not toward the shed.
            var outDir = (cb.center - new Vector3(17f, 17f, 13f)); outDir.y = 0f; outDir.Normalize();
            var eyeA = cb.center + outDir * 5.0f + Vector3.up * 0.9f;
            var eyeB = cb.center + outDir * 2.6f - Vector3.up * 1.1f;
            Debug.Log($"[ROOF] canopy cam A {eyeA}, B {eyeB}, outward {outDir}");
            Capture(shots, "11_canopy_close", eyeA, cb.center + Vector3.up * 0.1f, 45f);
            Capture(shots, "12_canopy_under", eyeB, cb.center + Vector3.up * 0.2f, 55f);
            if (player != null) player.gameObject.SetActive(true);
            Debug.Log("[ROOF] shots written; scene not saved");
        }


        /// <summary>Confirm the closed doors seal, the pivots survived, and the
        /// swing is still clear after widening the leaves.</summary>
        [MenuItem("Tools/Last Beacon/Verify Door Seal")]
        public static void VerifyDoorSeal()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Match";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            var L = pf.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Door_L"));
            var R = pf.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Door_R"));
            Debug.Log($"[SEAL] pivots L {L.position} R {R.position}");
            Debug.Log($"[SEAL] closed rotations L {L.localRotation.eulerAngles} R {R.localRotation.eulerAngles}");

            // raycast the closed doors from outside, across the whole opening
            var frame = pf.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("DoorFrame"));
            Vector3 c = frame.bounds.center;
            Vector3 outward = (c - new Vector3(17f, 17f, 13f)); outward.y = 0f; outward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, outward).normalized;
            var dd = pf.GetComponent<ScriptedDoubleDoor>();
            if (dd != null) { dd.PoseImmediate(0f); Physics.SyncTransforms(); }

            int through = 0, total = 0;
            var lm = 1 << LayerMask.NameToLayer("Default");
            foreach (float off in new[] { -1.6f, -1.0f, -0.4f, -0.05f, 0f, 0.05f, 0.4f, 1.0f, 1.6f })
                foreach (float z in new[] { 0.15f, 0.6f, 1.3f, 1.9f, 2.6f, 3.1f })
                {
                    Vector3 o = c + outward * 4f + side * off + Vector3.up * (17f + z - c.y);
                    total++;
                    // does anything of the shed stop the ray before it reaches the interior?
                    if (!Physics.Raycast(o, -outward, 3.6f, ~0, QueryTriggerInteraction.Ignore))
                        through++;
                }
            Debug.Log($"[SEAL] rays reaching the interior through the closed doors: {through}/{total} " +
                      "(collision only — art has no colliders, so this reads the wall/door boxes)");

            Capture(shots, "13_doors_closed_seal", c + outward * 4.5f + Vector3.up * 0.2f, c, 45f);
            Capture(shots, "14_doors_closed_low", c + outward * 2.2f - Vector3.up * 1.0f, c + Vector3.up * 0.4f, 55f);
            if (dd != null) { dd.PoseImmediate(95f); Physics.SyncTransforms(); }
            Capture(shots, "15_doors_open_95", c + outward * 5.5f + Vector3.up * 0.3f, c, 50f);
            if (dd != null) { dd.PoseImmediate(0f); Physics.SyncTransforms(); }
            if (player != null) player.gameObject.SetActive(true);
            Debug.Log("[SEAL] shots written; scene not saved");
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__MMCam");
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
