using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Renders the candidate generator sheds from identical cameras so they can be
    /// compared directly. Read-only: temporary instances are removed and renderer
    /// states restored, and the scene is NOT saved.
    /// </summary>
    public static class GeneratorShedCompare
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string FbxBoard  = "Assets/_Project/Art/Environment/Buildings/SM_GeneratorShed.fbx";
        const string FbxRebuild= "Assets/_Project/Art/Environment/Buildings/SM_GeneratorShed_Rebuild.fbx";
        const string MatDir    = "Assets/_Project/Art/Materials/ArtPass/GenShed";
        static readonly Vector3 Place = new Vector3(17f, 17f, 13f);

        // matched cameras, used for every candidate
        static readonly (string name, Vector3 eye, Vector3 look, float fov)[] Views =
        {
            ("a_courtyard", new Vector3(2.0f, 18.7f, 15.0f), new Vector3(15.0f, 18.6f, 12.6f), 60f),
            ("b_approach",  new Vector3(6.5f, 20.6f, 6.5f),  new Vector3(16.0f, 18.6f, 12.8f), 55f),
            ("c_threequarter", new Vector3(7.0f, 23.0f, 20.5f), new Vector3(17.0f, 18.6f, 13.0f), 55f),
            ("d_wide",      new Vector3(-6f, 27f, 4f),       new Vector3(15f, 18f, 13.5f), 60f),
        };

        [MenuItem("Tools/Last Beacon/Compare Generator Sheds")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Compare";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var mats = Directory.GetFiles(MatDir, "*.mat")
                .Select(p => AssetDatabase.LoadAssetAtPath<Material>(p.Replace('\\', '/')))
                .Where(m => m != null).ToDictionary(m => m.name, m => m);

            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "PF_GeneratorShed");
            var blockGroup = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            var blockRends = blockGroup != null
                ? blockGroup.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
            var wasEnabled = blockRends.Select(r => r.enabled).ToArray();

            void SetBlockout(bool on) { foreach (var r in blockRends) r.enabled = on; Physics.SyncTransforms(); }
            void SetCurrent(bool on)
            { if (pf != null) foreach (var r in pf.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = on;
              Physics.SyncTransforms(); }

            GameObject Spawn(string fbx, string label, float extraYaw)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (src == null) { Debug.LogError($"[CMP] missing {fbx}"); return null; }
                var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
                imp.useFileScale = true; imp.globalScale = 1f; imp.addCollider = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.SaveAndReimport();
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                go.name = "__CMP_" + label;
                go.transform.position = Place;
                go.transform.rotation = Quaternion.Euler(0f, -5f + extraYaw, 0f);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string k = slots[i] == null ? null : slots[i].name.Replace(" (Instance)", "").Trim();
                        if (k != null && mats.TryGetValue(k, out var m)) slots[i] = m;
                    }
                    r.sharedMaterials = slots;
                }
                // seat on the compound floor
                var rr = go.GetComponentsInChildren<MeshRenderer>();
                var bb = rr[0].bounds; foreach (var r in rr) bb.Encapsulate(r.bounds);
                go.transform.position += new Vector3(0f, 17f - bb.min.y, 0f);
                Physics.SyncTransforms();
                bb = rr[0].bounds; foreach (var r in rr) bb.Encapsulate(r.bounds);
                int tris = go.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
                Debug.Log($"[CMP] {label}: {tris} tris, footprint {bb.size.x:0.0} x {bb.size.z:0.0} m, " +
                          $"height {bb.size.y:0.0} m, {rr.Length} renderers");
                return go;
            }

            // 1 — blockout only
            SetBlockout(true); SetCurrent(false);
            Shoot(shots, "1_blockout");

            // 2 — the reference-board shed (the small wood one, 3.6 x 4.2)
            SetBlockout(false);
            var board = Spawn(FbxBoard, "board", 180f);
            Shoot(shots, "2_board_shed");
            if (board != null) Object.DestroyImmediate(board);

            // 3 — the first full-size rebuild, before the cleanup pass
            var rebuild = Spawn(FbxRebuild, "rebuild", 180f);
            Shoot(shots, "3_rebuild");
            if (rebuild != null) Object.DestroyImmediate(rebuild);

            // 4 — the current approved shell
            SetCurrent(true);
            Shoot(shots, "4_current");

            // 5 — current shell with the blockout also on, for fit
            SetBlockout(true);
            Shoot(shots, "5_current_plus_blockout");

            for (int i = 0; i < blockRends.Length; i++) blockRends[i].enabled = wasEnabled[i];
            SetCurrent(true);
            Physics.SyncTransforms();
            if (player != null) player.gameObject.SetActive(true);
            Debug.Log("[CMP] renderer states restored; scene NOT saved");
        }

        static void Shoot(string dir, string prefix)
        {
            foreach (var v in Views) Capture(dir, $"{prefix}_{v.name}", v.eye, v.look, v.fov);
        }

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__CmpCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.045f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
            cam.fieldOfView = fov;
            DynamicGI.UpdateEnvironment();
            var rt = new RenderTexture(1500, 900, 24) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render(); cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1500, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1500, 900), 0, 0); tex.Apply();
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
