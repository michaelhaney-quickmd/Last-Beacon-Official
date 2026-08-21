using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Brings the Phase 2 generator-room gameplay assets into the scene: the
    /// generator hero asset, the breaker cabinet and the fuse panel.
    ///
    /// The Phase 1 interior shell is deliberately NOT imported. Its walls sit on
    /// exactly the same planes as the exterior art shell, so bringing both in would
    /// z-fight across every wall — that merge is still an open decision.
    ///
    /// The blockout placeholder props are retired rather than deleted, so a
    /// regenerate still reproduces them and nothing is lost.
    /// </summary>
    public static class GeneratorShedPhase2Import
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string FbxPath   = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed_P2.fbx";
        const string MatDir    = "Assets/_Project/Art/Materials/ArtPass/GenShed";
        const string InstName  = "SM_GeneratorShed_P2";

        // The shed origin and yaw the exterior art already uses. Phase 2 is authored
        // in the same Blender frame, so it lands by sharing the same root transform.
        static readonly Vector3 ShedOrigin = new Vector3(17f, 17f, 13f);
        const float ShedYaw = -5f;

        // Placeholder props this replaces. Markers keep their names too, so only the
        // ones carrying a Renderer are props.
        static readonly string[] Placeholders =
        { "Generator_Body", "Generator_FuelCap", "Generator_Breaker", "Generator_FusePanel" };

        [MenuItem("Tools/Last Beacon/Import Generator Shed Phase 2")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureMaterials();
            ConfigureImporter();

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (asset == null) { Debug.LogError($"[P2] missing {FbxPath}"); return; }

            var artRoot = GameObject.Find("LB_ArtProto")
                          ?? new GameObject("LB_ArtProto");

            var stale = artRoot.transform.Find(InstName);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            inst.name = InstName;
            inst.transform.SetParent(artRoot.transform, false);
            inst.transform.position = ShedOrigin;
            inst.transform.rotation = Quaternion.Euler(0f, ShedYaw, 0f);
            inst.transform.localScale = Vector3.one;

            RetirePlaceholders();
            var boxes = BuildCollision(inst.transform);
            Verify(inst.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"[P2] imported, {boxes} colliders, scene saved.");

            Shots();
        }

        // ------------------------------------------------------------- materials
        static void EnsureMaterials()
        {
            // Reuse the shed's existing families where the names already match, so
            // Phase 2 shares the palette rather than forking it. Only genuinely new
            // ones get created.
            Make("MAT_Painted_Red", new Color(0.400f, 0.062f, 0.048f), 0.10f, 0.60f);
            Make("MAT_PanelDark",   new Color(0.085f, 0.090f, 0.098f), 0.20f, 0.70f);
            Make("MAT_Rubber",      new Color(0.048f, 0.048f, 0.052f), 0.00f, 0.92f);
            Make("MAT_GaugeFace",   new Color(0.860f, 0.860f, 0.830f), 0.00f, 0.35f);
            Make("MAT_Emissive_Red",   new Color(0.42f, 0.06f, 0.06f), 0f, 0.5f,
                 new Color(1.00f, 0.13f, 0.10f) * 2.2f);
            Make("MAT_Emissive_Green", new Color(0.09f, 0.38f, 0.14f), 0f, 0.5f,
                 new Color(0.20f, 1.00f, 0.34f) * 2.2f);
            AssetDatabase.SaveAssets();
        }

        static void Make(string name, Color baseCol, float metallic, float smoothInv, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            if (File.Exists(path)) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", baseCol);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", 1f - smoothInv);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                m.SetColor("_EmissionColor", emission.Value);
            }
            Directory.CreateDirectory(MatDir);
            AssetDatabase.CreateAsset(m, path);
            Debug.Log($"[P2] created {path}");
        }

        static void ConfigureImporter()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            if (imp == null) { Debug.LogError("[P2] no importer"); return; }

            // useFileScale MUST stay true. With it false every CHILD node keeps a
            // 100x scale, which is how a multi-object FBX silently imports giant.
            imp.useFileScale = true;
            imp.globalScale = 1f;
            imp.importNormals = ModelImporterNormals.Import;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importAnimation = false;
            imp.isReadable = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            // InPrefab, not External. Every material is remapped to the shared
            // ArtPass palette below, so External only made Unity extract a second,
            // unreferenced copy of the whole palette next to the FBX.
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;

            foreach (var name in new[]
            {
                "MAT_Metal", "MAT_Metal_Painted", "MAT_Rust", "MAT_Emissive_Warm",
                "MAT_Painted_Red", "MAT_PanelDark", "MAT_Rubber", "MAT_GaugeFace",
                "MAT_Emissive_Red", "MAT_Emissive_Green",
            })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");
                if (mat == null) { Debug.LogWarning($"[P2] no material asset {name}"); continue; }
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), mat);
            }
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------ placeholders
        static void RetirePlaceholders()
        {
            int n = 0;
            foreach (var name in Placeholders)
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                                        .Where(x => x.name == name))
                {
                    var r = t.GetComponent<Renderer>();
                    if (r == null) continue;              // that one is the marker
                    r.enabled = false;
                    foreach (var c in t.GetComponents<Collider>()) c.enabled = false;
                    n++;
                }
            }
            Debug.Log($"[P2] retired {n} blockout placeholder props (reversible — renderers off, not deleted)");
        }

        // -------------------------------------------------------------- collision
        static int BuildCollision(Transform root)
        {
            var holder = new GameObject("Collision");
            holder.transform.SetParent(root, false);

            int n = 0;
            n += Box(holder.transform, root, "Col_GeneratorBody",
                     root.GetComponentsInChildren<MeshRenderer>()
                         .Where(r => r.name.StartsWith("SM_Generator_")
                                  && !r.name.Contains("Exhaust")).ToArray());
            n += Box(holder.transform, root, "Col_GeneratorExhaust",
                     root.GetComponentsInChildren<MeshRenderer>()
                         .Where(r => r.name.Contains("Exhaust")).ToArray());
            n += Box(holder.transform, root, "Col_Breaker",
                     root.GetComponentsInChildren<MeshRenderer>()
                         .Where(r => r.name.Contains("Breaker")).ToArray());
            n += Box(holder.transform, root, "Col_FusePanel",
                     root.GetComponentsInChildren<MeshRenderer>()
                         .Where(r => r.name.Contains("Fuse")).ToArray());
            return n;
        }

        /// <summary>
        /// One box per assembly, sized in the ROOT's local axes so it stays snug on a
        /// yawed building instead of ballooning into a world-aligned AABB.
        /// </summary>
        static int Box(Transform holder, Transform root, string name, MeshRenderer[] parts)
        {
            if (parts.Length == 0) { Debug.LogWarning($"[P2] {name}: no parts"); return 0; }

            var min = Vector3.one * float.MaxValue;
            var max = Vector3.one * float.MinValue;
            foreach (var r in parts)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;                 // local to that mesh
                for (int i = 0; i < 8; i++)
                {
                    var lp = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var l = root.InverseTransformPoint(r.transform.TransformPoint(lp));
                    min = Vector3.Min(min, l); max = Vector3.Max(max, l);
                }
            }
            if (min.x > max.x) { Debug.LogWarning($"[P2] {name}: no meshes"); return 0; }
            var go = new GameObject(name);
            go.transform.SetParent(holder, false);
            var col = go.AddComponent<BoxCollider>();
            col.center = (min + max) * 0.5f;
            col.size = max - min;
            Debug.Log($"[P2] {name}: size {col.size} centre {col.center}");
            return 1;
        }

        // ------------------------------------------------------------- in-engine
        /// <summary>
        /// Renders the room as the game actually lights it, so the import is judged in
        /// engine rather than from Blender review renders.
        /// </summary>
        static void Shots()
        {
            const string dir = "GenShed_P2_InGame";
            Directory.CreateDirectory(dir);

            // Everything below is the shed's local frame taken out to world through
            // the same centre and yaw the building uses, so the eye points sit where a
            // player would actually stand.
            Vector3 W(float lx, float lz, float h)
            {
                float t = ShedYaw * Mathf.Deg2Rad, c = Mathf.Cos(t), sn = Mathf.Sin(t);
                return new Vector3(17f + lx * c + lz * sn, 17f + h, 13f - lx * sn + lz * c);
            }

            Capture(dir, "p2_01_from_yard",      W(-8.5f, 0f, 1.70f),   W(0.5f, 0f, 1.10f), 60f);
            Capture(dir, "p2_02_doorway",        W(-4.2f, 0f, 1.70f),   W(0.5f, 0f, 1.05f), 65f);
            Capture(dir, "p2_03_control_side",   W(-1.0f, 0.9f, 1.70f), W(-0.36f, 0.95f, 1.25f), 55f);
            Capture(dir, "p2_04_fuel_and_stack", W(0.2f, 1.9f, 1.70f),  W(1.10f, 0f, 2.05f), 60f);
            Capture(dir, "p2_05_service_panel",  W(0.5f, -2.9f, 1.70f), W(1.24f, -1.46f, 0.95f), 60f);
            Capture(dir, "p2_06_electrical",     W(3.3f, -1.8f, 1.70f), W(4.53f, -2.1f, 1.55f), 62f);
            Debug.Log($"[P2] in-engine shots -> {Path.GetFullPath(dir)}");
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__P2Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;
            DynamicGI.UpdateEnvironment();
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo);
            Debug.Log($"[P2] shot {name}");
        }

        // ----------------------------------------------------------------- verify
        static void Verify(Transform root)
        {
            var rends = root.GetComponentsInChildren<MeshRenderer>();
            Debug.Log($"[P2] renderers {rends.Length}");

            // Do NOT assert localScale == 1 here. A multi-object Blender FBX imports
            // with its root nodes at 100 and the meshes baked at 0.01, which nets to
            // 1:1 and is the correct result of useFileScale = true. Measure the net
            // size against the locked envelope instead of reading node scales.
            var skidR = rends.FirstOrDefault(r => r.name == "SM_Generator_Skid");
            if (skidR != null)
            {
                var mf = skidR.GetComponent<MeshFilter>();
                // Compare SORTED dimensions. The mesh's local bounds are still in the
                // authoring axis order and the node rotation is not in lossyScale, so a
                // correct import reads (2.00, 3.20, 0.20) rather than (2.00, 0.20, 3.20).
                // Matching axis-by-axis reported a good import as broken.
                var sz = Vector3.Scale(mf.sharedMesh.bounds.size, skidR.transform.lossyScale);
                var got = new[] { sz.x, sz.y, sz.z }; System.Array.Sort(got);
                var want = new[] { 0.200f, 2.000f, 3.200f };
                bool ok = !got.Where((g, i) => Mathf.Abs(g - want[i]) > 0.02f).Any();
                Debug.Log($"[P2] skid net world size {sz} (dims expect 0.20 / 2.00 / 3.20)  -> {(ok ? "SCALE OK" : "SCALE WRONG")}");
                if (!ok) Debug.LogError("[P2] net scale is wrong — check useFileScale on the model importer.");
            }

            var body = rends.FirstOrDefault(r => r.name == "SM_Generator_Main");
            var skid = rends.FirstOrDefault(r => r.name == "SM_Generator_Skid");
            if (skid != null)
            {
                var c = skid.bounds.center;
                // The locked generator centre in world, straight off the blockout.
                var want = new Vector3(17.498f, 17f, 13.044f);
                Debug.Log($"[P2] skid centre {c}  locked generator centre (x,z) = ({want.x:0.000}, {want.z:0.000})  " +
                          $"delta {(new Vector2(c.x - want.x, c.z - want.z)).magnitude:0.000} m");
            }
            if (body != null) Debug.Log($"[P2] main housing world bounds {body.bounds}");

            var missing = rends.Where(r => r.sharedMaterials.Any(m => m == null)).Select(r => r.name).ToArray();
            if (missing.Length > 0) Debug.LogError($"[P2] renderers with unassigned materials: {string.Join(", ", missing)}");
            else Debug.Log("[P2] every renderer has a material");
        }
    }
}
