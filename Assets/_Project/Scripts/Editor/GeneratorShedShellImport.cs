using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastBeacon.Editor
{
    /// <summary>
    /// Imports the approved Generator Shed art shell, builds PF_GeneratorShed, places
    /// it at the approved blockout transform and validates it against the blockout.
    ///
    /// The ProBuilder blockout is left VISIBLE — this pass is a visual-fit comparison,
    /// not a replacement. No mesh colliders are generated on the art.
    /// </summary>
    public static class GeneratorShedShellImport
    {
        const string ScenePath  = "Assets/_Project/Scenes/Island_Blockout.unity";
        const string Fbx        = "Assets/_Project/Art/Environment/Buildings/GeneratorShed/SM_GeneratorShed.fbx";
        const string MatDir     = "Assets/_Project/Art/Materials/ArtPass/GenShed";
        const string PrefabPath = "Assets/_Project/Prefabs/PF_GeneratorShed.prefab";
        const string InstName   = "PF_GeneratorShed";

        // Approved blockout transform.
        static readonly Vector3 Place = new Vector3(17f, 17f, 13f);
        const float Yaw = -5f;

        static readonly Dictionary<string, (Color c, float s, float m, Color e)> Palette = new()
        {
            { "MAT_Concrete",      (new Color(0.150f,0.152f,0.148f), 0.14f, 0f, Color.black) },
            { "MAT_Metal",         (new Color(0.135f,0.158f,0.185f), 0.62f, 0.90f, Color.black) },
            { "MAT_Metal_Painted", (new Color(0.205f,0.225f,0.235f), 0.45f, 0.35f, Color.black) },
            { "MAT_Rust",          (new Color(0.175f,0.082f,0.044f), 0.20f, 0.15f, Color.black) },
            { "MAT_Emissive_Warm", (new Color(0.90f,0.60f,0.30f),   0.50f, 0f, new Color(1.00f,0.62f,0.28f) * 6f) },
        };

        [MenuItem("Tools/Last Beacon/Import Generator Shed Shell")]
        public static void Run()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Shell";
            Directory.CreateDirectory(shots);
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory("Assets/_Project/Prefabs");

            // ---------- materials -------------------------------------------------
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

            // ---------- importer --------------------------------------------------
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp == null) { Debug.LogError($"[SHELL] {Fbx} not imported"); return; }
            imp.addCollider = false;                 // no colliders on the art shell
            imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            // Multi-object FBX: useFileScale=false leaves every CHILD node at 100x.
            // Verified on this project twice. true is correct here.
            imp.useFileScale = true; imp.globalScale = 1f;
            imp.importNormals = ModelImporterNormals.Import;   // keep the authored sharp/smooth split
            imp.importTangents = ModelImporterTangents.CalculateMikk;
            imp.weldVertices = false;                          // do not merge the authored splits
            imp.SaveAndReimport();
            Debug.Log("[SHELL] importer: useFileScale=true, scale=1, normals=Import, weld=false, colliders=off");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool playerWasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var stale = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).FirstOrDefault(t => t.name == InstName);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            // Earlier prototype imports of THIS building left a second static copy on
            // the same plot under LB_ArtProto, which sat on top of the new shell.
            // Remove only the shed prototype — the cliff and rock protos stay.
            foreach (var n in new[] { "SM_GeneratorShed_Rebuild", "SM_GeneratorShed" })
            {
                var oldProto = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == n && t.parent != null && t.parent.name == "LB_ArtProto");
                if (oldProto != null)
                {
                    Debug.LogWarning($"[SHELL] removing stale prototype LB_ArtProto/{n} — it was " +
                                     "overlapping the new shell on the same plot");
                    Object.DestroyImmediate(oldProto.gameObject);
                }
            }
            var protoRoot = GameObject.Find("LB_ArtProto");
            if (protoRoot != null)
                Debug.Log("[SHELL] LB_ArtProto still holds: " +
                    string.Join(", ", protoRoot.transform.Cast<Transform>().Select(t => t.name)));

            // ---------- prefab hierarchy -----------------------------------------
            var pf = new GameObject(InstName);
            var visual = new GameObject("Visual"); visual.transform.SetParent(pf.transform, false);
            var gameplay = new GameObject("Gameplay"); gameplay.transform.SetParent(pf.transform, false);
            var collision = new GameObject("Collision"); collision.transform.SetParent(pf.transform, false);

            var art = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            art.name = "SM_GeneratorShed_ROOT";
            art.transform.SetParent(visual.transform, false);
            PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            Debug.Log($"[SHELL] imported root localScale {art.transform.localScale}");
            var kids = art.GetComponentsInChildren<MeshRenderer>();
            Debug.Log($"[SHELL] child meshes: {kids.Length}, names: " +
                      string.Join(", ", kids.Select(k => k.name)));

            int remapped = 0, missed = 0;
            foreach (var r in kids)
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string key = slots[i] == null ? null : slots[i].name.Replace(" (Instance)", "").Trim();
                    if (key != null && mats.TryGetValue(key, out var m)) { slots[i] = m; remapped++; }
                    else { missed++; Debug.LogWarning($"[SHELL] unmatched material '{key}' on {r.name}"); }
                }
                r.sharedMaterials = slots;
            }
            Debug.Log($"[SHELL] material slots remapped: {remapped}, unmatched: {missed}");

            // ---------- place at the approved transform, root only ----------------
            pf.transform.position = Place;
            pf.transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
            pf.transform.localScale = Vector3.one;
            Physics.SyncTransforms();
            Debug.Log($"[SHELL] placed PF root at {pf.transform.position}, yaw {Yaw}, scale {pf.transform.localScale}");

            Validate(art, kids);
            OrientationCheck(art);
            DoorSetup(pf, art, shots);
            CompareToBlockout(art);

            // ---------- save prefab asset ----------------------------------------
            var saved = PrefabUtility.SaveAsPrefabAssetAndConnect(pf, PrefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[SHELL] prefab written to {PrefabPath} ({(saved != null ? "ok" : "FAILED")})");

            if (player != null) player.gameObject.SetActive(playerWasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[SHELL] scene saved. ProBuilder blockout left VISIBLE for comparison.");
        }

        static void Validate(GameObject art, MeshRenderer[] kids)
        {
            float worstScale = 0f;
            foreach (var k in kids)
                worstScale = Mathf.Max(worstScale, Mathf.Abs(k.transform.lossyScale.x - 1f));
            var bb = kids[0].bounds;
            foreach (var k in kids) bb.Encapsulate(k.bounds);
            int tris = art.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3);
            Debug.Log($"[SHELL] world AABB size {bb.size.x:0.000} x {bb.size.y:0.000} x {bb.size.z:0.000}");
            Debug.Log($"[SHELL] triangles {tris} (source 2680)");
            const float ExpectedHeight = 5.238f;
            float err = Mathf.Abs(bb.size.y - ExpectedHeight);
            Debug.Log($"[SHELL] scale check: height {bb.size.y:0.000} vs source {ExpectedHeight:0.000} " +
                      $"(err {err:0.000}) {(err < 0.02f ? "OK 1:1" : "SCALE WRONG")}");
            Debug.Log($"[SHELL] child lossyScale max deviation from 1.0: {worstScale:0.000} " +
                      "(non-zero is fine if the height check passes — the node scale and baked mesh cancel)");
        }

        static void OrientationCheck(GameObject art)
        {
            var frame = art.GetComponentsInChildren<MeshRenderer>()
                           .FirstOrDefault(r => r.name.Contains("DoorFrame"));
            var lean  = art.GetComponentsInChildren<MeshRenderer>()
                           .FirstOrDefault(r => r.name.Contains("LeanTo"));
            var all = art.GetComponentsInChildren<MeshRenderer>();
            var whole = all[0].bounds; foreach (var r in all) whole.Encapsulate(r.bounds);
            var courtyard = new Vector3(0f, 17f, 17f);

            var doorDir = frame.bounds.center - whole.center; doorDir.y = 0f; doorDir.Normalize();
            var yardDir = courtyard - whole.center; yardDir.y = 0f; yardDir.Normalize();
            float d = Vector3.Dot(doorDir, yardDir);
            Debug.Log($"[SHELL] doorway faces {doorDir}, courtyard is {yardDir}, alignment {d:0.000} " +
                      $"{(d > 0.7f ? "OK — faces the yard" : "MISALIGNED — diagnose at source, do not compensate here")}");

            var blkLean = Find("Shed_LeanToRoof");
            if (blkLean != null && lean != null)
            {
                var a = lean.bounds.center - whole.center; a.y = 0f;
                var b = blkLean.GetComponent<Renderer>().bounds.center - new Vector3(17f, 17f, 13f); b.y = 0f;
                Debug.Log($"[SHELL] lean-to side alignment vs blockout: {Vector3.Dot(a.normalized, b.normalized):0.000} " +
                          $"{(Vector3.Dot(a.normalized, b.normalized) > 0.7f ? "SAME SIDE" : "OPPOSITE SIDE")}");
            }
        }

        static void DoorSetup(GameObject pf, GameObject art, string shots)
        {
            var L = art.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Door_L"));
            var R = art.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Door_R"));
            if (L == null || R == null) { Debug.LogError("[SHELL] door leaves not found"); return; }
            Debug.Log($"[SHELL] door leaves are distinct children: {L.name} (parent {L.parent.name}), " +
                      $"{R.name} (parent {R.parent.name})");

            // hinge pivots: the leaf origin must not move when the leaf rotates
            foreach (var (leaf, label) in new[] { (L, "Door_L"), (R, "Door_R") })
            {
                Vector3 p0 = leaf.position;
                var rend = leaf.GetComponent<MeshRenderer>();
                Vector3 e0 = rend.bounds.center;
                // Rotation about ANY axis through the origin holds the pivot, so also
                // prove the leaf stays LEVEL — that is what identifies the hinge axis.
                var mfp = leaf.GetComponent<MeshFilter>();
                var vs = mfp.sharedMesh.vertices;
                int far = 0; float fd = 0f;
                for (int i = 0; i < vs.Length; i++) { float d = vs[i].magnitude; if (d > fd) { fd = d; far = i; } }
                Vector3 edge0 = leaf.TransformPoint(vs[far]);
                leaf.localRotation = Quaternion.Euler(0f, 0f, 95f);   // local Z is the hinge
                Physics.SyncTransforms();
                Vector3 p1 = leaf.position;
                Vector3 e1 = rend.bounds.center;
                Vector3 edge1 = leaf.TransformPoint(vs[far]);
                leaf.localRotation = Quaternion.identity;
                Physics.SyncTransforms();
                Debug.Log($"[SHELL] {label}: pivot {p0}, pivot drift {(p1 - p0).magnitude:0.0000} m " +
                          $"{((p1 - p0).magnitude < 0.001f ? "HELD" : "MOVED")}; free edge swept " +
                          $"{(edge1 - edge0).magnitude:0.000} m with dY {(edge1.y - edge0.y):0.000} " +
                          $"{(Mathf.Abs(edge1.y - edge0.y) < 0.01f ? "LEVEL — correct hinge axis" : "TILTED — WRONG AXIS")}");
            }

            // one simple BoxCollider per leaf, child of the leaf so it follows the hinge
            foreach (var (leaf, label) in new[] { (L, "Door_L"), (R, "Door_R") })
            {
                var old = leaf.Find("DoorCollider");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var go = new GameObject("DoorCollider");
                go.transform.SetParent(leaf, false);
                var bc = go.AddComponent<BoxCollider>();
                // major rectangular panel only, in the leaf's local space
                var mf = leaf.GetComponent<MeshFilter>();
                var lb = mf.sharedMesh.bounds;
                bc.center = lb.center; bc.size = lb.size;
                Physics.SyncTransforms();
                Debug.Log($"[SHELL] {label} BoxCollider world size {bc.bounds.size} " +
                          $"(local {bc.size} at the baked mesh scale; child of the leaf, rotates with the hinge)");
            }

            var dd = pf.GetComponent<ScriptedDoubleDoor>() ?? pf.AddComponent<ScriptedDoubleDoor>();
            dd.leftLeaf = L; dd.rightLeaf = R; dd.closedAngle = 0f; dd.openAngle = 95f;
            dd.hingeAxis = ScriptedDoubleDoor.Axis.Z;   // measured, not assumed
            Debug.Log("[SHELL] ScriptedDoubleDoor attached: closed 0, open 95, opposite directions, interpolated");

            foreach (float a in new[] { 0f, 45f, 95f })
            {
                dd.PoseImmediate(a);
                Physics.SyncTransforms();
                var lc = L.Find("DoorCollider").GetComponent<BoxCollider>();
                var rc = R.Find("DoorCollider").GetComponent<BoxCollider>();
                Debug.Log($"[SHELL] @{a:0} deg: L collider world centre {lc.bounds.center}, " +
                          $"R {rc.bounds.center}; gap between leaves {(lc.bounds.center - rc.bounds.center).magnitude:0.00} m");
                Capture(shots, $"door_{a:00}", new Vector3(6.0f, 19.4f, 13.6f),
                        new Vector3(12.5f, 18.6f, 13.2f), 55f);
            }
            dd.PoseImmediate(0f);
            Physics.SyncTransforms();
        }

        static void CompareToBlockout(GameObject art)
        {
            var all = art.GetComponentsInChildren<MeshRenderer>();
            var artBB = all[0].bounds; foreach (var r in all) artBB.Encapsulate(r.bounds);
            var body = all.FirstOrDefault(r => r.name.Contains("Body"));
            var blk = Find("Shed_Body"); var blkRoof = Find("Shed_Roof");
            Debug.Log("[SHELL] --- ART vs BLOCKOUT ---");
            if (blk != null && body != null)
            {
                var b = blk.GetComponent<Renderer>().bounds;
                var a = body.bounds;
                Debug.Log($"[SHELL] body    art {a.size.x:0.00} x {a.size.z:0.00} (h {a.size.y:0.00}) | " +
                          $"blockout {b.size.x:0.00} x {b.size.z:0.00} (h {b.size.y:0.00}) | " +
                          $"centre delta {(a.center - b.center).magnitude:0.000} m");
            }
            if (blkRoof != null)
            {
                var roof = all.FirstOrDefault(r => r.name.Contains("Roof"));
                var b = blkRoof.GetComponent<Renderer>().bounds;
                Debug.Log($"[SHELL] roof    art top {roof.bounds.max.y:0.00} | blockout top {b.max.y:0.00} | " +
                          $"delta {Mathf.Abs(roof.bounds.max.y - b.max.y):0.000} m");
            }
            // Several of these names exist twice: once as a ProBuilder prop and once
            // as a marker GameObject with no Renderer. Pick the one that has a mesh.
            foreach (var n in new[] { "Shed_LeanToRoof", "Generator_Body", "Generator_Breaker",
                                      "Generator_FusePanel", "Generator_FuelCap" })
            {
                var g = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.name == n).Select(t => t.gameObject)
                    .FirstOrDefault(o => o.GetComponent<Renderer>() != null);
                if (g == null) { Debug.Log($"[SHELL] {n,-22} no renderer found — skipped"); continue; }
                var gb = g.GetComponent<Renderer>().bounds;
                bool insideArt = artBB.Contains(gb.center);
                Debug.Log($"[SHELL] {n,-22} centre {gb.center} inside the art envelope: {insideArt}");
            }
            var lamp = all.FirstOrDefault(r => r.name.Contains("Lamp_Emissive"));
            var blkLamp = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                                .FirstOrDefault(l => l.name.Contains("Lamp_GeneratorShed"));
            if (lamp != null && blkLamp != null)
                Debug.Log($"[SHELL] lamp    art {lamp.bounds.center} | blockout practical {blkLamp.transform.position} | " +
                          $"delta {(lamp.bounds.center - blkLamp.transform.position).magnitude:0.00} m");
        }


        /// <summary>Second pass: deviation detail + comparison/night captures.
        /// Temporarily hides the blockout for the art-only shots, then restores it.</summary>
        [MenuItem("Tools/Last Beacon/Validate Generator Shed Shell")]
        public static void Validate2()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Shell";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);
            Physics.SyncTransforms();

            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            if (pf == null) { Debug.LogError("[DEV] PF_GeneratorShed not in the scene"); return; }
            var all = pf.GetComponentsInChildren<MeshRenderer>();
            var artBB = all[0].bounds; foreach (var r in all) artBB.Encapsulate(r.bounds);
            var body = all.FirstOrDefault(r => r.name.Contains("Body"));
            var blk  = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name == "Shed_Body").Select(t => t.gameObject)
                .FirstOrDefault(o => o.GetComponent<Renderer>() != null);

            Debug.Log("[DEV] ================= DEVIATION DETAIL =================");
            if (blk != null && body != null)
            {
                var a = body.bounds; var b = blk.GetComponent<Renderer>().bounds;
                var dXZ = new Vector2(a.center.x - b.center.x, a.center.z - b.center.z);
                Debug.Log($"[DEV] body centre: art {a.center} blockout {b.center}");
                Debug.Log($"[DEV]   horizontal delta {dXZ.magnitude:0.000} m | vertical delta {(a.center.y - b.center.y):0.000} m");
                Debug.Log($"[DEV]   (art Body carries the gable walls to the 5.2 ridge; the blockout Shed_Body");
                Debug.Log($"[DEV]    is a 4.2 box with a separate roof prism, so a vertical offset is expected)");
                Debug.Log($"[DEV]   footprint X art {a.min.x:0.00}..{a.max.x:0.00} | blockout {b.min.x:0.00}..{b.max.x:0.00}");
                Debug.Log($"[DEV]   footprint Z art {a.min.z:0.00}..{a.max.z:0.00} | blockout {b.min.z:0.00}..{b.max.z:0.00}");
                Debug.Log($"[DEV]   edge deltas: -X {Mathf.Abs(a.min.x-b.min.x):0.000}  +X {Mathf.Abs(a.max.x-b.max.x):0.000}  " +
                          $"-Z {Mathf.Abs(a.min.z-b.min.z):0.000}  +Z {Mathf.Abs(a.max.z-b.max.z):0.000}");
            }
            var lamp = all.FirstOrDefault(r => r.name.Contains("Lamp_Emissive"));
            var pract = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                              .FirstOrDefault(l => l.name.Contains("Lamp_GeneratorShed"));
            if (lamp != null && pract != null)
            {
                var d = lamp.bounds.center - pract.transform.position;
                Debug.Log($"[DEV] lamp: art fixture {lamp.bounds.center} | blockout practical {pract.transform.position}");
                Debug.Log($"[DEV]   delta {d.magnitude:0.000} m (horizontal {new Vector2(d.x,d.z).magnitude:0.000}) " +
                          "— the practical is a blockout light position, not art; moving it is a lighting change, not done here");
            }

            // captures: both visible, then art only, then blockout only
            var blockGroup = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "GeneratorShed");
            var rends = blockGroup != null ? blockGroup.GetComponentsInChildren<MeshRenderer>() : new MeshRenderer[0];
            var yard = new Vector3(2.5f, 18.7f, 14.5f);
            var look = artBB.center;
            Capture(shots, "cmp_01_both",      yard, look, 60f);
            Capture(shots, "cmp_02_both_wide", new Vector3(-6f, 25f, 6f), new Vector3(14f, 18f, 14f), 60f);
            foreach (var r in rends) r.enabled = false;
            Physics.SyncTransforms();
            Capture(shots, "cmp_03_art_only",   yard, look, 60f);
            Capture(shots, "cmp_04_art_courtyard", new Vector3(0f, 18.6f, 17f), look, 60f);
            Capture(shots, "cmp_05_art_wide",   new Vector3(-6f, 25f, 6f), new Vector3(14f, 18f, 14f), 60f);
            foreach (var r in rends) r.enabled = true;      // restore — blockout stays visible
            Physics.SyncTransforms();
            Debug.Log($"[DEV] blockout renderers restored: {rends.Count(r => r.enabled)}/{rends.Length} enabled");

            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[DEV] scene saved with the blockout VISIBLE.");
        }


        /// <summary>Diagnose which local axis is actually the hinge axis after import.</summary>
        [MenuItem("Tools/Last Beacon/Diagnose Generator Shed Doors")]
        public static void DiagnoseDoors()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Shell";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var pf = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == InstName);
            var L = pf.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith("Door_L"));
            var mf = L.GetComponent<MeshFilter>();
            Debug.Log($"[DIAG] leaf localScale {L.localScale} lossyScale {L.lossyScale}");
            Debug.Log($"[DIAG] leaf local axes in world: right {L.right}, up {L.up}, forward {L.forward}");
            var lb = mf.sharedMesh.bounds;
            Debug.Log($"[DIAG] mesh local bounds centre {lb.center} size {lb.size}");
            // farthest mesh vertex from the pivot = the free edge
            var verts = mf.sharedMesh.vertices;
            int far = 0; float fd = 0f;
            for (int i = 0; i < verts.Length; i++)
            { float d = verts[i].magnitude; if (d > fd) { fd = d; far = i; } }
            Debug.Log($"[DIAG] farthest local vert {verts[far]} at {fd:0.0000} (mesh units)");
            foreach (var axis in new[] { "X", "Y", "Z" })
            {
                L.localRotation = Quaternion.identity;
                Vector3 closed = L.TransformPoint(verts[far]);
                Vector3 e = axis == "X" ? new Vector3(95,0,0) : axis == "Y" ? new Vector3(0,95,0) : new Vector3(0,0,95);
                L.localRotation = Quaternion.Euler(e);
                Vector3 open = L.TransformPoint(verts[far]);
                Debug.Log($"[DIAG] rotate 95 about local {axis}: free edge {closed} -> {open}, " +
                          $"moved {(open-closed).magnitude:0.000} m, dY {(open.y-closed.y):0.000}");
            }
            L.localRotation = Quaternion.identity;
            Physics.SyncTransforms();
            Debug.Log("[DIAG] a correct hinge keeps dY near 0 and moves the free edge about 2.5 m");
        }


        /// <summary>List everything sitting on the shed plot, to catch duplicate art copies.</summary>
        [MenuItem("Tools/Last Beacon/Audit Shed Plot")]
        public static void AuditPlot()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var centre = new Vector3(17f, 17f, 13f);
            Debug.Log("[PLOT] renderers whose bounds centre is within 9 m of the shed plot:");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,
                                                                    FindObjectsSortMode.None))
            {
                var d = r.bounds.center - centre; d.y = 0f;
                if (d.magnitude > 9f) continue;
                var t = r.transform; string path = t.name;
                while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
                Debug.Log($"[PLOT]   {(r.enabled ? "ON " : "off")} {path}");
            }
        }


        /// <summary>Moves the blockout practical onto the art lamp fixture.</summary>
        [MenuItem("Tools/Last Beacon/Align Generator Shed Lamp")]
        public static void AlignLamp()
        {
            string shots = GetArg("-protoOutput") ?? "GenShed_Shell";
            Directory.CreateDirectory(shots);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            bool wasActive = player == null || player.gameObject.activeSelf;
            if (player != null) player.gameObject.SetActive(false);

            var bulb = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .FirstOrDefault(r => r.name.Contains("Lamp_Emissive"));
            var light = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.name == "Lamp_GeneratorShed");
            if (bulb == null || light == null)
            { Debug.LogError("[LAMP] bulb or practical not found"); return; }

            Capture(shots, "lamp_before", new Vector3(4.0f, 19.2f, 14.2f),
                    new Vector3(12.0f, 19.4f, 12.8f), 55f);

            Vector3 from = light.transform.position;
            Vector3 to = bulb.bounds.center;
            light.transform.position = to;
            Physics.SyncTransforms();
            Debug.Log($"[LAMP] art fixture centre : {to.x:0.000}, {to.y:0.000}, {to.z:0.000}");
            Debug.Log($"[LAMP] practical moved from {from.x:0.000}, {from.y:0.000}, {from.z:0.000}");
            Debug.Log($"[LAMP]                   to {to.x:0.000}, {to.y:0.000}, {to.z:0.000}");
            Debug.Log($"[LAMP] delta applied {(to - from).magnitude:0.000} m");
            Debug.Log($"[LAMP] generator-source offset to use: " +
                      $"new Vector3({to.x:0.00}f, TierCompound + {(to.y - 17f):0.00}f, {to.z:0.00}f)");
            Debug.Log($"[LAMP] light: range {light.range}, intensity {light.intensity}, colour {light.color} (unchanged)");

            Capture(shots, "lamp_after", new Vector3(4.0f, 19.2f, 14.2f),
                    new Vector3(12.0f, 19.4f, 12.8f), 55f);
            Capture(shots, "lamp_after_courtyard", new Vector3(0f, 18.6f, 17f),
                    new Vector3(12.5f, 19.0f, 12.9f), 60f);

            if (player != null) player.gameObject.SetActive(wasActive);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log("[LAMP] scene saved. Blockout still visible.");
        }

        static GameObject Find(string n) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == n)?.gameObject;

        static void Capture(string dir, string name, Vector3 eye, Vector3 look, float fov)
        {
            var camGo = new GameObject("__ShellCam");
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
