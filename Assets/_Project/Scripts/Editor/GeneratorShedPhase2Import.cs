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
        const string IntPath   = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed_Interior.fbx";
        const string IntName   = "SM_GeneratorShed_Interior";
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

            ImportInterior(artRoot.transform);
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

        // --------------------------------------------------------------- interior
        /// <summary>
        /// The interior contributes only what the exterior shell does not already
        /// own: the FLOOR (nothing owned it — the compound plateau was showing
        /// through at y = 17.000) and the STEEL structure. The Phase 1 walls,
        /// ceiling and plinth are deliberately absent; the exterior Body's own
        /// inward-facing faces are the single surface on each of those planes.
        /// </summary>
        static void ImportInterior(Transform artRoot)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(IntPath);
            if (imp == null) { Debug.LogError($"[P2] missing {IntPath}"); return; }
            imp.globalScale = 1f;
            imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
            imp.isReadable = false;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // Placeholder assignments only — the interior material pass is Phase 3.
            Remap(imp, "MAT_REVIEW_Floor", "MAT_Concrete");
            Remap(imp, "MAT_REVIEW_Steel", "MAT_Metal");

            // Do not trust useFileScale either way. A Blender FBX bakes meshes at 0.01
            // and puts 100 on a node, but WHICH node carries it differs between a
            // multi-root file and a single-root one, so the flag lands 1:1 for one and
            // 1:100 for the other. Measure the INSTANCE — reading lossyScale off the
            // model asset reported a good scale while the scene copy was 100x small.
            GameObject inst = null;
            foreach (var useFile in new[] { true, false })
            {
                imp.useFileScale = useFile;
                imp.SaveAndReimport();

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(IntPath);
                if (asset == null) { Debug.LogError($"[P2] cannot load {IntPath}"); return; }
                var stale = artRoot.Find(IntName);
                if (stale != null) Object.DestroyImmediate(stale.gameObject);

                inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                inst.name = IntName;
                inst.transform.SetParent(artRoot, false);
                inst.transform.position = ShedOrigin;
                inst.transform.rotation = Quaternion.Euler(0f, ShedYaw, 0f);
                inst.transform.localScale = Vector3.one;

                var floor = inst.GetComponentsInChildren<MeshRenderer>()
                                .FirstOrDefault(r => r.name.Contains("Floor"));
                if (floor == null) { Debug.LogError("[P2] interior floor renderer missing"); return; }
                // Check ORIENTATION as well as size. Sorting the dimensions proved a
                // 9.4 x 7.4 slab was present but could not tell that it was standing
                // on edge -- Blender Z-up had become Unity forward, so the "floor"
                // was a 7.4 m tall wall. The floor must be THIN on world Y.
                var sz = floor.bounds.size;
                bool flat  = sz.y < 0.30f;
                bool plan  = sz.x > 7.0f && sz.z > 7.0f;
                bool ok = flat && plan;
                Debug.Log($"[P2] interior floor in scene {sz} with useFileScale={useFile} -> " +
                          $"{(ok ? "OK" : (flat ? "WRONG SIZE" : "WRONG ORIENTATION — slab is on edge"))}");
                if (ok) break;
                if (!useFile) Debug.LogError("[P2] interior scale wrong on BOTH settings — do not ship this.");
            }

            // No colliders here on purpose: the compound plateau already carries the
            // walkable surface, and a 10 mm slab on top of it would only add an edge
            // for the capsule to catch on. The steel sits above head height.
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
                Debug.Log($"[P2] interior '{r.name}' world size {r.bounds.size}");
        }

        static void Remap(ModelImporter imp, string source, string target)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{target}.mat");
            if (mat == null) { Debug.LogWarning($"[P2] no material {target}"); return; }
            imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), mat);
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
            // Collision is deliberately coarser than the render mesh. The body box is
            // the player-blocking mass only: the fuel assembly is excluded so the box
            // stops at the 1.80 deck instead of being dragged to 2.10 by a cap the
            // player can never walk into.
            n += Box(holder.transform, root, "Col_GeneratorBody",
                     root.GetComponentsInChildren<MeshRenderer>()
                         .Where(r => r.name.StartsWith("SM_Generator_")
                                  && !r.name.Contains("Exhaust")
                                  && !r.name.Contains("Fuel")).ToArray());
            // A small proxy on the fuel assembly so a future interaction probe has
            // something to hit. It sits on top of the machine and blocks nothing.
            n += Box(holder.transform, root, "Col_GeneratorFuelCap",
                     root.GetComponentsInChildren<MeshRenderer>()
                         .Where(r => r.name.Contains("Fuel")
                                  && r.name.StartsWith("SM_Generator_")).ToArray());
            // No exhaust collider. Its lowest point is 1.710, but it sits entirely
            // inside the generator's own plan footprint, so the body blocks the player
            // first and an exhaust collider could never be reached.
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
        /// <summary>
        /// Shed-local (x, z) plus a height, out to world through the same centre and
        /// yaw the building uses — so eye points sit where a player would stand.
        /// </summary>
        static Vector3 W(float lx, float lz, float h)
        {
            float t = ShedYaw * Mathf.Deg2Rad, c = Mathf.Cos(t), sn = Mathf.Sin(t);
            return new Vector3(ShedOrigin.x + lx * c + lz * sn,
                               ShedOrigin.y + h,
                               ShedOrigin.z - lx * sn + lz * c);
        }

        static void Shots()
        {
            const string dir = "GenShed_P2_InGame";
            Directory.CreateDirectory(dir);

            Capture(dir, "p2_01_from_yard",      W(-8.5f, 0f, 1.70f),   W(0.5f, 0f, 1.10f), 60f);
            Capture(dir, "p2_02_doorway",        W(-4.2f, 0f, 1.70f),   W(0.5f, 0f, 1.05f), 65f);
            Capture(dir, "p2_03_control_side",   W(-1.0f, 0.9f, 1.70f), W(-0.36f, 0.95f, 1.25f), 55f);
            Capture(dir, "p2_04_fuel_and_stack", W(0.2f, 1.9f, 1.70f),  W(1.10f, 0f, 2.05f), 60f);
            Capture(dir, "p2_05_service_panel",  W(0.5f, -2.9f, 1.70f), W(1.24f, -1.46f, 0.95f), 60f);
            Capture(dir, "p2_06_electrical",     W(3.3f, -1.8f, 1.70f), W(4.53f, -2.1f, 1.55f), 62f);

            // Plan view: orthographic, from just BELOW the steel at 4.02 so the ridge
            // beam is not sitting on the lens, and lifted out of the night lighting so
            // the clearances are actually legible. The ambient boost is an inspection
            // aid for this shot only and is put back afterwards.
            var ambPrev = RenderSettings.ambientLight;
            var ambModePrev = RenderSettings.ambientMode;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.63f, 0.65f);
            DynamicGI.UpdateEnvironment();
            Capture(dir, "p2_07_topdown_clearance", W(0f, 0f, 3.95f), W(0.001f, 0f, 0f), 60f, ortho: 4.3f);

            CaptureColliders(dir);
            RenderSettings.ambientMode = ambModePrev;
            RenderSettings.ambientLight = ambPrev;
            DynamicGI.UpdateEnvironment();
            Debug.Log($"[P2] in-engine shots -> {Path.GetFullPath(dir)}");
        }

        /// <summary>
        /// Draws every BoxCollider on the shed assets as a translucent solid so the
        /// gameplay collision can be compared against the render mesh at a glance.
        /// The proxies are destroyed again immediately.
        /// </summary>
        static void CaptureColliders(string dir)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(shader);
            mat.SetFloat("_Surface", 1f);                       // transparent
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", new Color(0.15f, 0.95f, 0.45f, 0.34f));

            var proxies = new List<GameObject>();
            foreach (var bc in Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
                                     .Where(c => c.transform.root.name == "LB_ArtProto"
                                              || c.name.StartsWith("Col_")))
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = "__ColViz_" + bc.name;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                g.transform.SetParent(bc.transform, false);
                g.transform.localPosition = bc.center;
                g.transform.localRotation = Quaternion.identity;
                g.transform.localScale = bc.size;
                proxies.Add(g);
                Debug.Log($"[P2] colviz {bc.name} size {bc.size} centre {bc.center}");
            }

            Capture(dir, "p2_08_colliders", W(-4.6f, -3.1f, 2.30f), W(0.5f, 0f, 0.95f), 62f);
            foreach (var g in proxies) Object.DestroyImmediate(g);
            Capture(dir, "p2_08b_render_same_angle", W(-4.6f, -3.1f, 2.30f), W(0.5f, 0f, 0.95f), 62f);
            Object.DestroyImmediate(mat);
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov, float ortho = 0f)
        {
            var camGo = new GameObject("__P2Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;
            if (ortho > 0f) { cam.orthographic = true; cam.orthographicSize = ortho; }
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
