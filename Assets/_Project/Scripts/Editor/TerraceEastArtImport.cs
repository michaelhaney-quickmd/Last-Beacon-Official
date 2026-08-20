using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Replaces the rejected Cliff_Wall_Module_A stand-in on the east terrace with
    /// the faceted art shell. The blockout keeps its colliders and transforms; only
    /// its MeshRenderers stay off. The art mesh gets no collider.
    /// </summary>
    public static class TerraceEastArtImport
    {
        const string ScenePath = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string Fbx = "Assets/_Project/Art/Environment/Rocks/Test/SM_Cliff_TerraceEast_C.fbx";
        const string MatPath = "Assets/_Project/Art/Materials/ArtPass/Art_Rock_Cliff.mat";
        const string RootName = "LB_ArtProto";
        const string ObjName = "SM_Cliff_TerraceEast_C";
        static readonly string[] Stale = { "Cliff_Wall_Module_A", "Cliff_TerraceEast_A", "Cliff_TerraceEast_B" };
        static readonly string[] Blockout = { "Rock_TerraceEast", "Cliff_TerraceEastFace_Battered" };

        // The shell the blockout expects, carried over from the first prototype.
        static readonly Vector3 WantMin = new Vector3(16.85f, 6.50f, -21.00f);
        static readonly Vector3 WantMax = new Vector3(22.50f, 15.40f, -10.00f);

        [MenuItem("Tools/Last Beacon/Import Terrace East Art")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? Path.Combine(Path.GetTempPath(), "lb-te");
            Directory.CreateDirectory(shots);

            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp == null) { Debug.LogError($"[TA] {Fbx} not imported"); return; }
            imp.addCollider = false;
            imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.useFileScale = false; imp.globalScale = 1f;
            imp.SaveAndReimport();

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool playerWasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            foreach (var n in Stale)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(x => x.name == n);
                if (t != null) { Debug.Log($"[TA] removing stale prototype '{n}'"); Object.DestroyImmediate(t.gameObject); }
            }

            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            var old = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == ObjName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            go.name = ObjName;
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = Vector3.one;
            // Unity's FBX import applies its own axis conversion before this
            // rotation, so the mapping is not what the Euler angles alone imply.
            // Verified by the facing checks below: this puts the carved column
            // face west (toward the player) and the flat backing east.
            go.transform.rotation = Quaternion.Euler(270f, 180f, 0f);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;

            // Seat by the clearance rule, not by the AABB corner. Pinning the AABB
            // min to 16.85 makes the below-y11 rule unsatisfiable whenever the
            // lowest band is also the westernmost — the mesh can never comply, no
            // matter how much is carved off it. Height and run still key off the
            // target box; X is solved so the below-y11 band lands exactly on 17.0.
            var rend = go.GetComponentInChildren<MeshRenderer>();
            var meshFilter = go.GetComponentInChildren<MeshFilter>();
            go.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            var b0 = rend.bounds;
            var offset = new Vector3(0f, WantMin.y - b0.min.y, WantMin.z - b0.min.z);
            go.transform.position = offset;
            Physics.SyncTransforms();

            var localVerts = meshFilter.sharedMesh.vertices;
            var xfNow = meshFilter.transform;
            float lowBandMinX = localVerts.Select(xfNow.TransformPoint)
                                          .Where(v => v.y < 11f)
                                          .Select(v => v.x)
                                          .DefaultIfEmpty(0f).Min();
            const float LowBandLimitX = 17.0f;
            go.transform.position += new Vector3(LowBandLimitX - lowBandMinX, 0f, 0f);
            Physics.SyncTransforms();
            Debug.Log($"[TA] seated on the clearance rule: shifted X by " +
                      $"{LowBandLimitX - lowBandMinX:0.000} m so the below-y11 band sits on {LowBandLimitX:0.00}");

            var b = rend.bounds;
            var mf = go.GetComponentInChildren<MeshFilter>();
            Debug.Log($"[TA] mesh {mf.sharedMesh.triangles.Length / 3} tris, {mf.sharedMesh.vertexCount} verts");
            Debug.Log($"[TA] placed at {go.transform.position}, rot {go.transform.rotation.eulerAngles}");
            Debug.Log($"[TA] world AABB x {b.min.x:0.00}..{b.max.x:0.00}  y {b.min.y:0.00}..{b.max.y:0.00}  " +
                      $"z {b.min.z:0.00}..{b.max.z:0.00}");
            Debug.Log($"[TA] size {b.size.x:0.00} x {b.size.y:0.00} x {b.size.z:0.00} " +
                      $"(want ~5.65 x 8.90 x 11.00)");
            bool oriented = b.size.z > 10f && b.size.y > 8f && b.size.x < 7f;
            Debug.Log(oriented ? "[TA] ORIENTATION OK — run on Z, height on Y, depth on X"
                               : "[TA] ORIENTATION MISMATCH");

            foreach (var n in Blockout)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .FirstOrDefault(x => x.name == n);
                var r = t.GetComponent<MeshRenderer>();
                var col = t.GetComponent<Collider>();
                r.enabled = false;
                Debug.Log($"[TA] {n}: renderer={r.enabled}, collider={(col != null && col.enabled)}, present");
            }
            Physics.SyncTransforms();

            Validate(go);

            Capture(shots, "01_MainGateApproach", new Vector3(6.5f, 10.7f, -18f), new Vector3(19.5f, 11.5f, -15.5f), 70f);
            Capture(shots, "02_FromDock", new Vector3(0f, 2.1f, -47f), new Vector3(19.5f, 11.5f, -15.5f), 60f);
            Capture(shots, "03_SideProfile", new Vector3(19.5f, 13f, -36f), new Vector3(19.5f, 10f, -15.5f), 45f);
            Capture(shots, "04_CloseThreeQuarter", new Vector3(9f, 14.5f, -27f), new Vector3(19.5f, 11.5f, -15.5f), 55f);

            if (player != null) player.gameObject.SetActive(playerWasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[TA] scene saved.");
        }

        /// <summary>The clearance rules the first prototype had to satisfy.</summary>
        static void Validate(GameObject go)
        {
            var mf = go.GetComponentInChildren<MeshFilter>();
            var xf = mf.transform;
            var verts = mf.sharedMesh.vertices.Select(xf.TransformPoint).ToArray();

            // Where each mesh-local axis actually ends up in world space. Deduce
            // this from the Euler angles alone and you will get it wrong — the
            // importer's own conversion is applied first.
            Debug.Log($"[TA] axis map: local +X -> {xf.TransformDirection(Vector3.right)}, " +
                      $"+Y -> {xf.TransformDirection(Vector3.up)}, " +
                      $"+Z -> {xf.TransformDirection(Vector3.forward)}");

            // Facing check. The backing slab's flat back is by far the largest
            // face; it must point EAST. If it points west the mesh is reversed
            // and the player is looking at a blank slab.
            var mesh = mf.sharedMesh;
            var tris = mesh.triangles;
            var lv = mesh.vertices;
            float bestArea = 0f; Vector3 bestNormal = Vector3.zero;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = xf.TransformPoint(lv[tris[i]]);
                Vector3 c = xf.TransformPoint(lv[tris[i + 1]]);
                Vector3 d = xf.TransformPoint(lv[tris[i + 2]]);
                Vector3 cr = Vector3.Cross(c - a, d - a);
                float area = cr.magnitude * 0.5f;
                if (area > bestArea) { bestArea = area; bestNormal = cr.normalized; }
            }
            Debug.Log($"[TA] largest face: {bestArea:0.00} m2, normal {bestNormal} " +
                      $"({(bestNormal.x > 0.5f ? "points EAST — OK, backing is behind" : "NOT east — mesh may be reversed")})");

            // Independent confirmation: the carved face should carry far more
            // vertices than the flat backing.
            float midX = (verts.Min(v => v.x) + verts.Max(v => v.x)) * 0.5f;
            int west = verts.Count(v => v.x < midX), east = verts.Length - west;
            Debug.Log($"[TA] vertex split: {west} west of centre vs {east} east " +
                      $"({(west > east ? "OK — detail faces the player" : "SUSPECT — detail is on the buried side")})");

            float lowMinX = verts.Where(v => v.y < 11f).Select(v => v.x).DefaultIfEmpty(99f).Min();
            float highMinX = verts.Where(v => v.y >= 11f).Select(v => v.x).DefaultIfEmpty(99f).Min();
            Debug.Log($"[TA] clearance below y11: min X = {lowMinX:0.000} " +
                      $"({(lowMinX >= 17f - 1e-3f ? "OK" : "VIOLATION — west of 17.0")})");
            Debug.Log($"[TA] clearance above y11: min X = {highMinX:0.000} " +
                      $"({(highMinX >= 16.5f - 1e-3f ? "OK" : "VIOLATION — west of 16.5")})");

            // Print the deck's real bounds: the inherited test compares against
            // them without showing them, which made its count uninterpretable.
            var deck = Find("Terrace_Deck").GetComponent<Renderer>().bounds;
            Debug.Log($"[TA] Terrace_Deck bounds x {deck.min.x:0.00}..{deck.max.x:0.00} " +
                      $"y {deck.min.y:0.00}..{deck.max.y:0.00} z {deck.min.z:0.00}..{deck.max.z:0.00}");
            int insideDeck = verts.Count(v => deck.Contains(v));
            Debug.Log($"[TA] vertices actually inside the Terrace_Deck volume: {insideDeck}");

            var lane = new Vector3(11f, 9.2f, -17.3f);
            Debug.Log($"[TA] MainGate_SafePassageLane: nearest vertex {verts.Min(v => Vector3.Distance(v, lane)):0.00} m");

            var north = Find("Rock_TerraceNorth").GetComponent<Renderer>().bounds;
            Debug.Log($"[TA] Rock_TerraceNorth overlap: {verts.Count(v => north.Contains(v))} vertices");
            Debug.Log($"[TA] colliders on art mesh: {go.GetComponentsInChildren<Collider>().Length} (want 0)");
        }

        static GameObject Find(string n) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == n)?.gameObject;

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__TECam");
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
            var fill = new GameObject("__TEFill");
            fill.transform.rotation = Quaternion.Euler(38f, 215f, 0f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 1.25f; fl.shadows = LightShadows.Soft;

            DynamicGI.UpdateEnvironment();   // otherwise frame 1 uses the stale probe

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
