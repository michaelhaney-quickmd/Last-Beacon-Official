using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Imports and places the first cliff art shell over the terrace east wall.
    /// The blockout keeps its colliders and transforms; only its MeshRenderers are
    /// switched off. The art mesh gets no collider of its own.
    /// </summary>
    public static class CliffProtoImport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string Fbx = "Assets/_Project/Art/Environment/Rocks/Test/Cliff_TerraceEast_A.fbx";
        const string MatPath = "Assets/_Project/Art/Materials/ArtPass/Art_Rock_Cliff.mat";
        const string RootName = "LB_ArtProto";
        const string ObjName = "Cliff_TerraceEast_A";
        static readonly Vector3 Place = new Vector3(17f, 6.5f, -15.5f);
        static readonly string[] Blockout = { "Rock_TerraceEast", "Cliff_TerraceEastFace_Battered" };

        /// <summary>Places each variant in turn and shoots the same Main Gate frame.</summary>
        [MenuItem("Tools/Last Beacon/Compare Cliff Variants")]
        public static void Compare()
        {
            string dir = GetArg("-protoOutput") ?? Path.Combine(Path.GetTempPath(), "lb-proto");
            Directory.CreateDirectory(dir);

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);

            foreach (var n in Blockout)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(x => x.name == n);
                t.GetComponent<MeshRenderer>().enabled = false;
            }

            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

            foreach (var variant in new[] { "B", "Wall_Module_A" })
            {
                foreach (var stale in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                             .Where(t => t.name.StartsWith("Cliff_TerraceEast_") || t.name.StartsWith("Cliff_Wall_Module_")).ToArray())
                    Object.DestroyImmediate(stale.gameObject);

                string path = variant.StartsWith("Wall")
                    ? "Assets/_Project/Art/Environment/Rocks/Test/Cliff_Wall_Module_A.fbx"
                    : $"Assets/_Project/Art/Environment/Rocks/Test/Cliff_TerraceEast_{variant}.fbx";
                var mi = (ModelImporter)AssetImporter.GetAtPath(path);
                mi.addCollider = false; mi.useFileScale = false; mi.globalScale = 1f;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
                mi.SaveAndReimport();

                var go = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path));
                go.name = variant.StartsWith("Wall") ? "Cliff_Wall_Module_A" : $"Cliff_TerraceEast_{variant}";
                go.transform.SetParent(root.transform, false);
                go.transform.position = Place;
                go.transform.rotation = Quaternion.Euler(270f, 180f, 0f);
                go.transform.localScale = Vector3.one;
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;

                var bb = go.GetComponentInChildren<MeshRenderer>().bounds;
                var mf = go.GetComponentInChildren<MeshFilter>();
                var vs = mf.sharedMesh.vertices.Select(mf.transform.TransformPoint).ToArray();
                float low = vs.Where(v => v.y < 11f).Select(v => v.x).DefaultIfEmpty(99f).Min();
                Debug.Log($"[CV] {variant}: AABB x {bb.min.x:0.00}..{bb.max.x:0.00} y {bb.min.y:0.00}..{bb.max.y:0.00} " +
                          $"z {bb.min.z:0.00}..{bb.max.z:0.00}  tris {mf.sharedMesh.triangles.Length / 3}  " +
                          $"verts {mf.sharedMesh.vertexCount}  clearance below y11 minX {low:0.000} " +
                          $"{(low >= 17f - 1e-3f ? "OK" : "VIOLATION")}  colliders {go.GetComponentsInChildren<Collider>().Length}");

                Capture(dir, $"MainGate_{variant}", new Vector3(6.5f, 10.7f, -18f),
                    new Vector3(19.5f, 11.5f, -15.5f), 70f);
            }

            if (player != null) player.gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[CV] variant B left in the scene; scene saved.");
        }

        [MenuItem("Tools/Last Beacon/Import Cliff Prototype")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? Path.Combine(Path.GetTempPath(), "lb-proto");
            Directory.CreateDirectory(shots);

            // --- importer settings: no collider, mesh only ------------------------
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp == null) { Debug.LogError($"[PR] {Fbx} not imported"); return; }
            imp.addCollider = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            // The first import arrived at 1/100 scale. Do not trust the file's unit
            // declaration; force one unit to one metre.
            imp.useFileScale = false;
            imp.globalScale = 1f;
            imp.SaveAndReimport();
            Debug.Log("[PR] importer: addCollider=false, materials=None, useFileScale=false, scale=1");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player != null)
            {
                if (!player.gameObject.activeSelf)
                    Debug.LogWarning("[PR] player was left DISABLED in the saved scene — repairing");
                player.gameObject.SetActive(false);
            }
            Physics.SyncTransforms();

            // --- BEFORE shot, blockout still visible -------------------------------
            Capture(shots, "05a_BEFORE_MainGate", new Vector3(6.5f, 10.7f, -18f),
                new Vector3(19.5f, 11.5f, -15.5f), 70f);

            // --- place -------------------------------------------------------------
            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            var old = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == ObjName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.name = ObjName;
            go.transform.SetParent(root.transform, false);
            go.transform.position = Place;
            go.transform.localScale = Vector3.one;

            // The FBX axis flags did not convert as documented in either direction,
            // so the correction is found by measurement: try the 24 axis-aligned
            // orientations and keep the one whose world AABB matches the target.
            var want = new Bounds();
            want.SetMinMax(new Vector3(16.85f, 6.50f, -21.00f), new Vector3(22.50f, 15.40f, -10.00f));
            Quaternion best = Quaternion.identity;
            float bestErr = float.MaxValue;
            foreach (int rx in new[] { 0, 90, 180, 270 })
            foreach (int ry in new[] { 0, 90, 180, 270 })
            foreach (int rz in new[] { 0, 90, 180, 270 })
            {
                var q = Quaternion.Euler(rx, ry, rz);
                go.transform.rotation = q;
                go.transform.position = Place;
                var bb = go.GetComponentInChildren<MeshRenderer>().bounds;
                float err = (bb.min - want.min).sqrMagnitude + (bb.max - want.max).sqrMagnitude;
                if (err < bestErr) { bestErr = err; best = q; }
            }
            go.transform.rotation = best;
            go.transform.position = Place;
            var eul = best.eulerAngles;
            Debug.Log($"[PR] axis correction required: rotation ({eul.x:0},{eul.y:0},{eul.z:0}) " +
                      $"residual {Mathf.Sqrt(bestErr):0.000} m");

            foreach (var c in go.GetComponentsInChildren<Collider>())
            {
                Debug.LogWarning($"[PR] removing unexpected collider on {c.name}");
                Object.DestroyImmediate(c);
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterial = mat;

            // --- orientation check --------------------------------------------------
            var mf = go.GetComponentInChildren<MeshFilter>();
            var b = go.GetComponentInChildren<MeshRenderer>().bounds;
            Debug.Log($"[PR] placed at {go.transform.position}, rot {go.transform.rotation.eulerAngles}");
            Debug.Log($"[PR] world AABB x {b.min.x:0.00}..{b.max.x:0.00}  " +
                      $"y {b.min.y:0.00}..{b.max.y:0.00}  z {b.min.z:0.00}..{b.max.z:0.00}");
            Debug.Log($"[PR] size (want depth~5.65 x height~8.90 x width~11.00): " +
                      $"{b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00}");

            bool oriented = b.size.z > 10f && b.size.y > 8f && b.size.x < 7f;
            Debug.Log(oriented
                ? "[PR] ORIENTATION OK — width on Z, height on Y, depth on X"
                : "[PR] ORIENTATION MISMATCH — see size line above");

            // --- swap the renderers --------------------------------------------------
            foreach (var n in Blockout)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(x => x.name == n);
                var r = t.GetComponent<MeshRenderer>();
                var col = t.GetComponent<Collider>();
                r.enabled = false;
                Debug.Log($"[PR] {n}: renderer={r.enabled}, collider={(col != null && col.enabled)}, " +
                          $"pos {t.position}, still present");
            }
            Physics.SyncTransforms();

            Validate(go);

            Capture(shots, "01_FromDock", new Vector3(0f, 2.1f, -47f), new Vector3(19.5f, 11.5f, -15.5f), 60f);
            Capture(shots, "02_MainGateApproach", new Vector3(6.5f, 10.7f, -18f), new Vector3(19.5f, 11.5f, -15.5f), 70f);
            Capture(shots, "03_SideProfile", new Vector3(19.5f, 13f, -36f), new Vector3(19.5f, 10f, -15.5f), 45f);
            Capture(shots, "04_CloseThreeQuarter", new Vector3(9f, 14.5f, -27f), new Vector3(19.5f, 11.5f, -15.5f), 55f);
            Capture(shots, "05b_AFTER_MainGate", new Vector3(6.5f, 10.7f, -18f), new Vector3(19.5f, 11.5f, -15.5f), 70f);

            // Re-enable BEFORE saving: saving first persisted a disabled player and
            // broke two tests that look for the CharacterController.
            if (player != null) player.gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[PR] player re-enabled, scene saved.");
        }

        static void Validate(GameObject go)
        {
            var mf = go.GetComponentInChildren<MeshFilter>();
            var xf = mf.transform;
            var verts = mf.sharedMesh.vertices.Select(xf.TransformPoint).ToArray();

            float lowMinX = verts.Where(v => v.y < 11f).Select(v => v.x).DefaultIfEmpty(99f).Min();
            float highMinX = verts.Where(v => v.y >= 11f).Select(v => v.x).DefaultIfEmpty(99f).Min();
            Debug.Log($"[PR] clearance: below y11 min X = {lowMinX:0.000} " +
                      $"({(lowMinX >= 17f - 1e-3f ? "OK, not west of 17.0" : "VIOLATION")})");
            Debug.Log($"[PR] clearance: above y11 min X = {highMinX:0.000} " +
                      $"({(highMinX >= 16.5f - 1e-3f ? "OK, within 16.5" : "EXCEEDS 16.5")})");

            var deck = Find("Terrace_Deck").GetComponent<Renderer>().bounds;
            int intoDeck = verts.Count(v => v.x < deck.max.x && v.y < deck.max.y + 0.05f &&
                                            v.z > deck.min.z && v.z < deck.max.z);
            Debug.Log($"[PR] Terrace_Deck intrusion: {intoDeck} vertices inside the deck volume");

            var lane = new Vector3(11f, 9.2f, -17.3f);
            float laneDist = verts.Min(v => Vector3.Distance(v, lane));
            Debug.Log($"[PR] MainGate_SafePassageLane: nearest art vertex {laneDist:0.00} m");

            var north = Find("Rock_TerraceNorth").GetComponent<Renderer>().bounds;
            int intoNorth = verts.Count(v => north.Contains(v));
            Debug.Log($"[PR] Rock_TerraceNorth overlap: {intoNorth} vertices inside its AABB");

            var cols = go.GetComponentsInChildren<Collider>();
            Debug.Log($"[PR] colliders on art mesh: {cols.Length} " + (cols.Length == 0 ? "(OK)" : "(UNEXPECTED)"));
        }

        static GameObject Find(string n) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == n)?.gameObject;

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__ProtoCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;

            bool fogWas = RenderSettings.fog;
            var ambWas = RenderSettings.ambientSkyColor;
            RenderSettings.fog = false;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.5f);
            var fill = new GameObject("__ProtoFill");
            fill.transform.rotation = Quaternion.Euler(38f, 215f, 0f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 1.25f; fl.shadows = LightShadows.Soft;

            // The ambient probe does not rebuild until this is called, so the first
            // frame of a sequence renders under the PREVIOUS ambient and later ones
            // under the new one. That silently broke an A/B comparison.
            DynamicGI.UpdateEnvironment();

            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();          // warm-up frame, discarded
            cam.Render();
            Debug.Log($"[CAP] {name}: ambient {RenderSettings.ambientSkyColor}, " +
                      $"lights {Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l => l.enabled)}");
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());

            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(fill);
            RenderSettings.fog = fogWas; RenderSettings.ambientSkyColor = ambWas;
        }

        static string GetArg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
