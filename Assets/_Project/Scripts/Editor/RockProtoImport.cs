using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Places the low-poly shore outcrop prototype in the island scene. Nothing in
    /// the blockout is moved, hidden or given a collider — the rock is art only, so
    /// it can be deleted with no gameplay consequence.
    /// </summary>
    public static class RockProtoImport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string Fbx = "Assets/_Project/Art/Environment/Rocks/Test/SM_Rock_ShoreOutcrop_A.fbx";
        const string MatPath = "Assets/_Project/Art/Materials/ArtPass/Art_Rock_Cliff.mat";
        const string RootName = "LB_ArtProto";
        const string ObjName = "SM_Rock_ShoreOutcrop_A";

        // Candidates are tried in order; the first one that finds ground and keeps
        // its distance from the walk route wins. Guessing a Y by hand is how the
        // earlier prototype ended up floating.
        static readonly Vector2[] Candidates =
        {
            new Vector2(8.5f, -40.0f),   // shore beside the dock approach
            new Vector2(-8.0f, -38.0f),  // opposite shore
            new Vector2(12.0f, -30.0f),  // lower bench
        };

        const float SinkDepth = 0.30f;   // bed it into the ground, don't perch it
        const float MinRouteClearance = 2.0f;

        [MenuItem("Tools/Last Beacon/Import Rock Prototype")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? Path.Combine(Path.GetTempPath(), "lb-rock");
            Directory.CreateDirectory(shots);

            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp == null) { Debug.LogError($"[RK] {Fbx} not imported"); return; }
            imp.addCollider = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.useFileScale = false;   // the FBX unit declaration is not trustworthy here
            imp.globalScale = 1f;
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
            // Established empirically for Blender->Unity FBX in this project.
            go.transform.rotation = Quaternion.Euler(270f, 180f, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;

            var rend = go.GetComponentInChildren<MeshRenderer>();
            var mf = go.GetComponentInChildren<MeshFilter>();
            Debug.Log($"[RK] mesh: {mf.sharedMesh.triangles.Length / 3} tris, " +
                      $"{mf.sharedMesh.vertexCount} verts, local size {mf.sharedMesh.bounds.size}");

            bool placed = false;
            foreach (var c in Candidates)
            {
                if (!Physics.Raycast(new Vector3(c.x, 60f, c.y), Vector3.down, out var hit, 120f))
                { Debug.Log($"[RK] candidate ({c.x},{c.y}): no ground found — skipped"); continue; }

                go.transform.position = new Vector3(c.x, 0f, c.y);
                Physics.SyncTransforms();
                float lift = hit.point.y - SinkDepth - rend.bounds.min.y;
                go.transform.position += new Vector3(0f, lift, 0f);
                Physics.SyncTransforms();

                float clearance = RouteClearance(rend.bounds);
                Debug.Log($"[RK] candidate ({c.x},{c.y}): ground '{hit.collider.name}' at y={hit.point.y:0.00}, " +
                          $"route clearance {clearance:0.00} m");
                if (clearance >= MinRouteClearance) { placed = true; break; }
                Debug.LogWarning($"[RK]   rejected — inside the {MinRouteClearance:0.0} m route clearance band");
            }
            if (!placed)
            {
                Debug.LogError("[RK] no candidate satisfied the route clearance rule — rock left at the last spot");
            }

            var b = rend.bounds;
            Debug.Log($"[RK] placed at {go.transform.position}  world size " +
                      $"{b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00}");
            Debug.Log($"[RK] world AABB x {b.min.x:0.00}..{b.max.x:0.00}  " +
                      $"y {b.min.y:0.00}..{b.max.y:0.00}  z {b.min.z:0.00}..{b.max.z:0.00}");
            bool upright = b.size.y > 2.4f && b.size.y < 3.2f && b.size.x > 2f && b.size.z > 2f;
            Debug.Log(upright ? "[RK] ORIENTATION OK — height ~2.8 m on Y"
                              : "[RK] ORIENTATION SUSPECT — check the size line above");
            Debug.Log($"[RK] colliders on art mesh: {go.GetComponentsInChildren<Collider>().Length} (want 0)");

            Capture(shots, "01_FromDock", new Vector3(0f, 2.1f, -47f), b.center, 60f);
            Capture(shots, "02_Eyelevel", b.center + new Vector3(-6f, -0.6f, -6f), b.center, 60f);
            Capture(shots, "03_ThreeQuarter", b.center + new Vector3(7f, 3.5f, -7f), b.center, 55f);
            Capture(shots, "04_Wide", new Vector3(0f, 14f, -60f), new Vector3(0f, 8f, -25f), 60f);

            if (player != null) player.gameObject.SetActive(playerWasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[RK] scene saved.");
        }

        /// <summary>Nearest approach to anything the player walks on.</summary>
        static float RouteClearance(Bounds b)
        {
            string[] route = { "Dock_", "Path_", "Stair_", "Terrace_", "Ramp_", "Wp" };
            float best = 999f;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!route.Any(p => t.name.StartsWith(p))) continue;
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                float d = Mathf.Sqrt(b.SqrDistance(r.bounds.ClosestPoint(b.center)));
                if (d < best) best = d;
            }
            return best;
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__RockCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;

            // Without this the first frame renders under the previous ambient probe.
            DynamicGI.UpdateEnvironment();

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
