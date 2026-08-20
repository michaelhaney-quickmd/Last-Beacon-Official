using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Drops the generator shed art building into the compound over the blockout
    /// shed. The blockout keeps its colliders and transforms; only its MeshRenderers
    /// are switched off. The art gets no colliders.
    ///
    /// NOTE: the art shed is 3.6 x 4.2 m per the reference board; the blockout it
    /// covers is 10 x 8 m. That mismatch is reported, not hidden — see the [GS] log.
    /// </summary>
    public static class GeneratorShedImport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string Fbx = "Assets/_Project/Art/Environment/Buildings/SM_GeneratorShed_Rebuild.fbx";
        const string MatDir = "Assets/_Project/Art/Materials/ArtPass/GenShed";
        const string RootName = "LB_ArtProto";
        const string ObjName = "SM_GeneratorShed_Rebuild";

        // From VerticalIslandBlockoutGenerator: ShedC (17,13), ShedYaw -5, compound Y 17.
        static readonly Vector2 ShedC = new Vector2(17f, 13f);
        const float ShedYaw = -5f;
        const float TierCompound = 17f;

        // name -> (r,g,b, smoothness, metallic, emissive)
        static readonly Dictionary<string, (Color c, float s, float m, Color e)> Palette = new()
        {
            { "MAT_Wood_Wet",      (new Color(0.118f,0.099f,0.082f), 0.62f, 0f, Color.black) },
            { "MAT_Wood_Painted",  (new Color(0.145f,0.152f,0.162f), 0.38f, 0f, Color.black) },
            { "MAT_Metal",         (new Color(0.135f,0.158f,0.185f), 0.62f, 0.90f, Color.black) },
            { "MAT_Metal_Painted", (new Color(0.205f,0.225f,0.235f), 0.45f, 0.35f, Color.black) },
            { "MAT_Concrete",      (new Color(0.150f,0.152f,0.148f), 0.14f, 0f, Color.black) },
            { "MAT_Rust",          (new Color(0.175f,0.082f,0.044f), 0.20f, 0.15f, Color.black) },
            { "MAT_Glass",         (new Color(0.055f,0.070f,0.090f), 0.90f, 0f, new Color(1.00f,0.60f,0.26f) * 1.8f) },
            { "MAT_Emissive_Warm", (new Color(0.90f,0.60f,0.30f),   0.50f, 0f, new Color(1.00f,0.62f,0.28f) * 6f) },
        };

        [MenuItem("Tools/Last Beacon/Import Generator Shed")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? Path.Combine(Path.GetTempPath(), "lb-gs");
            Directory.CreateDirectory(shots);
            Directory.CreateDirectory(MatDir);

            var mats = new Dictionary<string, Material>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var kv in Palette)
            {
                string p = $"{MatDir}/{kv.Key}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, p); }
                m.shader = shader;
                m.SetColor("_BaseColor", kv.Value.c);
                m.SetFloat("_Smoothness", kv.Value.s);
                m.SetFloat("_Metallic", kv.Value.m);
                if (kv.Value.e.maxColorComponent > 0f)
                {
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    m.SetColor("_EmissionColor", kv.Value.e);
                }
                EditorUtility.SetDirty(m);
                mats[kv.Key] = m;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[GS] {mats.Count} URP materials ready in {MatDir}");

            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp == null) { Debug.LogError($"[GS] {Fbx} not imported"); return; }
            imp.addCollider = false;
            imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            // Multi-object FBX: useFileScale=false leaves every CHILD node at 100x
            // (it only neutralises the root). The single-mesh rock did not show this.
            imp.useFileScale = true; imp.globalScale = 1f;
            imp.SaveAndReimport();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool playerWasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            var old = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == ObjName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            go.name = ObjName;
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = Vector3.one;
            go.transform.position = new Vector3(ShedC.x, TierCompound, ShedC.y);
            go.transform.rotation = Quaternion.Euler(0f, ShedYaw, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            int remapped = 0, missed = 0;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var src = r.sharedMaterial;
                string key = src == null ? null : src.name.Replace(" (Instance)", "").Trim();
                if (key != null && mats.TryGetValue(key, out var m)) { r.sharedMaterial = m; remapped++; }
                else { missed++; Debug.LogWarning($"[GS] no URP material for '{key}' on {r.name}"); }
            }
            Debug.Log($"[GS] materials remapped: {remapped}, unmatched: {missed}");

            // seat on the compound floor
            var rends = go.GetComponentsInChildren<MeshRenderer>();
            // Solve the yaw instead of guessing an offset: the doorway must face the
            // courtyard. Try the four quarter-turns and keep the best alignment.
            var courtyard = new Vector3(0f, TierCompound, 17f);
            // Use RENDERER BOUNDS, not transform.position: every Blender object had
            // its transform applied, so all child transforms share the parent's
            // position and a direction built from them is always the zero vector.
            var frontWall = go.GetComponentsInChildren<MeshRenderer>()
                              .FirstOrDefault(r => r.name.Contains("DoorFrame"));
            if (frontWall == null) Debug.LogError("[GS] DoorFrame piece not found — cannot solve facing");
            else
            {
                float bestDot = -2f, bestYaw = ShedYaw;
                foreach (float extra in new[] { 0f, 90f, 180f, 270f })
                {
                    go.transform.rotation = Quaternion.Euler(0f, ShedYaw + extra, 0f);
                    Physics.SyncTransforms();
                    var all = go.GetComponentsInChildren<MeshRenderer>();
                    var whole = all[0].bounds; foreach (var r in all) whole.Encapsulate(r.bounds);
                    var outward = frontWall.bounds.center - whole.center; outward.y = 0f;
                    var toYard = courtyard - whole.center; toYard.y = 0f;
                    float d = Vector3.Dot(outward.normalized, toYard.normalized);
                    Debug.Log($"[GS]   yaw {ShedYaw + extra:0}: front-wall alignment with courtyard = {d:0.000}");
                    if (d > bestDot) { bestDot = d; bestYaw = ShedYaw + extra; }
                }
                go.transform.rotation = Quaternion.Euler(0f, bestYaw, 0f);
                Physics.SyncTransforms();
                Debug.Log($"[GS] doorway faces the courtyard at yaw {bestYaw:0} (alignment {bestDot:0.000})");
            }

            // Do NOT assert on lossyScale: useFileScale=true bakes meshes at 0.01 and
            // puts 100 on the node, which nets to 1:1. Assert on real world size.
            var probe = rends[0].bounds;
            foreach (var r in rends) probe.Encapsulate(r.bounds);
            const float ExpectedHeight = 5.238f;  // measured in Blender
            float hErr = Mathf.Abs(probe.size.y - ExpectedHeight);
            Debug.Log($"[GS] scale check: world height {probe.size.y:0.00} m vs Blender {ExpectedHeight:0.00} m " +
                      $"(error {hErr:0.000} m) {(hErr < 0.05f ? "OK — 1:1" : "SCALE WRONG")}");
            var bb = rends[0].bounds;
            foreach (var r in rends) bb.Encapsulate(r.bounds);
            go.transform.position += new Vector3(0f, TierCompound - bb.min.y, 0f);
            Physics.SyncTransforms();
            bb = rends[0].bounds; foreach (var r in rends) bb.Encapsulate(r.bounds);

            int tris = go.GetComponentsInChildren<MeshFilter>()
                         .Sum(f => f.sharedMesh.triangles.Length / 3);
            Debug.Log($"[GS] placed at {go.transform.position}, yaw {go.transform.rotation.eulerAngles.y:0.0}");
            Debug.Log($"[GS] pieces {rends.Length}, tris {tris}");
            Debug.Log($"[GS] world AABB x {bb.min.x:0.00}..{bb.max.x:0.00} y {bb.min.y:0.00}..{bb.max.y:0.00} " +
                      $"z {bb.min.z:0.00}..{bb.max.z:0.00}  size {bb.size.x:0.00} x {bb.size.y:0.00} x {bb.size.z:0.00}");

            // --- the footprint mismatch, stated with numbers ---------------------
            var blockShell = Find("Shed_Body");
            if (blockShell != null)
            {
                var sb = blockShell.GetComponent<Renderer>().bounds;
                float cover = (bb.size.x * bb.size.z) / (sb.size.x * sb.size.z) * 100f;
                string verdict = (cover > 85f && cover < 135f) ? "MATCHED" : "MISMATCH";
                Debug.Log($"[GS] FOOTPRINT {verdict} — blockout Shed_Body {sb.size.x:0.0} x {sb.size.z:0.0} m " +
                    $"(h {sb.size.y:0.0}); art {bb.size.x:0.0} x {bb.size.z:0.0} m (h {bb.size.y:0.0}); " +
                    $"art covers {cover:0}% of the blockout footprint (art AABB includes roof overhang and lean-to).");
            }

            // Switch off the WHOLE blockout shed group, not just body+roof: the
            // lean-to, posts, drums and generator props otherwise keep drawing
            // around the art building.
            var group = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            if (group == null) Debug.LogError("[GS] blockout group 'GeneratorShed' not found");
            else
            {
                int off = 0;
                foreach (var r in group.GetComponentsInChildren<MeshRenderer>())
                { r.enabled = false; off++; }
                int cols = group.GetComponentsInChildren<Collider>().Count(c => c.enabled);
                Debug.Log($"[GS] blockout group '{group.name}': {off} renderers off, " +
                          $"{cols} colliders left enabled");
                Debug.LogWarning("[GS] the blockout colliders are still the 10x8 shed volume, so " +
                                 "collision will not match what you see until the footprint is resolved.");
            }
            Physics.SyncTransforms();

            // Does the art lean-to end up on the same side as the blockout lean-to?
            var artLT = go.GetComponentsInChildren<MeshRenderer>()
                          .FirstOrDefault(r => r.name.Contains("LeanToRoof"));
            var blkLT = Find("Shed_LeanToRoof");
            if (artLT != null && blkLT != null)
            {
                var centre = new Vector3(ShedC.x, TierCompound, ShedC.y);
                var aDir = artLT.bounds.center - centre; aDir.y = 0f;
                var bDir = blkLT.GetComponent<Renderer>().bounds.center - centre; bDir.y = 0f;
                float dot = Vector3.Dot(aDir.normalized, bDir.normalized);
                Debug.Log($"[GS] lean-to side: art dir {aDir.normalized}, blockout dir {bDir.normalized}, " +
                          $"alignment {dot:0.000} {(dot > 0.7f ? "SAME SIDE" : "OPPOSITE SIDE — art is flipped vs the blockout")}");
            }
            var artDoor = go.GetComponentsInChildren<MeshRenderer>()
                            .FirstOrDefault(r => r.name.Contains("DoorFrame"));
            if (artDoor != null)
            {
                var centre = new Vector3(ShedC.x, TierCompound, ShedC.y);
                var d = artDoor.bounds.center - centre; d.y = 0f;
                Debug.Log($"[GS] doorway faces {d.normalized} (blockout frontage turns W onto the yard)");
            }

            var yard = new Vector3(0f, TierCompound + 1.6f, 17f);
            Capture(shots, "01_FromCourtyard", yard, bb.center, 60f);
            Capture(shots, "02_Approach", bb.center + new Vector3(-9f, 3.2f, -7f), bb.center, 55f);
            Capture(shots, "03_Close", bb.center + new Vector3(-5.5f, 0.4f, -4.5f), bb.center, 50f);
            Capture(shots, "04_CompoundWide", new Vector3(-14f, TierCompound + 11f, 2f),
                    new Vector3(6f, TierCompound + 2f, 14f), 60f);

            if (player != null) player.gameObject.SetActive(playerWasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[GS] scene saved.");
        }

        /// <summary>Puts the compound back to the blockout shed: art object removed,
        /// every blockout renderer in the GeneratorShed group switched back on.</summary>
        [MenuItem("Tools/Last Beacon/Revert Generator Shed")]
        public static void Revert()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool playerWasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);

            var art = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                          FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == ObjName);
            if (art == null) Debug.Log("[GS] no art shed in the scene — nothing to remove");
            else { Object.DestroyImmediate(art.gameObject); Debug.Log($"[GS] removed '{ObjName}'"); }

            var group = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                            FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            if (group == null) Debug.LogError("[GS] blockout group 'GeneratorShed' not found");
            else
            {
                int on = 0, already = 0;
                foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (r.enabled) already++; else { r.enabled = true; on++; }
                }
                var rs = group.GetComponentsInChildren<MeshRenderer>(true);
                var cs = group.GetComponentsInChildren<Collider>(true);
                Debug.Log($"[GS] blockout restored: {on} renderers re-enabled ({already} were already on)");
                Debug.Log($"[GS] group now: {rs.Count(r => r.enabled)}/{rs.Length} renderers enabled, " +
                          $"{cs.Count(c => c.enabled)}/{cs.Length} colliders enabled");
                var sb = Find("Shed_Body");
                if (sb != null)
                {
                    var b = sb.GetComponent<Renderer>().bounds;
                    Debug.Log($"[GS] Shed_Body visible again: {b.size.x:0.0} x {b.size.z:0.0} m " +
                              $"(h {b.size.y:0.0}) at {sb.transform.position}");
                }
            }

            // Anything else of ours still parked in the scene?
            var protoRoot = GameObject.Find(RootName);
            if (protoRoot != null)
                Debug.Log($"[GS] {RootName} still holds: " +
                          string.Join(", ", protoRoot.transform.Cast<Transform>().Select(t => t.name)));

            if (player != null) player.gameObject.SetActive(playerWasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[GS] scene saved.");
        }

        static GameObject Find(string n) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == n)?.gameObject;

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__GSCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;

            DynamicGI.UpdateEnvironment();   // else frame 1 uses the stale probe

            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();   // warm-up, discarded
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
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
